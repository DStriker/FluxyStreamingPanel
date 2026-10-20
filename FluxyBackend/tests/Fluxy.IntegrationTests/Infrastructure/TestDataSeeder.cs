using Fluxy.Application.Users.Utils;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Users.Commands;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Repositories;
using Fluxy.Core.Users.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxy.IntegrationTests.Infrastructure
{
    /// <summary>
    /// Creates the rows a test starts from without going through the API.
    /// </summary>
    public static class TestDataSeeder
    {
        /// <summary>
        /// Recreates the three groups the installation cannot operate without, with the permissions
        /// the catalog assigns to their roles.
        /// </summary>
        public static async Task EnsureBaseGroupsAsync(
            IServiceProvider services,
            CancellationToken cancellationToken = default)
        {
            var groups = services.GetRequiredService<IUserGroupRepository>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();

            await EnsureBaseGroupsAsync(groups, unitOfWork, cancellationToken);
        }

        /// <summary>
        /// Recreates the three base groups through repositories already resolved from a scope.
        /// </summary>
        public static async Task EnsureBaseGroupsAsync(
            IUserGroupRepository groups,
            IUnitOfWork unitOfWork,
            CancellationToken cancellationToken = default)
        {
            foreach (var role in new[] { UserRole.Client, UserRole.Reseller, UserRole.Admin })
            {
                var id = BaseUserGroups.ForRole(role);
                var wanted = UserPermissionCatalog.ForRole(role).ToHashSet();
                var existing = await groups.GetSummaryAsync(id, cancellationToken);

                if (existing is null)
                {
                    await groups.AddWithPermissionsAsync(
                        new NewUserGroup
                        {
                            Id = id,
                            Name = BaseUserGroups.NameOf(role),
                            Role = role,
                            Status = UserStatus.Registered
                        },
                        wanted,
                        cancellationToken);

                    continue;
                }

                if (existing.Role != role)
                {
                    await groups.SetRoleAsync(id, role, cancellationToken);
                }

                if (existing.Status != UserStatus.Registered)
                {
                    await groups.SetStatusAsync(id, UserStatus.Registered, cancellationToken);
                }

                var current = (await groups.GetPermissionsAsync(id, cancellationToken)).ToHashSet();
                if (!current.SetEquals(wanted))
                {
                    await groups.ReplacePermissionsAsync(id, wanted, cancellationToken);
                }
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Creates an account in its role's base group, or in <paramref name="groupId"/> when one is
        /// named. The password is hashed with a low cost factor; only tests ever read it.
        /// </summary>
        public static async Task<Guid> CreateUserAsync(
            IServiceProvider services,
            string username,
            string password,
            UserRole role,
            UserStatus status = UserStatus.Registered,
            Guid? groupId = null,
            string? email = null,
            string? timeZone = null,
            bool geoProtectionEnabled = false,
            bool bindSessionToIp = false,
            CancellationToken cancellationToken = default)
        {
            var users = services.GetRequiredService<IUserRepository>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();
            var now = DateTimeOffset.UtcNow;

            var id = await users.AddAsync(
                new NewAccount
                {
                    Username = username,
                    Email = EmailNormalizer.Normalize(email ?? $"{username}@example.test"),
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4),
                    GroupId = groupId ?? BaseUserGroups.ForRole(role),
                    Status = status,
                    RegisteredAt = status is UserStatus.Unregistered ? null : now,
                    TimeZone = timeZone,
                    GeoProtectionEnabled = geoProtectionEnabled,
                    BindSessionToIp = bindSessionToIp
                },
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return id;
        }

        /// <summary>
        /// Creates a custom group with the permissions the role is allowed to hold.
        /// </summary>
        public static async Task<Guid> CreateGroupAsync(
            IServiceProvider services,
            string name,
            UserRole role,
            UserStatus status = UserStatus.Registered,
            CancellationToken cancellationToken = default,
            params UserPermission[] permissions)
        {
            var groups = services.GetRequiredService<IUserGroupRepository>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();

            var wanted = permissions
                .Where(permission => UserPermissionCatalog.IsValidForRole(permission, role))
                .ToHashSet();

            var id = await groups.AddWithPermissionsAsync(
                new NewUserGroup
                {
                    Name = name,
                    Role = role,
                    Status = status
                },
                wanted,
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return id;
        }
    }
}
