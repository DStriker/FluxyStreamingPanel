using System.Linq.Expressions;
using Fluxy.Application.Services.GeoIp;
using Fluxy.Application.Services.Registration;
using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Users;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fluxy.Application.Services.Users
{
    /// <summary>
    /// Reads every account of the installation and changes them on an admin's behalf.
    /// </summary>
    /// <remarks>
    /// The rules it applies are the ones the public forms apply, taken from the same places:
    /// a username is a <c>RegistrationPolicy</c> username, a password is a
    /// <c>RegistrationPolicy</c> password, a time zone is a <c>TimeZonePolicy</c> time zone and
    /// a guard is a <c>LoginGuardNormalizer</c> guard. What differs is everything around them -
    /// no password of the caller's own is asked for, no code is mailed, and the anti-lock check
    /// is deliberately absent (see <see cref="LoginGuardNormalizer"/> for why).
    ///
    /// Three refusals protect the *caller* rather than the row: an account may not delete
    /// itself, block itself or take its own level down. They live here rather than in the
    /// controller so that a second endpoint reaching the same action cannot forget them.
    ///
    /// Nothing here knows about HTTP. Which <see cref="AdminUserAction"/> becomes a 201 and
    /// which becomes a 409 is the transport layer's business.
    /// </remarks>
    public sealed class UserAdminService : IUserAdminService
    {
        /// <summary>The sentence a username that is too short or too long gets.</summary>
        private static readonly string[] UsernameRule =
        [
            $"The username must be between {RegistrationPolicy.UsernameMinLength} and " +
            $"{RegistrationPolicy.UsernameMaxLength} characters."
        ];

        /// <summary>The sentence both this service and the registration form give a password.</summary>
        private static readonly string[] PasswordRule =
        [
            $"The password must be between {RegistrationPolicy.PasswordMinLength} and " +
            $"{RegistrationPolicy.PasswordMaxLength} characters and contain at least one " +
            "lower case letter, one upper case letter and one digit."
        ];

        private readonly FluxyDbContext _context;
        private readonly ITokenService _tokenService;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<UserAdminService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserAdminService"/> class.
        /// </summary>
        /// <param name="context">Context holding the accounts, their tokens and their rules.</param>
        /// <param name="tokenService">Ends the sessions a state change takes with it.</param>
        /// <param name="timeProvider">Clock, so a stamp is not wall time hard coded into a rule.</param>
        /// <param name="logger">Logger the changes worth remembering are reported to.</param>
        public UserAdminService(
            FluxyDbContext context,
            ITokenService tokenService,
            TimeProvider timeProvider,
            ILogger<UserAdminService> logger)
        {
            _context = context;
            _tokenService = tokenService;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<AdminUserPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            UserRole? role,
            UserStatus? status,
            AdminUserSortField sortBy,
            AdminUserSortOrder sortOrder,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Users.AsNoTracking();

            if (role is { } wantedRole)
            {
                query = query.Where(entry => entry.Role == wantedRole);
            }

            if (status is { } wantedStatus)
            {
                query = query.Where(entry => entry.Status == wantedStatus);
            }

            // Folded before the comparison rather than with a collation, the same way the visit
            // history searches its user agent: `Contains` on the lowered value is a substring
            // match any address or name answers to regardless of how the column was typed.
            var needle = search?.Trim().ToLowerInvariant() ?? string.Empty;
            if (needle.Length > 0)
            {
                query = query.Where(entry =>
                    entry.Username.ToLower().Contains(needle) ||
                    entry.Email.ToLower().Contains(needle));
            }

            // Counted *before* the page is taken and with the same filter, because the pager
            // shows this number: a total counted over every row would advertise pages that the
            // filter has already removed.
            var total = await query.CountAsync(cancellationToken);

            query = Order(query, sortBy, sortOrder);

            var rows = await query
                .Select(entry => new StoredUser
                {
                    Id = entry.Id,
                    Username = entry.Username,
                    Email = entry.Email,
                    Role = entry.Role,
                    Status = entry.Status
                })
                // Widening to long before the cast: a page number comes off a query string, and
                // `(page - 1) * pageSize` on a huge one overflows int into a negative OFFSET,
                // which the database answers as an error rather than as an empty page.
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new AdminUserPage
            {
                Items = await DescribeVisitsAsync(rows, cancellationToken),
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public async Task<AdminUserDetail?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(entry => entry.Id == userId, cancellationToken);

            if (user is null)
            {
                return null;
            }

            var rules = await _context.LoginGuardRules
                .AsNoTracking()
                .Where(rule => rule.UserId == userId)
                .ToListAsync(cancellationToken);

            var provider = rules.FirstOrDefault(
                rule => rule.Kind is LoginGuardRuleKind.AutonomousSystem);

            var visit = await LatestVisitAsync(user.Id, cancellationToken);

            return new AdminUserDetail
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                Status = user.Status,
                TimeZone = user.TimeZone,
                RegisteredAt = user.RegisteredAt,
                CreatedAt = user.CreatedAt,
                LastSeenAt = visit?.CreatedAt,
                LastIp = visit?.ClientAddress,
                LoginGuard = new LoginGuardSettings
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
                }
            };
        }

        /// <inheritdoc />
        public async Task<AdminUserOutcome> CreateAsync(
            NewUser user,
            CancellationToken cancellationToken = default)
        {
            var username = user.Username.Trim();
            var email = user.Email.Trim().ToLowerInvariant();

            var errors = Validate(username, email, user.Password, user.TimeZone);
            Merge(errors, user.LoginGuard, out var guard);

            if (errors.Count > 0)
            {
                return new AdminUserOutcome
                {
                    Action = AdminUserAction.InvalidInput,
                    Errors = errors
                };
            }

            if (await ConflictOutcomeAsync(null, username, email, cancellationToken) is { } taken)
            {
                return taken;
            }

            // Resolved once: validation has already proved it is an identifier this system
            // knows, and reading it back from the policy keeps the two from ever disagreeing
            // about what "empty" and "unknown" turned into.
            TimeZonePolicy.TryNormalize(user.TimeZone, out var zone);

            var entity = new UserEntity
            {
                Username = username,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.Password),
                Role = user.Role,
                Status = user.Status,

                // A row handed over already activated carries the stamp from the start: the
                // status says "registered", so the moment it left Unregistered has to be now.
                // Unregistered stays null, which is also what lets a later unblock know the
                // account has never been registered at all.
                RegisteredAt = user.Status is UserStatus.Unregistered ? null : _timeProvider.GetUtcNow(),
                TimeZone = zone,
                GeoProtectionEnabled = guard?.GeoProtectionEnabled ?? false,
                BindSessionToIp = guard?.BindSessionToIp ?? false
            };

            _context.Users.Add(entity);

            if (await SaveAsync(cancellationToken) is { } failure)
            {
                // A second request took the same username or address between the check above
                // and this write. The unique index is what actually refuses a duplicate, so the
                // loser is answered like a caller that never got past the check; anything else
                // is a failure this service does not know how to describe.
                if (await ConflictOutcomeAsync(null, username, email, cancellationToken) is { } raced)
                {
                    return raced;
                }

                throw failure;
            }

            if (guard is not null)
            {
                await LoginGuardStore.ReplaceRulesAsync(_context, entity.Id, guard, cancellationToken);
            }

            _logger.LogInformation(
                "Created the account {Username} ({Email}) at level {Role}, state {State}.",
                entity.Username,
                entity.Email,
                entity.Role,
                entity.Status);

            return new AdminUserOutcome
            {
                Action = AdminUserAction.Created,
                UserId = entity.Id
            };
        }

        /// <inheritdoc />
        public async Task<AdminUserOutcome> UpdateAsync(
            Guid userId,
            UserPatch patch,
            Guid actingUserId,
            CancellationToken cancellationToken = default)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(entry => entry.Id == userId, cancellationToken);

            if (user is null)
            {
                return NotFound();
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            // Everything that can refuse the request without writing anything comes first, so
            // a body with three mistakes is told about all three at once rather than one per
            // round trip - and so that nothing is written by a request that is about to be
            // refused for another reason.
            var username = patch.Username?.Trim();
            if (patch.Username is not null && !IsUsernameAcceptable(username))
            {
                errors[nameof(UserPatch.Username)] = UsernameRule;
            }

            var email = patch.Email?.Trim().ToLowerInvariant();
            if (patch.Email is not null && !RegistrationPolicy.IsEmailShapeValid(email ?? string.Empty))
            {
                errors[nameof(UserPatch.Email)] = ["The email address is not valid."];
            }

            // Empty means "leave the password alone" - see UserPatch. A password that is
            // present but unacceptable is refused rather than silently ignored, because the
            // form that sent it believes it has been set.
            var password = string.IsNullOrEmpty(patch.Password) ? null : patch.Password;
            if (password is not null && !RegistrationPolicy.IsPasswordAcceptable(password))
            {
                errors[nameof(UserPatch.Password)] = PasswordRule;
            }

            var timeZoneNamed = patch.TimeZone is not null;
            if (timeZoneNamed && !TimeZonePolicy.TryNormalize(patch.TimeZone, out _))
            {
                errors[nameof(UserPatch.TimeZone)] =
                ["The time zone identifier is not one this system knows."];
            }

            Merge(errors, patch.LoginGuard, out var guard);

            if (errors.Count > 0)
            {
                return new AdminUserOutcome
                {
                    Action = AdminUserAction.InvalidInput,
                    Errors = errors
                };
            }

            // The two refusals that are about the caller rather than about the row. They run
            // before anything is written, so a request that mixes one with a valid change
            // changes nothing at all.
            if (patch.Role is { } newRole && userId == actingUserId && newRole < user.Role)
            {
                return new AdminUserOutcome { Action = AdminUserAction.CannotDemoteSelf };
            }

            if (patch.Status is UserStatus.Blocked && userId == actingUserId)
            {
                return new AdminUserOutcome { Action = AdminUserAction.CannotBlockSelf };
            }

            if (await ConflictOutcomeAsync(userId, username, email, cancellationToken) is { } taken)
            {
                return taken;
            }

            // Hashed only now rather than during validation: bcrypt is the expensive step, and
            // a request refused for a taken username should not have paid for it.
            var passwordHash = password is null ? null : BCrypt.Net.BCrypt.HashPassword(password);

            if (username is not null)
            {
                user.Username = username;
            }

            if (email is not null)
            {
                user.Email = email;
            }

            if (passwordHash is not null)
            {
                user.PasswordHash = passwordHash;
            }

            if (timeZoneNamed)
            {
                TimeZonePolicy.TryNormalize(patch.TimeZone, out var zone);
                user.TimeZone = zone;
            }

            if (patch.Role is { } role)
            {
                user.Role = role;
            }

            var statusChanged = false;
            if (patch.Status is { } status && status != user.Status)
            {
                statusChanged = true;
                user.Status = status;

                if (status is UserStatus.Unregistered)
                {
                    // Going back to Unregistered has to take the stamp with it: the check
                    // constraint refuses a registration timestamp on an account that is still
                    // Unregistered, and rightly so - the row would mean two things at once.
                    user.RegisteredAt = null;
                }
                else if (user.RegisteredAt is null)
                {
                    user.RegisteredAt = _timeProvider.GetUtcNow();
                }
            }

            if (guard is not null)
            {
                user.GeoProtectionEnabled = guard.GeoProtectionEnabled;
                user.BindSessionToIp = guard.BindSessionToIp;
            }

            if (await SaveAsync(cancellationToken) is { } failure)
            {
                if (await ConflictOutcomeAsync(userId, username, email, cancellationToken) is { } raced)
                {
                    return raced;
                }

                throw failure;
            }

            if (guard is not null)
            {
                await LoginGuardStore.ReplaceRulesAsync(_context, user.Id, guard, cancellationToken);
            }

            // Both events end the sessions the account holds, and for the same reason: a
            // blocked account cannot sign in, and a password that changed means whoever held
            // the old one may be holding a session minted with it. Unregistered joins them
            // because it withdraws the right to sign in at all - a session outliving the
            // withdrawal is the same contradiction a blocked one is.
            var endsSessions = passwordHash is not null
                || (statusChanged && user.Status is UserStatus.Blocked or UserStatus.Unregistered);

            if (endsSessions)
            {
                await _tokenService.RevokeAllSessionsAsync(user.Id, cancellationToken);
            }

            _logger.LogInformation(
                "Updated {Username}{StatusPart}{PasswordPart}.",
                user.Username,
                statusChanged ? $" -> {user.Status}" : string.Empty,
                passwordHash is not null ? " (password changed, sessions revoked)" : string.Empty);

            return new AdminUserOutcome
            {
                Action = AdminUserAction.Updated,
                UserId = user.Id
            };
        }

        /// <inheritdoc />
        public async Task<AdminUserOutcome> ConfirmRegistrationAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(entry => entry.Id == userId, cancellationToken);

            if (user is null)
            {
                return NotFound();
            }

            if (user.Status is not UserStatus.Unregistered)
            {
                // Refused rather than answered as a success: the caller's button was drawn
                // for a row that has since changed under it, and telling it otherwise would
                // leave a stale list describing a state that is no longer there.
                return InvalidStatus(user.Id);
            }

            user.Status = UserStatus.Registered;
            user.RegisteredAt = _timeProvider.GetUtcNow();

            // The code this replaces was the way in for somebody who never used it. It is
            // spent the moment the account is registered by any other route, so that a code
            // mailed a week ago cannot confirm an account somebody else already confirmed.
            user.RegistrationCodeHash = null;
            user.RegistrationCodeExpiresAt = null;

            if (await SaveAsync(cancellationToken) is { } failure)
            {
                throw failure;
            }

            _logger.LogInformation("Registered {Username} from the admin panel.", user.Username);

            return new AdminUserOutcome
            {
                Action = AdminUserAction.RegistrationConfirmed,
                UserId = user.Id
            };
        }

        /// <inheritdoc />
        public async Task<AdminUserOutcome> SetBlockedAsync(
            Guid userId,
            bool blocked,
            Guid actingUserId,
            CancellationToken cancellationToken = default)
        {
            // Before the row is even read: whether the caller may end this account is a fact
            // about the request, and asking the database for an account that turns out to be
            // the caller's own would answer a question nobody should have asked.
            if (userId == actingUserId)
            {
                return new AdminUserOutcome { Action = AdminUserAction.CannotBlockSelf };
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(entry => entry.Id == userId, cancellationToken);

            if (user is null)
            {
                return NotFound();
            }

            if (blocked)
            {
                // Idempotent on purpose. The answer does not depend on where the block
                // started, so a second click on a row a colleague blocked a moment ago reports
                // the same truth rather than an error about a state nobody caused.
                if (user.Status is UserStatus.Blocked)
                {
                    return new AdminUserOutcome
                    {
                        Action = AdminUserAction.Blocked,
                        UserId = user.Id
                    };
                }

                // RegisteredAt is left where it is: a block is not a cancellation of the
                // registration. The timestamp says when the account got one, not whether it is
                // allowed in at this moment.
                user.Status = UserStatus.Blocked;

                if (await SaveAsync(cancellationToken) is { } blockFailure)
                {
                    throw blockFailure;
                }

                await _tokenService.RevokeAllSessionsAsync(user.Id, cancellationToken);

                _logger.LogInformation(
                    "Blocked {Username} and ended every session it held.",
                    user.Username);

                return new AdminUserOutcome
                {
                    Action = AdminUserAction.Blocked,
                    UserId = user.Id
                };
            }

            if (user.Status is not UserStatus.Blocked)
            {
                // Refused rather than answered like the block above: where an unblock goes
                // depends on where the account came from, so an account that is not blocked
                // has no answer to give. The caller is expected to re-read the list.
                return InvalidStatus(user.Id);
            }

            // The inverse of the block, not of "set the status to Registered": an account that
            // was never confirmed goes back to Unregistered, and no registration stamp is
            // invented for it on the way out of a state that never gave it one.
            user.Status = user.RegisteredAt is null
                ? UserStatus.Unregistered
                : UserStatus.Registered;

            if (await SaveAsync(cancellationToken) is { } unblockFailure)
            {
                throw unblockFailure;
            }

            _logger.LogInformation("Unblocked {Username}, now {State}.", user.Username, user.Status);

            return new AdminUserOutcome
            {
                Action = AdminUserAction.Unblocked,
                UserId = user.Id
            };
        }

        /// <inheritdoc />
        public async Task<AdminUserOutcome> DeleteAsync(
            Guid userId,
            Guid actingUserId,
            CancellationToken cancellationToken = default)
        {
            if (userId == actingUserId)
            {
                return new AdminUserOutcome { Action = AdminUserAction.CannotDeleteSelf };
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(entry => entry.Id == userId, cancellationToken);

            if (user is null)
            {
                return NotFound();
            }

            var username = user.Username;

            _context.Users.Remove(user);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();
                throw;
            }

            // The refresh tokens, the pending changes and the guard rules went with the row
            // through foreign keys that are `ON DELETE CASCADE`, which is what was verified
            // when they were made real: a session or a code that outlives its account is a
            // credential for nobody.
            _logger.LogWarning(
                "Deleted the account {Username}. Its tokens, pending changes and guard rules " +
                "went with it.",
                username);

            return new AdminUserOutcome
            {
                Action = AdminUserAction.Removed,
                UserId = userId
            };
        }

        /// <summary>
        /// Applies the registration rules to the values a create body carries, keyed by the
        /// C# name of the property that failed.
        /// </summary>
        private static Dictionary<string, string[]> Validate(
            string username,
            string email,
            string password,
            string? timeZone)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (!IsUsernameAcceptable(username))
            {
                errors[nameof(NewUser.Username)] = UsernameRule;
            }

            if (!RegistrationPolicy.IsEmailShapeValid(email))
            {
                errors[nameof(NewUser.Email)] = ["The email address is not valid."];
            }

            if (!RegistrationPolicy.IsPasswordAcceptable(password))
            {
                errors[nameof(NewUser.Password)] = PasswordRule;
            }

            if (!TimeZonePolicy.TryNormalize(timeZone, out _))
            {
                errors[nameof(NewUser.TimeZone)] =
                ["The time zone identifier is not one this system knows."];
            }

            return errors;
        }

        /// <summary>Whether a username fits the column and the registration rules.</summary>
        private static bool IsUsernameAcceptable(string? username)
            => username is
            {
                Length: >= RegistrationPolicy.UsernameMinLength
                    and <= RegistrationPolicy.UsernameMaxLength
            };

        /// <summary>
        /// Folds the guard's own errors into the map and hands back the value to store when
        /// there are none. A guard that failed half its checks is not staged at all - writing
        /// the lists without the country that did not validate would be a partially applied
        /// guard, and a guard nobody can read is worse than one that was refused.
        /// </summary>
        /// <param name="errors">Map of rejected fields, into which the guard's go.</param>
        /// <param name="requested">The guard the caller named, or null for "leave it alone".</param>
        /// <param name="guard">The value to store, or null when there was nothing or it failed.</param>
        private static void Merge(
            Dictionary<string, string[]> errors,
            LoginGuardSettings? requested,
            out NormalizedLoginGuard? guard)
        {
            guard = null;

            if (requested is null)
            {
                return;
            }

            var normalized = LoginGuardNormalizer.Normalize(requested);
            if (normalized.Errors.Count > 0)
            {
                foreach (var (key, value) in normalized.Errors)
                {
                    errors[key] = value;
                }

                return;
            }

            guard = normalized.Value;
        }

        /// <summary>
        /// Whether another account already holds this username or address, reported as the
        /// name of the property that is taken.
        /// </summary>
        /// <param name="excludeId">Account to compare against itself, or null for a create.</param>
        private async Task<string?> ConflictAsync(
            Guid? excludeId,
            string? username,
            string? email,
            CancellationToken cancellationToken)
        {
            var others = _context.Users.AsQueryable();
            if (excludeId is { } id)
            {
                others = others.Where(entry => entry.Id != id);
            }

            if (username is not null
                && await others.AnyAsync(entry => entry.Username == username, cancellationToken))
            {
                return nameof(UserPatch.Username);
            }

            if (email is not null
                && await others.AnyAsync(entry => entry.Email == email, cancellationToken))
            {
                return nameof(UserPatch.Email);
            }

            return null;
        }

        /// <summary>
        /// The answer to a value another account holds: one code, one sentence, and the field
        /// it belongs to so a form can put the reason under its own input - or nothing at all
        /// when nothing is taken.
        /// </summary>
        private async Task<AdminUserOutcome?> ConflictOutcomeAsync(
            Guid? excludeId,
            string? username,
            string? email,
            CancellationToken cancellationToken)
        {
            var property = await ConflictAsync(excludeId, username, email, cancellationToken);

            if (property is null)
            {
                return null;
            }

            var message = property == nameof(UserPatch.Username)
                ? "That username is already taken."
                : "That email address is already taken.";

            return new AdminUserOutcome
            {
                Action = AdminUserAction.AlreadyExists,
                Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [property] = [message]
                }
            };
        }

        private static AdminUserOutcome NotFound()
            => new() { Action = AdminUserAction.NotFound };

        private static AdminUserOutcome InvalidStatus(Guid userId)
            => new() { Action = AdminUserAction.InvalidStatus, UserId = userId };

        /// <summary>
        /// Writes whatever has been staged on the row, interpreting the one failure that is
        /// expected: a unique index refusing a value another account took meanwhile.
        /// </summary>
        /// <returns>Null when the row was written, or the exception when it was that race.</returns>
        private async Task<DbUpdateException?> SaveAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return null;
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                // The tracker is cleared because the row it was holding did not reach the
                // database - leaving it attached would make the next save try the same write.
                _context.ChangeTracker.Clear();
                return exception;
            }
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
        /// Puts the page in the order the caller asked for.
        /// </summary>
        /// <remarks>
        /// Every branch carries a tie-break, and it is the account's own name: two rows share
        /// the sorted value more often than not (an account that never signed in has no visit
        /// at all, and a whole filter may be one role), and without a tie-break the database is
        /// free to return them in any order it likes between one page and the next - which is
        /// how the same account appears on two pages or on none.
        ///
        /// The identifier sorts last rather than first: it is a random uuid, so it breaks ties
        /// exactly, but an order on it is an order nobody asked for.
        /// </remarks>
        private IQueryable<UserEntity> Order(
            IQueryable<UserEntity> query,
            AdminUserSortField field,
            AdminUserSortOrder order)
        {
            var descending = order is AdminUserSortOrder.Descending;

            return field switch
            {
                AdminUserSortField.Email => Chain(
                    query, entry => entry.Email, entry => entry.Username, descending),

                AdminUserSortField.Role => Chain(
                    query, entry => entry.Role, entry => entry.Username, descending),

                AdminUserSortField.Status => Chain(
                    query, entry => entry.Status, entry => entry.Username, descending),

                AdminUserSortField.LastSeenAt => ChainNullsLast(
                    query, LastSeen(), entry => entry.Username, descending),

                AdminUserSortField.Ip => ChainNullsLast(
                    query, LastVisitAddress(), entry => entry.Username, descending),

                _ => Chain(query, entry => entry.Username, entry => entry.Id, descending)
            };
        }

        /// <summary>Orders by the first key and breaks the tie with the second.</summary>
        private static IOrderedQueryable<UserEntity> Chain<TKey1, TKey2>(
            IQueryable<UserEntity> source,
            Expression<Func<UserEntity, TKey1>> primary,
            Expression<Func<UserEntity, TKey2>> tieBreak,
            bool descending)
        {
            var ordered = descending
                ? source.OrderByDescending(primary)
                : source.OrderBy(primary);

            return descending
                ? ordered.ThenByDescending(tieBreak)
                : ordered.ThenBy(tieBreak);
        }

        /// <summary>Like <see cref="Chain"/>, except that rows without a value go last.</summary>
        /// <remarks>
        /// Both keys that reach this are null exactly when the account has never signed in,
        /// and PostgreSQL treats null as larger than any value - so a plain `DESC` puts those
        /// accounts at the *top* of a "last seen" column, and the very first line of the page
        /// sorted by newest activity would be an account nobody has ever seen. Sorting the
        /// nulls into their own group first is what says they belong at the end of either
        /// direction: the real key then orders the accounts that have a value, and the group
        /// without one follows.
        ///
        /// The grouping is built from the key itself rather than from a companion predicate,
        /// so a key whose meaning changes still puts its own empty rows in the right place -
        /// and the expression is assembled rather than written inline because both callers
        /// pass a subquery: inlining it with `Expression.Invoke` would repeat it once per
        /// use, which EF Core refuses to translate.
        /// </remarks>
        private static IOrderedQueryable<UserEntity> ChainNullsLast<TKey1, TKey2>(
            IQueryable<UserEntity> source,
            Expression<Func<UserEntity, TKey1>> primary,
            Expression<Func<UserEntity, TKey2>> tieBreak,
            bool descending)
        {
            var isNull = Expression.Lambda<Func<UserEntity, bool>>(
                Expression.Equal(
                    primary.Body,
                    Expression.Constant(null, primary.Body.Type)),
                primary.Parameters);

            // `false` sorts before `true`, so the rows that have a value lead.
            var withGrouped = source.OrderBy(isNull);

            return descending
                ? withGrouped.ThenByDescending(primary).ThenByDescending(tieBreak)
                : withGrouped.ThenBy(primary).ThenBy(tieBreak);
        }

        /// <summary>
        /// When the account last signed in or refreshed: the creation time of its freshest
        /// refresh token, revoked or not, or null when it never has.
        /// </summary>
        /// <remarks>
        /// A correlated scalar subquery, and it has to be one: the value is not on the row, so
        /// a page ordered by it cannot be taken from the database in any other way - ordering
        /// in memory would mean reading every account the filter admits before knowing which
        /// ten belong on the page.
        ///
        /// `MAX` rather than "the newest row", because the sort only needs the moment; the row
        /// itself is read back per page in <see cref="DescribeVisitsAsync"/>, where it can be
        /// fetched for the ten ids on screen instead of a hundred.
        /// </remarks>
        private Expression<Func<UserEntity, DateTimeOffset?>> LastSeen()
            => entry => _context.RefreshTokens
                .Where(token => token.UserId == entry.Id)
                .Max(token => (DateTimeOffset?)token.CreatedAt);

        /// <summary>Address of the visit <see cref="LastSeen"/> measures.</summary>
        /// <remarks>
        /// The one sort that needs an ordered subquery rather than an aggregate: the address
        /// has to belong to that newest token, and `ORDER BY` inside a correlated subquery is
        /// what says which token that is.
        /// </remarks>
        private Expression<Func<UserEntity, string?>> LastVisitAddress()
            => entry => _context.RefreshTokens
                .Where(token => token.UserId == entry.Id)
                .OrderByDescending(token => token.CreatedAt)
                .ThenByDescending(token => token.Id)
                .Select(token => token.ClientAddress)
                .FirstOrDefault();

        /// <summary>
        /// Reads the freshest token of each account on the page and returns the rows with
        /// their visit attached.
        /// </summary>
        /// <remarks>
        /// Done in memory rather than as two more subqueries per row for one reason: the ids
        /// are already known, so one query with an `IN` over ten of them answers what ten
        /// correlated subqueries would - and it cannot disagree with the order that put them
        /// there, because both read the same freshest row by the same rule.
        /// </remarks>
        private async Task<List<AdminUser>> DescribeVisitsAsync(
            List<StoredUser> rows,
            CancellationToken cancellationToken)
        {
            var result = rows
                .Select(row => new AdminUser
                {
                    Id = row.Id,
                    Username = row.Username,
                    Email = row.Email,
                    Role = row.Role,
                    Status = row.Status
                })
                .ToList();

            if (rows.Count == 0)
            {
                return result;
            }

            var visits = await LatestVisitsAsync(
                rows.Select(row => row.Id).ToArray(),
                cancellationToken);

            for (var index = 0; index < rows.Count; index++)
            {
                if (visits.TryGetValue(rows[index].Id, out var visit))
                {
                    result[index] = result[index] with
                    {
                        LastSeenAt = visit.CreatedAt,
                        LastIp = visit.ClientAddress
                    };
                }
            }

            return result;
        }

        /// <summary>The freshest token of one account, or null when it has none.</summary>
        private async Task<RefreshTokenEntity?> LatestVisitAsync(
            Guid userId,
            CancellationToken cancellationToken)
            => await _context.RefreshTokens
                .AsNoTracking()
                .Where(token => token.UserId == userId)
                .OrderByDescending(token => token.CreatedAt)
                .ThenByDescending(token => token.Id)
                .FirstOrDefaultAsync(cancellationToken);

        /// <summary>
        /// The freshest token of each of these accounts, keyed by account. An account with no
        /// token is simply absent, which is the same as saying it has never signed in.
        /// </summary>
        private async Task<IReadOnlyDictionary<Guid, RefreshTokenEntity>> LatestVisitsAsync(
            Guid[] userIds,
            CancellationToken cancellationToken)
        {
            // Taken in order rather than grouped: the first row seen per account from a list
            // that arrives newest first *is* the grouping, and it needs no window function to
            // say what `ORDER BY created_at DESC` already said.
            var tokens = await _context.RefreshTokens
                .AsNoTracking()
                .Where(token => userIds.Contains(token.UserId))
                .OrderByDescending(token => token.CreatedAt)
                .ThenByDescending(token => token.Id)
                .ToListAsync(cancellationToken);

            var byAccount = new Dictionary<Guid, RefreshTokenEntity>();
            foreach (var token in tokens)
            {
                if (!byAccount.ContainsKey(token.UserId))
                {
                    byAccount[token.UserId] = token;
                }
            }

            return byAccount;
        }

        /// <summary>
        /// A row of the list before its visit has been attached. A class rather than the
        /// record because it is what the query projects into, and the record is what a client
        /// is handed - the same split the visit history makes, for the same reason.
        /// </summary>
        private sealed class StoredUser
        {
            public Guid Id { get; init; } = Guid.Empty;
            public string Username { get; init; } = string.Empty;
            public string Email { get; init; } = string.Empty;
            public UserRole Role { get; init; }
            public UserStatus Status { get; init; }
        }
    }
}
