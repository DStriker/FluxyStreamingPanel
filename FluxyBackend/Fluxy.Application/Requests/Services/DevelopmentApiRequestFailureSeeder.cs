using Fluxy.Core.Content.Enums;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Requests.Commands;
using Fluxy.Core.Requests.Enums;
using Fluxy.Core.Requests.Interfaces;
using Fluxy.Core.Requests.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fluxy.Application.Requests.Services
{
    /// <summary>
    /// Fills an empty API request failure log with a dozen trial rows, in Development only.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A development convenience, never a data source.</b> The log has no capture path yet - the
    /// future public API will own it - so a fresh installation has a screen with nothing to draw and
    /// no way to exercise its filters, its reason select or its click-through. This seeder exists
    /// for exactly that gap, and every guard below is about not becoming something more.
    /// </para>
    /// <para>
    /// <b>It invents nothing the schema has to point at.</b> Every column is a snapshot and none is
    /// a foreign key, so the rows can be built without reading a prerequisite first - unlike the
    /// connection log's seeder, whose rows must name real subscriptions and streams. The content
    /// identifiers are freshly generated opaque values, which is exactly what the column is for.
    /// </para>
    /// <para>
    /// <b>Idempotent twice over.</b> Outside Development it is a no-op - which includes the Testing
    /// host the integration suite runs on - and inside Development it refuses a table that already
    /// holds any row: trial rows dropped on top of a log somebody has actually used would be
    /// invented evidence, and "the table is empty" is the only state in which nothing is being
    /// overwritten.
    /// </para>
    /// </remarks>
    public sealed class DevelopmentApiRequestFailureSeeder : IApiRequestFailureSeeder
    {
        /// <summary>
        /// The refusals the trial log carries: every reason, several providers and countries, and
        /// some rows with no subscription data and no content snapshot.
        /// </summary>
        /// <remarks>
        /// The mix is the point. All six reasons must appear, because the reason select has six
        /// entries and an empty one would never be exercised; the content group must be present
        /// and absent, because the table has to draw both states; and the network facts must repeat
        /// across rows, because the click-to-filter of the shared cell is meaningless with one
        /// value.
        /// </remarks>
        private static readonly (
            string? SubscriptionData,
            ContentType? ContentKind,
            ApiRequestFailureReason Reason,
            string Ip,
            string? CountryCode,
            string? Isp,
            string Description)[] TrialFailures =
        [
            (
                "sub-7f3a91c2",
                ContentType.VideoStream,
                ApiRequestFailureReason.UnknownId,
                "203.0.113.9",
                "DE",
                "Deutsche Beispielfunk GmbH",
                "The request named a stream identifier this installation does not know."),
            (
                "sub-7f3a91c2",
                ContentType.VideoStream,
                ApiRequestFailureReason.TooManyConnections,
                "203.0.113.9",
                "DE",
                "Deutsche Beispielfunk GmbH",
                "The subscription already held as many connections as its plan allows."),
            (
                "sub-11c4e0b8",
                ContentType.AudioStream,
                ApiRequestFailureReason.AuthFailed,
                "198.51.100.24",
                "FR",
                "Reseau Exemple SA",
                "The request named no valid access parameter."),
            (
                null,
                null,
                ApiRequestFailureReason.AuthFailed,
                "198.51.100.24",
                "FR",
                "Reseau Exemple SA",
                "The request named no access parameter at all."),
            (
                "sub-11c4e0b8",
                ContentType.Series,
                ApiRequestFailureReason.SubscriptionExpired,
                "192.0.2.77",
                "NL",
                "Voorbeeld Netwerk B.V.",
                "The subscription behind the request expired on 2026-04-30."),
            (
                "sub-3ac90d15",
                ContentType.VideoOnDemand,
                ApiRequestFailureReason.UserBlocked,
                "192.0.2.77",
                "NL",
                "Voorbeeld Netwerk B.V.",
                "The account behind the subscription was blocked by an administrator."),
            (
                "sub-3ac90d15",
                null,
                ApiRequestFailureReason.GeoIpViolation,
                "203.0.113.9",
                "DE",
                "Deutsche Beispielfunk GmbH",
                "The request came from an address the login guard does not allow."),
            (
                "sub-9d2f64aa",
                ContentType.VideoStream,
                ApiRequestFailureReason.TooManyConnections,
                "198.51.100.24",
                "FR",
                "Reseau Exemple SA",
                "The subscription already held its single allowed connection."),
            (
                null,
                ContentType.AudioStream,
                ApiRequestFailureReason.UnknownId,
                "203.0.113.9",
                null,
                null,
                "The request named an audio stream identifier this installation does not know."),
            (
                "sub-9d2f64aa",
                null,
                ApiRequestFailureReason.AuthFailed,
                "198.51.100.24",
                "FR",
                null,
                "The presented access parameter named no subscription."),
            (
                "sub-7f3a91c2",
                ContentType.Series,
                ApiRequestFailureReason.SubscriptionExpired,
                "192.0.2.77",
                "NL",
                "Voorbeeld Netwerk B.V.",
                "The subscription behind the request had already expired."),
            (
                null,
                null,
                ApiRequestFailureReason.GeoIpViolation,
                "192.0.2.77",
                "NL",
                "Voorbeeld Netwerk B.V.",
                "The request came from an address outside the account's allow lists.")
        ];

        private readonly IHostEnvironment _environment;
        private readonly IApiRequestFailureRepository _failures;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DevelopmentApiRequestFailureSeeder> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DevelopmentApiRequestFailureSeeder"/> class.
        /// </summary>
        /// <param name="environment">Host environment the Development gate reads.</param>
        /// <param name="failures">Repository the trial rows are staged through.</param>
        /// <param name="unitOfWork">Commits the staged rows.</param>
        /// <param name="logger">Logger the skip or the insert is reported to.</param>
        public DevelopmentApiRequestFailureSeeder(
            IHostEnvironment environment,
            IApiRequestFailureRepository failures,
            IUnitOfWork unitOfWork,
            ILogger<DevelopmentApiRequestFailureSeeder> logger)
        {
            _environment = environment;
            _failures = failures;
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
                    "API request failure trial rows skipped: the host environment is {Environment}, not Development.",
                    _environment.EnvironmentName);

                return;
            }

            if (await _failures.CountAsync(cancellationToken) > 0)
            {
                _logger.LogInformation(
                    "API request failure trial rows skipped: the log already holds rows.");

                return;
            }

            var rows = BuildRows();

            await _failures.AddRangeAsync(rows, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Inserted {Count} trial API request failure row(s) for Development.",
                rows.Count);
        }

        /// <summary>
        /// Builds the trial rows out of the facts above.
        /// </summary>
        /// <remarks>
        /// The identifier of a content snapshot is generated per row, because the column is opaque
        /// and the seed must not pretend to name anything a screen could resolve. The audit
        /// timestamps are the context's, so every trial row carries the same instant - which is
        /// exactly the case the <c>(created_at, id)</c> tie-break exists for.
        /// </remarks>
        private static IReadOnlyList<NewApiRequestFailure> BuildRows() =>
            TrialFailures
                .Select(failure => new NewApiRequestFailure
                {
                    SubscriptionData = failure.SubscriptionData,
                    ContentKind = failure.ContentKind,
                    ContentId = failure.ContentKind is null ? null : Guid.NewGuid(),
                    ContentName = TrialContentName(failure.ContentKind),
                    Reason = failure.Reason,
                    Ip = failure.Ip,
                    Isp = failure.Isp,
                    CountryCode = failure.CountryCode,
                    Description = failure.Description
                })
                .ToList();

        /// <summary>The snapshot name a trial row carries for a kind of content.</summary>
        /// <param name="kind">Kind of content the row names, or null.</param>
        private static string? TrialContentName(ContentType? kind) => kind switch
        {
            ContentType.VideoStream => "Example Channel HD",
            ContentType.AudioStream => "Example Radio",
            ContentType.VideoOnDemand => "Example Film",
            ContentType.Series => "Example Series",
            _ => null
        };
    }
}
