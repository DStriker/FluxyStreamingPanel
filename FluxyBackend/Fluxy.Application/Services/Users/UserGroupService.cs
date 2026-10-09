using System.Linq.Expressions;
using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Users;
using Fluxy.DataAccess.Configurations;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

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
                + UserGroupConfiguration.NameMaxLength
                + " characters."
        ];

        /// <summary>The sentence a role that names no member of the enum gets.</summary>
        private static readonly string[] RoleRule =
        ["The role must be one of: client, reseller, admin."];

        /// <summary>The sentence a state that names no member of the enum gets.</summary>
        private static readonly string[] StatusRule =
        ["The status must be one of: unregistered, registered, blocked."];

        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserGroupService"/> class.
        /// </summary>
        /// <param name="context">Context holding the groups and their permissions.</param>
        public UserGroupService(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<UserGroupPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            UserRole? role,
            UserStatus? status,
            UserGroupSortField field,
            UserGroupSortOrder order,
            CancellationToken cancellationToken = default)
        {
            var query = _context.UserGroups.AsNoTracking();

            if (role is { } wantedRole)
            {
                query = query.Where(entry => entry.Role == wantedRole);
            }

            if (status is { } wantedStatus)
            {
                query = query.Where(entry => entry.Status == wantedStatus);
            }

            // Folded before the comparison rather than with a collation, the same way the
            // accounts list searches a username: `Contains` on the lowered value is a
            // substring match a name answers to however it was typed.
            var needle = search?.Trim().ToLowerInvariant() ?? string.Empty;
            if (needle.Length > 0)
            {
                query = query.Where(entry => entry.Name.ToLower().Contains(needle));
            }

            // Counted *before* the page is taken and with the same filter, because the pager
            // shows this number: a total counted over every row would advertise pages that the
            // filter has already removed.
            var total = await query.CountAsync(cancellationToken);

            query = Order(query, field, order);

            var rows = await query
                .Select(entry => new StoredGroup
                {
                    Id = entry.Id,
                    Name = entry.Name,
                    Role = entry.Role,
                    Status = entry.Status,

                    // Counted rather than selected: the column is a count, and the sort this
                    // page was ordered by may be this same number, so it is read for every
                    // row of every page once - a second query per page would buy nothing.
                    PermissionsCount = entry.Permissions.Count
                })
                // Widening to long before the cast: a page number comes off a query string,
                // and `(page - 1) * pageSize` on a huge one overflows int into a negative
                // OFFSET, which the database answers as an error rather than an empty page.
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new UserGroupPage
            {
                Items = rows
                    .Select(row => new UserGroupListItem
                    {
                        Id = row.Id,
                        Name = row.Name,
                        Role = row.Role,
                        Status = row.Status,
                        PermissionsCount = row.PermissionsCount,
                        IsBase = BaseUserGroups.IsBase(row.Id)
                    })
                    .ToList(),
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public async Task<UserGroupDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var group = await _context.UserGroups
                .AsNoTracking()
                .Include(entry => entry.Permissions)
                .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

            if (group is null)
            {
                return null;
            }

            var members = await _context.Users
                .AsNoTracking()
                .CountAsync(entry => entry.GroupId == id, cancellationToken);

            return new UserGroupDetail
            {
                Id = group.Id,
                Name = group.Name,
                Role = group.Role,
                Status = group.Status,
                Permissions = group.Permissions
                    .Select(grant => grant.Permission)
                    .ToHashSet(),
                IsBase = BaseUserGroups.IsBase(group.Id),
                Members = members,
                CreatedAt = group.CreatedAt,
                UpdatedAt = group.UpdatedAt
            };
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<UserGroupListItem>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            var rows = await _context.UserGroups
                .AsNoTracking()
                .OrderBy(entry => entry.Name)
                .Select(entry => new StoredGroup
                {
                    Id = entry.Id,
                    Name = entry.Name,
                    Role = entry.Role,
                    Status = entry.Status,
                    PermissionsCount = entry.Permissions.Count
                })
                .ToListAsync(cancellationToken);

            return rows
                .Select(row => new UserGroupListItem
                {
                    Id = row.Id,
                    Name = row.Name,
                    Role = row.Role,
                    Status = row.Status,
                    PermissionsCount = row.PermissionsCount,
                    IsBase = BaseUserGroups.IsBase(row.Id)
                })
                .ToList();
        }

        /// <inheritdoc />
        public async Task<UserGroupOutcome> CreateAsync(
            NewUserGroup input,
            CancellationToken cancellationToken = default)
        {
            var name = input.Name.Trim();

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (name.Length is < 1 or > UserGroupConfiguration.NameMaxLength)
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

            if (await NameTakenAsync(name, exceptGroup: null, cancellationToken))
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

            var entity = new UserGroupEntity
            {
                Name = name,
                Role = input.Role,
                Status = input.Status
            };

            foreach (var permission in permissions)
            {
                entity.Permissions.Add(new UserGroupPermissionEntity
                {
                    UserGroupId = entity.Id,
                    Permission = permission
                });
            }

            _context.UserGroups.Add(entity);

            if (await SaveAsync(cancellationToken) is not null)
            {
                // A name another request took between the check above and this write. The
                // answer is the same one the check would have given.
                return new UserGroupOutcome { Action = UserGroupAction.AlreadyExists };
            }

            return new UserGroupOutcome
            {
                Action = UserGroupAction.Created,
                GroupId = entity.Id
            };
        }

        /// <inheritdoc />
        public async Task<UserGroupOutcome> UpdateAsync(
            Guid id,
            UserGroupPatch patch,
            CancellationToken cancellationToken = default)
        {
            var group = await _context.UserGroups
                .Include(entry => entry.Permissions)
                .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

            if (group is null)
            {
                return NotFound(id);
            }

            var isBase = BaseUserGroups.IsBase(group.Id);

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var name = patch.Name?.Trim();
            if (patch.Name is not null && name!.Length is < 1 or > UserGroupConfiguration.NameMaxLength)
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

            if (name is not null && await NameTakenAsync(name, id, cancellationToken))
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
                    group.Permissions.Select(grant => grant.Permission),
                    movedTo));
            }

            if (name is not null)
            {
                group.Name = name;
            }

            if (patch.Role is { } newRole && newRole != group.Role)
            {
                group.Role = newRole;
            }

            if (patch.Status is { } newState)
            {
                group.Status = newState;
            }

            if (permissionsAfter is { } wanted)
            {
                ApplyPermissions(group, wanted);
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
            // Loaded with its grants so the delete takes them in the same save as the row
            // they belong to, rather than leaving them to the cascade to find. The cascade is
            // still there underneath - it is what makes an interrupted delete impossible to
            // leave half done - but a request that removes 30 rows says so out loud.
            var group = await _context.UserGroups
                .Include(entry => entry.Permissions)
                .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

            if (group is null)
            {
                return NotFound(id);
            }

            if (BaseUserGroups.IsBase(group.Id))
            {
                return new UserGroupOutcome { Action = UserGroupAction.ImmutableBase, GroupId = id };
            }

            if (await _context.Users.AsNoTracking()
                    .AnyAsync(entry => entry.GroupId == id, cancellationToken))
            {
                return new UserGroupOutcome { Action = UserGroupAction.InUse, GroupId = id };
            }

            _context.UserGroups.Remove(group);

            await _context.SaveChangesAsync(cancellationToken);

            return new UserGroupOutcome
            {
                Action = UserGroupAction.Removed,
                GroupId = id
            };
        }

        /// <summary>
        /// Orders by the first key and breaks the tie with the second.
        /// </summary>
        /// <remarks>
        /// Every branch carries a tie-break. Two groups share a name's first letter, share a
        /// role and quite often share a state, and without a tie-break the database is free to
        /// return them in any order it likes between one page and the next - which is how the
        /// same group appears on two pages or on none.
        /// </remarks>
        private static IQueryable<UserGroupEntity> Order(
            IQueryable<UserGroupEntity> query,
            UserGroupSortField field,
            UserGroupSortOrder order)
        {
            var descending = order is UserGroupSortOrder.Descending;

            return field switch
            {
                UserGroupSortField.Role => Chain(
                    query, entry => entry.Role, entry => entry.Name, descending),

                UserGroupSortField.Status => Chain(
                    query, entry => entry.Status, entry => entry.Name, descending),

                UserGroupSortField.PermissionsCount => Chain(
                    query, entry => entry.Permissions.Count, entry => entry.Name, descending),

                UserGroupSortField.CreatedAt => Chain(
                    query, entry => entry.CreatedAt, entry => entry.Name, descending),

                _ => Chain(query, entry => entry.Name, entry => entry.Id, descending)
            };
        }

        /// <summary>Orders by the first key and breaks the tie with the second.</summary>
        private static IOrderedQueryable<UserGroupEntity> Chain<TKey1, TKey2>(
            IQueryable<UserGroupEntity> source,
            Expression<Func<UserGroupEntity, TKey1>> primary,
            Expression<Func<UserGroupEntity, TKey2>> tieBreak,
            bool descending)
        {
            var ordered = descending
                ? source.OrderByDescending(primary)
                : source.OrderBy(primary);

            return descending
                ? ordered.ThenByDescending(tieBreak)
                : ordered.ThenBy(tieBreak);
        }

        /// <summary>
        /// Whether some other group already answers to that name.
        /// </summary>
        /// <remarks>
        /// Compared exactly, case sensitively, for the same reason username and email are:
        /// PostgreSQL decides what "equal" means for a varchar from the collation of the
        /// column, and the default one compares the bytes - which is what makes "Clients" and
        /// "clients" two groups, exactly as it makes them two accounts. Excluding
        /// <paramref name="exceptGroup"/> is what lets a rename keep its own name.
        /// </remarks>
        private async Task<bool> NameTakenAsync(
            string name,
            Guid? exceptGroup,
            CancellationToken cancellationToken)
        {
            return await _context.UserGroups
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.Name == name
                        && (exceptGroup == null || entry.Id != exceptGroup),
                    cancellationToken);
        }

        /// <summary>
        /// Makes the rows of one group read exactly like <paramref name="wanted"/>: grants it
        /// no longer holds are removed, grants it has gained are inserted, and the order is
        /// never a fact - a set has none.
        /// </summary>
        private static void ApplyPermissions(
            UserGroupEntity group,
            IReadOnlySet<UserPermission> wanted)
        {
            var stale = group.Permissions
                .Where(grant => !wanted.Contains(grant.Permission))
                .ToList();

            foreach (var grant in stale)
            {
                group.Permissions.Remove(grant);
            }

            var held = group.Permissions
                .Select(grant => grant.Permission)
                .ToHashSet();

            foreach (var permission in wanted)
            {
                if (held.Add(permission))
                {
                    group.Permissions.Add(new UserGroupPermissionEntity
                    {
                        UserGroupId = group.Id,
                        Permission = permission
                    });
                }
            }
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
        /// <returns>Null when it was written, or the exception when a unique index refused it.</returns>
        private async Task<DbUpdateException?> SaveAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return null;
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                // The tracker is cleared because the rows it was holding did not reach the
                // database - leaving them attached would make the next save try the same write.
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

        private static UserGroupOutcome NotFound(Guid id)
            => new() { Action = UserGroupAction.NotFound, GroupId = id };

        private static UserGroupOutcome Invalid(
            IReadOnlyDictionary<string, string[]> errors,
            Guid id = default)
            => new() { Action = UserGroupAction.Invalid, GroupId = id, Errors = errors };

        /// <summary>The columns a page of the group list draws, before the shape it returns.</summary>
        private sealed class StoredGroup
        {
            public Guid Id { get; init; } = Guid.Empty;
            public string Name { get; init; } = string.Empty;
            public UserRole Role { get; init; }
            public UserStatus Status { get; init; }
            public int PermissionsCount { get; init; }
        }
    }
}
