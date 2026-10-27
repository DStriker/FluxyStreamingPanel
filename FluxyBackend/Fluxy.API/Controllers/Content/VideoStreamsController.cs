using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Streams;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Content.Enums;
using Fluxy.Core.Content.Interfaces;
using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Interfaces;
using Fluxy.Core.Streams.Models;
using Fluxy.Core.Streams.ReadModels;
using Fluxy.Core.Streams.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Content
{
    /// <summary>
    /// Managing the configured streams: one page of the table, one stream in full, and the
    /// writes that create, change, start, stop, restart and delete them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two policies on every action rather than one on the class: the class carries
    /// <c>AdminOnly</c> the way the groups controller does, and each action adds its own
    /// permission on top. Read and write are separate grants here for the same reason they are
    /// there - a group which may watch the streams and a group which may run them are two
    /// different things, and one key could not express both.
    /// </para>
    /// <para>
    /// <b>No captcha and no attempt window</b>, for the reason the accounts controller gives:
    /// both exist to guard a request with no session at all, and everything here is behind an
    /// admin session and a fresh antiforgery pair. This is a recorded departure from the
    /// registration surface's shape, agreed for the whole admin area.
    /// </para>
    /// <para>
    /// <b>What the controller parses and what it leaves to the service is split by type, not
    /// by importance.</b> A body or a query string arrives as text, so anything that has to
    /// become an enum or a uuid is parsed here and a value that names nothing is refused with
    /// the field it came in - the transport's job. Everything that is a fact about a stream
    /// (is this category the right kind, is this name taken, may this kind of stream carry an
    /// archive) belongs to the service, so that no second endpoint reaching the same action
    /// can hold a second opinion about it.
    /// </para>
    /// <para>
    /// Both reads carry <c>Cache-Control: no-store</c> through
    /// <see cref="ApiControllerBase.NoStore"/>: this is the shape of a private area's
    /// configuration, and a shared cache that kept one would hand it to the next visitor.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/video-streams")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class VideoStreamsController : ApiControllerBase
    {
        /// <summary>Sentence a kind of stream that names no known member gets.</summary>
        private const string TypeHint =
            "The stream type must be one of: permanent, ondemand, redirect, "
            + "onlineseries, vodplaylist, fileplaylist.";

        /// <summary>Sentence a state that names no known member gets.</summary>
        private const string StatusHint = "The status must be either running or stopped.";

        /// <summary>Sentence a column that names nothing gets.</summary>
        private const string SortByHint =
            "The sort field must be one of: name, category, status, type.";

        /// <summary>Sentence a direction that names nothing gets.</summary>
        private const string SortOrderHint = "The sort order must be either asc or desc.";

        /// <summary>Sentence an identifier that is not a uuid gets.</summary>
        private const string IdHint = "That value must be the uuid of the row it names.";

        private readonly IVideoStreamService _streams;
        private readonly IBouquetService _bouquets;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoStreamsController"/> class.
        /// </summary>
        /// <param name="streams">Service that reads and changes the streams.</param>
        /// <param name="bouquets">
        /// Service that answers which bouquets a stream belongs to, for the form's picker.
        /// </param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the changes worth remembering are reported to.</param>
        public VideoStreamsController(
            IVideoStreamService streams,
            IBouquetService bouquets,
            IAntiforgery antiforgery,
            ILogger<VideoStreamsController> logger)
            : base(antiforgery, logger)
        {
            _streams = streams;
            _bouquets = bouquets;
        }

        /// <summary>
        /// Reports one page of the streams table, filtered and ordered the way the query asked.
        /// </summary>
        /// <param name="page">One-based page wanted.</param>
        /// <param name="pageSize">How many streams one page holds.</param>
        /// <param name="search">Case-insensitive substring of the stream's name.</param>
        /// <param name="categoryId">Only streams of this category, as text.</param>
        /// <param name="status">Only streams in this state, as the name of the enum member.</param>
        /// <param name="type">Only streams of this kind, as the name of the enum member.</param>
        /// <param name="sortBy">Column to order by.</param>
        /// <param name="sortOrder">Which way to run.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with the page, or 400 naming every parameter that broke a bound.</returns>
        /// <remarks>
        /// Search, filters, order and paging live here rather than in the browser for the
        /// reason the accounts list gives: the pager shows a <c>total</c>. A filter applied
        /// after the rows arrived would count what it is about to remove, and a sort applied
        /// there would order one page while the others kept whatever order the database
        /// happened to return - so the same stream could appear on two pages or on none.
        ///
        /// The bounds are refused rather than clamped, and a value that names nothing is
        /// refused rather than treated as "no filter" - silently turning a request for 500 rows
        /// into 100 would answer with a page whose items do not match the page size the caller
        /// was told it got.
        ///
        /// No antiforgery pair: the method changes nothing.
        /// </remarks>
        [HttpGet]
        [Authorize(Policy = AuthenticationExtensions.ViewStreamsPolicy)]
        [ProducesResponseType<AdminVideoStreamPageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetStreams(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = VideoStreamListLimits.DefaultPageSize,
            [FromQuery] string? search = null,
            [FromQuery] string? categoryId = null,
            [FromQuery] string? status = null,
            [FromQuery] string? type = null,
            [FromQuery] string? sortBy = "name",
            [FromQuery] string? sortOrder = "asc",
            CancellationToken cancellationToken = default)
        {
            NoStore();

            var field = ParseSortField(sortBy);
            var order = ParseSortOrder(sortOrder);
            var wantedStatus = VideoStreamTextParser.ParseStatusOrNull(status);
            var wantedType = VideoStreamTextParser.ParseTypeOrNull(type);

            // A filter that was not asked for and a filter that asked for nonsense are two
            // different answers, so the value and whether it was understood are read apart:
            // null means "keep everything", while a value that names no row is a parameter
            // worth refusing.
            Guid? wantedCategory = null;
            var categoryOk = true;

            if (!string.IsNullOrWhiteSpace(categoryId))
            {
                categoryOk = Guid.TryParse(categoryId, out var parsedCategory);
                wantedCategory = categoryOk ? parsedCategory : null;
            }

            var statusOk = wantedStatus is not null || string.IsNullOrWhiteSpace(status);
            var typeOk = wantedType is not null || string.IsNullOrWhiteSpace(type);

            if (page < 1 ||
                pageSize is < 1 or > VideoStreamListLimits.MaxPageSize ||
                (search?.Length ?? 0) > VideoStreamListLimits.MaxSearchLength ||
                field is null ||
                order is null ||
                !categoryOk ||
                !statusOk ||
                !typeOk)
            {
                return ApiResults.ValidationFailed(ListErrors(
                    page,
                    pageSize,
                    search,
                    field,
                    order,
                    categoryOk,
                    statusOk,
                    typeOk));
            }

            var list = await _streams.GetPageAsync(
                page,
                pageSize,
                search,
                wantedCategory,
                wantedStatus,
                wantedType,
                field.Value,
                order.Value,
                cancellationToken);

            return Ok(new AdminVideoStreamPageResponse
            {
                Items = list.Items.Select(ToItem).ToList(),
                Total = list.Total,
                Page = list.Page,
                PageSize = list.PageSize
            });
        }

        /// <summary>
        /// Reports one stream in full, for the edit form.
        /// </summary>
        /// <param name="id">Identifier of the stream.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with the stream, or 404 when no stream answers to that identifier.</returns>
        /// <remarks>
        /// No antiforgery pair: the method changes nothing. The identifier travels in the path
        /// and never in the body, because the path is the honest statement of which row is
        /// meant - a body that named a second one would be two claims about one request.
        /// </remarks>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.ViewStreamsPolicy)]
        [ProducesResponseType<AdminVideoStreamDetailResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetStream(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            NoStore();

            var stream = await _streams.GetAsync(id, cancellationToken);

            return stream is null
                ? Answer(new VideoStreamOutcome { Action = VideoStreamAction.NotFound, StreamId = id })
                : Ok(ToDetail(
                    stream,
                    await _bouquets.GetItemBouquetIdsAsync(ContentType.VideoStream, id, cancellationToken)));
        }

        /// <summary>
        /// Creates a stream in its category.
        /// </summary>
        /// <param name="request">The stream to create.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 201 with a <c>Location</c> naming the new stream, 400 with the rejected fields, 409
        /// <c>video_stream_already_exists</c>, 400 <c>csrf_invalid</c> without the antiforgery
        /// pair.
        /// </returns>
        /// <remarks>
        /// The 201 carries a <c>Location</c>, and it is honest for the reason the accounts
        /// endpoint's is: the row exists in full the moment it is written, this very controller
        /// serves the address it names, and no mailed code stands between creation and
        /// addressability.
        ///
        /// There is deliberately no <c>sortOrder</c> in the body. The order a stream is shown in
        /// belongs to the list that shows it rather than to the row, so the request has no
        /// position to name and the service has none to read.
        ///
        /// The body's other fields are judged by the service, not here: which of them a stream
        /// may carry at all is decided by its kind, and that rule belongs where every endpoint
        /// that writes a stream reaches it.
        /// </remarks>
        [HttpPost]
        [Authorize(Policy = AuthenticationExtensions.EditStreamsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateStream(
            [FromBody] CreateVideoStreamRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var type = VideoStreamTextParser.ParseTypeOrNull(request.Type);
            if (type is null)
            {
                errors[nameof(CreateVideoStreamRequest.Type)] = [TypeHint];
            }

            // An absent status keeps the contract every stream had before the form offered
            // the choice: create it stopped. A value that names no member is refused rather
            // than silently replaced by that default, because the caller plainly meant to
            // choose one.
            var status = VideoStreamTextParser.ParseStatusOrNull(request.Status);
            if (status is null)
            {
                if (string.IsNullOrWhiteSpace(request.Status))
                {
                    status = VideoStreamStatus.Stopped;
                }
                else
                {
                    errors[nameof(CreateVideoStreamRequest.Status)] = [StatusHint];
                }
            }

            Guid? categoryId = null;
            if (string.IsNullOrWhiteSpace(request.CategoryId) || !Guid.TryParse(request.CategoryId, out var parsedCategory))
            {
                errors[nameof(CreateVideoStreamRequest.CategoryId)] = [IdHint];
            }
            else
            {
                categoryId = parsedCategory;
            }

            // The three references are parsed here rather than by the service, because the
            // create model holds them as <c>Guid?</c> - it has nothing to "keep", so there is
            // no reason for the text form to survive this far. An empty one simply means
            // "none", which is the only thing a create can mean.
            var seriesId = ParseIdentifierOrNull(
                request.SeriesId, nameof(CreateVideoStreamRequest.SeriesId), errors);
            var vodId = ParseIdentifierOrNull(
                request.VodId, nameof(CreateVideoStreamRequest.VodId), errors);
            var epgSourceId = ParseIdentifierOrNull(
                request.EpgSourceId, nameof(CreateVideoStreamRequest.EpgSourceId), errors);

            // A set rather than an order: the service appends the stream to the end of every
            // bouquet it joins, and the position inside one belongs to the bouquet's own screen.
            var bouquetIds = BouquetTextParser.ParseOrNull(
                request.BouquetIds, nameof(CreateVideoStreamRequest.BouquetIds), errors);

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var outcome = await _streams.CreateAsync(
                new NewVideoStream
                {
                    Name = request.Name?.Trim() ?? string.Empty,
                    Type = type!.Value,
                    Status = status!.Value,
                    CategoryId = categoryId!.Value,
                    LogoUrl = request.LogoUrl,
                    Enigma2Sid = request.Enigma2Sid,
                    DelayMinutes = request.DelayMinutes ?? 0,
                    SeriesId = seriesId,
                    VodId = vodId,
                    EpgSourceId = epgSourceId,
                    EpgChannelKey = request.EpgChannelKey,
                    ArchiveDays = request.ArchiveDays ?? 0,
                    RestreamUserAgent = request.RestreamUserAgent,
                    RestreamProxy = request.RestreamProxy,
                    RestreamCookies = request.RestreamCookies,
                    RestreamHeaders = request.RestreamHeaders,
                    Sources = request.Sources ?? [],
                    Files = request.Files ?? [],
                    BouquetIds = bouquetIds
                },
                cancellationToken);

            var (statusCode, body) = VideoStreamResponses.Describe(outcome);

            if (statusCode == StatusCodes.Status201Created)
            {
                Response.Headers.Location = $"/admin/video-streams/{outcome.StreamId}";
            }

            return StatusCode(statusCode, body);
        }

        /// <summary>
        /// Changes a stream. Every field the body can name is optional.
        /// </summary>
        /// <param name="id">Identifier of the stream to change, in the path.</param>
        /// <param name="request">The fields to change. An absent field is left alone.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 when it was written, 400 with the rejected fields, 404 when there is no such
        /// stream, 409 when another stream of the same category holds that name.
        /// </returns>
        /// <remarks>
        /// A body that names nothing is a success rather than a complaint, the convention every
        /// other PATCH on this API follows.
        ///
        /// <b>Status is a field here.</b> The edit form chooses whether the stream is active,
        /// and an absent value keeps the stored one. The start, stop and restart actions
        /// below remain the transitions that change it on their own; this is the same row
        /// fact written from the form that already edits every other field of the stream.
        ///
        /// The three references travel as text so that "keep" and "clear" are two different
        /// answers without a second field to tell them apart: <c>null</c> keeps whatever the
        /// row holds, an empty string clears it. They are passed to the service as text for
        /// the same reason the lists are - one place decides what a uuid is and what a list
        /// means, and this is not that place.
        /// </remarks>
        [HttpPatch("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditStreamsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateStream(
            [FromRoute] Guid id,
            [FromBody] UpdateVideoStreamRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            VideoStreamType? type = null;
            if (!string.IsNullOrWhiteSpace(request.Type))
            {
                type = VideoStreamTextParser.ParseTypeOrNull(request.Type);
                if (type is null)
                {
                    errors[nameof(UpdateVideoStreamRequest.Type)] = [TypeHint];
                }
            }

            VideoStreamStatus? status = null;
            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                status = VideoStreamTextParser.ParseStatusOrNull(request.Status);
                if (status is null)
                {
                    errors[nameof(UpdateVideoStreamRequest.Status)] = [StatusHint];
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
                    errors[nameof(UpdateVideoStreamRequest.CategoryId)] = [IdHint];
                }
            }

            // Parsed before the check, so a broken identifier is refused with the field named
            // rather than silently dropped. Null keeps every link; an empty list clears them.
            var bouquetIds = BouquetTextParser.ParseOrNull(
                request.BouquetIds, nameof(UpdateVideoStreamRequest.BouquetIds), errors);

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var outcome = await _streams.UpdateAsync(
                id,
                new VideoStreamPatch
                {
                    Name = request.Name,
                    Type = type,
                    Status = status,
                    CategoryId = categoryId,
                    BouquetIds = bouquetIds,
                    LogoUrl = request.LogoUrl,
                    Enigma2Sid = request.Enigma2Sid,
                    DelayMinutes = request.DelayMinutes,
                    SeriesId = request.SeriesId,
                    VodId = request.VodId,
                    EpgSourceId = request.EpgSourceId,
                    EpgChannelKey = request.EpgChannelKey,
                    ArchiveDays = request.ArchiveDays,
                    RestreamUserAgent = request.RestreamUserAgent,
                    RestreamProxy = request.RestreamProxy,
                    RestreamCookies = request.RestreamCookies,
                    RestreamHeaders = request.RestreamHeaders,
                    Sources = request.Sources,
                    Files = request.Files
                },
                cancellationToken);

            return Answer(outcome);
        }

        /// <summary>
        /// Marks a stream as broadcasting.
        /// </summary>
        /// <param name="id">Identifier of the stream to start.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>video_stream_started</c>, 404 <c>video_stream_not_found</c>, 400
        /// <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// A <c>POST</c> rather than a <c>PATCH</c> of the status, for the reason the accounts
        /// controller gives about its own state transitions: this is an operation on a row, not
        /// a field write, and the alternative would carry the transition's whole meaning inside
        /// a caller's payload.
        ///
        /// The status is all this changes. Nothing here starts a process, because no process
        /// exists to start yet - the action is the row's state and the fact that a stream is
        /// meant to be relaying, and the part that actually relays it arrives with the relaying
        /// process itself. Starting a stream that is already running is a success rather than a
        /// complaint, so a double click is not a failure.
        /// </remarks>
        [HttpPost("{id:guid}/start")]
        [Authorize(Policy = AuthenticationExtensions.EditStreamsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public Task<IActionResult> StartStream(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
            => SetStatus(id, stream => stream.StartAsync(id, cancellationToken));

        /// <summary>
        /// Marks a stream as no longer broadcasting. The mirror of <see cref="StartStream"/>.
        /// </summary>
        /// <param name="id">Identifier of the stream to stop.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>video_stream_stopped</c>, 404 <c>video_stream_not_found</c>, 400
        /// <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        [HttpPost("{id:guid}/stop")]
        [Authorize(Policy = AuthenticationExtensions.EditStreamsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public Task<IActionResult> StopStream(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
            => SetStatus(id, stream => stream.StopAsync(id, cancellationToken));

        /// <summary>
        /// Marks a stream as broadcasting again.
        /// </summary>
        /// <param name="id">Identifier of the stream to restart.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>video_stream_restarted</c>, 404 <c>video_stream_not_found</c>, 400
        /// <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// A start with its own answer, so the operator can say which of the two they asked
        /// for. No process is signalled, for the reason <see cref="StartStream"/> gives: the
        /// relaying process does not exist here yet, and this transition is the row's state
        /// rather than a command to a worker.
        /// </remarks>
        [HttpPost("{id:guid}/restart")]
        [Authorize(Policy = AuthenticationExtensions.EditStreamsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public Task<IActionResult> RestartStream(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
            => SetStatus(id, stream => stream.RestartAsync(id, cancellationToken));

        /// <summary>
        /// Deletes a stream together with its sources and its files.
        /// </summary>
        /// <param name="id">Identifier of the stream to delete.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 <c>video_stream_deleted</c>, or 404 when there is no such stream.</returns>
        /// <remarks>
        /// A <c>DELETE</c> that answers 200 rather than 204, because it has something to say:
        /// the difference between a stream that was deleted and one that was not there to
        /// delete is the whole answer, and an empty body cannot say it.
        ///
        /// No "in use" refusal, on purpose: nothing a stream is filed under can be taken away
        /// while the stream exists - the category's own delete is the one that refuses, and it
        /// counts the streams that stand in its way.
        /// </remarks>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditStreamsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteStream(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            return Answer(await _streams.DeleteAsync(id, cancellationToken));
        }

        /// <summary>
        /// The shared shape of the three state transitions: refuse an unauthenticated caller's
        /// stale token first, then ask the service, then answer.
        /// </summary>
        /// <remarks>
        /// Factored out because start, stop and restart differ in exactly one call each, and a
        /// copy of the CSRF check is free to be pasted into one action and forgotten in another
        /// - which would leave a state transition reachable by a request this application did
        /// not make.
        /// </remarks>
        private async Task<IActionResult> SetStatus(
            Guid id,
            Func<IVideoStreamService, Task<VideoStreamOutcome>> transition)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            return Answer(await transition(_streams));
        }

        /// <summary>Turns <c>asc</c> / <c>desc</c> into an order, or nothing at all.</summary>
        private static VideoStreamSortOrder? ParseSortOrder(string? value)
            => SortOrderParser.Parse(
                value,
                VideoStreamSortOrder.Ascending,
                VideoStreamSortOrder.Descending);

        /// <summary>Turns the name of a column into the field to order by, or nothing at all.</summary>
        /// <remarks>
        /// Compared after lowering, so <c>Name</c> and <c>name</c> are the same request. The
        /// accepted values are the only columns the table can honestly order by: everything
        /// else it might want to show (which source is answering, for one) is a fact a process
        /// reports and is not in the row at all.
        /// </remarks>
        private static VideoStreamSortField? ParseSortField(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "name" => VideoStreamSortField.Name,
                "category" => VideoStreamSortField.Category,
                "status" => VideoStreamSortField.Status,
                "type" => VideoStreamSortField.Type,
                _ => null
            };

        /// <summary>
        /// Turns one nullable reference of a create body into the identifier it names, or
        /// nothing at all.
        /// </summary>
        /// <remarks>
        /// An absent value and an empty one both mean "none", because a create has nothing to
        /// keep: the difference between the two only starts existing once a row does.
        /// </remarks>
        private static Guid? ParseIdentifierOrNull(
            string? value,
            string propertyName,
            Dictionary<string, string[]> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (!Guid.TryParse(value.Trim(), out var parsed))
            {
                errors[propertyName] = [IdHint];
                return null;
            }

            return parsed;
        }

        /// <summary>
        /// Which of the bounds a request broke, keyed by the camelCase name of the query
        /// parameter so the reason lands under the input a client would fix.
        /// </summary>
        /// <remarks>
        /// A parameter that names no value gets the sentence listing the values that exist,
        /// which is the same sentence the parsers above would have given - collected here so
        /// that one request with four mistakes is told about all four rather than one per
        /// round trip.
        /// </remarks>
        private static Dictionary<string, string[]> ListErrors(
            int page,
            int pageSize,
            string? search,
            VideoStreamSortField? field,
            VideoStreamSortOrder? order,
            bool categoryOk,
            bool statusOk,
            bool typeOk)
        {
            var errors = ListQueryErrors.Common(
                page,
                pageSize,
                search,
                VideoStreamListLimits.MaxPageSize,
                VideoStreamListLimits.MaxSearchLength);

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

            if (!typeOk)
            {
                errors["type"] = [TypeHint];
            }

            return errors;
        }

        /// <summary>
        /// Turns the service's answer into the status and body a client is told, the one place
        /// where a <see cref="VideoStreamAction"/> becomes a number.
        /// </summary>
        private static IActionResult Answer(VideoStreamOutcome outcome)
        {
            var (status, body) = VideoStreamResponses.Describe(outcome);

            return new ObjectResult(body) { StatusCode = status };
        }

        private static AdminVideoStreamItemResponse ToItem(VideoStreamListItem item) => new()
        {
            Id = item.Id.ToString(),
            Name = item.Name,
            Status = item.Status.ToString(),
            Type = item.Type.ToString(),
            CategoryId = item.CategoryId.ToString(),
            CategoryName = item.CategoryName,
            LogoUrl = item.LogoUrl,
            ActiveSource = item.ActiveSource,
            HasEpg = item.HasEpg
        };

        private static AdminVideoStreamDetailResponse ToDetail(
            VideoStream stream,
            IReadOnlyList<Guid> bouquetIds) => new()
        {
            Id = stream.Id.ToString(),
            Name = stream.Name,
            Status = stream.Status.ToString(),
            Type = stream.Type.ToString(),
            CategoryId = stream.CategoryId.ToString(),
            CategoryName = stream.CategoryName,
            LogoUrl = stream.LogoUrl,
            ActiveSource = stream.ActiveSource,
            Enigma2Sid = stream.Enigma2Sid,
            DelayMinutes = stream.DelayMinutes,
            SeriesId = stream.SeriesId?.ToString(),
            VodId = stream.VodId?.ToString(),
            EpgSourceId = stream.EpgSourceId?.ToString(),
            EpgChannelKey = stream.EpgChannelKey,
            HasEpg = stream.HasEpg,
            ArchiveDays = stream.ArchiveDays,
            RestreamUserAgent = stream.RestreamUserAgent,
            RestreamProxy = stream.RestreamProxy,
            RestreamCookies = stream.RestreamCookies,
            RestreamHeaders = stream.RestreamHeaders,
            Sources = stream.Sources,
            Files = stream.Files,
            BouquetIds = bouquetIds.Select(bouquetId => bouquetId.ToString()).ToList()
        };
    }
}
