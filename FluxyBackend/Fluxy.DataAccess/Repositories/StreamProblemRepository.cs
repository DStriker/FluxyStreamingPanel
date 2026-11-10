using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.ReadModels;
using Fluxy.Core.Streams.Repositories;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IStreamProblemRepository"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The page is one query over the journal.</b> Every filter and the order are columns of
    /// <c>stream_problems</c> itself, so <c>total</c> and the page are cut from the same predicate
    /// and the same index can answer both. The read that fills the stream's name joins
    /// <c>video_streams</c> for the page only, after the count.
    /// </para>
    /// <para>
    /// <b><c>total</c> is counted under the same predicate the page is cut from, before
    /// <c>Skip</c>/<c>Take</c>.</b> Counting after paging would describe the page instead of the
    /// list and make the pager lie.
    /// </para>
    /// <para>
    /// <b>The stream name is resolved by a join on read, never stored.</b> The row carries a real
    /// foreign key, so the page joins <c>video_streams</c> once to fill <c>StreamName</c>; the
    /// relation is one stream per problem from the row side, so the join multiplies no row and
    /// <c>total</c> is still counted on <c>stream_problems</c> alone. No name snapshot exists on
    /// the table, which is what keeps the column showing the stream's current name.
    /// </para>
    /// </remarks>
    internal sealed class StreamProblemRepository : IStreamProblemRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamProblemRepository"/> class.
        /// </summary>
        /// <param name="context">Context the rows are read from and staged in.</param>
        public StreamProblemRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<StreamProblemPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? streamId,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            StreamProblemSortField sortBy,
            StreamProblemSortOrder sortOrder,
            CancellationToken cancellationToken = default)
        {
            var query = _context.StreamProblems.AsNoTracking();

            var needle = search?.Trim().ToLowerInvariant() ?? string.Empty;
            if (needle.Length > 0)
            {
                // The message is the only stored free text a support call quotes. Lowering both
                // sides is what the other tables do; the comparison is the database's own
                // collation. The stream's name is resolved by a join and is deliberately not part
                // of this predicate: a count built over a join is a different query shape, and the
                // name is not stored here.
                query = query.Where(problem => problem.ErrorMessage.ToLower().Contains(needle));
            }

            if (streamId is { } stream)
            {
                query = query.Where(problem => problem.StreamId == stream);
            }

            // Both bounds are inclusive and either one may be absent. Inclusive because the control
            // names whole days: a `to` bound read as exclusive would drop the rows written during
            // the very day the operator picked. A later `from` than `to` is not a conflict - it is
            // a predicate nothing satisfies, and the page is empty.
            if (dateFrom is { } from)
            {
                query = query.Where(problem => problem.CreatedAt >= from);
            }

            if (dateTo is { } to)
            {
                query = query.Where(problem => problem.CreatedAt <= to);
            }

            // Counted under the same predicate the page is cut from, and before the order is
            // applied: a total counted after Skip/Take would describe the page, not the list. The
            // count names stream_problems alone - the join below is one stream per problem, so it
            // could not change the answer.
            var total = await query.CountAsync(cancellationToken);

            // The name is resolved through the join rather than stored: `video_streams` is the
            // stream's current name, and a problem row carries a real foreign key to it. From the
            // row side the relation is 1:1, so this join neither duplicates a row nor widens the
            // predicate the page is cut from.
            var named = query.Join(
                _context.VideoStreams.AsNoTracking(),
                problem => problem.StreamId,
                stream => stream.Id,
                (problem, stream) => new
                {
                    problem.Id,
                    problem.StreamId,
                    StreamName = stream.Name,
                    problem.ErrorMessage,
                    problem.CreatedAt
                });

            // CreatedAt is the only sortable column, and both orders end with the identifier: two
            // problems can be reported in the same instant, and without a tie-break the database is
            // free to answer a different order on every read, which would make a page turn show a
            // row twice or skip one. `sortBy` needs no branch here because
            // StreamProblemSortField has exactly one member.
            var descending = sortOrder == StreamProblemSortOrder.Descending;

            var ordered = descending
                ? named.OrderByDescending(row => row.CreatedAt)
                : named.OrderBy(row => row.CreatedAt);

            var orderedWithTieBreak = descending
                ? ordered.ThenByDescending(row => row.Id)
                : ordered.ThenBy(row => row.Id);

            var rows = await orderedWithTieBreak
                // Arithmetic in long: page comes from a query string, so (page - 1) * pageSize can
                // overflow int and reach EF Core as a negative Skip.
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var items = rows
                .Select(row => new StreamProblemListItem
                {
                    Id = row.Id,
                    StreamId = row.StreamId,
                    StreamName = row.StreamName,
                    ErrorMessage = row.ErrorMessage,
                    CreatedAt = row.CreatedAt
                })
                .ToList();

            return new StreamProblemPage
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public async Task AddAsync(
            NewStreamProblem problem,
            CancellationToken cancellationToken = default)
        {
            // Staged, not saved: the caller owns the single save - the rule every repository here
            // follows. The identifier is generated by the row and the audit columns are the
            // context's to stamp, exactly as they are for every other entity.
            var row = new StreamProblemEntity
            {
                StreamId = problem.StreamId,
                ErrorMessage = problem.ErrorMessage
            };

            await _context.StreamProblems.AddAsync(row, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<long> ClearAllAsync(CancellationToken cancellationToken = default)
        {
            // Immediate, and deliberately outside the unit of work: one statement whose only
            // outcome is "the table is empty". There is nothing to roll back and nothing for a
            // save to commit. Its remarks in the interface say the same at length.
            //
            // No predicate: the button says "clear the journal", and a delete that obeyed a filter
            // the caller happened not to send would wipe a different set from the one it named.
            return await _context.StreamProblems.ExecuteDeleteAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task<int> CountAsync(CancellationToken cancellationToken = default)
            // Unfiltered by design: the seeder asks "has anything ever been reported here", not
            // "does this screen have rows".
            => _context.StreamProblems.CountAsync(cancellationToken);
    }
}
