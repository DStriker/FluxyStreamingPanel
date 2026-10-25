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
    /// Managing the series catalogue: one page of the table, one series in full, and the writes
    /// that create, change and delete them.
    /// </summary>
    /// <remarks>
    /// Two policies on every action: the class carries <c>AdminOnly</c> the way the rest of the
    /// area does, and each action adds its own permission on top. Read and write are separate
    /// grants, so a group which may watch the catalogue and a group which may run it stay two
    /// different things. No captcha and no attempt window, for the reason the rest of the admin
    /// area gives.
    /// </remarks>
    [ApiController]
    [Route("admin/series")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class SeriesController : ApiControllerBase
    {
        /// <summary>Sentence a state that names no member gets.</summary>
        private const string StatusHint = "The status must be either active or inactive.";

        /// <summary>Sentence a sort field that names nothing gets.</summary>
        private const string SortByHint =
            "The sort field must be one of: name, category, status, releasedate, rating, " +
            "seasons, episodes.";

        /// <summary>Sentence a direction that names nothing gets.</summary>
        private const string SortOrderHint = "The sort order must be either asc or desc.";

        /// <summary>Sentence an identifier that is not a uuid gets.</summary>
        private const string IdHint = "That value must be the uuid of the row it names.";

        private readonly ISeriesService _series;

        /// <summary>
        /// Initializes a new instance of the <see cref="SeriesController"/> class.
        /// </summary>
        /// <param name="series">Service that reads and changes the catalogue.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the changes worth remembering are reported to.</param>
        public SeriesController(
            ISeriesService series,
            IAntiforgery antiforgery,
            ILogger<SeriesController> logger)
            : base(antiforgery, logger)
        {
            _series = series;
        }

        /// <summary>Reports one page of the table, filtered and ordered the way the query asked.</summary>
        [HttpGet]
        [Authorize(Policy = AuthenticationExtensions.ViewSeriesPolicy)]
        [ProducesResponseType<AdminSeriesPageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSeries(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = SeriesListLimits.DefaultPageSize,
            [FromQuery] string? search = null,
            [FromQuery] string? categoryId = null,
            [FromQuery] string? status = null,
            [FromQuery] string? sortBy = "name",
            [FromQuery] string? sortOrder = "asc",
            CancellationToken cancellationToken = default)
        {
            NoStore();

            var field = SeriesTextParser.ParseSeriesSortField(sortBy);
            var order = SortOrderParser.Parse(
                sortOrder,
                SeriesSortOrder.Ascending,
                SeriesSortOrder.Descending);
            var wantedStatus = SeriesTextParser.ParseSeriesStatusOrNull(status);

            Guid? wantedCategory = null;
            var categoryOk = true;

            if (!string.IsNullOrWhiteSpace(categoryId))
            {
                categoryOk = Guid.TryParse(categoryId, out var parsedCategory);
                wantedCategory = categoryOk ? parsedCategory : null;
            }

            var statusOk = wantedStatus is not null || string.IsNullOrWhiteSpace(status);

            if (page < 1
                || pageSize is < 1 or > SeriesListLimits.MaxPageSize
                || (search?.Length ?? 0) > SeriesListLimits.MaxSearchLength
                || field is null
                || order is null
                || !categoryOk
                || !statusOk)
            {
                return ApiResults.ValidationFailed(ListErrors(
                    page,
                    pageSize,
                    search,
                    field,
                    order,
                    categoryOk,
                    statusOk));
            }

            var list = await _series.GetPageAsync(
                page,
                pageSize,
                search,
                wantedCategory,
                wantedStatus,
                field.Value,
                order.Value,
                cancellationToken);

            return Ok(new AdminSeriesPageResponse
            {
                Items = list.Items.Select(ToItem).ToList(),
                Total = list.Total,
                Page = list.Page,
                PageSize = list.PageSize
            });
        }

        /// <summary>Reports one series in full, for the edit form and the episode page.</summary>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.ViewSeriesPolicy)]
        [ProducesResponseType<AdminSeriesDetailResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSeriesDetail(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            NoStore();

            var series = await _series.GetAsync(id, cancellationToken);

            return series is null
                ? Answer(new SeriesOutcome { Action = SeriesAction.NotFound, SeriesId = id })
                : Ok(ToDetail(series));
        }

        /// <summary>Creates a series in its category.</summary>
        [HttpPost]
        [Authorize(Policy = AuthenticationExtensions.EditSeriesPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateSeries(
            [FromBody] CreateSeriesRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var status = SeriesTextParser.ParseSeriesStatusOrNull(request.Status);
            if (status is null)
            {
                if (string.IsNullOrWhiteSpace(request.Status))
                {
                    status = SeriesStatus.Inactive;
                }
                else
                {
                    errors[nameof(CreateSeriesRequest.Status)] = [StatusHint];
                }
            }

            Guid? categoryId = null;
            if (string.IsNullOrWhiteSpace(request.CategoryId)
                || !Guid.TryParse(request.CategoryId, out var parsedCategory))
            {
                errors[nameof(CreateSeriesRequest.CategoryId)] = [IdHint];
            }
            else
            {
                categoryId = parsedCategory;
            }

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var outcome = await _series.CreateAsync(
                new NewSeries
                {
                    Name = request.Name?.Trim() ?? string.Empty,
                    Status = status!.Value,
                    CategoryId = categoryId!.Value,
                    ImdbId = request.ImdbId,
                    PosterUrl = request.PosterUrl,
                    BackdropUrl = request.BackdropUrl,
                    Plot = request.Plot,
                    Cast = request.Cast ?? [],
                    Director = request.Director,
                    Genres = request.Genres ?? [],
                    ReleaseDate = request.ReleaseDate,
                    Rating = request.Rating
                },
                cancellationToken);

            var (statusCode, body) = SeriesResponses.Describe(outcome);

            if (statusCode == StatusCodes.Status201Created)
            {
                Response.Headers.Location = $"/admin/series/{outcome.SeriesId}";
            }

            return StatusCode(statusCode, body);
        }

        /// <summary>Changes a series. Every field the body can name is optional.</summary>
        [HttpPatch("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditSeriesPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateSeries(
            [FromRoute] Guid id,
            [FromBody] UpdateSeriesRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            SeriesStatus? status = null;
            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                status = SeriesTextParser.ParseSeriesStatusOrNull(request.Status);
                if (status is null)
                {
                    errors[nameof(UpdateSeriesRequest.Status)] = [StatusHint];
                }
            }

            Guid? categoryId = null;
            if (!string.IsNullOrWhiteSpace(request.CategoryId))
            {
                if (Guid.TryParse(request.CategoryId, out var parsedCategory))
                {
                    categoryId = parsedCategory;
                }
                else
                {
                    errors[nameof(UpdateSeriesRequest.CategoryId)] = [IdHint];
                }
            }

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var outcome = await _series.UpdateAsync(
                id,
                new SeriesPatch
                {
                    Name = request.Name,
                    Status = status,
                    CategoryId = categoryId,
                    ImdbId = request.ImdbId,
                    PosterUrl = request.PosterUrl,
                    BackdropUrl = request.BackdropUrl,
                    Plot = request.Plot,
                    Cast = request.Cast,
                    Director = request.Director,
                    Genres = request.Genres,
                    ReleaseDate = request.ReleaseDate,
                    Rating = request.Rating
                },
                cancellationToken);

            return Answer(outcome);
        }

        /// <summary>Deletes a series and, by cascade, every episode it holds.</summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditSeriesPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteSeries(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            return Answer(await _series.DeleteAsync(id, cancellationToken));
        }

        /// <summary>
        /// Which of the bounds a request broke, keyed by the camelCase name of the query
        /// parameter so the reason lands under the input a client would fix.
        /// </summary>
        private static Dictionary<string, string[]> ListErrors(
            int page,
            int pageSize,
            string? search,
            SeriesSortField? field,
            SeriesSortOrder? order,
            bool categoryOk,
            bool statusOk)
        {
            var errors = ListQueryErrors.Common(
                page,
                pageSize,
                search,
                SeriesListLimits.MaxPageSize,
                SeriesListLimits.MaxSearchLength);

            if (field is null)
            {
                errors["sortBy"] = [SortByHint];
            }

            if (order is null)
            {
                errors["sortOrder"] = [SortOrderHint];
            }

            if (!categoryOk)
            {
                errors["categoryId"] = [IdHint];
            }

            if (!statusOk)
            {
                errors["status"] = [StatusHint];
            }

            return errors;
        }

        private static IActionResult Answer(SeriesOutcome outcome)
        {
            var (status, body) = SeriesResponses.Describe(outcome);

            return new ObjectResult(body) { StatusCode = status };
        }

        private static AdminSeriesItemResponse ToItem(SeriesListItem item) => new()
        {
            Id = item.Id.ToString(),
            Name = item.Name,
            Status = item.Status.ToString(),
            CategoryId = item.CategoryId.ToString(),
            CategoryName = item.CategoryName,
            PosterUrl = item.PosterUrl,
            SeasonsCount = item.SeasonsCount,
            EpisodesCount = item.EpisodesCount,
            ReleaseDate = item.ReleaseDate?.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture),
            Rating = item.Rating
        };

        private static AdminSeriesDetailResponse ToDetail(SeriesDetail item) => new()
        {
            Id = item.Id.ToString(),
            Name = item.Name,
            Status = item.Status.ToString(),
            CategoryId = item.CategoryId.ToString(),
            CategoryName = item.CategoryName,
            ImdbId = item.ImdbId,
            PosterUrl = item.PosterUrl,
            BackdropUrl = item.BackdropUrl,
            Plot = item.Plot,
            Cast = item.Cast,
            Director = item.Director,
            Genres = item.Genres,
            ReleaseDate = item.ReleaseDate,
            Rating = item.Rating,
            SeasonsCount = item.SeasonsCount,
            EpisodesCount = item.EpisodesCount
        };
    }
}
