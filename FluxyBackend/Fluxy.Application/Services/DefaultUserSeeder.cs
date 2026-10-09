using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Users;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace Fluxy.Application.Services
{
    /// <summary>
    /// Applies the migrations, keeps the three base groups in the shape the catalog defines,
    /// and creates the first administrator of an installation.
    /// </summary>
    /// <remarks>
    /// The password is generated at run time, hashed, and reported once in the log. Nothing
    /// stores it in clear text - not the repository, not the database - which is the reason
    /// this is a service and not <c>HasData</c> inside the migration: a migration is a file in
    /// git, and a bootstrap password baked into one stays readable there forever.
    ///
    /// The base groups are reconciled here rather than created by a migration for the same
    /// reason, one step removed: a permission added to
    /// <see cref="UserPermissionCatalog"/> is a change to code, and the group that must always
    /// carry every permission of its role should follow it without anybody having to remember
    /// to scaffold a migration for a table of constants. The three rows themselves are seeded
    /// by the migration only because existing accounts need something to point at before the
    /// foreign key can be declared; this is what keeps them true afterwards.
    /// </remarks>
    public sealed class DefaultUserSeeder : IUserSeeder
    {
        /// <summary>Login name of the generated account.</summary>
        public const string DefaultUsername = "admin";

        /// <summary>Email address of the generated account.</summary>
        public const string DefaultEmail = "admin@localhost";

        /// <summary>Number of characters in a generated password.</summary>
        public const int PasswordLength = 20;

        /// <summary>The roles the three base groups exist in, in enum order.</summary>
        private static readonly IReadOnlyList<UserRole> BaseRoles =
        [
            UserRole.Client,
            UserRole.Reseller,
            UserRole.Admin
        ];

        // Visually ambiguous characters are left out: the password is read from a console
        // line and retyped into a login form, so l/1/I and 0/O would cost support time.
        private const string PasswordAlphabet =
            "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        private readonly FluxyDbContext _context;
        private readonly ILogger<DefaultUserSeeder> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultUserSeeder"/> class.
        /// </summary>
        /// <param name="context">Context used to apply the migrations and insert the account.</param>
        /// <param name="logger">Logger the credentials are reported to.</param>
        public DefaultUserSeeder(FluxyDbContext context, ILogger<DefaultUserSeeder> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            // A no-op once the schema is current. Doing it here is what makes the install a
            // single step: "docker compose up" plus "dotnet run" is enough.
            await _context.Database.MigrateAsync(cancellationToken);

            await EnsureBaseGroupsAsync(cancellationToken);

            // An account in an administrator's group, whichever group that is: an operator who
            // renamed the base group or built another one at the same level has still been
            // given a way in, and seeding a second account for them would be a second
            // bootstrap password nobody asked for.
            var hasAdmin = await _context.Users
                .AnyAsync(user => user.Group!.Role == UserRole.Admin, cancellationToken);

            if (hasAdmin)
            {
                return;
            }

            var password = GeneratePassword();

            _context.Users.Add(new UserEntity
            {
                Username = DefaultUsername,
                Email = DefaultEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                GroupId = BaseUserGroups.Administrators,
                Status = UserStatus.Registered,
                RegisteredAt = DateTimeOffset.UtcNow
            });

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Created the default administrator for this installation.{NewLine}  Username: {Username}{NewLine}  Password: {Password}{NewLine}The password is shown once and is stored nowhere - save it now.",
                Environment.NewLine,
                DefaultUsername,
                Environment.NewLine,
                password,
                Environment.NewLine);
        }

        /// <summary>
        /// Creates each of the three groups the installation cannot operate without, and puts
        /// every permission of its role back on it.
        /// </summary>
        /// <remarks>
        /// Idempotent and safe to run on every start, which is the point: it is what makes a
        /// permission added to the catalog reach the base groups without a migration, and what
        /// repairs an installation where one of the three rows was removed by hand.
        ///
        /// The name is deliberately <i>not</i> restored. All three may be renamed - that is the
        /// one thing the admin panel lets anyone do to them - and a seeder that put the
        /// original spelling back on the next restart would make renaming a lie. The role is
        /// restored, because the rule that the base group of a role holds that role is what
        /// makes the three rows mean anything.
        /// </remarks>
        private async Task EnsureBaseGroupsAsync(CancellationToken cancellationToken)
        {
            var changed = false;

            foreach (var role in BaseRoles)
            {
                var id = BaseUserGroups.ForRole(role);

                var group = await _context.UserGroups
                    .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

                if (group is null)
                {
                    group = new UserGroupEntity(id)
                    {
                        Name = BaseUserGroups.NameOf(role),
                        Role = role,
                        Status = UserStatus.Registered
                    };

                    _context.UserGroups.Add(group);
                    changed = true;
                }
                else if (group.Role != role)
                {
                    group.Role = role;
                    changed = true;
                }

                changed |= await ReconcilePermissionsAsync(id, role, cancellationToken);
            }

            if (changed)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Makes the grants of one base group exactly the permissions its role has: every one
        /// the catalog offers is added, every one it no longer offers is removed.
        /// </summary>
        /// <returns>Whether anything was written.</returns>
        private async Task<bool> ReconcilePermissionsAsync(
            Guid groupId,
            UserRole role,
            CancellationToken cancellationToken)
        {
            var wanted = UserPermissionCatalog.ForRole(role);

            var current = await _context.GroupPermissions
                .Where(grant => grant.UserGroupId == groupId)
                .Select(grant => grant.Permission)
                .ToListAsync(cancellationToken);

            var missing = wanted.Where(permission => !current.Contains(permission)).ToList();
            var stale = current.Where(permission => !wanted.Contains(permission)).ToList();

            if (missing.Count is 0 && stale.Count is 0)
            {
                return false;
            }

            foreach (var permission in stale)
            {
                // Removed rather than left alone: a grant the catalog no longer defines is a
                // grant nothing can name, and keeping it would make the set on the row differ
                // from the set the group's own edit form would save back.
                var row = await _context.GroupPermissions
                    .FirstOrDefaultAsync(
                        grant => grant.UserGroupId == groupId && grant.Permission == permission,
                        cancellationToken);

                if (row is not null)
                {
                    _context.GroupPermissions.Remove(row);
                }
            }

            foreach (var permission in missing)
            {
                _context.GroupPermissions.Add(new UserGroupPermissionEntity
                {
                    UserGroupId = groupId,
                    Permission = permission
                });
            }

            return true;
        }

        /// <summary>
        /// Builds a random password out of <see cref="PasswordAlphabet"/>.
        /// </summary>
        private static string GeneratePassword()
        {
            var characters = RandomNumberGenerator.GetItems(
                PasswordAlphabet.AsSpan(),
                PasswordLength);

            return new string(characters);
        }
    }
}
