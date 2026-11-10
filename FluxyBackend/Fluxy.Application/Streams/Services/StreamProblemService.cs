using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Interfaces;
using Fluxy.Core.Streams.ReadModels;
using Fluxy.Core.Streams.Repositories;
using Fluxy.Core.Streams.Utils;

namespace Fluxy.Application.Streams.Services
{
    /// <summary>
    /// Records the problems a stream producer reports, reads them a page at a time, and clears the
    /// journal on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The write boundary is the point of this type.</b> The repository owns the SQL shape and
    /// the transport owns the bounds it refuses before a call; what is left is the three decisions
    /// about the message itself - it is trimmed, a blank one is refused, and an oversized one is
    /// cut to its column. Those three belong together, because a producer's message is not a form
    /// field a client typed and the row must stay readable.
    /// </para>
    /// <para>
    /// <b>A failed write is not swallowed.</b> Unlike <c>IUserLogService</c>, which records
    /// something that has already happened and must never change the outcome it describes, the
    /// row here is the whole operation: a caller that cannot store its problem has to know. Retry,
    /// backoff and what to do when the journal is unreachable are the future producer's policy,
    /// not this service's.
    /// </para>
    /// </remarks>
    public sealed class StreamProblemService : IStreamProblemService
    {
        private readonly IStreamProblemRepository _problems;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamProblemService"/> class.
        /// </summary>
        /// <param name="problems">Repository that stages the row and answers the reads.</param>
        /// <param name="unitOfWork">Unit of work the record commit belongs to.</param>
        public StreamProblemService(
            IStreamProblemRepository problems,
            IUnitOfWork unitOfWork)
        {
            _problems = problems;
            _unitOfWork = unitOfWork;
        }

        /// <inheritdoc />
        public Task<StreamProblemPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? streamId,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            StreamProblemSortField sortBy,
            StreamProblemSortOrder sortOrder,
            CancellationToken cancellationToken = default)
            => _problems.GetPageAsync(
                page,
                pageSize,
                search,
                streamId,
                dateFrom,
                dateTo,
                sortBy,
                sortOrder,
                cancellationToken);

        /// <inheritdoc />
        public Task<long> ClearAllAsync(CancellationToken cancellationToken = default)
            // No filter is passed because there is none to pass: the button says "clear the
            // journal", and a delete that obeyed the screen's filters would remove a different
            // set from the one the caller could see it naming. An already-empty table answers
            // zero rather than a refusal - the second press of a button that already did its job
            // is not an error.
            => _problems.ClearAllAsync(cancellationToken);

        /// <inheritdoc />
        public async Task RecordAsync(
            NewStreamProblem problem,
            CancellationToken cancellationToken = default)
        {
            var message = problem.ErrorMessage?.Trim();

            if (string.IsNullOrEmpty(message))
            {
                // Refused at the boundary rather than stored as an empty row: an empty message is
                // evidence of nothing, and a silent insert would hide the producer's bug instead
                // of surfacing it.
                throw new ArgumentException(
                    "A stream problem must carry a message.",
                    nameof(problem));
            }

            // Cut rather than refused: the producer's sentence is what it is, and losing the whole
            // record over its length would lose the fact that the stream failed at all. The same
            // trade the user log makes for the values it snapshots.
            var stored = problem with
            {
                ErrorMessage = message.Length <= StreamProblemListLimits.ErrorMessageMaxLength
                    ? message
                    : message[..StreamProblemListLimits.ErrorMessageMaxLength]
            };

            await _problems.AddAsync(stored, cancellationToken);

            // Its own save: the producer hands one problem over at a time, and the row is the
            // whole operation - there is no business change for it to share a transaction with.
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
