using Fluxy.Core.Abstractions;
using Fluxy.Core.Abstractions.Repositories;
using Fluxy.Core.Models.Users;

namespace Fluxy.Application.Services.Users
{
    /// <summary>
    /// Reads every group of the installation and changes them on an admin's behalf.
    /// </summary>
    /// <remarks>
    /// Two refusals here protect the installation rather than the row, and both live in this
    /// class so that a second endpoint reaching the same action cannot forget them: a base
    /// group accepts a rename and nothing else, and a group with members may not be deleted.
    /// The first is a rule about what the three rows mean (see <see cref="BaseUserGroups"/>),
    /// the second is a rule about what a deletion would leave behind - an account whose group
    /// is gone is an account whose level nobody can answer for.
    ///
    /// What is deliberately <i>not</i> here: no session revocation when a group is blocked.
    /// Every authorized request re-reads the standing of its caller, so a blocked group makes
    /// every one of its members answer 403 on the next call, and the refresh path ends the
    /// chain the moment it notices. Reaching N accounts to revoke N chains would be a second
    /// mechanism for something the first one already guarantees, and it would need a bulk
    /// operation on a token service that otherwise knows about one session at a time.
    ///
    /// Nothing here knows about HTTP. Which <see cref="UserGroupAction"/> becomes a 201 and
    /// which becomes a 409 is the transport layer's business.
    /// </remarks>
    public sealed class UserGroupService : IUserGroupService
    {
        /// <summary>The sentence a name that is blank or too long gets.</summary>
        private static readonly string[] NameRule =
        [
            "The name must be between 1 and "
                + UserGroupLimits.NameMaxLength
                + " characters."
        ];

        /// <summary>The sentence a role that names no member of the enum gets.</summary>
        private static readonly string[] RoleRule =
        ["The role must be one of: client, reseller, admin."];

        /// <summary>The sentence a state that names no member of the enum gets.</summary>
        private static readonly string[] StatusRule =
        ["The status must be one of: unregistered, registered, blocked."];

        private readonly IUserGroupRepository _groups;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserGroupService"/> class.
        /// </summary>
        /// <param name="groups">Repository holding the groups and their permissions.</param>
        /// <param name="unitOfWork">Commits the changes one operation stages.</param>
        public UserGroupService(
            IUserGroupRepository groups,
            IUnitOfWork unitOfWork)
        {
            _groups = groups;
            _unitOfWork = unitOfWork;
        }

        /// <inheritdoc />
        public Task<UserGroupPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            UserRole? role,
            UserStatus? status,
            UserGroupSortField field,
            UserGroupSortOrder order,
            CancellationToken cancellationToken = default)
            => _groups.GetPageAsync(
                page,
                pageSize,
                search,
                role,
                status,
                field,
                order,
                cancellationToken);

        /// <inheritdoc />
        public Task<UserGroupDetail?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => _groups.GetAsync(id, cancellationToken);

        /// <inheritdoc />
        public Task<IReadOnlyList<UserGroupListItem>> GetAllAsync(
            CancellationToken cancellationToken = default)
            => _groups.GetAllAsync(cancellationToken);

        /// <inheritdoc />
        public async Task<UserGroupOutcome> CreateAsync(
            NewUserGroup input,
            CancellationToken cancellationToken = default)
        {
            var name = input.Name.Trim();

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (name.Length is < 1 or > UserGroupLimits.NameMaxLength)
            {
                errors[nameof(NewUserGroup.Name)] = NameRule;
            }

            // A body is parsed before it reaches here, but the enum is a number in the column
            // and a number outside the members would be a level every role check has to guess
            // about - refused rather than coerced, because a group at no level is a group
            // nobody can answer for.
            if (!Enum.IsDefined(input.Role))
            {
                errors[nameof(NewUserGroup.Role)] = RoleRule;
            }

            if (!Enum.IsDefined(input.Status))
            {
                errors[nameof(NewUserGroup.Status)] = StatusRule;
            }

            if (errors.Count > 0)
            {
                return Invalid(errors);
            }

            if (await _groups.IsNameTakenAsync(name, exceptGroup: null, cancellationToken))
            {
                return new UserGroupOutcome
                {
                    Action = UserGroupAction.AlreadyExists,
                    Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        [nameof(NewUserGroup.Name)] = ["Another group already has that name."]
                    }
                };
            }

            // Dropped rather than refused: a permission the role may not hold means nothing
            // once the group exists, and a save that wrote it anyway would answer a form's
            // checkboxes with a grant that does nothing - see UserPermissionCatalog.
            var permissions = ToSet(UserPermissionCatalog.Normalise(
                input.Permissions ?? (IEnumerable<UserPermission>)[],
                input.Role));

            var groupId = await _groups.AddWithPermissionsAsync(
                input with { Name = name },
                permissions,
                cancellationToken);

            if (await SaveAsync(cancellationToken) is not null)
            {
                // A name another request took between the check above and this write. The
                // answer is the same one the check would have given.
                return new UserGroupOutcome { Action = UserGroupAction.AlreadyExists };
            }

            return new UserGroupOutcome
            {
                Action = UserGroupAction.Created,
                GroupId = groupId
            };
        }

        /// <inheritdoc />
        public async Task<UserGroupOutcome> UpdateAsync(
            Guid id,
            UserGroupPatch patch,
            CancellationToken cancellationToken = default)
        {
            var group = await _groups.FindForUpdateAsync(id, cancellationToken);

            if (group is null)
            {
                return NotFound(id);
            }

            var isBase = BaseUserGroups.IsBase(group.Id);

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var name = patch.Name?.Trim();
            if (patch.Name is not null && name!.Length is < 1 or > UserGroupLimits.NameMaxLength)
            {
                errors[nameof(UserGroupPatch.Name)] = NameRule;
            }

            if (patch.Role is { } role && !Enum.IsDefined(role))
            {
                errors[nameof(UserGroupPatch.Role)] = RoleRule;
            }

            if (patch.Status is { } state && !Enum.IsDefined(state))
            {
                errors[nameof(UserGroupPatch.Status)] = StatusRule;
            }

            if (errors.Count > 0)
            {
                return Invalid(errors, id);
            }

            // After validation rather than before it, so a base group carrying both an
            // invalid name and a forbidden field is told about the name - which is the
            // mistake the form could have made - instead of a refusal that hides it.
            if (isBase && (patch.Role is not null || patch.Status is not null || patch.Permissions is not null))
            {
                return new UserGroupOutcome { Action = UserGroupAction.ImmutableBase, GroupId = id };
            }

            if (name is not null && await _groups.IsNameTakenAsync(name, id, cancellationToken))
            {
                return new UserGroupOutcome
                {
                    Action = UserGroupAction.AlreadyExists,
                    GroupId = id,
                    Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        [nameof(UserGroupPatch.Name)] = ["Another group already has that name."]
                    }
                };
            }

            var roleAfter = patch.Role ?? group.Role;

            // What the group will grant once this request is through, or null to leave the
            // set alone. Two cases reach it: an explicit replacement, and a role change -
            // where the permissions that belonged to the old level are re-checked against the
            // new one, because a grant the new role may not hold would be a grant nothing
            // can name. A role change never *adds* anything: the base group of the new role
            // holds the full list, and every other group starts from what it already had.
            IReadOnlySet<UserPermission>? permissionsAfter = null;

            if (patch.Permissions is { } requested)
            {
                permissionsAfter = ToSet(UserPermissionCatalog.Normalise(requested, roleAfter));
            }
            else if (patch.Role is { } movedTo && movedTo != group.Role)
            {
                permissionsAfter = ToSet(UserPermissionCatalog.Normalise(
                    group.Permissions,
                    movedTo));
            }

            if (name is not null)
            {
                await _groups.SetNameAsync(id, name, cancellationToken);
            }

            if (patch.Role is { } newRole && newRole != group.Role)
            {
                await _groups.SetRoleAsync(id, newRole, cancellationToken);
            }

            if (patch.Status is { } newState)
            {
                await _groups.SetStatusAsync(id, newState, cancellationToken);
            }

            if (permissionsAfter is { } wanted)
            {
                await _groups.ReplacePermissionsAsync(id, wanted, cancellationToken);
            }

            if (await SaveAsync(cancellationToken) is not null)
            {
                return new UserGroupOutcome
                {
                    Action = UserGroupAction.AlreadyExists,
                    GroupId = id
                };
            }

            return new UserGroupOutcome
            {
                Action = UserGroupAction.Updated,
                GroupId = id
            };
        }

        /// <inheritdoc />
        public async Task<UserGroupOutcome> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            // Read with its grants so the delete takes them in the same save as the row they
            // belong to, rather than leaving them to the cascade to find. The cascade is still
            // there underneath - it is what makes an interrupted delete impossible to leave
            // half done - but a request that removes 30 rows says so out loud.
            var group = await _groups.FindForUpdateAsync(id, cancellationToken);

            if (group is null)
            {
                return NotFound(id);
            }

            if (BaseUserGroups.IsBase(group.Id))
            {
                return new UserGroupOutcome { Action = UserGroupAction.ImmutableBase, GroupId = id };
            }

            if (await _groups.CountMembersAsync(id, cancellationToken) > 0)
            {
                return new UserGroupOutcome { Action = UserGroupAction.InUse, GroupId = id };
            }

            await _groups.RemoveAsync(id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new UserGroupOutcome
            {
                Action = UserGroupAction.Removed,
                GroupId = id
            };
        }

        /// <inheritdoc />
        public async Task<BulkUserGroupOutcome> BulkAsync(
            BulkUserGroupOperation operation,
            CancellationToken cancellationToken = default)
        {
            var items = new List<BulkUserGroupItemResult>(operation.GroupIds.Count);

            foreach (var groupId in operation.GroupIds)
            {
                // Every entry comes from the single-group method of the same name, so the two
                // refusals that protect the installation are the ones those methods already
                // make rather than a copy of them - see IUserGroupService.BulkAsync.
                var outcome = operation.Action switch
                {
                    BulkUserGroupAction.Delete => await DeleteAsync(groupId, cancellationToken),

                    // Block and Unblock are the state the row button sends, because there is
                    // no dedicated endpoint for either and one spelling of a state change is
                    // enough. Registered rather than Unregistered for an unblock is the same
                    // choice the single-row button makes: taking a group out of Blocked has no
                    // reason to also declare that every member never finished registering.
                    BulkUserGroupAction.Block => await UpdateAsync(
                        groupId,
                        new UserGroupPatch { Status = UserStatus.Blocked },
                        cancellationToken),

                    BulkUserGroupAction.Unblock => await UpdateAsync(
                        groupId,
                        new UserGroupPatch { Status = UserStatus.Registered },
                        cancellationToken),

                    // Unreachable: see the same throw in UserAdminService.BulkAsync.
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(operation),
                        operation.Action,
                        "Unknown bulk user group action.")
                };

                items.Add(new BulkUserGroupItemResult
                {
                    GroupId = groupId,
                    Action = outcome.Action,
                    Errors = outcome.Errors
                });
            }

            return new BulkUserGroupOutcome { Action = operation.Action, Items = items };
        }

        /// <summary>
        /// Turns the bitmask <see cref="UserPermissionCatalog.Normalise"/> hands back into the
        /// set of individual keys it stands for.
        /// </summary>
        /// <remarks>
        /// Driven by the catalog rather than by casting the number into each member in turn:
        /// a flag the catalog does not list is not a permission of this system, so a set built
        /// from <c>All</c> can only hold keys that exist.
        /// </remarks>
        /// <param name="flags">Combination of grants, possibly <see cref="UserPermission.None"/>.</param>
        /// <returns>Every member of the catalog that is set in <paramref name="flags"/>.</returns>
        private static IReadOnlySet<UserPermission> ToSet(UserPermission flags)
        {
            var result = new HashSet<UserPermission>();

            foreach (var member in UserPermissionCatalog.All)
            {
                if ((flags & member) == member)
                {
                    result.Add(member);
                }
            }

            return result;
        }

        /// <summary>Writes the staged changes, interpreting the one failure that is expected.</summary>
        /// <returns>Null when it was written, or the exception when the name index refused it.</returns>
        /// <remarks>
        /// Only the index making a group's name unique is interpreted. An update also writes the
        /// permission rows, and a refusal by their index is a bug in this service rather than a
        /// name another request took - answering it "already exists" is how such a bug hides.
        /// </remarks>
        private async Task<UniqueViolationException?> SaveAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return null;
            }
            catch (UniqueViolationException exception) when (exception.IsConstraint(NameIndexName))
            {
                return exception;
            }
        }

        /// <summary>The unique index making a group's name unique.</summary>
        private const string NameIndexName = "IX_user_groups_name";

        private static UserGroupOutcome NotFound(Guid id)
            => new() { Action = UserGroupAction.NotFound, GroupId = id };

        private static UserGroupOutcome Invalid(
            IReadOnlyDictionary<string, string[]> errors,
            Guid id = default)
            => new() { Action = UserGroupAction.Invalid, GroupId = id, Errors = errors };
    }
}
