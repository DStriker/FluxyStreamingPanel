using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Series;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Series.Commands;
using Fluxy.Core.Series.Enums;
using Fluxy.Core.Series.Interfaces;
using Fluxy.Core.Series.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Content
{
    /// <summary>
    /// The collective operations on the series resource, split from
    /// <see cref="SeriesController"/>.
    /// </summary>
    /// <remarks>
    /// The seam is the shape of the request and the answer: a bulk request names a set of rows
    /// a table assembled on screen rather than one row with an address of its own, and it
    /// answers with a per-identifier report. The route is <c>POST /admin/series/bulk</c>.
    /// </remarks>
    [ApiController]
    [Route("admin/series/bulk")]
    [Authorize(Policy = AuthenticationExtensions.EditSeriesPolicy)]
    [Produces("application/json")]
    public sealed class SeriesBulkController : ApiControllerBase
    {
        /// <summary>Names the operations a bulk body may ask for.</summary>
        private const string ActionHint =
            "The action must be 'change-status', 'move-category' or 'delete'.";

        /// <summary>Sentence a category that is not an identifier gets.</summary>
        private const string CategoryHint =
            "The category must be the identifier of a category, as a uuid.";

        /// <summary>Sentence a state that names no member gets.</summary>
        private const string StatusHint = "The status must be either active or inactive.";

        /// <summary>Sentence a set of identifiers that is not all uuids gets.</summary>
        private const string IdsHint = "Every identifier must be the uuid of a series.";

        private readonly ISeriesBulkService _bulk;

        /// <summary>
        /// Initializes a new instance of the <see cref="SeriesBulkController"/> class.
        /// </summary>
        /// <param name="bulk">Service that applies the operation to the named series.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the changes worth remembering are reported to.</param>
        public SeriesBulkController(
            ISeriesBulkService bulk,
            IAntiforgery antiforgery,
            ILogger<SeriesBulkController> logger)
            : base(antiforgery, logger)
        {
            _bulk = bulk;
        }

        /// <summary>Applies one operation to every series the request names.</summary>
        [HttpPost]
        [ProducesResponseType<BulkOperationResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> BulkSeries(
            [FromBody] BulkSeriesRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var action = SeriesTextParser.ParseBulkSeriesActionOrNull(request.Action);
            if (action is null)
            {
                errors[nameof(BulkSeriesRequest.Action)] = [ActionHint];
            }

            var ids = ParseIds(request.Ids, errors);

            Guid? categoryId = null;
            if (action is BulkSeriesAction.MoveCategory)
            {
                if (Guid.TryParse(request.CategoryId, out var parsedCategory))
                {
                    categoryId = parsedCategory;
                }
                else
                {
                    errors[nameof(BulkSeriesRequest.CategoryId)] = [CategoryHint];
                }
            }

            SeriesStatus? status = null;
            if (action is BulkSeriesAction.ChangeStatus)
            {
                status = SeriesTextParser.ParseSeriesStatusOrNull(request.Status);
                if (status is null)
                {
                    errors[nameof(BulkSeriesRequest.Status)] = [StatusHint];
                }
            }

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            return Ok(BulkResponses.Describe(await _bulk.BulkAsync(
                new BulkSeriesOperation
                {
                    Action = action!.Value,
                    SeriesIds = ids!,
                    CategoryId = categoryId,
                    Status = status
                },
                cancellationToken)));
        }

        private static IReadOnlyList<Guid>? ParseIds(
            IReadOnlyList<string>? ids,
            Dictionary<string, string[]> errors)
        {
            if (ids is null || ids.Count is 0)
            {
                errors[nameof(BulkSeriesRequest.Ids)] = ["Select at least one series."];
                return null;
            }

            if (ids.Count > SeriesBulkLimits.MaxItems)
            {
                errors[nameof(BulkSeriesRequest.Ids)] =
                [$"At most {SeriesBulkLimits.MaxItems} series may be named in one request."];
                return null;
            }

            var parsed = new List<Guid>(ids.Count);

            foreach (var text in ids)
            {
                if (!Guid.TryParse(text, out var id))
                {
                    errors[nameof(BulkSeriesRequest.Ids)] = [IdsHint];
                    return null;
                }

                if (!parsed.Contains(id))
                {
                    parsed.Add(id);
                }
            }

            return parsed;
        }
    }
}
