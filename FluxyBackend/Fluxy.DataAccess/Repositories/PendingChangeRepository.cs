using Fluxy.Core.Abstractions.Repositories;
using Fluxy.Core.Models.Users;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IPendingChangeRepository"/>.
    /// </summary>
    /// <remarks>
    /// One row per account is a database rule, not a convention here: the unique index on
    /// <c>user_id</c> is what turns two simultaneous requests into one winner, and the caller
    /// keeps interpreting that refusal. This class does not catch it, because only the service
    /// knows which refusal is a race and which one is a bug.
    /// </remarks>
    public sealed class PendingChangeRepository : IPendingChangeRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="PendingChangeRepository"/> class.
        /// </summary>
        /// <param name="context">Context the staged rows are read from and staged in.</param>
        public PendingChangeRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<StoredPendingChange?> FindByUserIdAsync(
            Guid userId,
            PendingChangeKind? kind = null,
            CancellationToken cancellationToken = default)
        {
            var query = _context.PendingChanges
                .Where(change => change.UserId == userId);

            if (kind is { } wanted)
            {
                query = query.Where(change => change.Kind == wanted);
            }

            var entity = await query.FirstOrDefaultAsync(cancellationToken);

            return entity is null ? null : ToModel(entity);
        }

        /// <inheritdoc />
        public async Task StageAsync(
            StoredPendingChange change,
            CancellationToken cancellationToken = default)
        {
            // The account's row, tracked so the update below is one statement on the next
            // save. A row that is not there yet is inserted with the identifier the caller
            // supplied; a row that is there keeps the identifier it already had.
            var entity = await _context.PendingChanges
                .FirstOrDefaultAsync(row => row.UserId == change.UserId, cancellationToken);

            if (entity is null)
            {
                entity = new PendingChangeEntity(
                    change.Id == Guid.Empty ? Guid.NewGuid() : change.Id)
                {
                    UserId = change.UserId
                };

                _context.PendingChanges.Add(entity);
            }

            entity.Kind = change.Kind;
            entity.TargetValue = change.TargetValue;
            entity.NewPasswordHash = change.NewPasswordHash;
            entity.CodeHash = change.CodeHash;
            entity.Payload = change.Payload;
            entity.ExpiresAt = change.ExpiresAt;
        }

        /// <inheritdoc />
        public async Task RemoveAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.PendingChanges.FindAsync([id], cancellationToken);

            if (entity is not null)
            {
                _context.PendingChanges.Remove(entity);
            }
        }

        /// <summary>
        /// Maps one tracked row to the model the caller reads.
        /// </summary>
        private static StoredPendingChange ToModel(PendingChangeEntity change)
            => new()
            {
                Id = change.Id,
                UserId = change.UserId,
                Kind = change.Kind,
                TargetValue = change.TargetValue,
                NewPasswordHash = change.NewPasswordHash,
                CodeHash = change.CodeHash,
                Payload = change.Payload,
                ExpiresAt = change.ExpiresAt
            };
    }
}
