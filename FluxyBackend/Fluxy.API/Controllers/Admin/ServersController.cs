using Fluxy.API.Contracts.Common;
using Fluxy.API.Contracts.Servers;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Servers.Commands;
using Fluxy.Core.Servers.Enums;
using Fluxy.Core.Servers.Interfaces;
using Fluxy.Core.Servers.Outcomes;
using Fluxy.Core.Servers.ReadModels;
using Fluxy.Core.Servers.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Admin
{
    /// <summary>
    /// Managing the servers this installation relays through: one page of the table, one server
    /// in full, and the writes that create, change and delete them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two policies on every action: the class carries <c>AdminOnly</c> the way the other admin
    /// controllers do, and each action adds its own permission on top. Read and write are
    /// separate grants, so a group which may look at the servers and a group which may change
    /// them stay two different things.
    /// </para>
    /// <para>
    /// <b>No captcha and no attempt window</b>, for the reason the rest of the admin area gives:
    /// both guard a request with no session at all, and everything here is behind an admin
    /// session and a fresh antiforgery pair, which <see cref="ApiControllerBase.RejectsCsrfAsync"/>
    /// checks before any body is read.
    /// </para>
    /// <para>
    /// <b>The main row is editable and not deletable.</b> This is a deliberate departure from
    /// "every row a <c>DELETE</c> addresses can go": the installation's own address is what every
    /// other row is an addition to, so removing it would leave the installation with no server it
    /// can call its own. The refusal is <c>409 server_immutable</c> from <c>DELETE {id}</c>; the
    /// row accepts every field write <c>PATCH {id}</c> carries, because an operator may rename it
    /// or change its port like any other. <c>isMain</c> is the seeder's answer and never appears
    /// in a request.
    /// </para>
    /// <para>
    /// <b>The enum fields travel as the member's name</b> (<c>active</c>, <c>disabled</c>),
    /// the convention the streams use, and the controller parses them through
    /// <see cref="ServerTextParser"/>, which refuses a value that names no member under the field
    /// it came in.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/servers")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class ServersController : ApiControllerBase
    {
        /// <summary>Sentence a column that names nothing gets.</summary>
        private const string SortByHint =
            "The sort field must be one of: name, status, latency, hostname, ip, clientcount, createdat.";

        /// <summary>Sentence a direction that names nothing gets.</summary>
        private const string SortOrderHint = "The sort order must be either asc or desc.";

        /// <summary>Sentence a state that names nothing gets.</summary>
        private const string StatusHint = "The status must be either active or disabled.";

        private readonly IServerService _servers;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServersController"/> class.
        /// </summary>
        /// <param name="servers">Service that reads and changes the servers.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public ServersController(
            IServerService servers,
            IAntiforgery antiforgery,
            ILogger<ServersController> logger)
            : base(antiforgery, logger)
        {
            _servers = servers;
        }

        /// <summary>
        /// Reports one page of the table, filtered and ordered the way the query asked.
        /// </summary>
        /// <remarks>
        /// Search, order and paging live here rather than in the browser because the pager shows
        /// a <c>total</c>. The bounds are refused rather than clamped, and a value that names
        /// nothing is refused rather than treated as "no filter".
        /// </remarks>
        [HttpGet]
        [Authorize(Policy = AuthenticationExtensions.ViewServersPolicy)]
        [ProducesResponseType<AdminServerPageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetServers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = ServerListLimits.DefaultPageSize,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = "name",
            [FromQuery] string? sortOrder = "asc",
            CancellationToken cancellationToken = default)
        {
            NoStore();

            var field = ServerTextParser.ParseSortField(sortBy);
            var order = ServerTextParser.ParseSortOrder(sortOrder);

            if (page < 1
                || pageSize is < 1 or > ServerListLimits.MaxPageSize
                || (search?.Length ?? 0) > ServerListLimits.MaxSearchLength
                || field is null
                || order is null)
            {
                return ApiResults.ValidationFailed(ListErrors(page, pageSize, search, field, order));
            }

            var list = await _servers.GetPageAsync(
                page,
                pageSize,
                search,
                field.Value,
                order.Value,
                cancellationToken);

            return Ok(new AdminServerPageResponse
            {
                Items = list.Items.Select(ToResponse).ToList(),
                Total = list.Total,
                Page = list.Page,
                PageSize = list.PageSize
            });
        }

        /// <summary>Reports one server, for the edit form.</summary>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.ViewServersPolicy)]
        [ProducesResponseType<AdminServerResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetServer(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            NoStore();

            var server = await _servers.GetAsync(id, cancellationToken);

            return server is null
                ? Answer(new ServerOutcome
                {
                    Action = ServerAction.NotFound,
                    ServerId = id
                })
                : Ok(ToResponse(server));
        }

        /// <summary>Creates a server.</summary>
        /// <remarks>
        /// The 201 carries a <c>Location</c>, and it is honest: the row exists in full the moment
        /// it is written and this very controller serves the address it names.
        /// </remarks>
        [HttpPost]
        [Authorize(Policy = AuthenticationExtensions.EditServersPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateServer(
            [FromBody] CreateServerRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var status = ServerTextParser.ParseStatus(request.Status);
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (!string.IsNullOrWhiteSpace(request.Status) && status is null)
            {
                errors[nameof(CreateServerRequest.Status)] = [StatusHint];
            }

            if (errors.Count > 0)
            {
                return ApiResults.ValidationFailed(errors);
            }

            var outcome = await _servers.CreateAsync(
                new NewServer
                {
                    Name = request.Name ?? string.Empty,
                    Ip = request.Ip ?? string.Empty,
                    Port = request.Port ?? 0,
                    OsName = request.OsName ?? string.Empty,
                    NetworkInterface = request.NetworkInterface ?? string.Empty,
                    ClientCount = request.ClientCount,
                    Status = status,
                    GuaranteedSpeedKbps = request.GuaranteedSpeedKbps,
                    TimeshiftOnly = request.TimeshiftOnly ?? false,
                    Hostname = request.Hostname,
                    VpnIp = request.VpnIp,
                    CountryCodes = request.CountryCodes
                },
                cancellationToken);

            var (statusCode, body) = ServerResponses.Describe(outcome);

            if (statusCode == StatusCodes.Status201Created)
            {
                Response.Headers.Location = $"/admin/servers/{outcome.ServerId}";
            }

            return StatusCode(statusCode, body);
        }

        /// <summary>
        /// Writes the whole server. The main row is an ordinary row here - it accepts every
        /// field - and only its deletion is refused.
        /// </summary>
        /// <remarks>
        /// The patch is the whole row, so a property the body omits is not "keep the stored
        /// value": an absent text is refused as the blank it becomes, an absent
        /// <c>clientCount</c> is the <c>0</c> that means unlimited, an absent
        /// <c>timeshiftOnly</c> is <c>false</c>, and an absent or unknown <c>status</c> is a
        /// refusal under that field.
        /// </remarks>
        [HttpPatch("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditServersPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateServer(
            [FromRoute] Guid id,
            [FromBody] UpdateServerRequest request,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            var status = ServerTextParser.ParseStatus(request.Status);

            if (status is null)
            {
                return ApiResults.ValidationFailed(
                    new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        [nameof(UpdateServerRequest.Status)] = [StatusHint]
                    });
            }

            return Answer(await _servers.UpdateAsync(
                id,
                new ServerPatch
                {
                    Name = request.Name ?? string.Empty,
                    Ip = request.Ip ?? string.Empty,
                    Port = request.Port ?? 0,
                    ClientCount = request.ClientCount ?? ServerLimits.ClientCountMin,
                    OsName = request.OsName ?? string.Empty,
                    NetworkInterface = request.NetworkInterface ?? string.Empty,
                    Latency = request.Latency ?? ServerLimits.LatencyMin,
                    Status = status.Value,
                    GuaranteedSpeedKbps =
                        request.GuaranteedSpeedKbps ?? ServerLimits.GuaranteedSpeedMinKbps,
                    TimeshiftOnly = request.TimeshiftOnly ?? false,
                    Hostname = request.Hostname,
                    VpnIp = request.VpnIp,
                    CountryCodes = request.CountryCodes ?? []
                },
                cancellationToken));
        }

        /// <summary>Deletes a server that is not the main one.</summary>
        /// <remarks>
        /// A <c>DELETE</c> that answers 200 rather than 204, because it has something to say:
        /// the difference between a server that was deleted and one that was not there is the
        /// whole answer, and the main row answers a 409 naming the refusal instead of
        /// disappearing. The existence check runs first, so a missing identifier answers
        /// <c>404 server_not_found</c> rather than claiming it is the main server.
        /// </remarks>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = AuthenticationExtensions.EditServersPolicy)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeleteServer(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            return Answer(await _servers.DeleteAsync(id, cancellationToken));
        }

        /// <summary>
        /// Which of the bounds a list request broke, keyed by the camelCase name of the query
        /// parameter so the reason lands under the input a client would fix.
        /// </summary>
        private static Dictionary<string, string[]> ListErrors(
            int page,
            int pageSize,
            string? search,
            ServerSortField? field,
            ServerSortOrder? order)
        {
            var errors = ListQueryErrors.Common(
                page,
                pageSize,
                search,
                ServerListLimits.MaxPageSize,
                ServerListLimits.MaxSearchLength);

            if (field is null)
            {
                errors["sortBy"] = [SortByHint];
            }

            if (order is null)
            {
                errors["sortOrder"] = [SortOrderHint];
            }

            return errors;
        }

        /// <summary>
        /// Turns the service's answer into the status and body a client is told, the one place
        /// where a <see cref="ServerAction"/> becomes a number.
        /// </summary>
        private static IActionResult Answer(ServerOutcome outcome)
        {
            var (status, body) = ServerResponses.Describe(outcome);

            return new ObjectResult(body) { StatusCode = status };
        }

        /// <summary>Turns a read model into the shape the API answers with.</summary>
        private static AdminServerResponse ToResponse(ServerView server) => new()
        {
            Id = server.Id.ToString(),
            Name = server.Name,
            Ip = server.Ip,
            Port = server.Port,
            ClientCount = server.ClientCount,
            OsName = server.OsName,
            NetworkInterface = server.NetworkInterface,
            Latency = server.Latency,
            Status = ServerTextParser.NameOf(server.Status),
            GuaranteedSpeedKbps = server.GuaranteedSpeedKbps,
            TimeshiftOnly = server.TimeshiftOnly,
            Hostname = server.Hostname,
            VpnIp = server.VpnIp,
            CountryCodes = server.CountryCodes,
            IsMain = server.IsMain,
            CreatedAt = server.CreatedAt
        };
    }
}
