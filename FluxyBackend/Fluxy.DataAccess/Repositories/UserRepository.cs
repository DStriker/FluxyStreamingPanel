using Fluxy.Core.Abstractions.Repositories;
using Fluxy.Core.Models.Authentication;
using Fluxy.Core.Models.Users;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IUserRepository"/>.
    /// </summary>
    /// <remarks>
    /// Every read that returns an account includes its group, because the model's role, group
    /// name and effective status all come from that row; a repository that returned a model
    /// built without it would have to invent those three values. The standing read is the one
    /// exception, and it projects instead of materializing so no entity exists to map.
    /// </remarks>
    public sealed class UserRepository : IUserRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserRepository"/> class.
        /// </summary>
        /// <param name="context">Context the account rows are read from and staged in.</param>
        public UserRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<User?> FindByUsernameAsync(
            string username,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Users
                .AsNoTracking()
                .Include(entry => entry.Group)
                .FirstOrDefaultAsync(entry => entry.Username == username, cancellationToken);

            return entity?.ToModel();
        }

        /// <inheritdoc />
        public async Task<User?> FindByIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Users
                .AsNoTracking()
                .Include(entry => entry.Group)
                .FirstOrDefaultAsync(entry => entry.Id == userId, cancellationToken);

            return entity?.ToModel();
        }

        /// <inheritdoc />
        public async Task<AccountStanding> GetStandingAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            // The projection is the point: one indexed read of an account and its group by
            // primary key, rather than materializing either entity. The password hash and the
            // registration code are not fetched at all, so a mistake that logged this would
            // have far less to leak.
            var row = await _context.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => new
                {
                    user.Status,

                    // The null-forgiving operators are about the *declaration*, not about the
                    // database: the foreign key is NOT NULL and RESTRICT, so the join EF builds
                    // for a required relationship cannot leave the group out.
                    GroupStatus = user.Group!.Status,
                    Role = user.Group!.Role,
                    Permissions = user.Group!.Permissions
                        .Select(grant => grant.Permission)
                        .ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (row is null)
            {
                // A token that names an account which no longer exists. Status null rather than
                // a state of its own, so the caller checks one thing.
                return new AccountStanding();
            }

            return new AccountStanding
            {
                Status = UserStatusComposition.Combine(row.Status, row.GroupStatus),
                Role = row.Role,
                Permissions = row.Permissions.ToHashSet()
            };
        }

        /// <inheritdoc />
        public Task<bool> IsRegisteredAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
            => _context.Users
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.Id == userId && entry.Status == UserStatus.Registered,
                    cancellationToken);

        /// <inheritdoc />
        public Task<bool> HasAdminAsync(CancellationToken cancellationToken = default)
            => _context.Users.AnyAsync(
                user => user.Group!.Role == UserRole.Admin,
                cancellationToken);

        /// <inheritdoc />
        public Task<UserRole?> GetRoleAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
            => _context.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => (UserRole?)user.Group!.Role)
                .FirstOrDefaultAsync(cancellationToken);

        /// <inheritdoc />
        public Task<Guid> AddAsync(
            NewAccount account,
            CancellationToken cancellationToken = default)
        {
            var entity = new UserEntity
            {
                Username = account.Username,
                Email = account.Email,
                PasswordHash = account.PasswordHash,
                GroupId = account.GroupId,
                Status = account.Status,
                RegistrationCodeHash = account.RegistrationCodeHash,
                RegistrationCodeExpiresAt = account.RegistrationCodeExpiresAt,
                RegisteredAt = account.RegisteredAt,
                TimeZone = account.TimeZone,
                GeoProtectionEnabled = account.GeoProtectionEnabled,
                BindSessionToIp = account.BindSessionToIp
            };

            _context.Users.Add(entity);

            return Task.FromResult(entity.Id);
        }
    }
}
