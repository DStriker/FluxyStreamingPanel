using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Interfaces;
using Fluxy.Core.Streams.ReadModels;
using Fluxy.Core.Streams.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fluxy.Application.Streams.Services
{
    /// <summary>
    /// Fills an empty stream problem journal with a dozen trial rows, in Development only.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A development convenience, never a data source.</b> The producer that will record real
    /// problems does not exist yet, so a fresh installation has a screen with nothing to draw and
    /// no way to exercise its filters, its date range or its click-through. This seeder exists for
    /// exactly that gap, and every guard below is about not becoming something more.
    /// </para>
    /// <para>
    /// <b>It never invents a stream.</b> <c>stream_id</c> is a real foreign key, so the streams are
    /// read through the Core repository and the rows name what they found. Without at least one
    /// stream there is nothing a problem could be about, and the seeder says so once and writes
    /// nothing - the same answer the connection log's seeder gives its three prerequisites.
    /// </para>
    /// <para>
    /// <b>Idempotent twice over.</b> Outside Development it is a no-op - which includes the Testing
    /// host the integration suite runs on - and inside Development it refuses a table that already
    /// holds any row: trial rows dropped on top of a journal somebody has actually used would be
    /// invented evidence, and "the table is empty" is the only state in which nothing is being
    /// overwritten.
    /// </para>
    /// <para>
    /// <b>The trial rows share one creation instant.</b> <c>created_at</c> is stamped by the
    /// context, never by the entity, so every row written in this save carries the same moment -
    /// which is exactly the case the <c>(created_at DESC, id DESC)</c> tie-break exists for. The
    /// variety the screen needs is in the streams and the messages, not in the order.
    /// </para>
    /// </remarks>
    public sealed class DevelopmentStreamProblemSeeder : IStreamProblemSeeder
    {
        /// <summary>How many streams the seeder looks at when it builds its rows.</summary>
        private const int TrialStreams = 3;

        /// <summary>
        /// The messages the trial journal carries.
        /// </summary>
        /// <remarks>
        /// The mix is the point: short sentences and long ones, and failures of different kinds - a
        /// refused connection, a timeout, a codec and a missing source - so the table draws more
        /// than one kind of line, the long one wraps, and the search has something to narrow. The
        /// rows rotate through the streams that were found, so when at least two streams were
        /// found the <c>streamId</c> filter has more than one value to tell apart.
        /// </remarks>
        private static readonly string[] TrialMessages =
        [
            "The upstream server refused the connection: HTTP 403.",
            "The source did not answer within 15 seconds; the relay gave up.",
            "The audio codec reported by the source is not supported by this installation.",
            "The playlist the stream plays is empty: no file or on-demand item is attached.",
            "The relaying process exited with code 1 while opening the transport stream.",
            "The source address could not be resolved: the host name no longer exists.",
            "The stream is configured as an archive channel but carries no archive length.",
            "The programme guide source named by the stream returned a malformed file.",
            "The upstream server closed the connection after 4 minutes of playback.",
            "The restream proxy refused the request: authentication is required.",
            "The on-demand item this stream plays is no longer available in the catalogue.",
            "The input buffer overran while the relay was switching to the next source; "
            + "the channel was restarted and the transport stream is being rebuilt from the "
            + "beginning, which is why this line is the longest one the trial journal carries."
        ];

        private readonly IHostEnvironment _environment;
        private readonly IStreamProblemRepository _problems;
        private readonly IVideoStreamRepository _videos;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DevelopmentStreamProblemSeeder> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DevelopmentStreamProblemSeeder"/> class.
        /// </summary>
        /// <param name="environment">Host environment the Development gate reads.</param>
        /// <param name="problems">Repository the trial rows are staged through.</param>
        /// <param name="videos">Repository the stream prerequisites are read from.</param>
        /// <param name="unitOfWork">Commits the staged rows.</param>
        /// <param name="logger">Logger the skip or the insert is reported to.</param>
        public DevelopmentStreamProblemSeeder(
            IHostEnvironment environment,
            IStreamProblemRepository problems,
            IVideoStreamRepository videos,
            IUnitOfWork unitOfWork,
            ILogger<DevelopmentStreamProblemSeeder> logger)
        {
            _environment = environment;
            _problems = problems;
            _videos = videos;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            if (!_environment.IsDevelopment())
            {
                // The Testing host is not Development, so the integration suite sees a no-op here
                // rather than a second seeder its fixtures would have to undo.
                _logger.LogInformation(
                    "Stream problem trial rows skipped: the host environment is {Environment}, not Development.",
                    _environment.EnvironmentName);

                return;
            }

            if (await _problems.CountAsync(cancellationToken) > 0)
            {
                _logger.LogInformation(
                    "Stream problem trial rows skipped: the journal already holds rows.");

                return;
            }

            // The prerequisite, read through the Core interface only - a seeder in this layer
            // cannot name a DbContext, and the rows it writes must point at real streams.
            var videos = await _videos.GetPageAsync(
                1,
                TrialStreams,
                null,
                null,
                null,
                null,
                VideoStreamSortField.Name,
                VideoStreamSortOrder.Ascending,
                cancellationToken);

            if (videos.Items.Count == 0)
            {
                _logger.LogInformation(
                    "Stream problem trial rows skipped: the installation needs at least one video stream to report a problem about.");

                return;
            }

            var rows = BuildRows(videos.Items);

            // One save for the whole set, so the dozen trial rows arrive together or not at all.
            foreach (var row in rows)
            {
                await _problems.AddAsync(row, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Inserted {Count} trial stream problem row(s) for Development.",
                rows.Count);
        }

        /// <summary>
        /// Builds the trial rows out of the streams that were found.
        /// </summary>
        /// <remarks>
        /// Each message names a stream by rotating through the page of streams, so every stream the
        /// seeder found carries at least one problem and, when it found at least two streams, the
        /// <c>streamId</c> filter has more than one value to tell apart.
        /// </remarks>
        private static IReadOnlyList<NewStreamProblem> BuildRows(
            IReadOnlyList<VideoStreamListItem> streams)
            => TrialMessages
                .Select((message, index) => new NewStreamProblem
                {
                    StreamId = streams[index % streams.Count].Id,
                    ErrorMessage = message
                })
                .ToList();
    }
}
