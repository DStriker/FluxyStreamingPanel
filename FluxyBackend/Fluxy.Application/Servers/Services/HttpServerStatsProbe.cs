using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Fluxy.Application.Servers.Options;
using Fluxy.Application.Servers.Utils;
using Fluxy.Core.Servers.Interfaces;
using Fluxy.Core.Servers.Models;
using Fluxy.Core.Servers.ReadModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fluxy.Application.Servers.Services
{
    /// <summary>
    /// Reads a server's own statistics over HTTP, and treats every failure as "unavailable".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The payload is an assumption (A1), and this is the only type that knows it.</b> The
    /// contract is <c>GET http://{ip}:{port}/api/stats</c> with
    /// <c>Authorization: Bearer &lt;Servers:StatsToken&gt;</c> when a token is configured, a two
    /// second timeout, and a body of six numbers. When the real agent protocol arrives it replaces
    /// this adapter and <see cref="StatsPayload"/> - no service, read model or endpoint changes.
    /// </para>
    /// <para>
    /// <b>A server that cannot be read is a fact, not a failure.</b> A malformed address, a
    /// non-2xx answer, a body that is not JSON, a missing field or a non-finite number all answer
    /// null. The dashboard draws those rows as unavailable with zeros. Throwing would turn one
    /// unreachable machine into a 500 for the whole screen.
    /// </para>
    /// <para>
    /// <b>The caller's token is the only cancellation that propagates.</b> The client's own timeout
    /// surfaces as a <see cref="TaskCanceledException"/> whose token is not the caller's, and that
    /// is an unavailable server like every other. Rethrowing it would make a slow server look like
    /// a cancelled request, and the two are different facts.
    /// </para>
    /// </remarks>
    public sealed class HttpServerStatsProbe : IServerStatsProbe
    {
        /// <summary>How long one server is given to answer.</summary>
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptionsMonitor<ServerStatsOptions> _options;
        private readonly ILogger<HttpServerStatsProbe> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpServerStatsProbe"/> class.
        /// </summary>
        /// <param name="httpClientFactory">Factory producing the client used for the probe.</param>
        /// <param name="options">Live options carrying the bearer token, if any.</param>
        /// <param name="logger">Logger failed probes are reported to.</param>
        public HttpServerStatsProbe(
            IHttpClientFactory httpClientFactory,
            IOptionsMonitor<ServerStatsOptions> options,
            ILogger<HttpServerStatsProbe> logger)
        {
            _httpClientFactory = httpClientFactory;
            _options = options;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<ServerStatsSample?> ProbeAsync(
            Server server,
            CancellationToken cancellationToken = default)
        {
            if (!ServerStatsUri.TryBuild(server.Ip, server.Port, out var uri))
            {
                _logger.LogWarning(
                    "The statistics of server {ServerId} ('{ServerName}') cannot be read: " +
                    "'{Ip}' is not an address or '{Port}' is not a port.",
                    server.Id,
                    server.Name,
                    server.Ip,
                    server.Port);

                return null;
            }

            try
            {
                var client = _httpClientFactory.CreateClient(nameof(HttpServerStatsProbe));

                using var request = new HttpRequestMessage(HttpMethod.Get, uri);

                var token = _options.CurrentValue.StatsToken;
                if (!string.IsNullOrWhiteSpace(token))
                {
                    request.Headers.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                // The named client is registered with the same two-second timeout. This linked
                // source restates it for the client a test or a future composition hands over,
                // and it is what lets the two cancellations be told apart below: the caller's
                // token is the one on `cancellationToken`, the timer's is not.
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(Timeout);

                using var response = await client.SendAsync(request, timeout.Token);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "The statistics of server {ServerId} ('{ServerName}') answered {StatusCode}, " +
                        "so the server is reported as unavailable.",
                        server.Id,
                        server.Name,
                        (int)response.StatusCode);

                    return null;
                }

                var payload = await response.Content.ReadFromJsonAsync<StatsPayload>(
                    cancellationToken: timeout.Token);

                return ToSample(server, payload);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The caller gave up: that is not the server failing, and pretending it is would
                // hide a cancelled request as an unavailable machine.
                throw;
            }
            catch (Exception exception)
            {
                // Every other failure is the server's answer to "how are you": it did not give
                // one. The message names what happened; the caller draws zeros.
                _logger.LogWarning(
                    exception,
                    "The statistics of server {ServerId} ('{ServerName}') could not be read, so " +
                    "the server is reported as unavailable.",
                    server.Id,
                    server.Name);

                return null;
            }
        }

        /// <summary>
        /// Turns a payload into a sample, or null when any field is absent or not a finite number.
        /// </summary>
        /// <remarks>
        /// All six fields are required by the contract: a partial reading would let the dashboard
        /// draw a zero that means "the server said nothing" as if it meant "the machine is idle".
        /// <c>NaN</c> and the infinities are refused for the same reason - they are not
        /// measurements, and they would poison every sum they reached.
        /// </remarks>
        private ServerStatsSample? ToSample(Server server, StatsPayload? payload)
        {
            if (payload is null
                || !IsFinite(payload.CpuPercent)
                || !IsFinite(payload.RamPercent)
                || !IsFinite(payload.DiskPercent)
                || payload.UptimeSeconds is not { } uptime
                || !IsFinite(payload.NetworkInMbps)
                || !IsFinite(payload.NetworkOutMbps))
            {
                _logger.LogWarning(
                    "The statistics of server {ServerId} ('{ServerName}') were incomplete or not " +
                    "finite numbers, so the server is reported as unavailable.",
                    server.Id,
                    server.Name);

                return null;
            }

            return new ServerStatsSample
            {
                CpuPercent = payload.CpuPercent!.Value,
                RamPercent = payload.RamPercent!.Value,
                DiskPercent = payload.DiskPercent!.Value,
                UptimeSeconds = uptime,
                NetworkInMbps = payload.NetworkInMbps!.Value,
                NetworkOutMbps = payload.NetworkOutMbps!.Value
            };
        }

        private static bool IsFinite(double? value)
            => value is { } number && double.IsFinite(number);

        /// <summary>
        /// The body a server's statistics endpoint answers with, as this pass assumes it (A1).
        /// </summary>
        /// <remarks>
        /// Every field is nullable so a body that omits one is answered as an incomplete reading
        /// rather than as a deserialization failure - the caller treats both the same way, but the
        /// log says which happened. The type is private because nothing outside this adapter may
        /// depend on the wire shape of an assumption.
        /// </remarks>
        private sealed class StatsPayload
        {
            [JsonPropertyName("cpuPercent")]
            public double? CpuPercent { get; init; }

            [JsonPropertyName("ramPercent")]
            public double? RamPercent { get; init; }

            [JsonPropertyName("diskPercent")]
            public double? DiskPercent { get; init; }

            [JsonPropertyName("uptimeSeconds")]
            public long? UptimeSeconds { get; init; }

            [JsonPropertyName("networkInMbps")]
            public double? NetworkInMbps { get; init; }

            [JsonPropertyName("networkOutMbps")]
            public double? NetworkOutMbps { get; init; }
        }
    }
}
