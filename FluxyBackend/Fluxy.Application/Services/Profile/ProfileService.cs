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
    /// Reads the profile of a signed-in account and changes its username, email or password,
    /// each behind a code that is mailed out - unless this installation has no mail server, in
    /// which case there is nothing to confirm with and the change is applied at once.
    /// </summary>
    /// <remarks>
    /// The order of the checks is the design of this class, the same way it is in
    /// <see cref="RegistrationService"/>: everything that can refuse the request without
    /// touching the database comes first, and the two expensive steps - comparing the current
    /// password and comparing the code - come last, so a rejected request costs a lookup rather
    /// than a hundred milliseconds of key derivation.
    ///
    /// The current password is required for all three operations. A session says that somebody
    /// signed in at some point; the password says it is the person who owns the account, which
    /// is the difference between an unattended browser and a stolen cookie.
    ///
    /// Nothing here knows about HTTP. Whether <see cref="ProfileChangeStatus.InvalidCode"/>
    /// becomes a 400 or a 502 is decided in the transport layer, and this class would report
    /// the same outcome to any other transport just the same.
    /// </remarks>
    public sealed class ProfileService : IProfileService
    {
        /// <summary>Digits a code is built from.</summary>
        private const string CodeAlphabet = "0123456789";

        private readonly FluxyDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly IOptionsMonitor<RegistrationOptions> _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ProfileService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfileService"/> class.
        /// </summary>
        /// <param name="context">Context holding the accounts and the pending changes.</param>
        /// <param name="emailSender">Sender that delivers the code.</param>
        /// <param name="options">Live settings, so a configuration reload applies without a restart.</param>
        /// <param name="timeProvider">
        /// Clock the code lifetime is measured with. Injected so the window can be exercised
        /// without waiting for it.
        /// </param>
        /// <param name="logger">Logger the security relevant steps are reported to.</param>
        public ProfileService(
            FluxyDbContext context,
            IEmailSender emailSender,
            IOptionsMonitor<RegistrationOptions> options,
            TimeProvider timeProvider,
            ILogger<ProfileService> logger)
        {
            _context = context;
            _emailSender = emailSender;
            _options = options;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<AccountProfile?> GetProfileAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(entry => entry.Id == userId, cancellationToken);

            if (user is null || user.Status is not UserStatus.Registered)
            {
                return null;
            }

            return new AccountProfile
            {
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                TimeZone = user.TimeZone
            };
        }

        /// <summary>
        /// Longest accepted identifier, matching the column width. Mirrored rather than
        /// imported from <c>UserConfiguration</c> for the same reason the registration rules
        /// keep their own copies: the limit belongs to the input being accepted, and the
        /// column is what happens to agree with it.
        /// </summary>
        private const int TimeZoneMaxLength = 64;

        /// <inheritdoc />
        public async Task<ProfileChangeOutcome> UpdateTimeZoneAsync(
            Guid userId,
            string? timeZone,
            CancellationToken cancellationToken = default)
        {
            var requested = timeZone?.Trim() ?? string.Empty;

            if (requested.Length > TimeZoneMaxLength)
            {
                return InvalidTimeZone();
            }

            // Empty means the visitor picked "auto", which is stored as NULL - the absence
            // of the value is the state that says "read it from the browser", and it is
            // written rather than skipped so that clearing a zone cannot leave the old one
            // behind.
            if (requested.Length > 0)
            {
                // An identifier this installation's clock cannot resolve is refused here
                // rather than stored and discovered later by whatever tries to format a date
                // with it. On Linux the same API reads IANA ids natively, so a name that
                // passes this check works where the application runs.
                try
                {
                    TimeZoneInfo.FindSystemTimeZoneById(requested);
                }
                catch (TimeZoneNotFoundException)
                {
                    return InvalidTimeZone();
                }
                catch (InvalidTimeZoneException)
                {
                    return InvalidTimeZone();
                }
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(entry => entry.Id == userId, cancellationToken);

            if (user is null || user.Status is not UserStatus.Registered)
            {
                return new ProfileChangeOutcome { Status = ProfileChangeStatus.AccountNotActive };
            }

            user.TimeZone = requested.Length > 0 ? requested : null;

            // No unique index on this column, so unlike the three changes above a lost race
            // cannot happen and the save needs no interpretation of its failures.
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Set the display time zone of {Username} to {TimeZone}.",
                user.Username,
                user.TimeZone ?? "(browser default)");

            return new ProfileChangeOutcome { Status = ProfileChangeStatus.Applied };

            ProfileChangeOutcome InvalidTimeZone() => new()
            {
                Status = ProfileChangeStatus.InvalidInput,
                Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [nameof(AccountProfile.TimeZone)] =
                    [
                        "The time zone identifier is not one this system knows."
                    ]
                }
            };
        }

        /// <inheritdoc />
        public Task<ProfileChangeOutcome> ChangeUsernameAsync(
            Guid userId,
            string currentPassword,
            string username,
            CancellationToken cancellationToken = default)
        {
            var name = username?.Trim() ?? string.Empty;
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (name.Length is < RegistrationPolicy.UsernameMinLength
                or > RegistrationPolicy.UsernameMaxLength)
            {
                errors[nameof(AccountProfile.Username)] =
                [
                    $"The username must be between {RegistrationPolicy.UsernameMinLength} and " +
                    $"{RegistrationPolicy.UsernameMaxLength} characters."
                ];
            }

            return RequestAsync(
                userId,
                PendingChangeKind.ChangeUsername,
                targetValue: name,
                newPassword: null,
                currentPassword,
                errors,
                cancellationToken);
        }

        /// <inheritdoc />
        public Task<ProfileChangeOutcome> ChangeEmailAsync(
            Guid userId,
            string currentPassword,
            string email,
            CancellationToken cancellationToken = default)
        {
            var normalized = NormalizeEmail(email);
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (!RegistrationPolicy.IsEmailShapeValid(normalized))
            {
                errors[nameof(AccountProfile.Email)] = ["The email address is not valid."];
            }

            return RequestAsync(
                userId,
                PendingChangeKind.ChangeEmail,
                targetValue: normalized,
                newPassword: null,
                currentPassword,
                errors,
                cancellationToken);
        }

        /// <inheritdoc />
        public Task<ProfileChangeOutcome> ChangePasswordAsync(
            Guid userId,
            string currentPassword,
            string newPassword,
            CancellationToken cancellationToken = default)
        {
            var password = newPassword ?? string.Empty;
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (!RegistrationPolicy.IsPasswordAcceptable(password))
            {
                errors["NewPassword"] =
                [
                    $"The password must be between {RegistrationPolicy.PasswordMinLength} and " +
                    $"{RegistrationPolicy.PasswordMaxLength} characters and contain at least one " +
                    "lower case letter, one upper case letter and one digit."
                ];
            }

            return RequestAsync(
                userId,
                PendingChangeKind.ChangePassword,
                targetValue: null,
                newPassword: password,
                currentPassword,
                errors,
                cancellationToken);
        }

        /// <inheritdoc />
        public async Task<ProfileChangeOutcome> ConfirmAsync(
            Guid userId,
            string code,
            CancellationToken cancellationToken = default)
        {
            var trimmedCode = code?.Trim() ?? string.Empty;

            if (trimmedCode.Length == 0)
            {
                return new ProfileChangeOutcome { Status = ProfileChangeStatus.InvalidCode };
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(entry => entry.Id == userId, cancellationToken);

            if (user is null || user.Status is not UserStatus.Registered)
            {
                return new ProfileChangeOutcome { Status = ProfileChangeStatus.AccountNotActive };
            }

            var pending = await _context.PendingChanges
                .FirstOrDefaultAsync(change => change.UserId == userId, cancellationToken);

            // A code minted for the public password reset form never confirms a profile change,
            // and the other way round: the two flows are separate proofs, and one row can only
            // be waiting for one of them.
            if (pending is null || pending.Kind is PendingChangeKind.PasswordReset)
            {
                return new ProfileChangeOutcome { Status = ProfileChangeStatus.InvalidCode };
            }

            // BCrypt recomputes the hash from the salt inside the stored value, so verifying a
            // code costs as much as producing one. That is why the endpoint is throttled before
            // it gets here rather than after.
            if (!BCrypt.Net.BCrypt.Verify(trimmedCode, pending.CodeHash))
            {
                return new ProfileChangeOutcome { Status = ProfileChangeStatus.InvalidCode };
            }

            if (_timeProvider.GetUtcNow() > pending.ExpiresAt)
            {
                return new ProfileChangeOutcome { Status = ProfileChangeStatus.CodeExpired };
            }

            // The value was checked when the change was requested, and the code may have spent
            // fifteen minutes in an inbox while somebody else took the same username. The row is
            // the authority, so it is checked again here rather than trusted.
            if (await IsTakenAsync(userId, pending.Kind, pending.TargetValue, cancellationToken))
            {
                return new ProfileChangeOutcome { Status = ProfileChangeStatus.AlreadyExists };
            }

            Apply(user, pending.Kind, pending.TargetValue, pending.NewPasswordHash);
            _context.PendingChanges.Remove(pending);

            if (!await SaveAsync(cancellationToken))
            {
                return new ProfileChangeOutcome { Status = ProfileChangeStatus.AlreadyExists };
            }

            _logger.LogInformation(
                "Applied a confirmed {Kind} for {Username}.",
                pending.Kind,
                user.Username);

            return new ProfileChangeOutcome
            {
                Status = ProfileChangeStatus.Confirmed,
                Kind = pending.Kind,
                Account = user.ToModel()
            };
        }

        /// <summary>
        /// One request, whether it ends in a stored row or in the change itself.
        /// </summary>
        private async Task<ProfileChangeOutcome> RequestAsync(
            Guid userId,
            PendingChangeKind kind,
            string? targetValue,
            string? newPassword,
            string currentPassword,
            IReadOnlyDictionary<string, string[]> errors,
            CancellationToken cancellationToken)
        {
            if (errors.Count > 0)
            {
                return new ProfileChangeOutcome
                {
                    Status = ProfileChangeStatus.InvalidInput,
                    Errors = errors
                };
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(entry => entry.Id == userId, cancellationToken);

            if (user is null || user.Status is not UserStatus.Registered)
            {
                return new ProfileChangeOutcome { Status = ProfileChangeStatus.AccountNotActive };
            }

            // Checked after the row is loaded and before anything is written, but it is still
            // the expensive step of this path, so the endpoint counts the attempt before it
            // calls in - the same order the sign-in endpoint uses for the same reason.
            if (string.IsNullOrEmpty(currentPassword)
                || !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            {
                return new ProfileChangeOutcome
                {
                    Status = ProfileChangeStatus.InvalidCurrentPassword
                };
            }

            if (await IsTakenAsync(userId, kind, targetValue, cancellationToken))
            {
                return new ProfileChangeOutcome { Status = ProfileChangeStatus.AlreadyExists };
            }

            var options = _options.CurrentValue;
            var now = _timeProvider.GetUtcNow();

            // An installation without a mail server has nothing to confirm with, so the change
            // is applied straight away rather than refused. The password has been verified
            // either way, which is what makes it safe to skip the second proof.
            if (!_emailSender.IsConfigured)
            {
                _logger.LogWarning(
                    "Applying a {Kind} without confirmation because this installation has no " +
                    "mail server configured. See the Email configuration section.",
                    kind);

                Apply(user, kind, targetValue, newPassword is null
                    ? null
                    : BCrypt.Net.BCrypt.HashPassword(newPassword));

                if (!await SaveAsync(cancellationToken))
                {
                    return new ProfileChangeOutcome { Status = ProfileChangeStatus.AlreadyExists };
                }

                return new ProfileChangeOutcome
                {
                    Status = ProfileChangeStatus.Applied,
                    Kind = kind,
                    Account = user.ToModel()
                };
            }

            var code = GenerateCode(options.CodeLength);

            if (!await StageAsync(
                    userId,
                    kind,
                    targetValue,
                    newPassword,
                    code,
                    now.Add(options.CodeLifetime),
                    cancellationToken))
            {
                // Two requests for the same account arrived at the same moment and the unique
                // index let one of them win. The winner stored a row and mailed a code of its
                // own, so this one has nothing of its own to send - answering "check your inbox"
                // sends the visitor to the code that does exist.
                return new ProfileChangeOutcome
                {
                    Status = ProfileChangeStatus.Submitted,
                    RowPersisted = true
                };
            }

            var recipient = kind is PendingChangeKind.ChangeEmail
                ? targetValue!
                : user.Email;

            var result = await _emailSender.SendAsync(
                new EmailMessage
                {
                    To = recipient,
                    Subject = "Your Fluxy confirmation code",
                    Body = BuildCodeBody(user.Username, kind, targetValue, code, options.CodeLifetime)
                },
                cancellationToken);

            if (result is not EmailSendResult.Sent)
            {
                // The row is stored, so the caller has to be told that the code did not arrive.
                // A new request replaces it once the visitor asks again.
                return new ProfileChangeOutcome
                {
                    Status = ProfileChangeStatus.EmailDeliveryFailed,
                    RowPersisted = true
                };
            }

            return new ProfileChangeOutcome
            {
                Status = ProfileChangeStatus.Submitted,
                RowPersisted = true
            };
        }

        /// <summary>
        /// Whether another account already holds the value this change wants to write.
        /// </summary>
        private async Task<bool> IsTakenAsync(
            Guid userId,
            PendingChangeKind kind,
            string? targetValue,
            CancellationToken cancellationToken)
        {
            if (kind is PendingChangeKind.ChangePassword || string.IsNullOrEmpty(targetValue))
            {
                return false;
            }

            return kind switch
            {
                PendingChangeKind.ChangeUsername => await _context.Users.AnyAsync(
                    entry => entry.Id != userId && entry.Username == targetValue,
                    cancellationToken),

                PendingChangeKind.ChangeEmail => await _context.Users.AnyAsync(
                    entry => entry.Id != userId && entry.Email == targetValue,
                    cancellationToken),

                _ => false
            };
        }

        /// <summary>
        /// Writes the change to the account. The row that owns the value does the rest: the
        /// unique indexes are what actually refuse a taken one.
        /// </summary>
        private static void Apply(
            UserEntity user,
            PendingChangeKind kind,
            string? targetValue,
            string? passwordHash)
        {
            switch (kind)
            {
                case PendingChangeKind.ChangeUsername:
                    user.Username = targetValue ?? user.Username;
                    break;

                case PendingChangeKind.ChangeEmail:
                    user.Email = targetValue ?? user.Email;
                    break;

                case PendingChangeKind.ChangePassword:
                    user.PasswordHash = passwordHash ?? user.PasswordHash;
                    break;
            }
        }

        /// <summary>
        /// Stores the pending change, replacing whatever this account was waiting for before.
        /// </summary>
        /// <returns>False when a concurrent request for the same account won the race.</returns>
        private async Task<bool> StageAsync(
            Guid userId,
            PendingChangeKind kind,
            string? targetValue,
            string? newPassword,
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

            pending.Kind = kind;
            pending.TargetValue = targetValue;
            pending.NewPasswordHash = newPassword is null
                ? null
                : BCrypt.Net.BCrypt.HashPassword(newPassword);
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
        /// Saves the account, turning a lost race for a unique value into the same refusal a
        /// sequential check would have produced.
        /// </summary>
        /// <returns>False when the value was taken by a concurrent request.</returns>
        private async Task<bool> SaveAsync(CancellationToken cancellationToken)
        {
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

        /// <summary>
        /// The plain text body of the message that carries a code, worded for the change it
        /// confirms.
        /// </summary>
        private static string BuildCodeBody(
            string username,
            PendingChangeKind kind,
            string? targetValue,
            string code,
            TimeSpan lifetime)
        {
            var minutes = (int)lifetime.TotalMinutes;
            var askedFor = kind switch
            {
                PendingChangeKind.ChangeUsername =>
                    $"the username of your account changed to {targetValue}",
                PendingChangeKind.ChangeEmail =>
                    $"the email address of your account changed to {targetValue}",
                PendingChangeKind.ChangePassword => "the password of your account changed",
                _ => "a change to your account"
            };

            return $"""
                Hello {username},

                Your Fluxy confirmation code is {code}.

                It confirms that you asked for {askedFor}. It is valid for {minutes} minutes.
                If you did not ask for this, ignore this message - nothing changes until the
                code is confirmed, and you can simply request a new change to replace it.

                {Environment.NewLine}-
                {Environment.NewLine}This is an automated message, please do not reply.
                """;
        }
    }
}
