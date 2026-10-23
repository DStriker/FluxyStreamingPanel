using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Streams;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Interfaces;
using Fluxy.Core.Streams.Models;
using Fluxy.Core.Streams.Outcomes;
using Fluxy.Core.Streams.ReadModels;
using Fluxy.Core.Streams.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Content
{
    /// <summary>
    /// Managing the configured audio streams: one page of the table, one stream in full, and
    /// the writes that create, change, start, stop, restart and delete them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two policies on every action rather than one on the class: the class carries
    /// <c>AdminOnly</c> the way the groups controller does, and each action adds its own
    /// permission on top. Read and write are separate grants here, and they are separate from
    /// the video stream grants too - an operator may run the video streams without touching the
    /// audio ones, which is the whole reason the permissions are a second pair rather than a
    /// reuse of the first.
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
    /// (is this category one of audio, is this name taken, may this stream carry a source
    /// list) belongs to the service, so that no second endpoint reaching the same action can
    /// hold a second opinion about it.
    /// </para>
    /// <para>
    /// Both reads carry <c>Cache-Control: no-store</c> through
    /// <see cref="ApiControllerBase.NoStore"/>: this is the shape of a private area's
    /// configuration, and a shared cache that kept one would hand it to the next visitor.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/audio-streams")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class AudioStreamsController : ApiControllerBase
    {
        /// <summary>Sentence a kind of stream that names no known member gets.</summary>
        private const string TypeHint =
            "The stream type must be one of: ondemand, redirect.";

        /// <summary>Sentence a state that names no known member gets.</summary>
        private const string StatusHint = "The status must be either running or stopped.";

        /// <summary>Sentence a column that names nothing gets.</summary>
        private const string SortByHint =
            "The sort field must be one of: name, category, status, type.";

        /// <summary>Sentence a direction that names nothing gets.</summary>
        private const string SortOrderHint = "The sort order must be either asc or desc.";

        /// <summary>Sentence an identifier that is not a uuid gets.</summary>
        private const string IdHint = "That value must be the uuid of the row it names.";

        private readonly IAudioStreamService _streams;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioStreamsController"/> class.
        /// </summary>
        /// <param name="streams">Service that reads and changes the streams.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the changes worth remembering are reported to.</param>
        public AudioStreamsController(
            IAudioStreamService streams,
            IAntiforgery antiforgery,
            ILogger<AudioStreamsController> logger)
            : base(antiforgery, logger)
        {
            _streams = streams;
        }

        /// <summary>
        /// Reports one page of the audio streams table, filtered and ordered the way the query
        /// asked.
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
        /// refused rather than treated as "no filter".
        ///
        /// No antiforgery pair: the method changes nothing.
        /// </remarks>
        [HttpGet]
        [Authorize(Policy = AuthenticationExtensions.ViewAudioStreamsPolicy)]
        [ProducesResponseType<AdminAudioStreamPageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetStreams(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = AudioStreamListLimits.DefaultPageSize,
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
            var wantedStatus = AudioStreamTextParser.ParseStatusOrNull(status);
            var wantedType = AudioStreamTextParser.ParseTypeOrNull(type);

            // A filter that was not asked for and a filter that asked for nonsense are two
            // different answers, so the value and whether it was understood are read apart.
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
                pageSize is < 1 or > AudioStreamListLimits.MaxPageSize ||
                (search?.Length ?? 0) > AudioStreamListLimits.MaxSearchLength ||
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

            return Ok(new AdminAudioStreamPageResponse
            {
                Items = list.Items.Select(ToItem).ToList(),
                Total = list.Total,
                Page = list.Page,
                PageSize = list.PageSize
            });
        }

        /// <summary>
        /// Reports one audio stream in full, for the edit form.
        /// </summary>
        /// <param name="id">Identifier of the stream.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with the stream, or 404 when no stream answers to that identifier.</returns>
        /// <remarks>
        /// No antiforgery pair: the method changes nothing. The identifier travels in the path
        /// and never in the body, because the path is the honest statement of which row is
        /// meant.
        /// </remarks>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.ViewAudioStreamsPolicy)]
        [ProducesResponseType<AdminAudioStreamDetailResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetStream(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            NoStore();

            var stream = await _streams.GetAsync(id, cancellationToken);

            return stream is null
                ? Answer(new AudioStreamOutcome { Action = AudioStreamAction.NotFound, StreamId = id })
                : Ok(ToDetail(stream));
        }

        /// <summary>
        /// Creates an audio stream in its category.
        /// </summary>
        /// <param name="request">The stream to create.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 201 with a <c>Location</c> naming the new stream, 400 with the rejected fields, 409
        /// <c>audio_stream_already_exists</c>, 400 <c>csrf_invalid</c> without the antiforgery
        /// pair.
        /// </returns>
        /// <remarks>
        /// The 201 carries a <c>Location</c>, and it is honest for the reason the accounts
        /// endpoint's is: the row exists in full the moment it is written, this very controller
        /// serves the address it names, and no mailed code stands between creation and
        /// addressability.
        ///
        /// The body carries the source list and nothing else that depends on the kind: both
        /// audio kinds relay from a list, so there is no field the chosen kind switches off.
        /// </remarks>
        [HttpPost]
        [Authorize(Policy = AuthenticationExtensions.EditAudioStreamsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateStream(
            [FromBody] CreateAudioStreamRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var type = AudioStreamTextParser.ParseTypeOrNull(request.Type);
            if (type is null)
            {
                errors[nameof(CreateAudioStreamRequest.Type)] = [TypeHint];
            }

            // An absent status keeps the contract every stream had before the form offered
            // the choice: create it stopped. A value that names no member is refused rather
            // than silently replaced by that default, because the caller plainly meant to
            // choose one.
            var status = AudioStreamTextParser.ParseStatusOrNull(request.Status);
            if (status is null)
            {
                if (string.IsNullOrWhiteSpace(request.Status))
                {
                    status = AudioStreamStatus.Stopped;
                }
                else
                {
                    errors[nameof(CreateAudioStreamRequest.Status)] = [StatusHint];
                }
            }

            Guid? categoryId = null;
            if (string.IsNullOrWhiteSpace(request.CategoryId)
                || !Guid.TryParse(request.CategoryId, out var parsedCategory))
            {
                errors[nameof(CreateAudioStreamRequest.CategoryId)] = [IdHint];
            }
            else
            {
                categoryId = parsedCategory;
            }

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var outcome = await _streams.CreateAsync(
                new NewAudioStream
                {
                    Name = request.Name?.Trim() ?? string.Empty,
                    Type = type!.Value,
                    Status = status!.Value,
                    CategoryId = categoryId!.Value,
                    LogoUrl = request.LogoUrl,
                    Enigma2Sid = request.Enigma2Sid,
                    RestreamUserAgent = request.RestreamUserAgent,
                    RestreamProxy = request.RestreamProxy,
                    RestreamCookies = request.RestreamCookies,
                    RestreamHeaders = request.RestreamHeaders,
                    Sources = request.Sources ?? []
                },
                cancellationToken);

            var (statusCode, body) = AudioStreamResponses.Describe(outcome);

            if (statusCode == StatusCodes.Status201Created)
            {
                Response.Headers.Location = $"/admin/audio-streams/{outcome.StreamId}";
            }

            return StatusCode(statusCode, body);
        }

        /// <summary>
        /// Changes an audio stream. Every field the body can name is optional.
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
        /// below remain the transitions that change it on their own.
        /// </remarks>
        [HttpPatch("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditAudioStreamsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateStream(
            [FromRoute] Guid id,
            [FromBody] UpdateAudioStreamRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            AudioStreamType? type = null;
            if (!string.IsNullOrWhiteSpace(request.Type))
            {
                type = AudioStreamTextParser.ParseTypeOrNull(request.Type);
                if (type is null)
                {
                    errors[nameof(UpdateAudioStreamRequest.Type)] = [TypeHint];
                }
            }

            AudioStreamStatus? status = null;
            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                status = AudioStreamTextParser.ParseStatusOrNull(request.Status);
                if (status is null)
                {
                    errors[nameof(UpdateAudioStreamRequest.Status)] = [StatusHint];
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
                    errors[nameof(UpdateAudioStreamRequest.CategoryId)] = [IdHint];
                }
            }

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var outcome = await _streams.UpdateAsync(
                id,
                new AudioStreamPatch
                {
                    Name = request.Name,
                    Type = type,
                    Status = status,
                    CategoryId = categoryId,
                    LogoUrl = request.LogoUrl,
                    Enigma2Sid = request.Enigma2Sid,
                    RestreamUserAgent = request.RestreamUserAgent,
                    RestreamProxy = request.RestreamProxy,
                    RestreamCookies = request.RestreamCookies,
                    RestreamHeaders = request.RestreamHeaders,
                    Sources = request.Sources
                },
                cancellationToken);

            return Answer(outcome);
        }

        /// <summary>
        /// Marks an audio stream as active.
        /// </summary>
        /// <param name="id">Identifier of the stream to start.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>audio_stream_started</c>, 404 <c>audio_stream_not_found</c>, 400
        /// <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// A <c>POST</c> rather than a <c>PATCH</c> of the status, for the reason the accounts
        /// controller gives about its own state transitions: this is an operation on a row, not
        /// a field write, and the alternative would carry the transition's whole meaning inside
        /// a caller's payload.
        ///
        /// The status is all this changes. Nothing here starts a process, because the process
        /// that relays an audio stream is not part of this API - the action is the row's state
        /// and the fact that the stream is meant to be relaying. Starting a stream that is
        /// already running is a success rather than a complaint, so a double click is not a
        /// failure.
        /// </remarks>
        [HttpPost("{id:guid}/start")]
        [Authorize(Policy = AuthenticationExtensions.EditAudioStreamsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public Task<IActionResult> StartStream(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
            => SetStatus(id, stream => stream.StartAsync(id, cancellationToken));

        /// <summary>
        /// Marks an audio stream as no longer active. The mirror of <see cref="StartStream"/>.
        /// </summary>
        /// <param name="id">Identifier of the stream to stop.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>audio_stream_stopped</c>, 404 <c>audio_stream_not_found</c>, 400
        /// <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        [HttpPost("{id:guid}/stop")]
        [Authorize(Policy = AuthenticationExtensions.EditAudioStreamsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public Task<IActionResult> StopStream(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
            => SetStatus(id, stream => stream.StopAsync(id, cancellationToken));

        /// <summary>
        /// Marks an audio stream as active again.
        /// </summary>
        /// <param name="id">Identifier of the stream to restart.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>audio_stream_restarted</c>, 404 <c>audio_stream_not_found</c>, 400
        /// <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// A start with its own answer, so the operator can say which of the two they asked
        /// for. No process is signalled, for the reason <see cref="StartStream"/> gives.
        /// </remarks>
        [HttpPost("{id:guid}/restart")]
        [Authorize(Policy = AuthenticationExtensions.EditAudioStreamsPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public Task<IActionResult> RestartStream(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
            => SetStatus(id, stream => stream.RestartAsync(id, cancellationToken));

        /// <summary>
        /// Deletes an audio stream together with its sources.
        /// </summary>
        /// <param name="id">Identifier of the stream to delete.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 <c>audio_stream_deleted</c>, or 404 when there is no such stream.</returns>
        /// <remarks>
        /// A <c>DELETE</c> that answers 200 rather than 204, because it has something to say:
        /// the difference between a stream that was deleted and one that was not there to
        /// delete is the whole answer, and an empty body cannot say it.
        /// </remarks>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditAudioStreamsPolicy)]
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
            Func<IAudioStreamService, Task<AudioStreamOutcome>> transition)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            return Answer(await transition(_streams));
        }

        /// <summary>Turns <c>asc</c> / <c>desc</c> into an order, or nothing at all.</summary>
        private static AudioStreamSortOrder? ParseSortOrder(string? value)
            => SortOrderParser.Parse(
                value,
                AudioStreamSortOrder.Ascending,
                AudioStreamSortOrder.Descending);

        /// <summary>Turns the name of a column into the field to order by, or nothing at all.</summary>
        /// <remarks>
        /// Compared after lowering, so <c>Name</c> and <c>name</c> are the same request. The
        /// accepted values are the only columns the table can honestly order by: everything
        /// else it might want to show (which source is answering, for one) is a fact a process
        /// reports and is not in the row at all.
        /// </remarks>
        private static AudioStreamSortField? ParseSortField(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "name" => AudioStreamSortField.Name,
                "category" => AudioStreamSortField.Category,
                "status" => AudioStreamSortField.Status,
                "type" => AudioStreamSortField.Type,
                _ => null
            };

        /// <summary>
        /// Which of the bounds a request broke, keyed by the camelCase name of the query
        /// parameter so the reason lands under the input a client would fix.
        /// </summary>
        private static Dictionary<string, string[]> ListErrors(
            int page,
            int pageSize,
            string? search,
            AudioStreamSortField? field,
            AudioStreamSortOrder? order,
            bool categoryOk,
            bool statusOk,
            bool typeOk)
        {
            var errors = ListQueryErrors.Common(
                page,
                pageSize,
                search,
                AudioStreamListLimits.MaxPageSize,
                AudioStreamListLimits.MaxSearchLength);

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
        /// where an <see cref="AudioStreamAction"/> becomes a number.
        /// </summary>
        private static IActionResult Answer(AudioStreamOutcome outcome)
        {
            var (status, body) = AudioStreamResponses.Describe(outcome);

            return new ObjectResult(body) { StatusCode = status };
        }

        private static AdminAudioStreamItemResponse ToItem(AudioStreamListItem item) => new()
        {
            Id = item.Id.ToString(),
            Name = item.Name,
            Status = item.Status.ToString(),
            Type = item.Type.ToString(),
            CategoryId = item.CategoryId.ToString(),
            CategoryName = item.CategoryName,
            LogoUrl = item.LogoUrl,
            ActiveSource = item.ActiveSource
        };

        private static AdminAudioStreamDetailResponse ToDetail(AudioStream stream) => new()
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
            RestreamUserAgent = stream.RestreamUserAgent,
            RestreamProxy = stream.RestreamProxy,
            RestreamCookies = stream.RestreamCookies,
            RestreamHeaders = stream.RestreamHeaders,
            Sources = stream.Sources
        };
    }
}
