using System.Security.Cryptography;
using Fluxy.Application.Services.Registration;
using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Users;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fluxy.Application.Services.Profile
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
        /// <summary>Digits a code is built from.</summary>
        private const string CodeAlphabet = "0123456789";

        private readonly FluxyDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly IOptionsMonitor<RegistrationOptions> _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<PasswordResetService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PasswordResetService"/> class.
        /// </summary>
        /// <param name="context">Context holding the accounts and the pending changes.</param>
        /// <param name="emailSender">Sender that delivers the code.</param>
        /// <param name="options">Live settings, so a configuration reload applies without a restart.</param>
        /// <param name="timeProvider">
        /// Clock the code lifetime is measured with. Injected so the window can be exercised
        /// without waiting for it.
        /// </param>
        /// <param name="logger">Logger the security relevant steps are reported to.</param>
        public PasswordResetService(
            FluxyDbContext context,
            IEmailSender emailSender,
            IOptionsMonitor<RegistrationOptions> options,
            TimeProvider timeProvider,
            ILogger<PasswordResetService> logger)
        {
            _context = context;
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
            var normalized = NormalizeEmail(email);
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
                            $"The password must be between {RegistrationPolicy.PasswordMinLength} " +
                            $"and {RegistrationPolicy.PasswordMaxLength} characters and contain " +
                            "at least one lower case letter, one upper case letter and one digit."
                        ]
                    }
                };
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(entry => entry.Username == name, cancellationToken);

            // One answer for all three reasons the pair can fail - no such account, an account
            // under a different address, an account that is not usable - so the form cannot be
            // used to find out which combinations of username and email exist. The attempt is
            // counted by the endpoint either way, which is what keeps that probing to the same
            // handful of guesses a sign-in gets.
            if (user is null
                || user.Email != normalized
                || user.Status is not UserStatus.Registered)
            {
                return new PasswordResetOutcome { Status = PasswordResetStatus.InvalidPair };
            }

            var options = _options.CurrentValue;
            var code = GenerateCode(options.CodeLength);

            if (!await StageAsync(
                    user.Id,
                    password,
                    code,
                    _timeProvider.GetUtcNow().Add(options.CodeLifetime),
                    cancellationToken))
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

            var user = await _context.Users
                .FirstOrDefaultAsync(entry => entry.Username == name, cancellationToken);

            if (user is null || user.Status is not UserStatus.Registered)
            {
                // Reported exactly like a wrong code, so the endpoint does not confirm which
                // login names hold an account.
                return new PasswordResetOutcome { Status = PasswordResetStatus.InvalidCode };
            }

            var pending = await _context.PendingChanges.FirstOrDefaultAsync(
                change => change.UserId == user.Id && change.Kind == PendingChangeKind.PasswordReset,
                cancellationToken);

            if (pending is null || pending.NewPasswordHash is null)
            {
                return new PasswordResetOutcome { Status = PasswordResetStatus.InvalidCode };
            }

            // BCrypt recomputes the hash from the salt inside the stored value, so verifying a
            // code costs as much as producing one - which is why the endpoint counts the attempt
            // before it calls in here.
            if (!BCrypt.Net.BCrypt.Verify(trimmedCode, pending.CodeHash))
            {
                return new PasswordResetOutcome { Status = PasswordResetStatus.InvalidCode };
            }

            if (_timeProvider.GetUtcNow() > pending.ExpiresAt)
            {
                return new PasswordResetOutcome { Status = PasswordResetStatus.CodeExpired };
            }

            user.PasswordHash = pending.NewPasswordHash;
            _context.PendingChanges.Remove(pending);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
            {
                // No unique value is written here, so anything the database refuses is a real
                // failure rather than an outcome of the form.
                if (IsUniqueViolation(exception))
                {
                    _context.ChangeTracker.Clear();

                    return new PasswordResetOutcome { Status = PasswordResetStatus.InvalidCode };
                }

                throw;
            }

            _logger.LogInformation(
                "Replaced the password of {Username} from the public reset form.",
                user.Username);

            return new PasswordResetOutcome
            {
                Status = PasswordResetStatus.Confirmed,
                UserId = user.Id
            };
        }

        /// <summary>
        /// Stores the pending reset, replacing whatever this account was waiting for before.
        /// </summary>
        /// <returns>False when a concurrent request for the same account won the race.</returns>
        private async Task<bool> StageAsync(
            Guid userId,
            string newPassword,
            string code,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken)
        {
            var pending = await _context.PendingChanges
                .FirstOrDefaultAsync(change => change.UserId == userId, cancellationToken);

            if (pending is null)
            {
                pending = new PendingChangeEntity { UserId = userId };
                _context.PendingChanges.Add(pending);
            }

            pending.Kind = PendingChangeKind.PasswordReset;
            pending.TargetValue = null;

            // The password is hashed here, once, and copied onto the account only after the
            // code matches. No clear text and no reversible form of it is ever stored.
            pending.NewPasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            pending.CodeHash = BCrypt.Net.BCrypt.HashPassword(code);
            pending.ExpiresAt = expiresAt;

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
            {
                if (IsUniqueViolation(exception))
                {
                    _context.ChangeTracker.Clear();

                    return false;
                }

                throw;
            }

            return true;
        }

        /// <summary>
        /// Whether the failure was a unique index violation. The provider is not referenced
        /// here, so the check is on the message the server sends, which is the documented text
        /// of <c>SQLSTATE 23505</c>.
        /// </summary>
        private static bool IsUniqueViolation(DbUpdateException exception)
            => exception.InnerException?.Message.Contains(
                "23505",
                StringComparison.Ordinal) == true;

        /// <summary>Folds an address into the single form the database compares against.</summary>
        private static string NormalizeEmail(string? email)
            => email?.Trim().ToLowerInvariant() ?? string.Empty;

        /// <summary>Builds a code of <paramref name="length"/> digits.</summary>
        /// <remarks>
        /// The digits come from <see cref="RandomNumberGenerator"/>, not from
        /// <see cref="Random.Shared"/> and not from a modulo of a random integer, both of which
        /// bias the result. A short code is only acceptable because the stored value is a hash
        /// and the endpoint is throttled.
        /// </remarks>
        private static string GenerateCode(int length)
        {
            var digits = RandomNumberGenerator.GetItems(CodeAlphabet.AsSpan(), length);

            return new string(digits);
        }

        /// <summary>The plain text body of the message that carries the code.</summary>
        private static string BuildCodeBody(string username, string code, TimeSpan lifetime)
        {
            var minutes = (int)lifetime.TotalMinutes;

            return $"""
                Hello {username},

                Your Fluxy password reset code is {code}.

                It is valid for {minutes} minutes and replaces the password only after it is
                entered. If you did not ask to reset your password, ignore this message and
                nothing will change - your current password keeps working.

                {Environment.NewLine}-
                {Environment.NewLine}This is an automated message, please do not reply.
                """;
        }
    }
}
