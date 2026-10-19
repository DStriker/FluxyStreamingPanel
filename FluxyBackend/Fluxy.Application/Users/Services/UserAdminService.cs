using Fluxy.Application.Users.Utils;
using Fluxy.Core.Authentication.Interfaces;
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
using Fluxy.Core.Users.Utils;
using Microsoft.Extensions.Logging;

namespace Fluxy.Application.Users.Services
{
    /// <summary>
    /// Reads every account of the installation and changes them on an admin's behalf.
    /// </summary>
    /// <remarks>
    /// The rules it applies are the ones the public forms apply, taken from the same places:
    /// a username is bounded by <c>RegistrationLimits</c>, a password by <c>RegistrationLimits</c>
    /// and <c>RegistrationPolicy</c>, a time zone is a <c>TimeZonePolicy</c> time zone and
    /// a guard is a <c>LoginGuardNormalizer</c> guard. What differs is everything around them -
    /// no password of the caller's own is asked for, no code is mailed, and the anti-lock check
    /// is deliberately absent (see <see cref="LoginGuardNormalizer"/> for why).
    ///
    /// Three refusals protect the *caller* rather than the row: an account may not delete
    /// itself, block itself or take its own level down. They live here rather than in the
    /// controller so that a second endpoint reaching the same action cannot forget them.
    ///
    /// A fourth refusal protects neither and is about the target instead: every method is
    /// handed the permissions the caller's group holds and refuses an account whose role is
    /// outside them. The coarse policy on the endpoint has already settled that this caller
    /// may read or write *some* accounts at all - which of the installation's roles they may
    /// reach is decided here, because the role of the target is a fact about the row and the
    /// row is what this service is for. The list is the one place where the same question is
    /// not a refusal: it filters, and counts after filtering, because a page whose rows were
    /// never going to be shown is a worse answer than an empty one.
    ///
    /// Nothing here knows about HTTP. Which <see cref="AdminUserAction"/> becomes a 201 and
    /// which becomes a 409 is the transport layer's business.
    /// </remarks>
    public sealed class UserAdminService : IUserAdminService
    {
        /// <summary>The sentence a username that is too short or too long gets.</summary>
        private static readonly string[] UsernameRule =
        [
            $"The username must be between {RegistrationLimits.UsernameMinLength} and " +
            $"{RegistrationLimits.UsernameMaxLength} characters."
        ];

        /// <summary>The sentence both this service and the registration form give a password.</summary>
        private static readonly string[] PasswordRule =
        [
            $"The password must be between {RegistrationLimits.PasswordMinLength} and " +
            $"{RegistrationLimits.PasswordMaxLength} characters and contain at least one " +
            "lower case letter, one upper case letter and one digit."
        ];

        private readonly IUserRepository _users;
        private readonly IUserGroupRepository _groups;
        private readonly ILoginGuardRepository _guards;
        private readonly ITokenService _tokenService;
        private readonly TimeProvider _timeProvider;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UserAdminService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserAdminService"/> class.
        /// </summary>
        /// <param name="users">Repository holding the account rows.</param>
        /// <param name="groups">Repository holding the groups accounts join.</param>
        /// <param name="guards">Repository that replaces an account's allow lists.</param>
        /// <param name="tokenService">Ends the sessions a state change takes with it.</param>
        /// <param name="timeProvider">Clock, so a stamp is not wall time hard coded into a rule.</param>
        /// <param name="unitOfWork">Commits the changes one operation stages.</param>
        /// <param name="logger">Logger the changes worth remembering are reported to.</param>
        public UserAdminService(
            IUserRepository users,
            IUserGroupRepository groups,
            ILoginGuardRepository guards,
            ITokenService tokenService,
            TimeProvider timeProvider,
            IUnitOfWork unitOfWork,
            ILogger<UserAdminService> logger)
        {
            _users = users;
            _groups = groups;
            _guards = guards;
            _tokenService = tokenService;
            _timeProvider = timeProvider;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<AdminUserPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? groupId,
            UserStatus? status,
            AdminUserSortField sortBy,
            AdminUserSortOrder sortOrder,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default)
        {
            // Which accounts this operator may see is decided before the count rather than
            // after the rows, because the pager shows the count: a total taken over every row
            // would advertise pages whose rows the caller was never going to be shown. The
            // coarse policy has already refused a caller holding no view permission at all, so
            // this set is not empty by the time it reaches here.
            var visible = UserPermissionCatalog.VisibleRoles(granted);

            return await _users.GetAdminPageAsync(
                page,
                pageSize,
                search,
                groupId,
                status,
                sortBy,
                sortOrder,
                visible,
                cancellationToken);
        }

        /// <inheritdoc />
        public async Task<AdminUserReadResult> GetAsync(
            Guid userId,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default)
        {
            var detail = await _users.GetAdminDetailAsync(userId, cancellationToken);

            if (detail is null)
            {
                return AdminUserReadResult.Refused(AdminUserAction.NotFound);
            }

            if (!UserPermissionCatalog.CanView(granted, detail.Role))
            {
                // A different answer from "no such account" on purpose: the row is there and
                // the operator is being told what is standing between them and it. The
                // identifier is a uuid, so confirming that an account of another role exists
                // tells an attacker nothing they could act on, while a 404 would tell the
                // operator looking at their own list that the list was lying.
                return AdminUserReadResult.Refused(AdminUserAction.PermissionDenied);
            }

            return AdminUserReadResult.Found(detail);
        }

        /// <inheritdoc />
        public async Task<AdminUserOutcome> CreateAsync(
            NewUser user,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default)
        {
            var username = user.Username.Trim();
            var email = user.Email.Trim().ToLowerInvariant();

            var errors = Validate(username, email, user.Password, user.TimeZone);
            Merge(errors, user.LoginGuard, out var guard);

            var group = await _groups.GetSummaryAsync(user.GroupId, cancellationToken);

            if (group is null)
            {
                // Refused rather than defaulted: falling back to the clients group would
                // create an account somewhere the operator did not ask for, and a level the
                // form never showed anybody.
                errors[nameof(NewUser.GroupId)] = ["No group with that identifier."];
            }
            else if (!UserPermissionCatalog.CanEdit(granted, group.Role))
            {
                // The earliest this can be decided, because the role of the target is the
                // role of the group being joined. Before the field errors and well before the
                // uniqueness check: an operator who may not create accounts of this role has
                // no business learning whether the name they typed is already taken.
                return Denied();
            }

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

            var account = new NewAccount
            {
                Username = username,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.Password),

                // The null check above reports a missing group as an error rather than
                // returning at once, so that one answer can name every bad field - and any
                // error at all returns just below. `group` therefore cannot be null here,
                // but the compiler cannot follow that chain, so the assertion is written
                // down beside the only place the group is read.
                GroupId = group!.Id,
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

            var userId = await _users.AddAsync(account, cancellationToken);

            // The guard is staged before the one save rather than written by a second one:
            // the account and the lists it starts with are one operation, and a failure on
            // either must not leave the other committed. A row this request is still inserting
            // has no previous rules, so ApplyAsync only stages the new ones.
            if (guard is not null)
            {
                await _guards.ApplyAsync(userId, guard, cancellationToken);
            }

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

            _logger.LogInformation(
                "Created the account {Username} ({Email}) in group {Group} at level {Role}, " +
                "state {State}.",
                account.Username,
                account.Email,
                group.Name,
                group.Role,
                account.Status);

            return new AdminUserOutcome
            {
                Action = AdminUserAction.Created,
                UserId = userId
            };
        }

        /// <inheritdoc />
        public async Task<AdminUserOutcome> UpdateAsync(
            Guid userId,
            UserPatch patch,
            Guid actingUserId,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default)
        {
            var user = await _users.GetAdminAccountAsync(userId, cancellationToken);

            if (user is null)
            {
                return NotFound();
            }

            var groupBefore = new UserGroupSummary
            {
                Id = user.GroupId,
                Name = user.GroupName,
                Role = user.Role,
                Status = user.GroupStatus
            };

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

            // The group has to exist before any of the rules below can ask what level the
            // account is moving to, and it is read once: the level of the *new* group is
            // what the self-demotion refusal compares against, and the level of the old one
            // is already on the row through groupBefore.
            var groupAfter = groupBefore;
            if (patch.GroupId is { } wantedGroup)
            {
                var movedTo = await _groups.GetSummaryAsync(wantedGroup, cancellationToken);

                if (movedTo is null)
                {
                    // Refused rather than defaulted: an identifier naming no group is a
                    // request to move the account somewhere that is not there, and keeping
                    // the current group would answer a different question.
                    errors[nameof(UserPatch.GroupId)] = ["No group with that identifier."];
                }
                else
                {
                    groupAfter = movedTo;
                }
            }

            if (errors.Count > 0)
            {
                return new AdminUserOutcome
                {
                    Action = AdminUserAction.InvalidInput,
                    Errors = errors
                };
            }

            // The row may only be touched by an operator whose group holds the grant for its
            // role - and a move has to satisfy the grant for the role it arrives in as well,
            // or a group allowed to run the clients would be a staircase into the
            // administrators.
            if (!UserPermissionCatalog.CanEdit(granted, groupBefore.Role) ||
                !UserPermissionCatalog.CanEdit(granted, groupAfter.Role))
            {
                return Denied();
            }

            // The two refusals that are about the caller rather than about the row. They run
            // before anything is written, so a request that mixes one with a valid change
            // changes nothing at all.
            if (user.GroupId != groupAfter.Id && userId == actingUserId && groupAfter.Role < groupBefore.Role)
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

            // What the account is *now*, before the group moves under it: the effective
            // status is what decides whether a session outlives this request, and it can
            // change without a single column on the row changing.
            var effectiveBefore = UserStatusComposition.Combine(user.Status, groupBefore.Status);

            // Hashed only now rather than during validation: bcrypt is the expensive step, and
            // a request refused for a taken username should not have paid for it.
            var passwordHash = password is null ? null : BCrypt.Net.BCrypt.HashPassword(password);

            // The values this request resolves, kept together so the log below and the session
            // decision read the same ones the repositories were handed.
            var resolvedUsername = username ?? user.Username;
            var resolvedStatus = patch.Status ?? user.Status;
            var registeredAt = user.RegisteredAt;

            var statusChanged = false;
            if (patch.Status is { } status && status != user.Status)
            {
                statusChanged = true;

                if (status is UserStatus.Unregistered)
                {
                    // Going back to Unregistered has to take the stamp with it: the check
                    // constraint refuses a registration timestamp on an account that is still
                    // Unregistered, and rightly so - the row would mean two things at once.
                    registeredAt = null;
                }
                else if (registeredAt is null)
                {
                    registeredAt = _timeProvider.GetUtcNow();
                }
            }

            var groupChanged = user.GroupId != groupAfter.Id;

            if (username is not null)
            {
                await _users.SetUsernameAsync(userId, username, cancellationToken);
            }

            if (email is not null)
            {
                await _users.SetEmailAsync(userId, email, cancellationToken);
            }

            if (passwordHash is not null)
            {
                await _users.SetPasswordHashAsync(userId, passwordHash, cancellationToken);
            }

            if (timeZoneNamed)
            {
                TimeZonePolicy.TryNormalize(patch.TimeZone, out var zone);
                await _users.SetTimeZoneAsync(userId, zone, cancellationToken);
            }

            if (groupChanged)
            {
                await _users.SetGroupAsync(userId, groupAfter.Id, cancellationToken);
            }

            if (statusChanged)
            {
                await _users.SetStatusAsync(userId, resolvedStatus, registeredAt, cancellationToken);
            }

            // Staged before the one save, so the account and the lists that guard it are one
            // operation. The repository removes the previous rules through the change tracker,
            // so their deletion and the new rows commit with the rest of this request.
            if (guard is not null)
            {
                await _guards.ApplyAsync(userId, guard, cancellationToken);
            }

            if (await SaveAsync(cancellationToken) is { } failure)
            {
                if (await ConflictOutcomeAsync(userId, username, email, cancellationToken) is { } raced)
                {
                    return raced;
                }

                throw failure;
            }

            // Both events end the sessions the account holds, and for the same reason: a
            // blocked account cannot sign in, and a password that changed means whoever held
            // the old one may be holding a session minted with it. Unregistered joins them
            // because it withdraws the right to sign in at all - a session outliving the
            // withdrawal is the same contradiction a blocked one is.
            //
            // The third clause is the group: moving an account into a blocked group withdraws
            // the same right without a single column on the account changing, so the state
            // that decides is the effective one, before and after the write.
            var effectiveAfter = UserStatusComposition.Combine(resolvedStatus, groupAfter.Status);

            var endsSessions = passwordHash is not null
                || (statusChanged && resolvedStatus is UserStatus.Blocked or UserStatus.Unregistered)
                || (effectiveAfter != effectiveBefore
                    && effectiveAfter is UserStatus.Blocked or UserStatus.Unregistered);

            if (endsSessions)
            {
                await _tokenService.RevokeAllSessionsAsync(userId, cancellationToken);
            }

            _logger.LogInformation(
                "Updated {Username}{StatusPart}{GroupPart}{PasswordPart}.",
                resolvedUsername,
                statusChanged ? $" -> {resolvedStatus}" : string.Empty,
                groupChanged ? $" (moved to group {groupAfter.Name})" : string.Empty,
                passwordHash is not null ? " (password changed, sessions revoked)" : string.Empty);

            return new AdminUserOutcome
            {
                Action = AdminUserAction.Updated,
                UserId = userId
            };
        }

        /// <inheritdoc />
        public async Task<AdminUserOutcome> ConfirmRegistrationAsync(
            Guid userId,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default)
        {
            // The group's role travels with the account so the permission check does not have
            // to ask a second time.
            var user = await _users.GetAdminAccountAsync(userId, cancellationToken);

            if (user is null)
            {
                return NotFound();
            }

            if (!UserPermissionCatalog.CanEdit(granted, user.Role))
            {
                return Denied();
            }

            if (user.Status is not UserStatus.Unregistered)
            {
                // Refused rather than answered as a success: the caller's button was drawn
                // for a row that has since changed under it, and telling it otherwise would
                // leave a stale list describing a state that is no longer there.
                return InvalidStatus(userId);
            }

            // Registered and the pending code cleared together: the code this replaces was
            // the way in for somebody who never used it, and it is spent the moment the
            // account is registered by any other route.
            await _users.ConfirmRegistrationAsync(
                userId,
                _timeProvider.GetUtcNow(),
                cancellationToken);

            if (await SaveAsync(cancellationToken) is { } failure)
            {
                throw failure;
            }

            _logger.LogInformation("Registered {Username} from the admin panel.", user.Username);

            return new AdminUserOutcome
            {
                Action = AdminUserAction.RegistrationConfirmed,
                UserId = userId
            };
        }

        /// <inheritdoc />
        public async Task<AdminUserOutcome> SetBlockedAsync(
            Guid userId,
            bool blocked,
            Guid actingUserId,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default)
        {
            // Before the row is even read: whether the caller may end this account is a fact
            // about the request, and asking the database for an account that turns out to be
            // the caller's own would answer a question nobody should have asked.
            if (userId == actingUserId)
            {
                return new AdminUserOutcome { Action = AdminUserAction.CannotBlockSelf };
            }

            var user = await _users.GetAdminAccountAsync(userId, cancellationToken);

            if (user is null)
            {
                return NotFound();
            }

            if (!UserPermissionCatalog.CanEdit(granted, user.Role))
            {
                return Denied();
            }

            var group = new UserGroupSummary
            {
                Id = user.GroupId,
                Name = user.GroupName,
                Role = user.Role,
                Status = user.GroupStatus
            };

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
                        UserId = userId
                    };
                }

                // RegisteredAt is left where it is: a block is not a cancellation of the
                // registration. The timestamp says when the account got one, not whether it is
                // allowed in at this moment.
                await _users.SetStatusAsync(
                    userId,
                    UserStatus.Blocked,
                    user.RegisteredAt,
                    cancellationToken);

                if (await SaveAsync(cancellationToken) is { } blockFailure)
                {
                    throw blockFailure;
                }

                await _tokenService.RevokeAllSessionsAsync(userId, cancellationToken);

                _logger.LogInformation(
                    "Blocked {Username} and ended every session it held.",
                    user.Username);

                return new AdminUserOutcome
                {
                    Action = AdminUserAction.Blocked,
                    UserId = userId
                };
            }

            if (user.Status is not UserStatus.Blocked)
            {
                // Refused rather than answered like the block above: where an unblock goes
                // depends on where the account came from, so an account that is not blocked
                // has no answer to give. The caller is expected to re-read the list.
                //
                // Except when the *group* is what blocks it. The list drew "Blocked" for this
                // row because the account is blocked - the fact is on the screen - and
                // answering "the account is not blocked" would be answering a question the
                // operator did not ask. The row has nothing to clear; the group does.
                return group.Status is UserStatus.Blocked
                    ? new AdminUserOutcome
                    {
                        Action = AdminUserAction.BlockedByGroup,
                        UserId = userId,
                        Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                        {
                            [nameof(UserPatch.Status)] =
                                [$"The account is blocked by the group it belongs to ({group.Name})."]
                        }
                    }
                    : InvalidStatus(userId);
            }

            // The inverse of the block, not of "set the status to Registered": an account that
            // was never confirmed goes back to Unregistered, and no registration stamp is
            // invented for it on the way out of a state that never gave it one.
            var unblockedStatus = user.RegisteredAt is null
                ? UserStatus.Unregistered
                : UserStatus.Registered;

            await _users.SetStatusAsync(
                userId,
                unblockedStatus,
                user.RegisteredAt,
                cancellationToken);

            if (await SaveAsync(cancellationToken) is { } unblockFailure)
            {
                throw unblockFailure;
            }

            _logger.LogInformation(
                "Unblocked {Username}, now {State}.",
                user.Username,
                unblockedStatus);

            return new AdminUserOutcome
            {
                Action = AdminUserAction.Unblocked,
                UserId = userId
            };
        }

        /// <inheritdoc />
        public async Task<AdminUserOutcome> DeleteAsync(
            Guid userId,
            Guid actingUserId,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default)
        {
            // Before the row is even read, for the same reason the block refuses itself first:
            // deleting the caller's own account is a request that must not reach the row.
            if (userId == actingUserId)
            {
                return new AdminUserOutcome { Action = AdminUserAction.CannotDeleteSelf };
            }

            // The group's role travels only so that the permission check can ask it; nothing
            // about the group is written or deleted here.
            var user = await _users.GetAdminAccountAsync(userId, cancellationToken);

            if (user is null)
            {
                return NotFound();
            }

            if (!UserPermissionCatalog.CanEdit(granted, user.Role))
            {
                return Denied();
            }

            var username = user.Username;

            await _users.RemoveAsync(userId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

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
                Length: >= RegistrationLimits.UsernameMinLength
                    and <= RegistrationLimits.UsernameMaxLength
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
            if (username is not null
                && await _users.IsUsernameTakenAsync(username, excludeId, cancellationToken))
            {
                return nameof(UserPatch.Username);
            }

            if (email is not null
                && await _users.IsEmailTakenAsync(email, excludeId, cancellationToken))
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
        /// The refusal the two-layer gate produces: the request was well formed, the account is
        /// there, and this operator's group may not reach accounts of its role.
        /// </summary>
        /// <remarks>
        /// A named helper rather than a literal at each of the six call sites so that every one
        /// of them produces exactly the same action - the client branches on this word, and a
        /// refusal spelled two ways would be two answers to the same question. It carries no
        /// user identifier, so a caller cannot read the account they were refused out of the
        /// response body.
        /// </remarks>
        private static AdminUserOutcome Denied()
            => new() { Action = AdminUserAction.PermissionDenied };

        /// <summary>
        /// Writes whatever has been staged on the row, interpreting the one failure that is
        /// expected: a unique index refusing a value another account took meanwhile.
        /// </summary>
        /// <returns>Null when the row was written, or the exception when it was that race.</returns>
        private async Task<UniqueViolationException?> SaveAsync(CancellationToken cancellationToken)
            // Any unique refusal is returned rather than interpreted here: the callers ask
            // whether the username or the address is what another account now holds, and
            // rethrow the exception when neither is. That query is the discriminator, so a
            // refusal by an index this service does not expect cannot be answered as a
            // taken name.
            => await _unitOfWork.TrySaveAsync(cancellationToken);
    }
}
