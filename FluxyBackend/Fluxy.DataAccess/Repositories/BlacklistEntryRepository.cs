using System.Linq.Expressions;
using Fluxy.Core.Blacklists.Commands;
using Fluxy.Core.Blacklists.Enums;
using Fluxy.Core.Blacklists.Models;
using Fluxy.Core.Blacklists.ReadModels;
using Fluxy.Core.Blacklists.Repositories;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IBlacklistEntryRepository"/>.
    /// </summary>
    /// <remarks>
    /// Every read is scoped to one list: the three screens are three filters over one table,
    /// and a query that could cross them would be one no screen asks for. Search covers the
    /// value and the note, because those are the two text columns a row holds - the note is the
    /// one place an operator explains an entry, and a search that ignored it would leave the
    /// explanation reachable only by scrolling.
    /// </remarks>
    internal sealed class BlacklistEntryRepository : IBlacklistEntryRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlacklistEntryRepository"/> class.
        /// </summary>
        /// <param name="context">Context the entry rows are read from and staged in.</param>
        public BlacklistEntryRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<BlacklistEntryPage> GetPageAsync(
            BlacklistType type,
            int page,
            int pageSize,
            string? search,
            BlacklistSortField sortBy,
            BlacklistSortOrder sortOrder,
            CancellationToken cancellationToken = default)
        {
            var query = _context.BlacklistEntries
                .AsNoTracking()
                .Where(entry => entry.Type == type);

            var needle = search?.Trim().ToLowerInvariant() ?? string.Empty;
            if (needle.Length > 0)
            {
                query = query.Where(entry =>
                    entry.Value.ToLower().Contains(needle)
                    || (entry.Note ?? string.Empty).ToLower().Contains(needle));
            }

            // Counted under the same predicate the page is cut from, and before the order is
            // applied: a total counted after Skip/Take would describe the page, not the list.
            var total = await query.CountAsync(cancellationToken);

            query = Order(query, sortBy, sortOrder);

            var rows = await query
                .Select(entry => new BlacklistEntryView
                {
                    Id = entry.Id,
                    Type = entry.Type,
                    Value = entry.Value,
                    BlockedAttempts = entry.BlockedAttempts,
                    Note = entry.Note,
                    CreatedAt = entry.CreatedAt
                })
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new BlacklistEntryPage
            {
                Items = rows,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public Task<BlacklistEntry?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => _context.BlacklistEntries
                .AsNoTracking()
                .Where(entry => entry.Id == id)
                .Select(entry => new BlacklistEntry
                {
                    Id = entry.Id,
                    Type = entry.Type,
                    Value = entry.Value,
                    BlockedAttempts = entry.BlockedAttempts,
                    Note = entry.Note,
                    CreatedAt = entry.CreatedAt,
                    UpdatedAt = entry.UpdatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);

        /// <inheritdoc />
        public Task<bool> IsValueTakenAsync(
            BlacklistType type,
            string value,
            Guid? exceptEntry,
            CancellationToken cancellationToken = default)
            => _context.BlacklistEntries
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.Type == type
                        && entry.Value == value
                        && (exceptEntry == null || entry.Id != exceptEntry),
                    cancellationToken);

        /// <inheritdoc />
        public Task<Guid> AddAsync(
            BlacklistType type,
            BlacklistEntryWrite entry,
            CancellationToken cancellationToken = default)
        {
            var entity = new BlacklistEntryEntity
            {
                Type = type,
                Value = entry.Value,

                // A new entry has blocked nothing yet. This is the only write the column ever
                // receives in this pass; the enforcement will add to it later.
                BlockedAttempts = 0,
                Note = entry.Note
            };

            _context.BlacklistEntries.Add(entity);

            return Task.FromResult(entity.Id);
        }

        /// <inheritdoc />
        public async Task<bool> UpdateAsync(
            Guid id,
            BlacklistEntryWrite entry,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.BlacklistEntries
                .FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

            if (entity is null)
            {
                return false;
            }

            // The counter, the type and the added date are left alone on purpose: they are
            // facts the entry already has, and a value change is not a new entry.
            entity.Value = entry.Value;
            entity.Note = entry.Note;

            return true;
        }

        /// <inheritdoc />
        public async Task<bool> RemoveAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.BlacklistEntries
                .FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

            if (entity is null)
            {
                return false;
            }

            _context.BlacklistEntries.Remove(entity);
            return true;
        }

        /// <inheritdoc />
        /// <remarks>
        /// One statement over the type's rows: the command names a set, and loading every row of
        /// a list an operator just decided to empty would be work the answer does not need. The
        /// delete is immediate, which is the documented behaviour of <c>ExecuteDelete</c> - there
        /// is nothing else staged around it that could roll it back.
        /// </remarks>
        public Task<int> RemoveAllAsync(
            BlacklistType type,
            CancellationToken cancellationToken = default)
            => _context.BlacklistEntries
                .Where(entry => entry.Type == type)
                .ExecuteDeleteAsync(cancellationToken);

        private static IQueryable<BlacklistEntryEntity> Order(
            IQueryable<BlacklistEntryEntity> query,
            BlacklistSortField field,
            BlacklistSortOrder order)
        {
            var descending = order is BlacklistSortOrder.Descending;

            return field switch
            {
                BlacklistSortField.BlockedAttempts => Chain(
                    query,
                    entry => entry.BlockedAttempts,
                    entry => entry.CreatedAt,
                    descending),

                BlacklistSortField.Value => Chain(
                    query, entry => entry.Value, entry => entry.CreatedAt, descending),

                // The default: newest added first. The identifier is the tie-break, because
                // two entries written in the same transaction share a stamp and without it two
                // pages could both show or both skip a row.
                _ => Chain(
                    query, entry => entry.CreatedAt, entry => entry.Id, descending)
            };
        }

        private static IOrderedQueryable<TEntity> Chain<TEntity, TKey1, TKey2>(
            IQueryable<TEntity> source,
            Expression<Func<TEntity, TKey1>> primary,
            Expression<Func<TEntity, TKey2>> tieBreak,
            bool descending)
        {
            var ordered = descending
                ? source.OrderByDescending(primary)
                : source.OrderBy(primary);

            return descending
                ? ordered.ThenByDescending(tieBreak)
                : ordered.ThenBy(tieBreak);
        }
    }
}
