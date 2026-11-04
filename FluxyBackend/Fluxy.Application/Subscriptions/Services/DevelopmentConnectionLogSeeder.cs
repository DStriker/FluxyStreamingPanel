using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.ReadModels;
using Fluxy.Core.Streams.Repositories;
using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Interfaces;
using Fluxy.Core.Subscriptions.ReadModels;
using Fluxy.Core.Subscriptions.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fluxy.Application.Subscriptions.Services
{
    /// <summary>
    /// Fills an empty connection log with a handful of trial rows, in Development only.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A development convenience, never a data source.</b> The log has no capture path yet -
    /// the process that relays a stream will own it - so a fresh installation has a screen with
    /// nothing to draw and no way to exercise its filters, its flag images or its click-through.
    /// This seeder exists for exactly that gap, and every guard below is about not becoming
    /// something more.
    /// </para>
    /// <para>
    /// <b>It never invents a row it can point at.</b> The subscription is a real foreign key and
    /// the stream title is resolved from a real stream row, so prerequisites are read through
    /// the Core repositories and the inserted rows name what they found. Without at least one
    /// subscription, one video stream and one audio stream there is nothing a connection could
    /// be about, and the seeder says so once and writes nothing.
    /// </para>
    /// <para>
    /// <b>Idempotent twice over.</b> Outside Development it is a no-op, and inside Development it
    /// refuses a table that already holds any row: trial rows dropped on top of a log somebody
    /// has actually used would be invented evidence, and "the table is empty" is the only state
    /// in which nothing is being overwritten.
    /// </para>
    /// </remarks>
    public sealed class DevelopmentConnectionLogSeeder : IConnectionLogSeeder
    {
        /// <summary>How many subscriptions the seeder looks at when it builds its rows.</summary>
        private const int TrialSubscriptions = 3;

        /// <summary>How many streams of one medium it looks at.</summary>
        private const int TrialStreams = 3;

        /// <summary>Length of a finished trial session.</summary>
        private static readonly TimeSpan SessionLength = TimeSpan.FromMinutes(42);

        /// <summary>
        /// The network facts the trial rows carry, so the country and provider filters have more
        /// than one value to narrow by and the click-through has somewhere to go.
        /// </summary>
        private static readonly (string Ip, string Country, string Isp, string Agent)[] Networks =
        [
            ("203.0.113.9", "DE", "Deutsche Beispielfunk GmbH", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"),
            ("198.51.100.24", "FR", "Reseau Exemple SA", "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_5)"),
            ("192.0.2.77", "NL", "Voorbeeld Netwerk B.V.", "VLC/3.0.20 LibVLC/3.0.20")
        ];

        private readonly IHostEnvironment _environment;
        private readonly IConnectionLogRepository _logs;
        private readonly ISubscriptionRepository _subscriptions;
        private readonly IVideoStreamRepository _videos;
        private readonly IAudioStreamRepository _audios;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _time;
        private readonly ILogger<DevelopmentConnectionLogSeeder> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DevelopmentConnectionLogSeeder"/> class.
        /// </summary>
        /// <param name="environment">Host environment the Development gate reads.</param>
        /// <param name="logs">Repository the trial rows are staged through.</param>
        /// <param name="subscriptions">Repository the subscription prerequisites are read from.</param>
        /// <param name="videos">Repository the video stream prerequisites are read from.</param>
        /// <param name="audios">Repository the audio stream prerequisites are read from.</param>
        /// <param name="unitOfWork">Commits the staged rows.</param>
        /// <param name="time">Clock the trial timestamps are derived from.</param>
        /// <param name="logger">Logger the skip or the insert is reported to.</param>
        public DevelopmentConnectionLogSeeder(
            IHostEnvironment environment,
            IConnectionLogRepository logs,
            ISubscriptionRepository subscriptions,
            IVideoStreamRepository videos,
            IAudioStreamRepository audios,
            IUnitOfWork unitOfWork,
            TimeProvider time,
            ILogger<DevelopmentConnectionLogSeeder> logger)
        {
            _environment = environment;
            _logs = logs;
            _subscriptions = subscriptions;
            _videos = videos;
            _audios = audios;
            _unitOfWork = unitOfWork;
            _time = time;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            if (!_environment.IsDevelopment())
            {
                _logger.LogInformation(
                    "Connection log trial rows skipped: the host environment is {Environment}, not Development.",
                    _environment.EnvironmentName);

                return;
            }

            if (await _logs.CountAsync(cancellationToken) > 0)
            {
                _logger.LogInformation(
                    "Connection log trial rows skipped: the log already holds rows.");

                return;
            }

            // The three prerequisites, read through Core interfaces only - a seeder in this layer
            // cannot name a DbContext, and the rows it writes must point at real records.
            var subscriptions = await _subscriptions.GetPageAsync(
                1,
                TrialSubscriptions,
                null,
                null,
                null,
                null,
                SubscriptionSortField.CreatedAt,
                SubscriptionSortOrder.Ascending,
                cancellationToken);

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

            var audios = await _audios.GetPageAsync(
                1,
                TrialStreams,
                null,
                null,
                null,
                null,
                AudioStreamSortField.Name,
                AudioStreamSortOrder.Ascending,
                cancellationToken);

            if (subscriptions.Items.Count == 0
                || videos.Items.Count == 0
                || audios.Items.Count == 0)
            {
                _logger.LogInformation(
                    "Connection log trial rows skipped: the installation needs at least one subscription, one video stream and one audio stream (found {Subscriptions} subscription(s), {Videos} video stream(s), {Audios} audio stream(s)).",
                    subscriptions.Total,
                    videos.Total,
                    audios.Total);

                return;
            }

            var rows = BuildRows(subscriptions.Items, videos.Items, audios.Items);

            await _logs.AddRangeAsync(rows, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Inserted {Count} trial connection log row(s) for Development.",
                rows.Count);
        }

        /// <summary>
        /// Builds the trial rows out of the records that were found.
        /// </summary>
        /// <remarks>
        /// Every row is finished except one video and one audio session, which are left open with a
        /// reported process identifier - the two states the screen distinguishes, and the reason a
        /// live session needs a pid at all. The network facts rotate through
        /// <see cref="Networks"/>, so the country and provider filters always have at least two
        /// values to tell apart.
        /// </remarks>
        private IReadOnlyList<NewConnectionLog> BuildRows(
            IReadOnlyList<SubscriptionListItem> subscriptions,
            IReadOnlyList<VideoStreamListItem> videos,
            IReadOnlyList<AudioStreamListItem> audios)
        {
            var now = _time.GetUtcNow();
            var rows = new List<NewConnectionLog>();
            var network = 0;

            for (var index = 0; index < videos.Count; index++)
            {
                var video = videos[index];
                var subscription = subscriptions[index % subscriptions.Count];
                var facts = Networks[network++ % Networks.Length];

                // Each video stream gets one finished session, so the activity screen has an end
                // time and a duration to draw.
                rows.Add(Build(
                    subscription.Id,
                    ConnectionStreamKind.VideoStream,
                    video.Id,
                    facts,
                    now.AddHours(-(index + 2)),
                    SessionLength,
                    broadcastPid: null));

                // The first of them is also the one still running: an open row with a pid is the
                // live screen's whole content.
                if (index == 0)
                {
                    rows.Add(Build(
                        subscription.Id,
                        ConnectionStreamKind.VideoStream,
                        video.Id,
                        facts,
                        now.AddMinutes(-12),
                        endedAfter: null,
                        broadcastPid: 4821));
                }
            }

            for (var index = 0; index < audios.Count; index++)
            {
                var audio = audios[index];
                var subscription = subscriptions[index % subscriptions.Count];
                var facts = Networks[network++ % Networks.Length];

                rows.Add(Build(
                    subscription.Id,
                    ConnectionStreamKind.AudioStream,
                    audio.Id,
                    facts,
                    now.AddHours(-(index + 1)),
                    SessionLength,
                    broadcastPid: null));
            }

            // One live audio session as well, so the live screen is not video-only.
            if (audios.Count > 0 && subscriptions.Count > 0)
            {
                rows.Add(Build(
                    subscriptions[0].Id,
                    ConnectionStreamKind.AudioStream,
                    audios[0].Id,
                    Networks[network % Networks.Length],
                    now.AddMinutes(-4),
                    endedAfter: null,
                    broadcastPid: 5150));
            }

            return rows;
        }

        /// <summary>Builds one row, deriving its end from its start when it is a finished session.</summary>
        private static NewConnectionLog Build(
            Guid subscriptionId,
            ConnectionStreamKind kind,
            Guid streamId,
            (string Ip, string Country, string Isp, string Agent) facts,
            DateTimeOffset startedAt,
            TimeSpan? endedAfter,
            int? broadcastPid) => new()
            {
                SubscriptionId = subscriptionId,
                StreamKind = kind,
                StreamId = streamId,
                Ip = facts.Ip,
                UserAgent = facts.Agent,
                CountryCode = facts.Country,
                Isp = facts.Isp,
                StartedAt = startedAt,
                EndedAt = endedAfter is { } length ? startedAt + length : null,
                BroadcastPid = broadcastPid
            };
    }
}
