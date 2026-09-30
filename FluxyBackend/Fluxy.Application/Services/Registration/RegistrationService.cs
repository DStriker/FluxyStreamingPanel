using System.Security.Cryptography;
using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Users;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fluxy.Application.Services.Registration
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
        /// <summary>Digits a code is built from.</summary>
        private const string CodeAlphabet = "0123456789";

        private readonly FluxyDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly IOptionsMonitor<RegistrationOptions> _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<RegistrationService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RegistrationService"/> class.
        /// </summary>
        /// <param name="context">Context holding the account.</param>
        /// <param name="emailSender">Sender that delivers the code.</param>
        /// <param name="options">Live settings, so a configuration reload applies without a restart.</param>
        /// <param name="timeProvider">
        /// Clock the code lifetime is measured with. Injected so the window can be exercised
        /// without waiting for it.
        /// </param>
        /// <param name="logger">Logger the security relevant steps are reported to.</param>
        public RegistrationService(
            FluxyDbContext context,
            IEmailSender emailSender,
            IOptionsMonitor<RegistrationOptions> options,
            TimeProvider timeProvider,
            ILogger<RegistrationService> logger)
        {
            _context = context;
            _emailSender = emailSender;
            _options = options;
            _timeProvider = timeProvider;
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

                return new RegistrationOutcome
                {
                    Status = RegistrationStatus.RegistrationNotConfigured
                };
            }

            var username = registration.Username?.Trim() ?? string.Empty;
            var email = NormalizeEmail(registration.Email);
            var password = registration.Password ?? string.Empty;

            var errors = Validate(username, email, password);

            if (errors.Count > 0)
            {
                return new RegistrationOutcome
                {
                    Status = RegistrationStatus.InvalidInput,
                    Errors = errors
                };
            }

            var options = _options.CurrentValue;

            var byUsername = await _context.Users
                .FirstOrDefaultAsync(user => user.Username == username, cancellationToken);

            // The address is matched on the normalized value only. The column is compared case
            // sensitively, so storing the normalized value is what keeps "Admin@example.com"
            // from sitting next to "admin@example.com" as a second free address on the same
            // mailbox.
            var byEmail = await _context.Users
                .FirstOrDefaultAsync(user => user.Email == email, cancellationToken);

            var now = _timeProvider.GetUtcNow();
            var code = GenerateCode(options.CodeLength);
            var codeHash = BCrypt.Net.BCrypt.HashPassword(code);

            // An address that never finished registering may be taken over: the pending code is
            // replaced, the password is the one just submitted, and a fresh message goes out. A
            // finished account is never touched, because resetting its password from a public
            // form would be an account takeover.
            if (TryReusePending(byUsername, username, email, password, code, codeHash, options, now)
                || TryReusePending(byEmail, username, email, password, code, codeHash, options, now))
            {
                await SaveAsync(cancellationToken);

                return await DeliverAsync(email, username, code, rowPersisted: true, cancellationToken);
            }

            if (IsTaken(byUsername, username) || IsTaken(byEmail, email))
            {
                return new RegistrationOutcome
                {
                    Status = RegistrationStatus.AlreadyExists
                };
            }

            _context.Users.Add(new UserEntity
            {
                Username = username,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = UserRole.Client,
                Status = UserStatus.Unregistered,
                RegistrationCodeHash = codeHash,
                RegistrationCodeExpiresAt = now.Add(options.CodeLifetime)
            });

            await SaveAsync(cancellationToken);

            _logger.LogInformation(
                "Stored a registration request for {Username}. The account stays " +
                "{Status} until its code is confirmed.",
                username,
                UserStatus.Unregistered);

            return await DeliverAsync(email, username, code, rowPersisted: true, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<RegistrationOutcome> ConfirmAsync(
            string email,
            string code,
            CancellationToken cancellationToken = default)
        {
            var normalized = NormalizeEmail(email);
            var trimmedCode = code?.Trim() ?? string.Empty;

            if (trimmedCode.Length == 0)
            {
                return new RegistrationOutcome
                {
                    Status = RegistrationStatus.InvalidCode
                };
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(entry => entry.Email == normalized, cancellationToken);

            if (user is null || user.Status is not UserStatus.Unregistered
                || user.RegistrationCodeHash is null)
            {
                // Reported exactly like a wrong code, so the endpoint does not confirm which
                // addresses have an account. There is nothing here worth a log line either: a
                // caller guessing addresses is the expected case.
                return new RegistrationOutcome
                {
                    Status = RegistrationStatus.InvalidCode
                };
            }

            // BCrypt does not store the hash - it recomputes it from the salt inside the stored
            // value - so verifying a code costs as much as producing one. That is the point of
            // the algorithm, and it is why the endpoint has to be throttled rather than left
            // open as a free way to make the server do work.
            if (!BCrypt.Net.BCrypt.Verify(trimmedCode, user.RegistrationCodeHash))
            {
                return new RegistrationOutcome
                {
                    Status = RegistrationStatus.InvalidCode
                };
            }

            if (user.RegistrationCodeExpiresAt is not { } expiresAt || _timeProvider.GetUtcNow() > expiresAt)
            {
                return new RegistrationOutcome
                {
                    Status = RegistrationStatus.CodeExpired
                };
            }

            // The code is spent either way. An expired one is cleared so a later confirmation
            // reports "no pending code" rather than the same expiry forever.
            user.Status = UserStatus.Registered;
            user.RegisteredAt = _timeProvider.GetUtcNow();
            user.RegistrationCodeHash = null;
            user.RegistrationCodeExpiresAt = null;

            await SaveAsync(cancellationToken);

            _logger.LogInformation(
                "Confirmed the registration of {Username}. The account is now {Status}.",
                user.Username,
                user.Status);

            return new RegistrationOutcome
            {
                Status = RegistrationStatus.Confirmed
            };
        }

        /// <summary>
        /// Whether an existing row may be taken over by this request, and prepares it when so.
        /// </summary>
        /// <returns>True when the row was prepared and has to be saved.</returns>
        private static bool TryReusePending(
            UserEntity? existing,
            string username,
            string email,
            string password,
            string code,
            string codeHash,
            RegistrationOptions options,
            DateTimeOffset now)
        {
            if (existing is null
                || existing.Status is not UserStatus.Unregistered
                || existing.Username != username
                || existing.Email != email)
            {
                return false;
            }

            existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            existing.RegistrationCodeHash = codeHash;
            existing.RegistrationCodeExpiresAt = now.Add(options.CodeLifetime);

            return true;
        }

        /// <summary>
        /// Whether a row blocks this request because it holds one of the two taken values.
        /// </summary>
        private static bool IsTaken(UserEntity? existing, string value)
            => existing is not null
                && (existing.Username == value || existing.Email == value);

        /// <summary>
        /// Sends the code and turns the delivery outcome into the result of the call.
        /// </summary>
        private async Task<RegistrationOutcome> DeliverAsync(
            string email,
            string username,
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

            return result switch
            {
                EmailSendResult.Sent => new RegistrationOutcome
                {
                    Status = RegistrationStatus.Submitted,
                    RowPersisted = rowPersisted
                },
                _ => new RegistrationOutcome
                {
                    // The account is already stored, so the caller has to be told that the code
                    // did not arrive. A new request replaces it once the window is over.
                    Status = RegistrationStatus.EmailDeliveryFailed,
                    RowPersisted = rowPersisted
                }
            };
        }

        /// <summary>
        /// Applies the registration rules to the normalized values.
        /// </summary>
        private static Dictionary<string, string[]> Validate(string username, string email, string password)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (username.Length is < RegistrationPolicy.UsernameMinLength
                or > RegistrationPolicy.UsernameMaxLength)
            {
                errors[nameof(NewRegistration.Username)] =
                [
                    $"The username must be between {RegistrationPolicy.UsernameMinLength} and " +
                    $"{RegistrationPolicy.UsernameMaxLength} characters."
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
                    $"The password must be between {RegistrationPolicy.PasswordMinLength} and " +
                    $"{RegistrationPolicy.PasswordMaxLength} characters and contain at least one " +
                    "lower case letter, one upper case letter and one digit."
                ];
            }

            return errors;
        }

        /// <summary>
        /// Folds an address into the single form the database compares against.
        /// </summary>
        /// <remarks>
        /// Addresses are case insensitive in practice, and the column is not: a check that
        /// folded nothing would let a second free account be taken on a mailbox that already has
        /// one, by writing the same address with a different case. Trimming matters for the same
        /// reason - a trailing space from a paste would otherwise make a second account.
        /// </remarks>
        private static string NormalizeEmail(string? email)
            => email?.Trim().ToLowerInvariant() ?? string.Empty;

        /// <summary>
        /// Builds a code of <paramref name="length"/> digits.
        /// </summary>
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
        /// Saves the account, turning a lost race for a unique value into the same refusal a
        /// sequential check would have produced.
        /// </summary>
        /// <remarks>
        /// Two requests can both pass the lookup and then both insert. The unique index is the
        /// real guard, and without this the loser would surface as a database exception on what
        /// is an ordinary outcome for a public form.
        /// </remarks>
        private async Task SaveAsync(CancellationToken cancellationToken)
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

                    throw new DuplicateRegistrationException(exception);
                }

                throw;
            }
        }

        /// <summary>
        /// Whether the failure was a unique index violation. The provider is not referenced here,
        /// so the check is on the message the server sends, which is the documented text of
        /// <c>SQLSTATE 23505</c>.
        /// </summary>
        private static bool IsUniqueViolation(DbUpdateException exception)
            => exception.InnerException?.Message.Contains(
                "23505",
                StringComparison.Ordinal) == true;

        /// <summary>
        /// The plain text body of the message that carries a code. The code is the only content:
        /// the account does not exist yet, so there is nothing to link to and nothing to style.
        /// </summary>
        private static string BuildCodeBody(string username, string code, TimeSpan lifetime)
        {
            var minutes = (int)lifetime.TotalMinutes;

            return $"""
                Hello {username},

                Your Fluxy registration code is {code}.

                It is valid for {minutes} minutes. If you did not ask to register, ignore this
                message - the account stays inactive until the code is confirmed.

                {Environment.NewLine}-
                {Environment.NewLine}This is an automated message, please do not reply.
                """;
        }
    }

    /// <summary>
    /// Raised when a registration lost the race for a value the database requires to be unique.
    /// </summary>
    /// <remarks>
    /// It exists so the transport layer can answer the loser with the same refusal the winner
    /// would have received, instead of a database error. A sequential check cannot see the race,
    /// which is why this type exists at all.
    /// </remarks>
    public sealed class DuplicateRegistrationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuplicateRegistrationException"/> class.
        /// </summary>
        /// <param name="innerException">The database failure that triggered it.</param>
        public DuplicateRegistrationException(Exception innerException)
            : base("The username or the email was taken by a concurrent request.", innerException)
        {
        }
    }
}
