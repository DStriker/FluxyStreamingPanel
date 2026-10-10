using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Fluxy.Application.Services.GeoIp;
using Fluxy.Application.Services.Registration;
using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Users;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
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
        private readonly IGeoIpResolver _geoIp;
        private readonly IHostEnvironment _environment;
        private readonly IOptionsMonitor<RegistrationOptions> _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ProfileService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfileService"/> class.
        /// </summary>
        /// <param name="context">Context holding the accounts and the pending changes.</param>
        /// <param name="emailSender">Sender that delivers the code.</param>
        /// <param name="geoIp">Resolver that places the client address on the map.</param>
        /// <param name="environment">Host environment, for the Development bypass of local networks.</param>
        /// <param name="options">Live settings, so a configuration reload applies without a restart.</param>
        /// <param name="timeProvider">
        /// Clock the code lifetime is measured with. Injected so the window can be exercised
        /// without waiting for it.
        /// </param>
        /// <param name="logger">Logger the security relevant steps are reported to.</param>
        public ProfileService(
            FluxyDbContext context,
            IEmailSender emailSender,
            IGeoIpResolver geoIp,
            IHostEnvironment environment,
            IOptionsMonitor<RegistrationOptions> options,
            TimeProvider timeProvider,
            ILogger<ProfileService> logger)
        {
            _context = context;
            _emailSender = emailSender;
            _geoIp = geoIp;
            _environment = environment;
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
                .Include(entry => entry.Group)
                .ThenInclude(entry => entry!.Permissions)
                .FirstOrDefaultAsync(entry => entry.Id == userId, cancellationToken);

            if (user is null)
            {
                return null;
            }

            // The effective status rather than the row's own: a blocked group blocks its
            // members everywhere, and the profile endpoint answers with "no such active
            // account" for exactly the accounts a sign-in would refuse.
            var account = user.ToModel();

            if (account.Status is not UserStatus.Registered)
            {
                return null;
            }

            return new AccountProfile
            {
                Username = user.Username,
                Email = user.Email,
                Role = account.Role,
                TimeZone = user.TimeZone,
                Permissions = ReadPermissions(user.Group)
            };
        }

        /// <inheritdoc />
        public async Task<ProfileChangeOutcome> UpdateTimeZoneAsync(
            Guid userId,
            string? timeZone,
            CancellationToken cancellationToken = default)
        {
            // One rule decides here and in the admin user form alike, so the same identifier
            // cannot be refused by one and stored by the other. The rule itself explains why
            // the check is a lookup rather than a pattern over the shape of the name.
            if (!TimeZonePolicy.TryNormalize(timeZone, out var requested))
            {
                return InvalidTimeZone();
            }

            var user = await _context.Users
                .Include(entry => entry.Group)
                .FirstOrDefaultAsync(entry => entry.Id == userId, cancellationToken);

            if (user is null || user.Status is not UserStatus.Registered)
            {
                return new ProfileChangeOutcome { Status = ProfileChangeStatus.AccountNotActive };
            }

            // A cleared zone is written rather than skipped, so that going back to "auto"
            // cannot leave the old one behind: the absence of the value is the state that
            // says "read it from the browser".
            user.TimeZone = requested;

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
        public async Task<LoginGuardSettings?> GetLoginGuardAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var user = await _context.Users
                .AsNoTracking()
                .Include(entry => entry.Group)
                .FirstOrDefaultAsync(entry => entry.Id == userId, cancellationToken);

            if (user is null || user.Status is not UserStatus.Registered)
            {
                return null;
            }

            var rules = await _context.LoginGuardRules
                .AsNoTracking()
                .Where(rule => rule.UserId == userId)
                .ToListAsync(cancellationToken);

            var provider = rules.FirstOrDefault(rule => rule.Kind is LoginGuardRuleKind.AutonomousSystem);

            return new LoginGuardSettings
            {
                GeoProtectionEnabled = user.GeoProtectionEnabled,
                BindSessionToIp = user.BindSessionToIp,
                AllowedIps = rules
                    .Where(rule => rule.Kind is LoginGuardRuleKind.IpAddress)
                    .Select(rule => rule.Value)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                AllowedCountry = rules
                    .FirstOrDefault(rule => rule.Kind is LoginGuardRuleKind.Country)?.Value,
                AllowedAutonomousSystemNumber = provider is not null
                    && int.TryParse(provider.Value, out var asn) ? asn : null
            };
        }

        /// <inheritdoc />
        public async Task<ProfileChangeOutcome> ChangeLoginGuardAsync(
            Guid userId,
            string currentPassword,
            LoginGuardSettings settings,
            string? clientAddress,
            CancellationToken cancellationToken = default)
        {
            // Normalized once, so the anti-lock check below, the staging and the confirmation
            // all read the same values the database will hold. The rules themselves live
            // beside the store that writes them, shared with the admin service.
            var staged = LoginGuardNormalizer.Normalize(settings);
            if (staged.Errors.Count > 0)
            {
                return new ProfileChangeOutcome
                {
                    Status = ProfileChangeStatus.InvalidInput,
                    Errors = staged.Errors
                };
            }

            var user = await _context.Users
                .Include(entry => entry.Group)
                .FirstOrDefaultAsync(entry => entry.Id == userId, cancellationToken);

            if (user is null || user.Status is not UserStatus.Registered)
            {
                return new ProfileChangeOutcome { Status = ProfileChangeStatus.AccountNotActive };
            }

            // The expensive step of this path, so the endpoint counts the attempt before it
            // calls in - the same order the sign-in endpoint uses for the same reason.
            if (string.IsNullOrEmpty(currentPassword)
                || !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            {
                return new ProfileChangeOutcome
                {
                    Status = ProfileChangeStatus.InvalidCurrentPassword
                };
            }

            // The anti-lock: a guard that refuses the very network it is saved from is a
            // self-inflicted lockout with the password reset as its only way back. Refused
            // here, while the owner is still holding a session that proves who they are.
            if (staged.Value.GeoProtectionEnabled && !BypassesGuard(clientAddress))
            {
                var geo = await _geoIp.ResolveAsync(clientAddress, cancellationToken);

                if (!LoginGuardPolicy.AllowsAddress(staged.Value.AllowedIps, geo)
                    || !LoginGuardPolicy.AllowsCountry(
                        staged.Value.AllowedCountry is { } country ? [country] : [],
                        geo)
                    || !LoginGuardPolicy.AllowsProvider(
                        staged.Value.AllowedAutonomousSystemNumber is { } asn ? [asn] : [],
                        geo))
                {
                    return new ProfileChangeOutcome
                    {
                        Status = ProfileChangeStatus.InvalidInput,
                        Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                        {
                            [nameof(LoginGuardSettings.GeoProtectionEnabled)] =
                            [
                                "Your current network would not pass this guard, so saving it " +
                                "would lock you out. Add your current address, country or " +
                                "provider first."
                            ]
                        }
                    };
                }
            }

            var options = _options.CurrentValue;
            var now = _timeProvider.GetUtcNow();

            // An installation without a mail server has nothing to confirm with, so the change
            // is applied straight away rather than refused. The password has been verified
            // either way, which is what makes it safe to skip the second proof.
            if (!_emailSender.IsConfigured)
            {
                _logger.LogWarning(
                    "Applying a login guard change without confirmation because this " +
                    "installation has no mail server configured. See the Email configuration " +
                    "section.");

                await ApplyLoginGuardAsync(user, staged.Value, cancellationToken);

                return new ProfileChangeOutcome
                {
                    Status = ProfileChangeStatus.Applied,
                    Kind = PendingChangeKind.ChangeLoginGuard,
                    Account = user.ToModel()
                };
            }

            var code = GenerateCode(options.CodeLength);

            if (!await StageLoginGuardAsync(
                    userId,
                    staged.Value,
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

            var result = await _emailSender.SendAsync(
                new EmailMessage
                {
                    To = user.Email,
                    Subject = "Your Fluxy confirmation code",
                    Body = BuildCodeBody(
                        user.Username,
                        PendingChangeKind.ChangeLoginGuard,
                        null,
                        code,
                        options.CodeLifetime)
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
                .Include(entry => entry.Group)
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

            // The guard carries its content in the payload rather than in the staged columns,
            // so it is confirmed on its own branch: the uniqueness check below has nothing to
            // say about networks, and the generic apply knows nothing about rules.
            if (pending.Kind is PendingChangeKind.ChangeLoginGuard)
            {
                return await ConfirmLoginGuardAsync(user, pending, cancellationToken);
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
                .Include(entry => entry.Group)
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

        /// <summary>
        /// Stores the staged guard, replacing whatever this account was waiting for before.
        /// </summary>
        /// <returns>False when a concurrent request for the same account won the race.</returns>
        private async Task<bool> StageLoginGuardAsync(
            Guid userId,
            NormalizedLoginGuard staged,
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

            pending.Kind = PendingChangeKind.ChangeLoginGuard;
            pending.TargetValue = null;
            pending.NewPasswordHash = null;
            pending.Payload = JsonSerializer.Serialize(staged);
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
        /// Applies a confirmed guard: the switches go on the account, the lists replace
        /// whatever the account held before.
        /// </summary>
        private async Task<ProfileChangeOutcome> ConfirmLoginGuardAsync(
            UserEntity user,
            PendingChangeEntity pending,
            CancellationToken cancellationToken)
        {
            NormalizedLoginGuard? staged = null;
            try
            {
                staged = JsonSerializer.Deserialize<NormalizedLoginGuard>(pending.Payload ?? string.Empty);
            }
            catch (JsonException)
            {
                // A row this installation cannot read authorizes nothing. Reported like a wrong
                // code, so a corrupted row is indistinguishable from a guessed one.
            }

            if (staged is null)
            {
                return new ProfileChangeOutcome { Status = ProfileChangeStatus.InvalidCode };
            }

            await ApplyLoginGuardAsync(user, staged, cancellationToken);
            _context.PendingChanges.Remove(pending);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
            {
                // Two confirmations of the same code raced and the unique index let one win.
                // The code is spent either way, so the loser gets the same answer a wrong code
                // gets rather than a conflict about a list it never saw.
                if (IsUniqueViolation(exception))
                {
                    _context.ChangeTracker.Clear();

                    return new ProfileChangeOutcome { Status = ProfileChangeStatus.InvalidCode };
                }

                throw;
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
        /// Writes the switches and replaces the rules. The old lists are deleted by the shared
        /// store, so a guard that drops a network does not keep it beside the new one.
        /// </summary>
        private async Task ApplyLoginGuardAsync(
            UserEntity user,
            NormalizedLoginGuard staged,
            CancellationToken cancellationToken)
        {
            user.GeoProtectionEnabled = staged.GeoProtectionEnabled;
            user.BindSessionToIp = staged.BindSessionToIp;

            await LoginGuardStore.ReplaceRulesAsync(_context, user.Id, staged, cancellationToken);

            _logger.LogInformation(
                "Set the login guard of {Username}: protection {Protection}, session binding " +
                "{Binding}, {Ips} address(es), country {Country}, provider AS{Asn}.",
                user.Username,
                staged.GeoProtectionEnabled ? "on" : "off",
                staged.BindSessionToIp ? "on" : "off",
                staged.AllowedIps.Length,
                staged.AllowedCountry ?? "any",
                staged.AllowedAutonomousSystemNumber?.ToString(CultureInfo.InvariantCulture) ?? "any");
        }

        /// <summary>
        /// Whether the anti-lock check is skipped for this address. Same rule as on the
        /// sign-in path: local networks carry no GeoIP data, so in Development they are
        /// bypassed rather than refused.
        /// </summary>
        private bool BypassesGuard(string? clientAddress)
        {
            if (!_environment.IsDevelopment()
                || !LoginGuardPolicy.IsLocalAddress(clientAddress))
            {
                return false;
            }

            _logger.LogWarning(
                "The login guard anti-lock check was bypassed for local address " +
                "{ClientAddress} because this host runs in Development.",
                clientAddress);

            return true;
        }

        /// <summary>
        /// The permissions of the group an account points at, read from the rows the include
        /// already loaded.
        /// </summary>
        /// <param name="group">The group the account belongs to, or null when it has none.</param>
        /// <returns>
        /// The grants of that group, or an empty set when the navigation was not loaded. An
        /// account whose group could not be read is granted nothing, which is the safe answer
        /// for a field whose only job is to draw buttons - the server refuses either way.
        /// </returns>
        private static IReadOnlySet<UserPermission> ReadPermissions(UserGroupEntity? group)
        {
            if (group?.Permissions is null)
            {
                return new HashSet<UserPermission>();
            }

            return group.Permissions
                .Select(grant => grant.Permission)
                .ToHashSet();
        }

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
                PendingChangeKind.ChangeLoginGuard => "the login protection of your account changed",
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
