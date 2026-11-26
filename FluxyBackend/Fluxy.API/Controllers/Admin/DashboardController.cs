using Fluxy.API.Contracts.Dashboard;
using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.Core.Servers.Interfaces;
using Fluxy.Core.Servers.ReadModels;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Admin
{
    /// <summary>
    /// The admin dashboard: installation KPIs plus one row per server, with live telemetry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One read, and it is a reading rather than a resource.</b> The body is a composition of
    /// several aggregates plus a live probe of every server, so there is no row to address and no
    /// collection to page; <c>GET /admin/dashboard</c> names the screen's own reading and is the
    /// REST shape for "tell me everything this page shows".
    /// </para>
    /// <para>
    /// <b>The gate is the three view grants, all of them.</b> The class carries the admin role like
    /// every admin controller here; the action adds <see cref="AuthenticationExtensions.ViewDashboardPolicy"/>,
    /// which stacks the subscriptions, streams and servers read requirements on one policy. An
    /// operator holding only one or two of them is refused, because this screen draws all three.
    /// </para>
    /// <para>
    /// <b>The answer is private and short-lived.</b> It carries <c>Cache-Control: no-store</c> like
    /// every other read in the admin area; the snapshot behind it is cached for a few seconds in
    /// redis, which is the server's own decision about how fresh the numbers need to be, not a
    /// cache an intermediary may keep.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("admin/dashboard")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Produces("application/json")]
    public sealed class DashboardController : ApiControllerBase
    {
        private readonly IDashboardService _dashboard;

        /// <summary>
        /// Initializes a new instance of the <see cref="DashboardController"/> class.
        /// </summary>
        /// <param name="dashboard">Service that builds the snapshot.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public DashboardController(
            IDashboardService dashboard,
            IAntiforgery antiforgery,
            ILogger<DashboardController> logger)
            : base(antiforgery, logger)
        {
            _dashboard = dashboard;
        }

        /// <summary>
        /// Reads the dashboard: installation KPIs and one row per server.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 with the snapshot, or the policy's own 403.</returns>
        /// <remarks>
        /// The snapshot is cached for a few seconds on the server, so two immediate calls answer
        /// the same numbers and the second performs no probes. <c>generatedAt</c> says how old the
        /// reading is.
        /// </remarks>
        [HttpGet]
        [Authorize(Policy = AuthenticationExtensions.ViewDashboardPolicy)]
        [ProducesResponseType<AdminDashboardResponse>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
        {
            NoStore();

            var snapshot = await _dashboard.GetAsync(cancellationToken);

            return Ok(ToResponse(snapshot));
        }

        private static AdminDashboardResponse ToResponse(DashboardSnapshot snapshot) => new()
        {
            Kpis = new AdminDashboardKpisResponse
            {
                ActiveConnections = snapshot.Kpis.ActiveConnections,
                UsersWithActiveConnections = snapshot.Kpis.UsersWithActiveConnections,
                StreamsRunning = snapshot.Kpis.StreamsRunning,
                StreamsStopped = snapshot.Kpis.StreamsStopped,
                NetworkInMbps = snapshot.Kpis.NetworkInMbps,
                NetworkOutMbps = snapshot.Kpis.NetworkOutMbps
            },
            Servers = snapshot.Servers.Select(ToResponse).ToList(),
            ServersUnavailable = snapshot.ServersUnavailable,
            GeneratedAt = snapshot.GeneratedAt
        };

        private static AdminDashboardServerResponse ToResponse(DashboardServer server) => new()
        {
            Id = server.Id.ToString(),
            Name = server.Name,
            Status = ServerTextParser.NameOf(server.Status),
            Available = server.Available,
            ActiveConnections = server.ActiveConnections,
            PreferredServerUsers = server.PreferredServerUsers,
            UsersWithActiveConnections = server.UsersWithActiveConnections,
            UptimeSeconds = server.UptimeSeconds,
            CpuPercent = server.CpuPercent,
            RamPercent = server.RamPercent,
            DiskPercent = server.DiskPercent,
            NetworkInMbps = server.NetworkInMbps,
            NetworkOutMbps = server.NetworkOutMbps
        };
    }
}
