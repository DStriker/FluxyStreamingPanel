using Fluxy.Application.Users.Options;
using Fluxy.Application.Users.Utils;
using Fluxy.Core.Email.Commands;
using Fluxy.Core.Email.Enums;
using Fluxy.Core.Email.Interfaces;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Persistence.Utils;
using Fluxy.Core.Users.Commands;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Interfaces;
using Fluxy.Core.Users.Outcomes;
using Fluxy.Core.Users.Repositories;
using Fluxy.Core.Users.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fluxy.Application.Users.Services
{
    /// <summary>
    /// Replaces the password of an account for a visitor who is not signed in, behind a code
    /// that is mailed to the address on that account.
    /// </summary>
    /// <remarks>
    /// There is no session here to lean on, so the proof has two halves: a username and an email
    /// that belong to the same account, and then a code delivered to that mailbox. The first
    /// half alone proves nothing - both halves are things a person could have read off an old
    /// message - and the second half is the one that actually authorizes the change, which is
    /// why the new password is only ever written when the code matches.
    ///
    /// Without a mail server there is no second half, so the whole flow is refused rather than
    /// skipped. That is the deliberate difference from a profile change, which can fall back on
    /// the session and the current password it already holds.
    ///
    /// The order of the checks is the design of this class: the cheap ones first, and the two
    /// expensive steps - hashing the code and comparing it - last, so a refused request costs a
    /// lookup rather than key derivation.
    /// </remarks>
    public sealed class PasswordResetService : IPasswordResetService
    {
        private readonly IUserRepository _users;
        private readonly IPendingChangeRepository _pendingChanges;
        private readonly ILoginGuardRepository _guards;
        private readonly IUnitOfWork _unitOfWork;
        private readonly PendingChangeService _pendingChangeService;
        private readonly IEmailSender _emailSender;
        private readonly IOptionsMonitor<RegistrationOptions> _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<PasswordResetService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PasswordResetService"/> class.
        /// </summary>
        /// <param name="users">Repository holding the accounts.</param>
        /// <param name="pendingChanges">Repository holding the staged resets.</param>
        /// <param name="guards">Repository that clears the account's allow lists.</param>
        /// <param name="unitOfWork">Commits one reset as one save.</param>
        /// <param name="pendingChangeService">The machinery of a change that waits for a code.</param>
        /// <param name="emailSender">Sender that delivers the code.</param>
        /// <param name="options">Live settings, so a configuration reload applies without a restart.</param>
        /// <param name="timeProvider">
        /// Clock the code lifetime is measured with. Injected so the window can be exercised
        /// without waiting for it.
        /// </param>
        /// <param name="logger">Logger the security relevant steps are reported to.</param>
        public PasswordResetService(
            IUserRepository users,
            IPendingChangeRepository pendingChanges,
            ILoginGuardRepository guards,
            IUnitOfWork unitOfWork,
            PendingChangeService pendingChangeService,
            IEmailSender emailSender,
            IOptionsMonitor<RegistrationOptions> options,
            TimeProvider timeProvider,
            ILogger<PasswordResetService> logger)
        {
            _users = users;
            _pendingChanges = pendingChanges;
            _guards = guards;
            _unitOfWork = unitOfWork;
            _pendingChangeService = pendingChangeService;
            _emailSender = emailSender;
            _options = options;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <inheritdoc />
        public bool IsConfigured => _emailSender.IsConfigured;

        /// <inheritdoc />
        public async Task<PasswordResetOutcome> RequestAsync(
            string username,
            string email,
            string newPassword,
            CancellationToken cancellationToken = default)
        {
            // Before validation, before the lookup, before anything: an installation that
            // cannot mail cannot finish a reset, and accepting the values first would leave a
            // visitor waiting for a message that was never sent.
            if (!_emailSender.IsConfigured)
            {
                _logger.LogWarning(
                    "A password reset was refused because this installation has no mail server " +
                    "configured. See the Email configuration section.");

                return new PasswordResetOutcome
                {
                    Status = PasswordResetStatus.NotConfigured
                };
            }

            var name = username?.Trim() ?? string.Empty;
            var normalized = EmailNormalizer.Normalize(email);
            var password = newPassword ?? string.Empty;

            if (!RegistrationPolicy.IsPasswordAcceptable(password))
            {
                return new PasswordResetOutcome
                {
                    Status = PasswordResetStatus.InvalidInput,
                    Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        ["NewPassword"] =
                        [
                            $"The password must be between {RegistrationLimits.PasswordMinLength} " +
                            $"and {RegistrationLimits.PasswordMaxLength} characters and contain " +
                            "at least one lower case letter, one upper case letter and one digit."
                        ]
                    }
                };
            }

            var user = await _users.FindForPasswordResetAsync(name, cancellationToken);

            // One answer for all three reasons the pair can fail - no such account, an account
            // under a different address, an account that is not usable - so the form cannot be
            // used to find out which combinations of username and email exist. The attempt is
            // counted by the endpoint either way, which is what keeps that probing to the same
            // handful of guesses a sign-in gets.
            if (user is null
                || user.Email != normalized
                || user.RowStatus is not UserStatus.Registered)
            {
                return new PasswordResetOutcome { Status = PasswordResetStatus.InvalidPair };
            }

            var options = _options.CurrentValue;
            var code = OneTimeCode.Generate(options.CodeLength);

            if (!await _pendingChangeService.StageAsync(
                    user.Id,
                    PendingChangeKind.PasswordReset,
                    code,
                    _timeProvider.GetUtcNow().Add(options.CodeLifetime),
                    newPassword: password,
                    cancellationToken: cancellationToken))
            {
                // A concurrent request for the same account won the unique index and mailed a
                // code of its own. Sending a second one here would put two codes in the inbox
                // where only the stored one works.
                return new PasswordResetOutcome
                {
                    Status = PasswordResetStatus.Submitted,
                    RowPersisted = true
                };
            }

            var result = await _emailSender.SendAsync(
                new EmailMessage
                {
                    To = user.Email,
                    Subject = "Your Fluxy password reset code",
                    Body = BuildCodeBody(user.Username, code, options.CodeLifetime)
                },
                cancellationToken);

            if (result is not EmailSendResult.Sent)
            {
                return new PasswordResetOutcome
                {
                    Status = PasswordResetStatus.EmailDeliveryFailed,
                    RowPersisted = true
                };
            }

            _logger.LogInformation(
                "Stored a password reset request for {Username}.",
                user.Username);

            return new PasswordResetOutcome
            {
                Status = PasswordResetStatus.Submitted,
                RowPersisted = true
            };
        }

        /// <inheritdoc />
        public async Task<PasswordResetOutcome> ConfirmAsync(
            string username,
            string code,
            CancellationToken cancellationToken = default)
        {
            var name = username?.Trim() ?? string.Empty;
            var trimmedCode = code?.Trim() ?? string.Empty;

            if (trimmedCode.Length == 0)
            {
                return new PasswordResetOutcome { Status = PasswordResetStatus.InvalidCode };
            }

            var user = await _users.FindForPasswordResetAsync(name, cancellationToken);

            if (user is null || user.RowStatus is not UserStatus.Registered)
            {
                // Reported exactly like a wrong code, so the endpoint does not confirm which
                // login names hold an account.
                return new PasswordResetOutcome { Status = PasswordResetStatus.InvalidCode };
            }

            var pending = await _pendingChanges.FindByUserIdAsync(
                user.Id,
                PendingChangeKind.PasswordReset,
                cancellationToken);

            if (pending is null || pending.NewPasswordHash is null)
            {
                return new PasswordResetOutcome { Status = PasswordResetStatus.InvalidCode };
            }

            switch (_pendingChangeService.Verify(pending, trimmedCode))
            {
                case PendingCodeStatus.Invalid:
                    return new PasswordResetOutcome { Status = PasswordResetStatus.InvalidCode };

                case PendingCodeStatus.Expired:
                    return new PasswordResetOutcome { Status = PasswordResetStatus.CodeExpired };
            }

            await _users.SetPasswordHashAsync(user.Id, pending.NewPasswordHash, cancellationToken);

            // A password reset proves the mailbox, which is also the recovery path for a guard
            // that locked its owner out - so the guard goes with the password rather than
            // staying on to refuse the sign-in the reset was made for. The sessions end in the
            // controller, like every other password replacement.
            await _guards.ApplyAsync(
                user.Id,
                new NormalizedLoginGuard(false, false, [], null, null),
                cancellationToken);

            await _pendingChangeService.RemoveAsync(pending.Id, cancellationToken);

            if (await _unitOfWork.TrySaveAsync(cancellationToken) is not null)
            {
                // No unique value is written here, so anything the database refuses is a real
                // failure rather than an outcome of the form. Reported like a wrong code so a
                // raced confirmation cannot tell the loser anything about the row.
                return new PasswordResetOutcome { Status = PasswordResetStatus.InvalidCode };
            }

            _logger.LogInformation(
                "Replaced the password of {Username} from the public reset form and cleared " +
                "its login guard.",
                user.Username);

            return new PasswordResetOutcome
            {
                Status = PasswordResetStatus.Confirmed,
                UserId = user.Id
            };
        }

        /// <summary>The plain text body of the message that carries the code.</summary>
        private static string BuildCodeBody(string username, string code, TimeSpan lifetime)
        {
            var minutes = (int)lifetime.TotalMinutes;

            return CodeMessage.Build(
                username,
                $"Your Fluxy password reset code is {code}.",
                $"""
                It is valid for {minutes} minutes and replaces the password only after it is
                entered. If you did not ask to reset your password, ignore this message and
                nothing will change - your current password keeps working.
                """);
        }
    }
}