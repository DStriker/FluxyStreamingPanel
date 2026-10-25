using System.Globalization;
using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Series;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Series.Commands;
using Fluxy.Core.Series.Enums;
using Fluxy.Core.Series.Interfaces;
using Fluxy.Core.Series.Models;
using Fluxy.Core.Series.Outcomes;
using Fluxy.Core.Series.ReadModels;
using Fluxy.Core.Series.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Content
{
    /// <summary>
    /// The episodes of one series: one page of the table, one episode in full, and the writes
    /// that create, change and delete them.
    /// </summary>
    /// <remarks>
    /// Every route is nested under the series, because an episode has no meaning outside the
    /// series it belongs to. The series is checked before any episode work, so a request under
    /// a missing series is answered as <c>series_not_found</c> rather than as an episode of a
    /// series nobody has.
    /// </remarks>
    [ApiController]
    [Route("admin/series/{seriesId:guid}/episodes")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class SeriesEpisodesController : ApiControllerBase
    {
        /// <summary>Sentence a state that names no member gets.</summary>
        private const string StatusHint = "The status must be either active or inactive.";

        /// <summary>Sentence a delivery kind that names no member gets.</summary>
        private const string TypeHint = "The type must be either relay or redirect.";

        /// <summary>Sentence a sort field that names nothing gets.</summary>
        private const string SortByHint =
            "The sort field must be one of: season, episode, name, status, type, " +
            "releasedate, rating.";

        /// <summary>Sentence a direction that names nothing gets.</summary>
        private const string SortOrderHint = "The sort order must be either asc or desc.";

        /// <summary>Sentence a season number that is not one gets.</summary>
        private const string SeasonHint = "The season number must be between 1 and 999.";

        private readonly IEpisodeService _episodes;
        private readonly ISeriesService _series;

        /// <summary>
        /// Initializes a new instance of the <see cref="SeriesEpisodesController"/> class.
        /// </summary>
        /// <param name="episodes">Service that reads and changes the episodes.</param>
        /// <param name="series">Service used to refuse a request under a missing series.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the changes worth remembering are reported to.</param>
        public SeriesEpisodesController(
            IEpisodeService episodes,
            ISeriesService series,
            IAntiforgery antiforgery,
            ILogger<SeriesEpisodesController> logger)
            : base(antiforgery, logger)
        {
            _episodes = episodes;
            _series = series;
        }

        /// <summary>Reports one page of the episode table of one series.</summary>
        [HttpGet]
        [Authorize(Policy = AuthenticationExtensions.ViewSeriesPolicy)]
        [ProducesResponseType<AdminEpisodePageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEpisodes(
            [FromRoute] Guid seriesId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = EpisodeListLimits.DefaultPageSize,
            [FromQuery] string? search = null,
            [FromQuery] string? seasonNumber = null,
            [FromQuery] string? status = null,
            [FromQuery] string? type = null,
            [FromQuery] string? sortBy = "season",
            [FromQuery] string? sortOrder = "asc",
            CancellationToken cancellationToken = default)
        {
            NoStore();

            if (await MissingSeriesAsync(seriesId, cancellationToken) is { } missing)
            {
                return missing;
            }

            var field = SeriesTextParser.ParseEpisodeSortField(sortBy);
            var order = SortOrderParser.Parse(
                sortOrder,
                EpisodeSortOrder.Ascending,
                EpisodeSortOrder.Descending);
            var wantedStatus = SeriesTextParser.ParseEpisodeStatusOrNull(status);
            var wantedType = SeriesTextParser.ParseEpisodeTypeOrNull(type);

            int? wantedSeason = null;
            var seasonOk = true;

            if (!string.IsNullOrWhiteSpace(seasonNumber))
            {
                if (int.TryParse(seasonNumber, out var parsedSeason)
                    && parsedSeason is >= SeriesLimits.SeasonNumberMin
                        and <= SeriesLimits.SeasonNumberMax)
                {
                    wantedSeason = parsedSeason;
                }
                else
                {
                    seasonOk = false;
                }
            }

            var statusOk = wantedStatus is not null || string.IsNullOrWhiteSpace(status);
            var typeOk = wantedType is not null || string.IsNullOrWhiteSpace(type);

            if (page < 1
                || pageSize is < 1 or > EpisodeListLimits.MaxPageSize
                || (search?.Length ?? 0) > EpisodeListLimits.MaxSearchLength
                || field is null
                || order is null
                || !seasonOk
                || !statusOk
                || !typeOk)
            {
                return ApiResults.ValidationFailed(ListErrors(
                    page,
                    pageSize,
                    search,
                    field,
                    order,
                    seasonOk,
                    statusOk,
                    typeOk));
            }

            var list = await _episodes.GetPageAsync(
                seriesId,
                page,
                pageSize,
                search,
                wantedSeason,
                wantedStatus,
                wantedType,
                field.Value,
                order.Value,
                cancellationToken);

            return Ok(new AdminEpisodePageResponse
            {
                Items = list.Items.Select(ToItem).ToList(),
                Total = list.Total,
                Page = list.Page,
                PageSize = list.PageSize
            });
        }

        /// <summary>Reports one episode in full, for the edit form.</summary>
        [HttpGet("{episodeId:guid}")]
        [Authorize(Policy = AuthenticationExtensions.ViewSeriesPolicy)]
        [ProducesResponseType<AdminEpisodeDetailResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEpisode(
            [FromRoute] Guid seriesId,
            [FromRoute] Guid episodeId,
            CancellationToken cancellationToken)
        {
            NoStore();

            if (await MissingSeriesAsync(seriesId, cancellationToken) is { } missing)
            {
                return missing;
            }

            var episode = await _episodes.GetAsync(seriesId, episodeId, cancellationToken);

            return episode is null
                ? Answer(new EpisodeOutcome
                {
                    Action = EpisodeAction.NotFound,
                    SeriesId = seriesId,
                    EpisodeId = episodeId
                })
                : Ok(ToDetail(episode));
        }

        /// <summary>Creates an episode inside the series.</summary>
        [HttpPost]
        [Authorize(Policy = AuthenticationExtensions.EditSeriesPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateEpisode(
            [FromRoute] Guid seriesId,
            [FromBody] CreateEpisodeRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (await MissingSeriesAsync(seriesId, cancellationToken) is { } missing)
            {
                return missing;
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var status = SeriesTextParser.ParseEpisodeStatusOrNull(request.Status);
            if (status is null)
            {
                if (string.IsNullOrWhiteSpace(request.Status))
                {
                    status = EpisodeStatus.Inactive;
                }
                else
                {
                    errors[nameof(CreateEpisodeRequest.Status)] = [StatusHint];
                }
            }

            var type = SeriesTextParser.ParseEpisodeTypeOrNull(request.Type);
            if (type is null)
            {
                errors[nameof(CreateEpisodeRequest.Type)] = [TypeHint];
            }

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var outcome = await _episodes.CreateAsync(
                new NewEpisode
                {
                    SeriesId = seriesId,
                    SeasonNumber = request.SeasonNumber ?? 0,
                    EpisodeNumber = request.EpisodeNumber ?? 0,
                    Name = request.Name,
                    Source = request.Source?.Trim() ?? string.Empty,
                    Status = status!.Value,
                    Type = type!.Value,
                    ImdbId = request.ImdbId,
                    ImageUrl = request.ImageUrl,
                    Plot = request.Plot,
                    ReleaseDate = request.ReleaseDate,
                    Rating = request.Rating
                },
                cancellationToken);

            var (statusCode, body) = SeriesResponses.Describe(outcome);

            if (statusCode == StatusCodes.Status201Created)
            {
                Response.Headers.Location =
                    $"/admin/series/{seriesId}/episodes/{outcome.EpisodeId}";
            }

            return StatusCode(statusCode, body);
        }

        /// <summary>Changes an episode. Every field the body can name is optional.</summary>
        [HttpPatch("{episodeId:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditSeriesPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateEpisode(
            [FromRoute] Guid seriesId,
            [FromRoute] Guid episodeId,
            [FromBody] UpdateEpisodeRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (await MissingSeriesAsync(seriesId, cancellationToken) is { } missing)
            {
                return missing;
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            EpisodeStatus? status = null;
            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                status = SeriesTextParser.ParseEpisodeStatusOrNull(request.Status);
                if (status is null)
                {
                    errors[nameof(UpdateEpisodeRequest.Status)] = [StatusHint];
                }
            }

            EpisodeType? type = null;
            if (!string.IsNullOrWhiteSpace(request.Type))
            {
                type = SeriesTextParser.ParseEpisodeTypeOrNull(request.Type);
                if (type is null)
                {
                    errors[nameof(UpdateEpisodeRequest.Type)] = [TypeHint];
                }
            }

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var outcome = await _episodes.UpdateAsync(
                seriesId,
                episodeId,
                new EpisodePatch
                {
                    SeasonNumber = request.SeasonNumber,
                    EpisodeNumber = request.EpisodeNumber,
                    Name = request.Name,
                    Source = request.Source,
                    Status = status,
                    Type = type,
                    ImdbId = request.ImdbId,
                    ImageUrl = request.ImageUrl,
                    Plot = request.Plot,
                    ReleaseDate = request.ReleaseDate,
                    Rating = request.Rating
                },
                cancellationToken);

            return Answer(outcome);
        }

        /// <summary>Deletes an episode.</summary>
        [HttpDelete("{episodeId:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditSeriesPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteEpisode(
            [FromRoute] Guid seriesId,
            [FromRoute] Guid episodeId,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (await MissingSeriesAsync(seriesId, cancellationToken) is { } missing)
            {
                return missing;
            }

            return Answer(await _episodes.DeleteAsync(seriesId, episodeId, cancellationToken));
        }

        /// <summary>The refusal a route under a missing series answers with, or null.</summary>
        private async Task<IActionResult?> MissingSeriesAsync(
            Guid seriesId,
            CancellationToken cancellationToken)
        {
            if (await _series.ExistsAsync(seriesId, cancellationToken))
            {
                return null;
            }

            var (status, body) = SeriesResponses.Describe(new SeriesOutcome
            {
                Action = SeriesAction.NotFound,
                SeriesId = seriesId
            });

            return new ObjectResult(body) { StatusCode = status };
        }

        private static Dictionary<string, string[]> ListErrors(
            int page,
            int pageSize,
            string? search,
            EpisodeSortField? field,
            EpisodeSortOrder? order,
            bool seasonOk,
            bool statusOk,
            bool typeOk)
        {
            var errors = ListQueryErrors.Common(
                page,
                pageSize,
                search,
                EpisodeListLimits.MaxPageSize,
                EpisodeListLimits.MaxSearchLength);

            if (field is null)
            {
                errors["sortBy"] = [SortByHint];
            }

            if (order is null)
            {
                errors["sortOrder"] = [SortOrderHint];
            }

            if (!seasonOk)
            {
                errors["seasonNumber"] = [SeasonHint];
            }

            if (!statusOk)
            {
                errors["status"] = [StatusHint];
            }

            if (!typeOk)
            {
                errors["type"] = [TypeHint];
            }

            return errors;
        }

        private static IActionResult Answer(EpisodeOutcome outcome)
        {
            var (status, body) = SeriesResponses.Describe(outcome);

            return new ObjectResult(body) { StatusCode = status };
        }

        private static AdminEpisodeItemResponse ToItem(EpisodeListItem item) => new()
        {
            Id = item.Id.ToString(),
            SeriesId = item.SeriesId.ToString(),
            SeasonNumber = item.SeasonNumber,
            EpisodeNumber = item.EpisodeNumber,
            Name = item.Name,
            Status = item.Status.ToString(),
            Type = item.Type.ToString(),
            Source = item.Source,
            ImageUrl = item.ImageUrl,
            ReleaseDate = item.ReleaseDate?.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture),
            Rating = item.Rating
        };

        private static AdminEpisodeDetailResponse ToDetail(Episode item) => new()
        {
            Id = item.Id.ToString(),
            SeriesId = item.SeriesId.ToString(),
            SeasonNumber = item.SeasonNumber,
            EpisodeNumber = item.EpisodeNumber,
            Name = item.Name,
            Status = item.Status.ToString(),
            Type = item.Type.ToString(),
            Source = item.Source,
            ImdbId = item.ImdbId,
            ImageUrl = item.ImageUrl,
            Plot = item.Plot,
            ReleaseDate = item.ReleaseDate?.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture),
            Rating = item.Rating
        };
    }
}
