using Fluxy.Application.Users.Options;
using Fluxy.Application.Users.Utils;
using Fluxy.Core.Email.Commands;
using Fluxy.Core.Email.Enums;
using Fluxy.Core.Email.Interfaces;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Persistence.Utils;
using Fluxy.Core.Users.Commands;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Exceptions;
using Fluxy.Core.Users.Interfaces;
using Fluxy.Core.Users.Outcomes;
using Fluxy.Core.Users.Repositories;
using Fluxy.Core.Users.Rows;
using Fluxy.Core.Users.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fluxy.Application.Users.Services
{
    /// <summary>
    /// Stores a requested account and hands out the one-time code that confirms it.
    /// </summary>
    /// <remarks>
    /// The order of the checks is the design of this class. Everything that can refuse the
    /// request without touching the database comes first, and the two deliberately expensive
    /// steps - hashing the password and comparing the code - come after every cheap check, so a
    /// rejected request costs a lookup rather than a hundred milliseconds of key derivation.
    ///
    /// The account is written before the message is sent, not after. A confirmation code that
    /// exists only in memory could not survive a crash, and the alternative of sending first
    /// would mail a code for a row that may never be committed. An account whose message could
    /// not be delivered therefore stays in <see cref="UserStatus.Unregistered"/> with a pending
    /// code, and a later request replaces that code.
    /// </remarks>
    public sealed class RegistrationService : IRegistrationService
    {
        /// <summary>The unique index making a username unique.</summary>
        private const string UsernameIndexName = "IX_users_username";

        /// <summary>The unique index making an email address unique.</summary>
        private const string EmailIndexName = "IX_users_email";

        private readonly IUserRepository _users;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailSender _emailSender;
        private readonly IOptionsMonitor<RegistrationOptions> _options;
        private readonly TimeProvider _timeProvider;
        private readonly IUserLogService _userLogs;
        private readonly ILogger<RegistrationService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RegistrationService"/> class.
        /// </summary>
        /// <param name="users">Repository holding the accounts.</param>
        /// <param name="unitOfWork">Commits one registration step as one save.</param>
        /// <param name="emailSender">Sender that delivers the code.</param>
        /// <param name="options">Live settings, so a configuration reload applies without a restart.</param>
        /// <param name="timeProvider">
        /// Clock the code lifetime is measured with. Injected so the window can be exercised
        /// without waiting for it.
        /// </param>
        /// <param name="userLogs">The audit log every outcome of this flow is recorded in.</param>
        /// <param name="logger">Logger the security relevant steps are reported to.</param>
        public RegistrationService(
            IUserRepository users,
            IUnitOfWork unitOfWork,
            IEmailSender emailSender,
            IOptionsMonitor<RegistrationOptions> options,
            TimeProvider timeProvider,
            IUserLogService userLogs,
            ILogger<RegistrationService> logger)
        {
            _users = users;
            _unitOfWork = unitOfWork;
            _emailSender = emailSender;
            _options = options;
            _timeProvider = timeProvider;
            _userLogs = userLogs;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<RegistrationOutcome> RegisterAsync(
            NewRegistration registration,
            CancellationToken cancellationToken = default)
        {
            // Checked before anything else, including the cheap input checks. An installation
            // that cannot mail cannot complete a registration, so there is no point in accepting
            // the values, and returning early keeps a visitor free to retry the moment a mail
            // server appears rather than having spent their request on a stored account.
            if (!_emailSender.IsConfigured)
            {
                _logger.LogWarning(
                    "A registration was refused because this installation has no mail server " +
                    "configured. See the Email configuration section.");

                await RecordAsync(
                    registration,
                    UserLogDescriptions.RegistrationRefusedNotConfigured,
                    isSuccess: false,
                    userId: null,
                    cancellationToken);

                return new RegistrationOutcome
                {
                    Status = RegistrationStatus.RegistrationNotConfigured
                };
            }

            var username = registration.Username?.Trim() ?? string.Empty;
            var email = EmailNormalizer.Normalize(registration.Email);
            var password = registration.Password ?? string.Empty;

            var errors = Validate(username, email, password);

            if (errors.Count > 0)
            {
                await RecordAsync(
                    registration,
                    UserLogDescriptions.RegistrationRefusedInvalidInput,
                    isSuccess: false,
                    userId: null,
                    cancellationToken);

                return new RegistrationOutcome
                {
                    Status = RegistrationStatus.InvalidInput,
                    Errors = errors
                };
            }

            var options = _options.CurrentValue;

            var byUsername = await _users.FindForRegistrationByUsernameAsync(
                username,
                cancellationToken);

            // The address is matched on the normalized value only. The column is compared case
            // sensitively, so storing the normalized value is what keeps "Admin@example.com"
            // from sitting next to "admin@example.com" as a second free address on the same
            // mailbox.
            var byEmail = await _users.FindForRegistrationByEmailAsync(
                email,
                cancellationToken);

            var now = _timeProvider.GetUtcNow();
            var code = OneTimeCode.Generate(options.CodeLength);
            var codeHash = BCrypt.Net.BCrypt.HashPassword(code);

            // An address that never finished registering may be taken over: the pending code is
            // replaced, the password is the one just submitted, and a fresh message goes out. A
            // finished account is never touched, because resetting its password from a public
            // form would be an account takeover.
            var reusable = TryReusePending(byUsername, username, email)
                ? byUsername
                : TryReusePending(byEmail, username, email)
                    ? byEmail
                    : null;

            if (reusable is not null)
            {
                await _users.UpdatePendingRegistrationAsync(
                    reusable.Id,
                    BCrypt.Net.BCrypt.HashPassword(password),
                    codeHash,
                    now.Add(options.CodeLifetime),
                    cancellationToken);

                await SaveAsync(cancellationToken);

                return await DeliverAsync(
                    registration,
                    reusable.Id,
                    username,
                    email,
                    code,
                    rowPersisted: true,
                    cancellationToken);
            }

            if (IsTaken(byUsername, username) || IsTaken(byEmail, email))
            {
                await RecordAsync(
                    registration,
                    UserLogDescriptions.RegistrationRefusedAlreadyExists,
                    isSuccess: false,
                    userId: null,
                    cancellationToken);

                return new RegistrationOutcome
                {
                    Status = RegistrationStatus.AlreadyExists
                };
            }

            var accountId = await _users.AddAsync(
                new NewAccount
                {
                    Username = username,
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),

                    // The base clients group, not a level: a fresh account arrives with the
                    // group every client belongs to, and everything about what it may do follows
                    // from the group rather than from anything written on this row.
                    GroupId = BaseUserGroups.Clients,
                    Status = UserStatus.Unregistered,
                    RegistrationCodeHash = codeHash,
                    RegistrationCodeExpiresAt = now.Add(options.CodeLifetime)
                },
                cancellationToken);

            await SaveAsync(cancellationToken);

            _logger.LogInformation(
                "Stored a registration request for {Username}. The account stays " +
                "{Status} until its code is confirmed.",
                username,
                UserStatus.Unregistered);

            return await DeliverAsync(
                registration,
                accountId,
                username,
                email,
                code,
                rowPersisted: true,
                cancellationToken);
        }

        /// <inheritdoc />
        public async Task<RegistrationOutcome> ConfirmAsync(
            string email,
            string code,
            string? clientAddress = null,
            string? userAgent = null,
            CancellationToken cancellationToken = default)
        {
            var normalized = EmailNormalizer.Normalize(email);
            var trimmedCode = code?.Trim() ?? string.Empty;

            if (trimmedCode.Length == 0)
            {
                await RecordConfirmAsync(
                    normalized,
                    UserLogDescriptions.RegistrationConfirmFailedInvalidCode,
                    isSuccess: false,
                    userId: null,
                    clientAddress,
                    userAgent,
                    cancellationToken);

                return new RegistrationOutcome
                {
                    Status = RegistrationStatus.InvalidCode
                };
            }

            var user = await _users.FindEditableByEmailAsync(normalized, cancellationToken);

            if (user is null || user.RowStatus is not UserStatus.Unregistered
                || user.Account.RegistrationCodeHash is null)
            {
                // Reported exactly like a wrong code, so the endpoint does not confirm which
                // addresses have an account. The audit row is the one place that distinction is
                // kept: it names the address that was typed and says the code did not match,
                // without confirming whether the address holds an account.
                await RecordConfirmAsync(
                    normalized,
                    UserLogDescriptions.RegistrationConfirmFailedInvalidCode,
                    isSuccess: false,
                    userId: user?.Account.Id,
                    clientAddress,
                    userAgent,
                    cancellationToken);

                return new RegistrationOutcome
                {
                    Status = RegistrationStatus.InvalidCode
                };
            }

            // BCrypt does not store the hash - it recomputes it from the salt inside the stored
            // value - so verifying a code costs as much as producing one. That is the point of
            // the algorithm, and it is why the endpoint has to be throttled rather than left
            // open as a free way to make the server do work.
            if (!BCrypt.Net.BCrypt.Verify(trimmedCode, user.Account.RegistrationCodeHash))
            {
                await RecordConfirmAsync(
                    normalized,
                    UserLogDescriptions.RegistrationConfirmFailedInvalidCode,
                    isSuccess: false,
                    userId: user.Account.Id,
                    clientAddress,
                    userAgent,
                    cancellationToken);

                return new RegistrationOutcome
                {
                    Status = RegistrationStatus.InvalidCode
                };
            }

            if (user.Account.RegistrationCodeExpiresAt is not { } expiresAt
                || _timeProvider.GetUtcNow() > expiresAt)
            {
                await RecordConfirmAsync(
                    normalized,
                    UserLogDescriptions.RegistrationConfirmFailedExpiredCode,
                    isSuccess: false,
                    userId: user.Account.Id,
                    clientAddress,
                    userAgent,
                    cancellationToken);

                return new RegistrationOutcome
                {
                    Status = RegistrationStatus.CodeExpired
                };
            }

            // The code is spent either way. An expired one is cleared so a later confirmation
            // reports "no pending code" rather than the same expiry forever.
            var registeredAt = _timeProvider.GetUtcNow();

            await _users.ConfirmRegistrationAsync(
                user.Account.Id,
                registeredAt,
                cancellationToken);

            await SaveAsync(cancellationToken);

            var confirmed = user.Account with
            {
                Status = UserStatusComposition.Combine(UserStatus.Registered, user.GroupStatus),
                RegisteredAt = registeredAt,
                RegistrationCodeHash = null,
                RegistrationCodeExpiresAt = null
            };

            _logger.LogInformation(
                "Confirmed the registration of {Username}. The account is now {Status}.",
                confirmed.Username,
                confirmed.Status);

            await RecordConfirmAsync(
                normalized,
                UserLogDescriptions.RegistrationConfirmed,
                isSuccess: true,
                userId: confirmed.Id,
                clientAddress,
                userAgent,
                cancellationToken);

            return new RegistrationOutcome
            {
                Status = RegistrationStatus.Confirmed,

                // The account is handed back rather than looked up again by the transport layer.
                // Proving a mailbox was never a right to name an account, and re-reading the row
                // by the address the visitor typed would make that string the credential.
                ConfirmedAccount = confirmed
            };
        }

        /// <summary>
        /// Whether an existing row may be taken over by this request.
        /// </summary>
        /// <returns>True when the row is an unfinished registration of these values.</returns>
        private static bool TryReusePending(
            StoredAccount? existing,
            string username,
            string email)
            => existing is not null
                && existing.RowStatus is UserStatus.Unregistered
                && existing.Username == username
                && existing.Email == email;

        /// <summary>
        /// Whether a row blocks this request because it holds one of the two taken values.
        /// </summary>
        private static bool IsTaken(StoredAccount? existing, string value)
            => existing is not null
                && (existing.Username == value || existing.Email == value);

        /// <summary>
        /// Sends the code and turns the delivery outcome into the result of the call.
        /// </summary>
        private async Task<RegistrationOutcome> DeliverAsync(
            NewRegistration registration,
            Guid accountId,
            string username,
            string email,
            string code,
            bool rowPersisted,
            CancellationToken cancellationToken)
        {
            var result = await _emailSender.SendAsync(
                new EmailMessage
                {
                    To = email,
                    Subject = "Your Fluxy registration code",
                    Body = BuildCodeBody(username, code, _options.CurrentValue.CodeLifetime)
                },
                cancellationToken);

            if (result is EmailSendResult.Sent)
            {
                await RecordAsync(
                    registration,
                    UserLogDescriptions.RegistrationSubmitted,
                    isSuccess: true,
                    userId: accountId,
                    cancellationToken);

                return new RegistrationOutcome
                {
                    Status = RegistrationStatus.Submitted,
                    RowPersisted = rowPersisted
                };
            }

            await RecordAsync(
                registration,
                UserLogDescriptions.RegistrationFailedEmailDelivery,
                isSuccess: false,
                userId: accountId,
                cancellationToken);

            return new RegistrationOutcome
            {
                // The account is already stored, so the caller has to be told that the code
                // did not arrive. A new request replaces it once the window is over.
                Status = RegistrationStatus.EmailDeliveryFailed,
                RowPersisted = rowPersisted
            };
        }

        /// <summary>
        /// Records one registration outcome in the audit log.
        /// </summary>
        /// <remarks>
        /// The subject is the login name the visitor typed, not the normalized address: it is the
        /// identifier a reader recognises, and the log is the one place the typed value is kept
        /// after the request is gone. The network comes straight from the request and the GeoIP
        /// snapshot is the log service's to take.
        /// </remarks>
        private Task RecordAsync(
            NewRegistration registration,
            string phrase,
            bool isSuccess,
            Guid? userId,
            CancellationToken cancellationToken)
            => _userLogs.RecordAsync(
                new NewUserLog
                {
                    UserId = userId,
                    RecordType = UserLogRecordType.RegistrationSubmit,
                    IsSuccess = isSuccess,
                    Description = UserLogDescriptions.Describe(
                        phrase,
                        registration.Username?.Trim()),
                    Ip = registration.ClientAddress,
                    UserAgent = registration.UserAgent
                },
                cancellationToken);

        /// <summary>Records one registration confirmation outcome in the audit log.</summary>
        /// <remarks>
        /// The subject is the address the visitor typed, because that is what a confirmation is
        /// keyed by; the account identifier is carried separately and is null when the address
        /// belongs to no account. A wrong code against a real account names that account, which
        /// is a fact the operator may see and the client never is told.
        /// </remarks>
        private Task RecordConfirmAsync(
            string email,
            string phrase,
            bool isSuccess,
            Guid? userId,
            string? clientAddress,
            string? userAgent,
            CancellationToken cancellationToken)
            => _userLogs.RecordAsync(
                new NewUserLog
                {
                    UserId = userId,
                    RecordType = UserLogRecordType.RegistrationConfirm,
                    IsSuccess = isSuccess,
                    Description = UserLogDescriptions.Describe(phrase, email),
                    Ip = clientAddress,
                    UserAgent = userAgent
                },
                cancellationToken);

        /// <summary>
        /// Applies the registration rules to the normalized values.
        /// </summary>
        private static Dictionary<string, string[]> Validate(string username, string email, string password)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (username.Length is < RegistrationLimits.UsernameMinLength
                or > RegistrationLimits.UsernameMaxLength)
            {
                errors[nameof(NewRegistration.Username)] =
                [
                    $"The username must be between {RegistrationLimits.UsernameMinLength} and " +
                    $"{RegistrationLimits.UsernameMaxLength} characters."
                ];
            }

            if (!RegistrationPolicy.IsEmailShapeValid(email))
            {
                errors[nameof(NewRegistration.Email)] = ["The email address is not valid."];
            }

            if (!RegistrationPolicy.IsPasswordAcceptable(password))
            {
                errors[nameof(NewRegistration.Password)] =
                [
                    $"The password must be between {RegistrationLimits.PasswordMinLength} and " +
                    $"{RegistrationLimits.PasswordMaxLength} characters and contain at least one " +
                    "lower case letter, one upper case letter and one digit."
                ];
            }

            return errors;
        }

        /// <summary>
        /// Saves the account, turning a lost race for a unique value into the same refusal a
        /// sequential check would have produced.
        /// </summary>
        /// <remarks>
        /// Two requests can both pass the lookup and then both insert. The unique index is the
        /// real guard, and without this the loser would surface as a database exception on what
        /// is an ordinary outcome for a public form. Only the two indexes a registration can
        /// legitimately collide on are interpreted: a refusal by anything else is a bug in this
        /// service and is left to surface as one.
        /// </remarks>
        private async Task SaveAsync(CancellationToken cancellationToken)
        {
            var violation = await _unitOfWork.TrySaveAsync(
                [UsernameIndexName, EmailIndexName],
                cancellationToken);

            if (violation is not null)
            {
                throw new DuplicateRegistrationException(violation);
            }
        }

        /// <summary>
        /// The plain text body of the message that carries a code. The code is the only content:
        /// the account does not exist yet, so there is nothing to link to and nothing to style.
        /// </summary>
        private static string BuildCodeBody(string username, string code, TimeSpan lifetime)
        {
            var minutes = (int)lifetime.TotalMinutes;

            return CodeMessage.Build(
                username,
                $"Your Fluxy registration code is {code}.",
                $"""
                It is valid for {minutes} minutes. If you did not ask to register, ignore this
                message - the account stays inactive until the code is confirmed.
                """);
        }
    }
}
