using Fluxy.Application.Users.Options;
using Fluxy.Application.Users.Utils;
using Fluxy.Core.Email.Commands;
using Fluxy.Core.Email.Enums;
using Fluxy.Core.Email.Interfaces;
using Fluxy.Core.GeoIp.Interfaces;
using Fluxy.Core.Persistence.Exceptions;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Persistence.Utils;
using Fluxy.Core.Users.Commands;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Interfaces;
using Fluxy.Core.Users.Models;
using Fluxy.Core.Users.Outcomes;
using Fluxy.Core.Users.ReadModels;
using Fluxy.Core.Users.Repositories;
using Fluxy.Core.Users.Rows;
using Fluxy.Core.Users.Utils;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fluxy.Application.Users.Services
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
        /// <summary>The unique index making a username unique.</summary>
        private const string UsernameIndexName = "IX_users_username";

        /// <summary>The unique index making an email address unique.</summary>
        private const string EmailIndexName = "IX_users_email";

        private readonly IUserRepository _users;
        private readonly IPendingChangeRepository _pendingChanges;
        private readonly ILoginGuardRepository _guards;
        private readonly IUnitOfWork _unitOfWork;
        private readonly PendingChangeService _pendingChangeService;
        private readonly IEmailSender _emailSender;
        private readonly IGeoIpResolver _geoIp;
        private readonly IUserLogService _userLogs;
        private readonly IHostEnvironment _environment;
        private readonly IOptionsMonitor<RegistrationOptions> _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ProfileService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfileService"/> class.
        /// </summary>
        /// <param name="users">Repository holding the accounts.</param>
        /// <param name="pendingChanges">Repository holding the staged changes.</param>
        /// <param name="guards">Repository that replaces an account's allow lists.</param>
        /// <param name="unitOfWork">Commits one change as one save.</param>
        /// <param name="pendingChangeService">The machinery of a change that waits for a code.</param>
        /// <param name="emailSender">Sender that delivers the code.</param>
        /// <param name="geoIp">Resolver that places the client address on the map.</param>
        /// <param name="userLogs">The audit log every profile outcome is recorded in.</param>
        /// <param name="environment">Host environment, for the Development bypass of local networks.</param>
        /// <param name="options">Live settings, so a configuration reload applies without a restart.</param>
        /// <param name="timeProvider">
        /// Clock the code lifetime is measured with. Injected so the window can be exercised
        /// without waiting for it.
        /// </param>
        /// <param name="logger">Logger the security relevant steps are reported to.</param>
        public ProfileService(
            IUserRepository users,
            IPendingChangeRepository pendingChanges,
            ILoginGuardRepository guards,
            IUnitOfWork unitOfWork,
            PendingChangeService pendingChangeService,
            IEmailSender emailSender,
            IGeoIpResolver geoIp,
            IUserLogService userLogs,
            IHostEnvironment environment,
            IOptionsMonitor<RegistrationOptions> options,
            TimeProvider timeProvider,
            ILogger<ProfileService> logger)
        {
            _users = users;
            _pendingChanges = pendingChanges;
            _guards = guards;
            _unitOfWork = unitOfWork;
            _pendingChangeService = pendingChangeService;
            _emailSender = emailSender;
            _geoIp = geoIp;
            _userLogs = userLogs;
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
            var profile = await _users.GetProfileAsync(userId, cancellationToken);

            if (profile is null)
            {
                return null;
            }

            // The effective status rather than the row's own: a blocked group blocks its
            // members everywhere, and the profile endpoint answers with "no such active
            // account" for exactly the accounts a sign-in would refuse.
            if (profile.Status is not UserStatus.Registered)
            {
                return null;
            }

            return new AccountProfile
            {
                Username = profile.Username,
                Email = profile.Email,
                Role = profile.Role,
                Credits = profile.Credits,
                TimeZone = profile.TimeZone,
                Permissions = profile.Permissions
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

            var candidate = await _users.FindEditableByIdAsync(userId, cancellationToken);

            if (candidate is null || candidate.RowStatus is not UserStatus.Registered)
            {
                return new ProfileChangeOutcome { Status = ProfileChangeStatus.AccountNotActive };
            }

            // A cleared zone is written rather than skipped, so that going back to "auto"
            // cannot leave the old one behind: the absence of the value is the state that
            // says "read it from the browser".
            await _users.SetTimeZoneAsync(userId, requested, cancellationToken);

            // No unique index on this column, so unlike the three changes above a lost race
            // cannot happen and the save needs no interpretation of its failures.
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Set the display time zone of {Username} to {TimeZone}.",
                candidate.Account.Username,
                requested ?? "(browser default)");

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
            var candidate = await _users.FindEditableByIdAsync(userId, cancellationToken);

            if (candidate is null || candidate.RowStatus is not UserStatus.Registered)
            {
                return null;
            }

            var rules = await _guards.GetRulesAsync(userId, cancellationToken);

            var provider = rules.FirstOrDefault(rule => rule.Kind is LoginGuardRuleKind.AutonomousSystem);

            return new LoginGuardSettings
            {
                GeoProtectionEnabled = candidate.Account.GeoProtectionEnabled,
                BindSessionToIp = candidate.Account.BindSessionToIp,
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
            string? userAgent = null,
            CancellationToken cancellationToken = default)
        {
            // Normalized once, so the anti-lock check below, the staging and the confirmation
            // all read the same values the database will hold. The rules themselves live
            // beside the store that writes them, shared with the admin service.
            var staged = LoginGuardNormalizer.Normalize(settings);
            if (staged.Errors.Count > 0)
            {
                await RecordGuardAsync(
                    userId,
                    UserLogDescriptions.LoginGuardChangeRefusedInvalidInput,
                    isSuccess: false,
                    subject: null,
                    clientAddress,
                    userAgent,
                    cancellationToken);

                return new ProfileChangeOutcome
                {
                    Status = ProfileChangeStatus.InvalidInput,
                    Errors = staged.Errors
                };
            }

            var candidate = await _users.FindEditableByIdAsync(userId, cancellationToken);

            if (candidate is null || candidate.RowStatus is not UserStatus.Registered)
            {
                await RecordGuardAsync(
                    userId,
                    UserLogDescriptions.LoginGuardChangeRefusedAccountNotActive,
                    isSuccess: false,
                    subject: candidate?.Account.Username,
                    clientAddress,
                    userAgent,
                    cancellationToken);

                return new ProfileChangeOutcome { Status = ProfileChangeStatus.AccountNotActive };
            }

            // The expensive step of this path, so the endpoint counts the attempt before it
            // calls in - the same order the sign-in endpoint uses for the same reason.
            if (string.IsNullOrEmpty(currentPassword)
                || !BCrypt.Net.BCrypt.Verify(currentPassword, candidate.Account.PasswordHash))
            {
                await RecordGuardAsync(
                    userId,
                    UserLogDescriptions.LoginGuardChangeRefusedWrongPassword,
                    isSuccess: false,
                    subject: candidate.Account.Username,
                    clientAddress,
                    userAgent,
                    cancellationToken);

                return new ProfileChangeOutcome
                {
                    Status = ProfileChangeStatus.InvalidCurrentPassword
                };
            }

            // The anti-lock: a guard that refuses the very network it is saved from is a
            // self-inflicted lockout with the password reset as its only way back. Refused
            // here, while the owner is still holding a session that proves who they are.
            if (staged.Value.GeoProtectionEnabled
                && !DevelopmentGuardBypass.BypassesAntiLock(clientAddress, _environment, _logger))
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
                    await RecordGuardAsync(
                        userId,
                        UserLogDescriptions.LoginGuardChangeRefusedWouldLockOut,
                        isSuccess: false,
                        subject: candidate.Account.Username,
                        clientAddress,
                        userAgent,
                        cancellationToken);

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

                await _guards.ApplyAsync(userId, staged.Value, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                LogGuardChanged(candidate.Account, staged.Value);

                await RecordGuardAsync(
                    userId,
                    UserLogDescriptions.LoginGuardChanged,
                    isSuccess: true,
                    subject: candidate.Account.Username,
                    clientAddress,
                    userAgent,
                    cancellationToken);

                return new ProfileChangeOutcome
                {
                    Status = ProfileChangeStatus.Applied,
                    Kind = PendingChangeKind.ChangeLoginGuard,
                    Account = ApplyGuard(candidate.Account, staged.Value)
                };
            }

            var code = OneTimeCode.Generate(options.CodeLength);

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
                // sends the visitor to the code that does exist. The attempt is still recorded
                // as a success: two requests reached the service, and the visitor is being sent
                // to a code that works.
                await RecordGuardAsync(
                    userId,
                    UserLogDescriptions.LoginGuardChangeRequested,
                    isSuccess: true,
                    subject: candidate.Account.Username,
                    clientAddress,
                    userAgent,
                    cancellationToken);

                return new ProfileChangeOutcome
                {
                    Status = ProfileChangeStatus.Submitted,
                    RowPersisted = true
                };
            }

            var result = await _emailSender.SendAsync(
                new EmailMessage
                {
                    To = candidate.Account.Email,
                    Subject = "Your Fluxy confirmation code",
                    Body = BuildCodeBody(
                        candidate.Account.Username,
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
                await RecordGuardAsync(
                    userId,
                    UserLogDescriptions.LoginGuardChangeFailedEmailDelivery,
                    isSuccess: false,
                    subject: candidate.Account.Username,
                    clientAddress,
                    userAgent,
                    cancellationToken);

                return new ProfileChangeOutcome
                {
                    Status = ProfileChangeStatus.EmailDeliveryFailed,
                    RowPersisted = true
                };
            }

            await RecordGuardAsync(
                userId,
                UserLogDescriptions.LoginGuardChangeRequested,
                isSuccess: true,
                subject: candidate.Account.Username,
                clientAddress,
                userAgent,
                cancellationToken);

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
            string? clientAddress = null,
            string? userAgent = null,
            CancellationToken cancellationToken = default)
        {
            var name = username?.Trim() ?? string.Empty;
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (name.Length is < RegistrationLimits.UsernameMinLength
                or > RegistrationLimits.UsernameMaxLength)
            {
                errors[nameof(AccountProfile.Username)] =
                [
                    $"The username must be between {RegistrationLimits.UsernameMinLength} and " +
                    $"{RegistrationLimits.UsernameMaxLength} characters."
                ];
            }

            return RequestAsync(
                userId,
                PendingChangeKind.ChangeUsername,
                targetValue: name,
                newPassword: null,
                currentPassword,
                errors,
                clientAddress,
                userAgent,
                cancellationToken);
        }

        /// <inheritdoc />
        public Task<ProfileChangeOutcome> ChangeEmailAsync(
            Guid userId,
            string currentPassword,
            string email,
            string? clientAddress = null,
            string? userAgent = null,
            CancellationToken cancellationToken = default)
        {
            var normalized = EmailNormalizer.Normalize(email);
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
                clientAddress,
                userAgent,
                cancellationToken);
        }

        /// <inheritdoc />
        public Task<ProfileChangeOutcome> ChangePasswordAsync(
            Guid userId,
            string currentPassword,
            string newPassword,
            string? clientAddress = null,
            string? userAgent = null,
            CancellationToken cancellationToken = default)
        {
            var password = newPassword ?? string.Empty;
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (!RegistrationPolicy.IsPasswordAcceptable(password))
            {
                errors["NewPassword"] =
                [
                    $"The password must be between {RegistrationLimits.PasswordMinLength} and " +
                    $"{RegistrationLimits.PasswordMaxLength} characters and contain at least one " +
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
                clientAddress,
                userAgent,
                cancellationToken);
        }

        /// <inheritdoc />
        public async Task<ProfileChangeOutcome> ConfirmAsync(
            Guid userId,
            string code,
            string? clientAddress = null,
            string? userAgent = null,
            CancellationToken cancellationToken = default)
        {
            var trimmedCode = code?.Trim() ?? string.Empty;

            // The pending row is read first because it is the only thing that names which change
            // a refusal belongs to. A confirmation for a code that confirms nothing has no kind
            // to log under, so those two branches write no row: the audit vocabulary is typed by
            // the change, and inventing a kind would put a fact in the table that never happened.
            var pending = await _pendingChanges.FindByUserIdAsync(userId, cancellationToken: cancellationToken);
            var isProfileChange = pending is not null
                && pending.Kind is not PendingChangeKind.PasswordReset;

            if (trimmedCode.Length == 0)
            {
                if (isProfileChange)
                {
                    await RecordChangeAsync(
                        userId,
                        pending!.Kind,
                        ConfirmInvalidCodePhrase(pending.Kind),
                        isSuccess: false,
                        subject: null,
                        clientAddress,
                        userAgent,
                        cancellationToken);
                }

                return new ProfileChangeOutcome { Status = ProfileChangeStatus.InvalidCode };
            }

            var candidate = await _users.FindEditableByIdAsync(userId, cancellationToken);

            if (candidate is null || candidate.RowStatus is not UserStatus.Registered)
            {
                if (isProfileChange)
                {
                    await RecordChangeAsync(
                        userId,
                        pending!.Kind,
                        AccountNotActivePhrase(pending.Kind),
                        isSuccess: false,
                        subject: candidate?.Account.Username,
                        clientAddress,
                        userAgent,
                        cancellationToken);
                }

                return new ProfileChangeOutcome { Status = ProfileChangeStatus.AccountNotActive };
            }

            // A code minted for the public password reset form never confirms a profile change,
            // and the other way round: the two flows are separate proofs, and one row can only
            // be waiting for one of them.
            if (pending is null || pending.Kind is PendingChangeKind.PasswordReset)
            {
                return new ProfileChangeOutcome { Status = ProfileChangeStatus.InvalidCode };
            }

            switch (_pendingChangeService.Verify(pending, trimmedCode))
            {
                case PendingCodeStatus.Invalid:
                    await RecordChangeAsync(
                        userId,
                        pending.Kind,
                        ConfirmInvalidCodePhrase(pending.Kind),
                        isSuccess: false,
                        subject: candidate.Account.Username,
                        clientAddress,
                        userAgent,
                        cancellationToken);

                    return new ProfileChangeOutcome { Status = ProfileChangeStatus.InvalidCode };

                case PendingCodeStatus.Expired:
                    await RecordChangeAsync(
                        userId,
                        pending.Kind,
                        ConfirmExpiredCodePhrase(pending.Kind),
                        isSuccess: false,
                        subject: candidate.Account.Username,
                        clientAddress,
                        userAgent,
                        cancellationToken);

                    return new ProfileChangeOutcome { Status = ProfileChangeStatus.CodeExpired };
            }

            // The guard carries its content in the payload rather than in the staged columns,
            // so it is confirmed on its own branch: the uniqueness check below has nothing to
            // say about networks, and the generic apply knows nothing about rules.
            if (pending.Kind is PendingChangeKind.ChangeLoginGuard)
            {
                return await ConfirmLoginGuardAsync(
                    candidate,
                    pending,
                    clientAddress,
                    userAgent,
                    cancellationToken);
            }

            // The value was checked when the change was requested, and the code may have spent
            // fifteen minutes in an inbox while somebody else took the same username. The row is
            // the authority, so it is checked again here rather than trusted.
            if (await IsTakenAsync(userId, pending.Kind, pending.TargetValue, cancellationToken))
            {
                await RecordChangeAsync(
                    userId,
                    pending.Kind,
                    AlreadyExistsPhrase(pending.Kind),
                    isSuccess: false,
                    subject: candidate.Account.Username,
                    clientAddress,
                    userAgent,
                    cancellationToken);

                return new ProfileChangeOutcome { Status = ProfileChangeStatus.AlreadyExists };
            }

            await ApplyChangeAsync(
                candidate.Account.Id,
                pending.Kind,
                pending.TargetValue,
                pending.NewPasswordHash,
                cancellationToken);

            await _pendingChangeService.RemoveAsync(pending.Id, cancellationToken);

            if (!await SaveAsync(cancellationToken))
            {
                await RecordChangeAsync(
                    userId,
                    pending.Kind,
                    AlreadyExistsPhrase(pending.Kind),
                    isSuccess: false,
                    subject: candidate.Account.Username,
                    clientAddress,
                    userAgent,
                    cancellationToken);

                return new ProfileChangeOutcome { Status = ProfileChangeStatus.AlreadyExists };
            }

            _logger.LogInformation(
                "Applied a confirmed {Kind} for {Username}.",
                pending.Kind,
                candidate.Account.Username);

            if (pending.Kind is PendingChangeKind.ChangeUsername or PendingChangeKind.ChangeEmail)
            {
                // A rename is the one profile change whose detail matters after the fact: the
                // subject is the name the account read under when the change was recorded, so
                // without the old value the row would not say what it used to be.
                await RecordRenameAsync(
                    userId,
                    pending.Kind,
                    ConfirmedPhrase(pending.Kind),
                    candidate.Account.Username,
                    CurrentValue(candidate.Account, pending.Kind),
                    pending.TargetValue,
                    clientAddress,
                    userAgent,
                    cancellationToken);
            }
            else
            {
                await RecordChangeAsync(
                    userId,
                    pending.Kind,
                    ConfirmedPhrase(pending.Kind),
                    isSuccess: true,
                    subject: candidate.Account.Username,
                    clientAddress,
                    userAgent,
                    cancellationToken);
            }

            return new ProfileChangeOutcome
            {
                Status = ProfileChangeStatus.Confirmed,
                Kind = pending.Kind,
                Account = ApplyChange(
                    candidate.Account,
                    pending.Kind,
                    pending.TargetValue,
                    pending.NewPasswordHash)
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
            string? clientAddress,
            string? userAgent,
            CancellationToken cancellationToken)
        {
            if (errors.Count > 0)
            {
                await RecordChangeAsync(
                    userId,
                    kind,
                    InvalidInputPhrase(kind),
                    isSuccess: false,
                    subject: null,
                    clientAddress,
                    userAgent,
                    cancellationToken);

                return new ProfileChangeOutcome
                {
                    Status = ProfileChangeStatus.InvalidInput,
                    Errors = errors
                };
            }

            var candidate = await _users.FindEditableByIdAsync(userId, cancellationToken);

            if (candidate is null || candidate.RowStatus is not UserStatus.Registered)
            {
                await RecordChangeAsync(
                    userId,
                    kind,
                    AccountNotActivePhrase(kind),
                    isSuccess: false,
                    subject: candidate?.Account.Username,
                    clientAddress,
                    userAgent,
                    cancellationToken);

                return new ProfileChangeOutcome { Status = ProfileChangeStatus.AccountNotActive };
            }

            // Checked after the row is loaded and before anything is written, but it is still
            // the expensive step of this path, so the endpoint counts the attempt before it
            // calls in - the same order the sign-in endpoint uses for the same reason.
            if (string.IsNullOrEmpty(currentPassword)
                || !BCrypt.Net.BCrypt.Verify(currentPassword, candidate.Account.PasswordHash))
            {
                await RecordChangeAsync(
                    userId,
                    kind,
                    WrongPasswordPhrase(kind),
                    isSuccess: false,
                    subject: candidate.Account.Username,
                    clientAddress,
                    userAgent,
                    cancellationToken);

                return new ProfileChangeOutcome
                {
                    Status = ProfileChangeStatus.InvalidCurrentPassword
                };
            }

            if (await IsTakenAsync(userId, kind, targetValue, cancellationToken))
            {
                await RecordChangeAsync(
                    userId,
                    kind,
                    AlreadyExistsPhrase(kind),
                    isSuccess: false,
                    subject: candidate.Account.Username,
                    clientAddress,
                    userAgent,
                    cancellationToken);

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

                var passwordHash = newPassword is null
                    ? null
                    : BCrypt.Net.BCrypt.HashPassword(newPassword);

                await ApplyChangeAsync(
                    candidate.Account.Id,
                    kind,
                    targetValue,
                    passwordHash,
                    cancellationToken);

                if (!await SaveAsync(cancellationToken))
                {
                    await RecordChangeAsync(
                        userId,
                        kind,
                        AlreadyExistsPhrase(kind),
                        isSuccess: false,
                        subject: candidate.Account.Username,
                        clientAddress,
                        userAgent,
                        cancellationToken);

                    return new ProfileChangeOutcome { Status = ProfileChangeStatus.AlreadyExists };
                }

                if (kind is PendingChangeKind.ChangeUsername or PendingChangeKind.ChangeEmail)
                {
                    await RecordRenameAsync(
                        userId,
                        kind,
                        ConfirmedPhrase(kind),
                        candidate.Account.Username,
                        CurrentValue(candidate.Account, kind),
                        targetValue,
                        clientAddress,
                        userAgent,
                        cancellationToken);
                }
                else
                {
                    await RecordChangeAsync(
                        userId,
                        kind,
                        ConfirmedPhrase(kind),
                        isSuccess: true,
                        subject: candidate.Account.Username,
                        clientAddress,
                        userAgent,
                        cancellationToken);
                }

                return new ProfileChangeOutcome
                {
                    Status = ProfileChangeStatus.Applied,
                    Kind = kind,
                    Account = ApplyChange(candidate.Account, kind, targetValue, passwordHash)
                };
            }

            var code = OneTimeCode.Generate(options.CodeLength);

            if (!await _pendingChangeService.StageAsync(
                    userId,
                    kind,
                    code,
                    now.Add(options.CodeLifetime),
                    targetValue: targetValue,
                    newPassword: newPassword,
                    cancellationToken: cancellationToken))
            {
                // Two requests for the same account arrived at the same moment and the unique
                // index let one of them win. The winner stored a row and mailed a code of its
                // own, so this one has nothing of its own to send - answering "check your inbox"
                // sends the visitor to the code that does exist. The attempt is still recorded
                // as a success, exactly like the guard branch and the password reset: two
                // requests reached the service, and the visitor is being sent to a code that
                // works.
                await RecordChangeAsync(
                    userId,
                    kind,
                    RequestedPhrase(kind),
                    isSuccess: true,
                    subject: candidate.Account.Username,
                    clientAddress,
                    userAgent,
                    cancellationToken);

                return new ProfileChangeOutcome
                {
                    Status = ProfileChangeStatus.Submitted,
                    RowPersisted = true
                };
            }

            var recipient = kind is PendingChangeKind.ChangeEmail
                ? targetValue!
                : candidate.Account.Email;

            var result = await _emailSender.SendAsync(
                new EmailMessage
                {
                    To = recipient,
                    Subject = "Your Fluxy confirmation code",
                    Body = BuildCodeBody(
                        candidate.Account.Username,
                        kind,
                        targetValue,
                        code,
                        options.CodeLifetime)
                },
                cancellationToken);

            if (result is not EmailSendResult.Sent)
            {
                // The row is stored, so the caller has to be told that the code did not arrive.
                // A new request replaces it once the visitor asks again.
                await RecordChangeAsync(
                    userId,
                    kind,
                    EmailDeliveryPhrase(kind),
                    isSuccess: false,
                    subject: candidate.Account.Username,
                    clientAddress,
                    userAgent,
                    cancellationToken);

                return new ProfileChangeOutcome
                {
                    Status = ProfileChangeStatus.EmailDeliveryFailed,
                    RowPersisted = true
                };
            }

            await RecordChangeAsync(
                userId,
                kind,
                RequestedPhrase(kind),
                isSuccess: true,
                subject: candidate.Account.Username,
                clientAddress,
                userAgent,
                cancellationToken);

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
                PendingChangeKind.ChangeUsername => await _users.IsUsernameTakenAsync(
                    targetValue,
                    userId,
                    cancellationToken),

                PendingChangeKind.ChangeEmail => await _users.IsEmailTakenAsync(
                    targetValue,
                    userId,
                    cancellationToken),

                _ => false
            };
        }

        /// <summary>
        /// Stages the change on the account. The row that owns the value does the rest: the
        /// unique indexes are what actually refuse a taken one.
        /// </summary>
        private async Task ApplyChangeAsync(
            Guid userId,
            PendingChangeKind kind,
            string? targetValue,
            string? passwordHash,
            CancellationToken cancellationToken)
        {
            switch (kind)
            {
                case PendingChangeKind.ChangeUsername when targetValue is not null:
                    await _users.SetUsernameAsync(userId, targetValue, cancellationToken);
                    break;

                case PendingChangeKind.ChangeEmail when targetValue is not null:
                    await _users.SetEmailAsync(userId, targetValue, cancellationToken);
                    break;

                case PendingChangeKind.ChangePassword when passwordHash is not null:
                    await _users.SetPasswordHashAsync(userId, passwordHash, cancellationToken);
                    break;
            }
        }

        /// <summary>The value a rename replaces, read from the account before it was changed.</summary>
        private static string? CurrentValue(User account, PendingChangeKind kind) => kind switch
        {
            PendingChangeKind.ChangeUsername => account.Username,
            PendingChangeKind.ChangeEmail => account.Email,
            _ => null
        };

        /// <summary>
        /// The account model after the change, without reading the row again.
        /// </summary>
        private static User ApplyChange(
            User account,
            PendingChangeKind kind,
            string? targetValue,
            string? passwordHash)
            => kind switch
            {
                PendingChangeKind.ChangeUsername when targetValue is not null =>
                    account with { Username = targetValue },
                PendingChangeKind.ChangeEmail when targetValue is not null =>
                    account with { Email = targetValue },
                PendingChangeKind.ChangePassword when passwordHash is not null =>
                    account with { PasswordHash = passwordHash },
                _ => account
            };

        /// <summary>
        /// The account model after its guard switches changed.
        /// </summary>
        private static User ApplyGuard(User account, NormalizedLoginGuard guard)
            => account with
            {
                GeoProtectionEnabled = guard.GeoProtectionEnabled,
                BindSessionToIp = guard.BindSessionToIp
            };

        /// <summary>
        /// <summary>
        /// Saves the account, turning a lost race for a unique value into the same refusal a
        /// sequential check would have produced.
        /// </summary>
        /// <returns>False when the value was taken by a concurrent request.</returns>
        /// <remarks>
        /// Only the two indexes a profile change can legitimately collide on are interpreted: a
        /// refusal by anything else is a bug in this service and is left to surface as one.
        /// </remarks>
        private async Task<bool> SaveAsync(CancellationToken cancellationToken)
            => await _unitOfWork.TrySaveAsync(
                [UsernameIndexName, EmailIndexName],
                cancellationToken) is null;

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
            return await _pendingChangeService.StageAsync(
                userId,
                PendingChangeKind.ChangeLoginGuard,
                code,
                expiresAt,
                payload: JsonSerializer.Serialize(staged),
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Applies a confirmed guard: the switches go on the account, the lists replace
        /// whatever the account held before.
        /// </summary>
        private async Task<ProfileChangeOutcome> ConfirmLoginGuardAsync(
            EditableAccount candidate,
            PendingChange pending,
            string? clientAddress,
            string? userAgent,
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
                await RecordGuardAsync(
                    candidate.Account.Id,
                    UserLogDescriptions.LoginGuardChangeConfirmFailedInvalidCode,
                    isSuccess: false,
                    subject: candidate.Account.Username,
                    clientAddress,
                    userAgent,
                    cancellationToken);

                return new ProfileChangeOutcome { Status = ProfileChangeStatus.InvalidCode };
            }

            await _guards.ApplyAsync(candidate.Account.Id, staged, cancellationToken);
            await _pendingChangeService.RemoveAsync(pending.Id, cancellationToken);

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (UniqueViolationException)
            {
                // Two confirmations of the same code raced and the unique index let one win.
                // The code is spent either way, so the loser gets the same answer a wrong code
                // gets rather than a conflict about a list it never saw.
                await RecordGuardAsync(
                    candidate.Account.Id,
                    UserLogDescriptions.LoginGuardChangeConfirmFailedInvalidCode,
                    isSuccess: false,
                    subject: candidate.Account.Username,
                    clientAddress,
                    userAgent,
                    cancellationToken);

                return new ProfileChangeOutcome { Status = ProfileChangeStatus.InvalidCode };
            }

            LogGuardChanged(candidate.Account, staged);

            _logger.LogInformation(
                "Applied a confirmed {Kind} for {Username}.",
                pending.Kind,
                candidate.Account.Username);

            await RecordGuardAsync(
                candidate.Account.Id,
                UserLogDescriptions.LoginGuardChangeConfirmed,
                isSuccess: true,
                subject: candidate.Account.Username,
                clientAddress,
                userAgent,
                cancellationToken);

            return new ProfileChangeOutcome
            {
                Status = ProfileChangeStatus.Confirmed,
                Kind = pending.Kind,
                Account = ApplyGuard(candidate.Account, staged)
            };
        }

        /// <summary>
        /// Records one profile-change outcome in the audit log, typed by the kind of change.
        /// </summary>
        /// <remarks>
        /// <b>Neither password ever reaches this method.</b> The phrase names the change, the
        /// subject is the login name, and nothing else is written: a permanent audit row
        /// holding the current or the new password would be a credential nobody can rotate.
        /// </remarks>
        private Task RecordChangeAsync(
            Guid userId,
            PendingChangeKind kind,
            string phrase,
            bool isSuccess,
            string? subject,
            string? clientAddress,
            string? userAgent,
            CancellationToken cancellationToken)
            => _userLogs.RecordAsync(
                new NewUserLog
                {
                    UserId = userId,
                    RecordType = RecordTypeOf(kind),
                    IsSuccess = isSuccess,
                    Description = UserLogDescriptions.Describe(phrase, subject),
                    Ip = clientAddress,
                    UserAgent = userAgent
                },
                cancellationToken);

        /// <summary>
        /// Records one rename - a username or email change - with the value it replaced, so the
        /// row reads as <c>old → new</c> after the account no longer carries the old value.
        /// </summary>
        /// <remarks>
        /// Only the two rename kinds pass a detail. A password change never does: the detail
        /// would be a credential, and the phrase already says the password changed.
        /// </remarks>
        private Task RecordRenameAsync(
            Guid userId,
            PendingChangeKind kind,
            string phrase,
            string? subject,
            string? before,
            string? after,
            string? clientAddress,
            string? userAgent,
            CancellationToken cancellationToken)
            => _userLogs.RecordAsync(
                new NewUserLog
                {
                    UserId = userId,
                    RecordType = RecordTypeOf(kind),
                    IsSuccess = true,
                    Description = UserLogDescriptions.Renamed(phrase, subject, before, after),
                    Ip = clientAddress,
                    UserAgent = userAgent
                },
                cancellationToken);

        /// <summary>Records one login guard outcome in the audit log.</summary>
        private Task RecordGuardAsync(
            Guid userId,
            string phrase,
            bool isSuccess,
            string? subject,
            string? clientAddress,
            string? userAgent,
            CancellationToken cancellationToken)
            => _userLogs.RecordAsync(
                new NewUserLog
                {
                    UserId = userId,
                    RecordType = UserLogRecordType.LoginGuardChange,
                    IsSuccess = isSuccess,
                    Description = UserLogDescriptions.Describe(phrase, subject),
                    Ip = clientAddress,
                    UserAgent = userAgent
                },
                cancellationToken);

        /// <summary>The record type a staged profile change is logged under.</summary>
        private static UserLogRecordType RecordTypeOf(PendingChangeKind kind) => kind switch
        {
            PendingChangeKind.ChangeUsername => UserLogRecordType.UsernameChange,
            PendingChangeKind.ChangeEmail => UserLogRecordType.EmailChange,
            PendingChangeKind.ChangePassword => UserLogRecordType.PasswordChange,
            _ => UserLogRecordType.LoginGuardChange
        };

        /// <summary>The phrase a refused value of the named change is recorded with.</summary>
        private static string InvalidInputPhrase(PendingChangeKind kind) => kind switch
        {
            PendingChangeKind.ChangeUsername => UserLogDescriptions.UsernameChangeRefusedInvalidInput,
            PendingChangeKind.ChangeEmail => UserLogDescriptions.EmailChangeRefusedInvalidInput,
            _ => UserLogDescriptions.PasswordChangeRefusedInvalidInput
        };

        /// <summary>The phrase a wrong current password is recorded with, by kind.</summary>
        private static string WrongPasswordPhrase(PendingChangeKind kind) => kind switch
        {
            PendingChangeKind.ChangeUsername => UserLogDescriptions.UsernameChangeRefusedWrongPassword,
            PendingChangeKind.ChangeEmail => UserLogDescriptions.EmailChangeRefusedWrongPassword,
            _ => UserLogDescriptions.PasswordChangeRefusedWrongPassword
        };

        /// <summary>The phrase an inactive account is recorded with, by kind.</summary>
        private static string AccountNotActivePhrase(PendingChangeKind kind) => kind switch
        {
            PendingChangeKind.ChangeUsername =>
                UserLogDescriptions.UsernameChangeRefusedAccountNotActive,
            PendingChangeKind.ChangeEmail => UserLogDescriptions.EmailChangeRefusedAccountNotActive,
            _ => UserLogDescriptions.PasswordChangeRefusedAccountNotActive
        };

        /// <summary>The phrase a taken value is recorded with, by kind.</summary>
        private static string AlreadyExistsPhrase(PendingChangeKind kind) => kind switch
        {
            PendingChangeKind.ChangeUsername => UserLogDescriptions.UsernameChangeRefusedAlreadyExists,
            PendingChangeKind.ChangeEmail => UserLogDescriptions.EmailChangeRefusedAlreadyExists,
            _ => UserLogDescriptions.PasswordChangeRefusedInvalidInput
        };

        /// <summary>The phrase a stored change is recorded with, by kind.</summary>
        private static string RequestedPhrase(PendingChangeKind kind) => kind switch
        {
            PendingChangeKind.ChangeUsername => UserLogDescriptions.UsernameChangeRequested,
            PendingChangeKind.ChangeEmail => UserLogDescriptions.EmailChangeRequested,
            _ => UserLogDescriptions.PasswordChangeRequested
        };

        /// <summary>The phrase an applied change is recorded with, by kind.</summary>
        private static string ConfirmedPhrase(PendingChangeKind kind) => kind switch
        {
            PendingChangeKind.ChangeUsername => UserLogDescriptions.UsernameChanged,
            PendingChangeKind.ChangeEmail => UserLogDescriptions.EmailChanged,
            _ => UserLogDescriptions.PasswordChanged
        };

        /// <summary>The phrase a failed delivery is recorded with, by kind.</summary>
        private static string EmailDeliveryPhrase(PendingChangeKind kind) => kind switch
        {
            PendingChangeKind.ChangeUsername =>
                UserLogDescriptions.UsernameChangeFailedEmailDelivery,
            PendingChangeKind.ChangeEmail => UserLogDescriptions.EmailChangeFailedEmailDelivery,
            _ => UserLogDescriptions.PasswordChangeFailedEmailDelivery
        };

        /// <summary>The phrase a wrong confirmation code is recorded with, by kind.</summary>
        private static string ConfirmInvalidCodePhrase(PendingChangeKind kind) => kind switch
        {
            PendingChangeKind.ChangeUsername =>
                UserLogDescriptions.UsernameChangeConfirmFailedInvalidCode,
            PendingChangeKind.ChangeEmail => UserLogDescriptions.EmailChangeConfirmFailedInvalidCode,
            PendingChangeKind.ChangePassword =>
                UserLogDescriptions.PasswordChangeConfirmFailedInvalidCode,
            _ => UserLogDescriptions.LoginGuardChangeConfirmFailedInvalidCode
        };

        /// <summary>The phrase an expired confirmation code is recorded with, by kind.</summary>
        private static string ConfirmExpiredCodePhrase(PendingChangeKind kind) => kind switch
        {
            PendingChangeKind.ChangeUsername =>
                UserLogDescriptions.UsernameChangeConfirmFailedExpiredCode,
            PendingChangeKind.ChangeEmail => UserLogDescriptions.EmailChangeConfirmFailedExpiredCode,
            PendingChangeKind.ChangePassword =>
                UserLogDescriptions.PasswordChangeConfirmFailedExpiredCode,
            _ => UserLogDescriptions.LoginGuardChangeConfirmFailedExpiredCode
        };

        /// <summary>
        /// Writes the switches and replaces the rules. The old lists are deleted by the guard
        /// repository, so a guard that drops a network does not keep it beside the new one.
        /// </summary>
        private void LogGuardChanged(User account, NormalizedLoginGuard guard)
            => _logger.LogInformation(
                "Set the login guard of {Username}: protection {Protection}, session binding " +
                "{Binding}, {Ips} address(es), country {Country}, provider AS{Asn}.",
                account.Username,
                guard.GeoProtectionEnabled ? "on" : "off",
                guard.BindSessionToIp ? "on" : "off",
                guard.AllowedIps.Length,
                guard.AllowedCountry ?? "any",
                guard.AllowedAutonomousSystemNumber?.ToString(CultureInfo.InvariantCulture) ?? "any");

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

            return CodeMessage.Build(
                username,
                $"Your Fluxy confirmation code is {code}.",
                $"""
                It confirms that you asked for {askedFor}. It is valid for {minutes} minutes.
                If you did not ask for this, ignore this message - nothing changes until the
                code is confirmed, and you can simply request a new change to replace it.
                """);
        }
    }
}
