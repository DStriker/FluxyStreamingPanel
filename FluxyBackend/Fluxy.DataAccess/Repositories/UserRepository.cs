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
        public async Task<StoredProfile?> GetProfileAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Users
                .AsNoTracking()
                .Include(entry => entry.Group)
                .ThenInclude(entry => entry!.Permissions)
                .FirstOrDefaultAsync(entry => entry.Id == userId, cancellationToken);

            if (entity is null)
            {
                return null;
            }

            var account = entity.ToModel();

            return new StoredProfile
            {
                Username = account.Username,
                Email = account.Email,
                Role = account.Role,
                Status = account.Status,
                TimeZone = account.TimeZone,
                Permissions = entity.Group!.Permissions
                    .Select(grant => grant.Permission)
                    .ToHashSet()
            };
        }

        /// <inheritdoc />
        public async Task<EditableAccount?> FindEditableByIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Users
                .Include(entry => entry.Group)
                .FirstOrDefaultAsync(entry => entry.Id == userId, cancellationToken);

            return ToEditable(entity);
        }

        /// <inheritdoc />
        public async Task<EditableAccount?> FindEditableByEmailAsync(
            string email,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Users
                .Include(entry => entry.Group)
                .FirstOrDefaultAsync(entry => entry.Email == email, cancellationToken);

            return ToEditable(entity);
        }

        /// <inheritdoc />
        public async Task<StoredAccount?> FindForRegistrationByUsernameAsync(
            string username,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Users
                .FirstOrDefaultAsync(entry => entry.Username == username, cancellationToken);

            return entity is null ? null : ToStoredAccount(entity);
        }

        /// <inheritdoc />
        public async Task<StoredAccount?> FindForRegistrationByEmailAsync(
            string email,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Users
                .FirstOrDefaultAsync(entry => entry.Email == email, cancellationToken);

            return entity is null ? null : ToStoredAccount(entity);
        }

        /// <inheritdoc />
        public async Task<StoredAccount?> FindForPasswordResetAsync(
            string username,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Users
                .FirstOrDefaultAsync(entry => entry.Username == username, cancellationToken);

            return entity is null ? null : ToStoredAccount(entity);
        }

        /// <inheritdoc />
        public Task<bool> IsUsernameTakenAsync(
            string username,
            Guid exceptUserId,
            CancellationToken cancellationToken = default)
            => _context.Users
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.Id != exceptUserId && entry.Username == username,
                    cancellationToken);

        /// <inheritdoc />
        public Task<bool> IsEmailTakenAsync(
            string email,
            Guid exceptUserId,
            CancellationToken cancellationToken = default)
            => _context.Users
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.Id != exceptUserId && entry.Email == email,
                    cancellationToken);

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

        /// <inheritdoc />
        public async Task UpdatePendingRegistrationAsync(
            Guid userId,
            string passwordHash,
            string codeHash,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken = default)
        {
            var entity = await FindTrackedAsync(userId, cancellationToken);

            if (entity is null)
            {
                return;
            }

            entity.PasswordHash = passwordHash;
            entity.RegistrationCodeHash = codeHash;
            entity.RegistrationCodeExpiresAt = expiresAt;
        }

        /// <inheritdoc />
        public async Task ConfirmRegistrationAsync(
            Guid userId,
            DateTimeOffset registeredAt,
            CancellationToken cancellationToken = default)
        {
            var entity = await FindTrackedAsync(userId, cancellationToken);

            if (entity is null)
            {
                return;
            }

            entity.Status = UserStatus.Registered;
            entity.RegisteredAt = registeredAt;
            entity.RegistrationCodeHash = null;
            entity.RegistrationCodeExpiresAt = null;
        }

        /// <inheritdoc />
        public async Task SetUsernameAsync(
            Guid userId,
            string username,
            CancellationToken cancellationToken = default)
        {
            var entity = await FindTrackedAsync(userId, cancellationToken);

            if (entity is not null)
            {
                entity.Username = username;
            }
        }

        /// <inheritdoc />
        public async Task SetEmailAsync(
            Guid userId,
            string email,
            CancellationToken cancellationToken = default)
        {
            var entity = await FindTrackedAsync(userId, cancellationToken);

            if (entity is not null)
            {
                entity.Email = email;
            }
        }

        /// <inheritdoc />
        public async Task SetPasswordHashAsync(
            Guid userId,
            string passwordHash,
            CancellationToken cancellationToken = default)
        {
            var entity = await FindTrackedAsync(userId, cancellationToken);

            if (entity is not null)
            {
                entity.PasswordHash = passwordHash;
            }
        }

        /// <inheritdoc />
        public async Task SetTimeZoneAsync(
            Guid userId,
            string? timeZone,
            CancellationToken cancellationToken = default)
        {
            var entity = await FindTrackedAsync(userId, cancellationToken);

            if (entity is not null)
            {
                entity.TimeZone = timeZone;
            }
        }

        /// <summary>
        /// Loads a row that a preceding intent method is about to stage a change on.
        /// </summary>
        /// <remarks>
        /// The row read by the service is already tracked, so this resolves from the change
        /// tracker and issues no second query in the ordinary flow. A row that is no longer
        /// tracked - or was never read - is loaded once rather than assumed to exist.
        /// </remarks>
        private async Task<UserEntity?> FindTrackedAsync(
            Guid userId,
            CancellationToken cancellationToken)
            => await _context.Users.FindAsync([userId], cancellationToken);

        private static EditableAccount? ToEditable(UserEntity? entity)
        {
            if (entity is null)
            {
                return null;
            }

            return new EditableAccount
            {
                Account = entity.ToModel(),
                RowStatus = entity.Status,
                GroupStatus = entity.Group!.Status
            };
        }

        private static StoredAccount ToStoredAccount(UserEntity entity)
            => new()
            {
                Id = entity.Id,
                Username = entity.Username,
                Email = entity.Email,
                RowStatus = entity.Status
            };
    }
}
