using System.Net;
using System.Text;
using Fluxy.Application.Servers.Options;
using Fluxy.Application.Servers.Services;
using Fluxy.Core.Servers.Enums;
using Fluxy.Core.Servers.Models;
using Fluxy.Core.Servers.ReadModels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fluxy.UnitTests.Servers
{
    /// <summary>
    /// The statistics probe's exception matrix: every shape a server can answer with ends in a
    /// sample or in null, and none of them throws.
    /// </summary>
    /// <remarks>
    /// The transport is a stub handler behind a fake factory, so the matrix is the adapter's own
    /// decisions rather than a real socket's timing. The one case that must propagate - the
    /// caller's own cancellation - is included, because the difference between "the server did not
    /// answer" and "we stopped asking" is the whole point of the distinction.
    /// </remarks>
    public sealed class HttpServerStatsProbeTests
    {
        private static readonly Server Subject = new()
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000411"),
            Name = "Edge one",
            Ip = "203.0.113.7",
            Port = 6200,
            ClientCount = 0,
            OsName = "Debian 12",
            NetworkInterface = "eth0",
            Latency = 0,
            Status = ServerStatus.Active,
            GuaranteedSpeedKbps = 0,
            TimeshiftOnly = false,
            Hostname = null,
            VpnIp = null,
            CountryCodes = [],
            IsMain = false,
            CreatedAt = new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero)
        };

        [Fact]
        public async Task AValidBodyBecomesASampleAndCarriesTheBearerToken()
        {
            var handler = new StubHandler(
                HttpStatusCode.OK,
                """
                {
                  "cpuPercent": 12.5,
                  "ramPercent": 44.25,
                  "diskPercent": 61,
                  "uptimeSeconds": 864000,
                  "networkInMbps": 120.5,
                  "networkOutMbps": 90.25
                }
                """);

            var probe = Probe(handler, token: "secret-token");

            var sample = await probe.ProbeAsync(Subject, TestContext.Current.CancellationToken);

            Assert.NotNull(sample);
            Assert.Equal(12.5, sample!.CpuPercent);
            Assert.Equal(44.25, sample.RamPercent);
            Assert.Equal(61, sample.DiskPercent);
            Assert.Equal(864000, sample.UptimeSeconds);
            Assert.Equal(120.5, sample.NetworkInMbps);
            Assert.Equal(90.25, sample.NetworkOutMbps);

            Assert.Equal("http://203.0.113.7:6200/api/stats", handler.LastRequestUri?.ToString());
            Assert.Equal("Bearer", handler.LastAuthorizationScheme);
            Assert.Equal("secret-token", handler.LastAuthorizationParameter);
        }

        [Fact]
        public async Task NoConfiguredTokenMeansNoAuthorizationHeader()
        {
            var handler = new StubHandler(HttpStatusCode.OK, ValidBody());

            await Probe(handler, token: null)
                .ProbeAsync(Subject, TestContext.Current.CancellationToken);

            Assert.Null(handler.LastAuthorizationScheme);
        }

        [Fact]
        public async Task ANonSuccessAnswerIsUnavailable()
        {
            var handler = new StubHandler(HttpStatusCode.InternalServerError, "boom");

            Assert.Null(await Probe(handler).ProbeAsync(
                Subject,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task ABodyThatIsNotJsonIsUnavailable()
        {
            var handler = new StubHandler(HttpStatusCode.OK, "not json");

            Assert.Null(await Probe(handler).ProbeAsync(
                Subject,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task AnEmptyObjectIsUnavailable()
        {
            var handler = new StubHandler(HttpStatusCode.OK, "{}");

            Assert.Null(await Probe(handler).ProbeAsync(
                Subject,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task AFieldThatIsNotAFiniteNumberIsUnavailable()
        {
            // `1e999` is a number JSON can spell but a double cannot hold - it reads as infinity,
            // which is not a measurement. The answer is the same one every other unusable body
            // gets; a parser that refuses it outright lands in the same place.
            var handler = new StubHandler(
                HttpStatusCode.OK,
                ValidBody().Replace("\"cpuPercent\": 12.5", "\"cpuPercent\": 1e999"));

            Assert.Null(await Probe(handler).ProbeAsync(
                Subject,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task AMissingFieldIsUnavailable()
        {
            var handler = new StubHandler(
                HttpStatusCode.OK,
                """
                {
                  "cpuPercent": 12.5,
                  "diskPercent": 61,
                  "uptimeSeconds": 864000,
                  "networkInMbps": 120.5,
                  "networkOutMbps": 90.25
                }
                """);

            Assert.Null(await Probe(handler).ProbeAsync(
                Subject,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task AnUnreachableServerIsUnavailableRatherThanAnException()
        {
            var handler = new StubHandler(
                new HttpRequestException("The connection was refused."));

            Assert.Null(await Probe(handler).ProbeAsync(
                Subject,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task AClientTimeoutIsUnavailableRatherThanAnException()
        {
            var handler = new StubHandler(
                new TaskCanceledException("The request timed out."));

            Assert.Null(await Probe(handler).ProbeAsync(
                Subject,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task AMalformedAddressIsUnavailableWithoutARequest()
        {
            var handler = new StubHandler(HttpStatusCode.OK, ValidBody());
            var server = Subject with { Ip = "not-an-address" };

            Assert.Null(await Probe(handler).ProbeAsync(
                server,
                TestContext.Current.CancellationToken));

            Assert.Equal(0, handler.Calls);
        }

        [Fact]
        public async Task TheCallersOwnCancellationPropagates()
        {
            var handler = new StubHandler(HttpStatusCode.OK, ValidBody());
            using var cancelled = new CancellationTokenSource();
            await cancelled.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                Probe(handler).ProbeAsync(Subject, cancelled.Token));
        }

        private static string ValidBody() =>
            """
            {
              "cpuPercent": 12.5,
              "ramPercent": 44.25,
              "diskPercent": 61,
              "uptimeSeconds": 864000,
              "networkInMbps": 120.5,
              "networkOutMbps": 90.25
            }
            """;

        private static HttpServerStatsProbe Probe(StubHandler handler, string? token = null)
            => new(
                new StubFactory(handler),
                new StubOptions(token),
                NullLogger<HttpServerStatsProbe>.Instance);

        /// <summary>A client factory that hands over one client over the stub handler.</summary>
        private sealed class StubFactory : IHttpClientFactory
        {
            private readonly StubHandler _handler;

            public StubFactory(StubHandler handler)
            {
                _handler = handler;
            }

            public HttpClient CreateClient(string name)
                => new(_handler, disposeHandler: false);
        }

        /// <summary>Options carrying one token, or none.</summary>
        private sealed class StubOptions : IOptionsMonitor<ServerStatsOptions>
        {
            public StubOptions(string? token)
            {
                CurrentValue = new ServerStatsOptions { StatsToken = token };
            }

            public ServerStatsOptions CurrentValue { get; }

            public ServerStatsOptions Get(string? name) => CurrentValue;

            public IDisposable? OnChange(Action<ServerStatsOptions, string?> listener) => null;
        }

        /// <summary>
        /// Answers with a body, a status or an exception, and records what the probe asked for.
        /// </summary>
        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode? _status;
            private readonly string? _body;
            private readonly Exception? _failure;

            public StubHandler(HttpStatusCode status, string body)
            {
                _status = status;
                _body = body;
            }

            public StubHandler(Exception failure)
            {
                _failure = failure;
            }

            public int Calls { get; private set; }

            public Uri? LastRequestUri { get; private set; }

            public string? LastAuthorizationScheme { get; private set; }

            public string? LastAuthorizationParameter { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                Calls++;
                LastRequestUri = request.RequestUri;
                LastAuthorizationScheme = request.Headers.Authorization?.Scheme;
                LastAuthorizationParameter = request.Headers.Authorization?.Parameter;

                if (_failure is not null)
                {
                    throw _failure;
                }

                return Task.FromResult(new HttpResponseMessage(_status!.Value)
                {
                    Content = new StringContent(_body ?? string.Empty, Encoding.UTF8, "application/json")
                });
            }
        }
    }
}
