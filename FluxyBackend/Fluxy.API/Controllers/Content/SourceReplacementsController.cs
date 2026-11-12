using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Content;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Content.Commands;
using Fluxy.Core.Content.Enums;
using Fluxy.Core.Content.Interfaces;
using Fluxy.Core.Content.Outcomes;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// The contract and the outcome deliberately share a name: the wire shape is the plain
// restatement of the domain count, and a second invented name would be a synonym a reader has
// to map. The alias is what keeps the two apart inside this file, where both are named.
using CoreDomainCount = Fluxy.Core.Content.Outcomes.SourceReplacementDomainCount;

namespace Fluxy.API.Controllers.Content
{
    /// <summary>
    /// Mass source replacement: one find string, one replacement, every playback address of
    /// the installation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One route, and it is a <c>POST</c> on the collection - a recorded REST departure.</b>
    /// The operation has no resource to name and no member of a collection to address: it reads
    /// every playback address in four tables and writes a new value over each occurrence of a
    /// substring. <c>PATCH</c> needs a target, and no single target describes a run across four
    /// domains; the only alternatives were four per-domain endpoints the operator would have to
    /// call in a chosen order, which is a client-side transaction with no server-side rule.
    /// Answering <c>POST /admin/source-replacements</c> makes the run itself the thing created,
    /// which is the reading the agreed network-wide operations use.
    /// </para>
    /// <para>
    /// <b>All four edit grants are required at once, and that is the fence.</b> A run changes
    /// playback addresses of video streams, audio streams, on-demand items and episodes; a
    /// caller who may edit one of those but not another would be given a control that writes
    /// rows their grants do not cover. Stacked <c>[Authorize]</c> policies compose as an AND, so
    /// the endpoint answers only an operator holding every one of the four - no new permission,
    /// no catalog change and no migration.
    /// </para>
    /// <para>
    /// <b>The antiforgery pair is checked before the body is read</b>, and before the two
    /// strings are judged, because a request without the pair is not this application's
    /// request at all.
    /// </para>
    /// <para>
    /// <b>A valid run is always a <c>200</c>, even when nothing matched or some entities were
    /// refused.</b> Refusing an entity is not a failed request: every entity that could be
    /// written was written, and the refused ones are named in the answer so the operator knows
    /// which rows still hold the old address. Only the two input strings can make the request
    /// a <c>400</c>, and that is the protocol's usual <c>validation_failed</c> with the field
    /// named.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/source-replacements")]
    [Authorize(Policy = AuthenticationExtensions.EditStreamsPolicy)]
    [Authorize(Policy = AuthenticationExtensions.EditAudioStreamsPolicy)]
    [Authorize(Policy = AuthenticationExtensions.EditVideoOnDemandPolicy)]
    [Authorize(Policy = AuthenticationExtensions.EditSeriesPolicy)]
    [Produces("application/json")]
    public sealed class SourceReplacementsController : ApiControllerBase
    {
        private readonly ISourceReplacementService _replacements;

        /// <summary>
        /// Initializes a new instance of the <see cref="SourceReplacementsController"/> class.
        /// </summary>
        /// <param name="replacements">Service that applies the replacement across all four domains.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the runs worth remembering are reported to.</param>
        public SourceReplacementsController(
            ISourceReplacementService replacements,
            IAntiforgery antiforgery,
            ILogger<SourceReplacementsController> logger)
            : base(antiforgery, logger)
        {
            _replacements = replacements;
        }

        /// <summary>
        /// Replaces every occurrence of the find string in every playback address.
        /// </summary>
        /// <param name="request">The text to find and the text to write over it.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 <c>source_replacement_completed</c> with one count per domain and every refusal,
        /// 400 <c>validation_failed</c> when the two strings cannot be used, 400
        /// <c>csrf_invalid</c> without the antiforgery pair.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <b>The scope is playback addresses only</b> - video and audio stream sources, an
        /// on-demand item's source and an episode's source. Logos, posters, backdrops,
        /// subtitle addresses, stored file paths and the reported <c>activeSource</c> are
        /// deliberately outside it: swapping a host is a question about where a medium is
        /// played from, not about every address a row stores.
        /// </para>
        /// <para>
        /// <b>The replacement is ordinal and case sensitive, and every occurrence is
        /// replaced.</b> Nothing is trimmed on either side; an operator replacing an
        /// authorization fragment is naming characters.
        /// </para>
        /// <para>
        /// <b>A refusal is per entity and all-or-nothing, and the wording of each code is
        /// stable.</b> A replacement that would leave any address of one entity blank, or longer
        /// than its column, leaves that entity exactly as it was - the codes are
        /// <c>source_replacement_blank</c> and <c>source_replacement_too_long</c> - while every
        /// other entity is still written. A blank result is reported first, because it is the
        /// more fundamental problem.
        /// </para>
        /// </remarks>
        [HttpPost]
        [ProducesResponseType<SourceReplacementResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ReplaceSources(
            [FromBody] SourceReplacementRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var outcome = await _replacements.ReplaceAsync(
                new ReplaceSourcesOperation
                {
                    Find = request.Find,
                    Replace = request.Replace ?? string.Empty
                },
                cancellationToken);

            if (!outcome.Accepted)
            {
                // The service reports exactly one or both of the two fields, so the map
                // carries only the ones it reported rather than an empty second key.
                var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

                foreach (var entry in outcome.Errors!)
                {
                    errors[entry.Key] = entry.Value;
                }

                return ApiResults.ValidationFailed(errors);
            }

            Logger.LogInformation(
                "Source replacement completed: {Updated} entity or entities updated, " +
                "{Skipped} candidate address(es) unchanged, {Refused} entity or entities refused.",
                outcome.Updated,
                outcome.Skipped,
                outcome.Refusals.Count);

            return Ok(new SourceReplacementResponse
            {
                Code = SourceReplacementCodes.CompletedCode,
                Message = $"Replaced sources: {outcome.Updated} row(s) updated.",
                Updated = outcome.Updated,
                Skipped = outcome.Skipped,
                Domains = outcome.Domains.Select(ToResponse).ToList(),
                Refusals = outcome.Refusals.Select(ToResponse).ToList()
            });
        }

        /// <summary>Names one domain in the wire spelling a client keys its labels by.</summary>
        private static string WireName(SourceDomain domain) => domain switch
        {
            SourceDomain.VideoStreams => "videoStreams",
            SourceDomain.AudioStreams => "audioStreams",
            SourceDomain.VideoOnDemand => "videoOnDemand",
            SourceDomain.SeriesEpisodes => "seriesEpisodes",

            // Unreachable: the service reports nothing outside this enum. Thrown rather than
            // answered with an invented name, so a new member surfaces here instead of reaching
            // a screen that has no label for it.
            _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, "Unknown source domain.")
        };

        /// <summary>Renders one domain count for the wire.</summary>
        private static Fluxy.API.Contracts.Content.SourceReplacementDomainCount ToResponse(CoreDomainCount count)
            => new()
            {
                Domain = WireName(count.Domain),
                Updated = count.Updated,
                Skipped = count.Skipped
            };

        /// <summary>Renders one refused entity for the wire.</summary>
        private static SourceReplacementRefusalItem ToResponse(SourceReplacementRefusal refusal)
            => new()
            {
                Domain = WireName(refusal.Domain),
                Id = refusal.Id.ToString(),
                Name = refusal.Name,
                Code = refusal.Reason switch
                {
                    SourceRefusalReason.Blank => SourceReplacementCodes.BlankCode,
                    SourceRefusalReason.TooLong => SourceReplacementCodes.TooLongCode,

                    // Unreachable, for the reason the wire-name switch throws.
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(refusal),
                        refusal.Reason,
                        "Unknown source refusal reason.")
                }
            };
    }
}
