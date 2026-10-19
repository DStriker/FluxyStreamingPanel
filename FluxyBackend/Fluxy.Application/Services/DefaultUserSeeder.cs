using Fluxy.Core.Abstractions;
using Fluxy.Core.Abstractions.Repositories;
using Fluxy.Core.Models.Users;
using Fluxy.Core.Persistence.Interfaces;
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

        private readonly IDatabaseInitializer _database;
        private readonly IUserRepository _users;
        private readonly IUserGroupRepository _groups;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DefaultUserSeeder> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultUserSeeder"/> class.
        /// </summary>
        /// <param name="database">Initializer that applies the migrations.</param>
        /// <param name="users">Repository the account is written through.</param>
        /// <param name="groups">Repository the base groups are reconciled through.</param>
        /// <param name="unitOfWork">Commits the seeded rows.</param>
        /// <param name="logger">Logger the credentials are reported to.</param>
        public DefaultUserSeeder(
            IDatabaseInitializer database,
            IUserRepository users,
            IUserGroupRepository groups,
            IUnitOfWork unitOfWork,
            ILogger<DefaultUserSeeder> logger)
        {
            _database = database;
            _users = users;
            _groups = groups;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            // A no-op once the schema is current. Doing it here is what makes the install a
            // single step: "docker compose up" plus "dotnet run" is enough.
            await _database.MigrateAsync(cancellationToken);

            await EnsureBaseGroupsAsync(cancellationToken);

            // An account in an administrator's group, whichever group that is: an operator who
            // renamed the base group or built another one at the same level has still been
            // given a way in, and seeding a second account for them would be a second
            // bootstrap password nobody asked for.
            var hasAdmin = await _users.HasAdminAsync(cancellationToken);

            if (hasAdmin)
            {
                return;
            }

            var password = GeneratePassword();

            await _users.AddAsync(
                new NewAccount
                {
                    Username = DefaultUsername,
                    Email = DefaultEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    GroupId = BaseUserGroups.Administrators,
                    Status = UserStatus.Registered,
                    RegisteredAt = DateTimeOffset.UtcNow
                },
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

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

                var currentRole = await _groups.GetRoleAsync(id, cancellationToken);

                if (currentRole is null)
                {
                    await _groups.AddAsync(
                        new NewUserGroup
                        {
                            Id = id,
                            Name = BaseUserGroups.NameOf(role),
                            Role = role,
                            Status = UserStatus.Registered
                        },
                        cancellationToken);

                    changed = true;
                }
                else if (currentRole.Value != role)
                {
                    await _groups.SetRoleAsync(id, role, cancellationToken);
                    changed = true;
                }

                changed |= await ReconcilePermissionsAsync(id, role, cancellationToken);
            }

            if (changed)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
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

            var current = await _groups.GetPermissionsAsync(groupId, cancellationToken);

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
                await _groups.RemovePermissionAsync(groupId, permission, cancellationToken);
            }

            foreach (var permission in missing)
            {
                await _groups.AddPermissionAsync(groupId, permission, cancellationToken);
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
