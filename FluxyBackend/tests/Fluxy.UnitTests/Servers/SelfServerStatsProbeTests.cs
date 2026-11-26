using Fluxy.Application.Servers.Services;
using Fluxy.Application.Servers.Utils;
using Fluxy.Core.Servers.Enums;
using Fluxy.Core.Servers.Interfaces;
using Fluxy.Core.Servers.Models;
using Fluxy.Core.Servers.ReadModels;
using Fluxy.Core.Servers.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Fluxy.UnitTests.Servers
{
    /// <summary>
    /// The decorator's two rules: the main identifier is answered locally, and every other
    /// identifier - and every main reading that could not be taken - is the remote probe's answer.
    /// </summary>
    /// <remarks>
    /// The sampler is the real one with a declared reader, so every raw fact is a queue of
    /// readings rather than a machine. The remote probe counts its calls, which is how "the
    /// delegate was not asked" is proved rather than inferred.
    /// </remarks>
    public sealed class SelfServerStatsProbeTests
    {
        private static readonly DateTimeOffset Start =
            new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public async Task TheMainIdentifierIsAnsweredByTheSamplerAndTheDelegateIsNotAsked()
        {
            // A cold start: the sampler measures its own short window and answers immediately, so
            // the very first dashboard build already gets a sample.
            var sampler = SamplerOver(
                Reading(Start, processorSeconds: 1, received: 0, sent: 0),
                Reading(
                    Start + SelfServerStatsSampler.MinimumWindow,
                    processorSeconds: 1.25,
                    received: 1_000_000,
                    sent: 500_000));

            var remote = new CountingProbe();
            var probe = new SelfServerStatsProbe(remote, sampler, NullLogger<SelfServerStatsProbe>.Instance);

            var sample = await probe.ProbeAsync(
                Row(MainServer.Id),
                TestContext.Current.CancellationToken);

            Assert.NotNull(sample);
            Assert.Equal(1_000_000 * 8d / 0.25 / 1_000_000, sample!.NetworkInMbps, precision: 9);
            Assert.Equal(500_000 * 8d / 0.25 / 1_000_000, sample.NetworkOutMbps, precision: 9);
            Assert.Equal(0, remote.Calls);
        }

        [Fact]
        public async Task AFailedLocalReadFallsThroughToTheDelegateAndItsAnswerIsReturned()
        {
            // The reader answers nothing, which is what a platform that refuses every fact looks
            // like.
            var sampler = SamplerOver();

            var remote = new CountingProbe
            {
                Result = new ServerStatsSample
                {
                    CpuPercent = 1,
                    RamPercent = 2,
                    DiskPercent = 3,
                    UptimeSeconds = 4,
                    NetworkInMbps = 5,
                    NetworkOutMbps = 6
                }
            };

            var probe = new SelfServerStatsProbe(remote, sampler, NullLogger<SelfServerStatsProbe>.Instance);

            var sample = await probe.ProbeAsync(
                Row(MainServer.Id),
                TestContext.Current.CancellationToken);

            Assert.Equal(1, remote.Calls);
            Assert.Same(remote.Result, sample);
        }

        [Fact]
        public async Task AFailedLocalReadWithNoRemoteAnswerIsUnavailableNotZero()
        {
            var sampler = SamplerOver();

            var remote = new CountingProbe();
            var probe = new SelfServerStatsProbe(remote, sampler, NullLogger<SelfServerStatsProbe>.Instance);

            var sample = await probe.ProbeAsync(
                Row(MainServer.Id),
                TestContext.Current.CancellationToken);

            Assert.Equal(1, remote.Calls);
            Assert.Null(sample);
        }

        [Fact]
        public async Task ASamplerThatThrowsFallsThroughToTheDelegateInsteadOfFailingTheRequest()
        {
            var sampler = new SelfServerStatsSampler(
                new FakeTimeProvider(Start),
                () => throw new InvalidOperationException("the operating system refused a fact"));

            var remote = new CountingProbe
            {
                Result = new ServerStatsSample
                {
                    CpuPercent = 1,
                    RamPercent = 2,
                    DiskPercent = 3,
                    UptimeSeconds = 4,
                    NetworkInMbps = 5,
                    NetworkOutMbps = 6
                }
            };

            var probe = new SelfServerStatsProbe(remote, sampler, NullLogger<SelfServerStatsProbe>.Instance);

            var sample = await probe.ProbeAsync(
                Row(MainServer.Id),
                TestContext.Current.CancellationToken);

            Assert.Equal(1, remote.Calls);
            Assert.Same(remote.Result, sample);
        }

        [Fact]
        public async Task ACancelledLocalReadAnswersUnavailableForThatRequestOnly()
        {
            var sampler = SamplerOver(
                Reading(Start, processorSeconds: 1, received: 0, sent: 0),
                Reading(
                    Start + SelfServerStatsSampler.MinimumWindow,
                    processorSeconds: 1.25,
                    received: 1_000_000,
                    sent: 0));

            var remote = new CountingProbe
            {
                Result = new ServerStatsSample
                {
                    CpuPercent = 1,
                    RamPercent = 2,
                    DiskPercent = 3,
                    UptimeSeconds = 4,
                    NetworkInMbps = 5,
                    NetworkOutMbps = 6
                }
            };

            var probe = new SelfServerStatsProbe(remote, sampler, NullLogger<SelfServerStatsProbe>.Instance);

            using var cancelled = new CancellationTokenSource();

            cancelled.Cancel();

            var sample = await probe.ProbeAsync(Row(MainServer.Id), cancelled.Token);

            // A cancelled request is not a machine failure: no fallback to the remote probe, and
            // no exception into the dashboard.
            Assert.Null(sample);
            Assert.Equal(0, remote.Calls);

            // The next request is unaffected.
            Assert.NotNull(await probe.ProbeAsync(Row(MainServer.Id), TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task ANonMainIdentifierIsTheDelegateOnly()
        {
            var sampler = SamplerOver(
                Reading(Start, processorSeconds: 1, received: 0, sent: 0),
                Reading(
                    Start + SelfServerStatsSampler.MinimumWindow,
                    processorSeconds: 1.25,
                    received: 1_000_000,
                    sent: 0));

            var remote = new CountingProbe
            {
                Result = new ServerStatsSample
                {
                    CpuPercent = 12,
                    RamPercent = 34,
                    DiskPercent = 56,
                    UptimeSeconds = 78,
                    NetworkInMbps = 90,
                    NetworkOutMbps = 10
                }
            };

            var probe = new SelfServerStatsProbe(remote, sampler, NullLogger<SelfServerStatsProbe>.Instance);

            var sample = await probe.ProbeAsync(
                Row(Guid.NewGuid()),
                TestContext.Current.CancellationToken);

            Assert.Equal(1, remote.Calls);
            Assert.Same(remote.Result, sample);
        }

        [Fact]
        public async Task TheStoredFlagDoesNotDecideTheMainRow()
        {
            // A row carrying the main flag but an ordinary identifier is still a remote server;
            // the identifier is the whole rule.
            var sampler = SamplerOver(
                Reading(Start, processorSeconds: 1, received: 0, sent: 0),
                Reading(
                    Start + SelfServerStatsSampler.MinimumWindow,
                    processorSeconds: 1.25,
                    received: 1_000_000,
                    sent: 0));

            var remote = new CountingProbe();
            var probe = new SelfServerStatsProbe(remote, sampler, NullLogger<SelfServerStatsProbe>.Instance);

            await probe.ProbeAsync(
                Row(Guid.NewGuid()) with { IsMain = true },
                TestContext.Current.CancellationToken);

            Assert.Equal(1, remote.Calls);
        }

        [Fact]
        public async Task AMainRowMarkedDisabledIsStillAnsweredLocally()
        {
            var sampler = SamplerOver(
                Reading(Start, processorSeconds: 1, received: 0, sent: 0),
                Reading(
                    Start + SelfServerStatsSampler.MinimumWindow,
                    processorSeconds: 1.25,
                    received: 1_000_000,
                    sent: 0));

            var remote = new CountingProbe();
            var probe = new SelfServerStatsProbe(remote, sampler, NullLogger<SelfServerStatsProbe>.Instance);

            var sample = await probe.ProbeAsync(
                Row(MainServer.Id) with { Status = ServerStatus.Disabled },
                TestContext.Current.CancellationToken);

            Assert.NotNull(sample);
            Assert.Equal(0, remote.Calls);
        }

        private static SelfServerStatsSampler SamplerOver(params SelfStatsReading?[] readings)
        {
            var queue = new Queue<SelfStatsReading?>(readings);

            return new SelfServerStatsSampler(
                new FakeTimeProvider(Start),
                () => queue.Count == 0 ? null : queue.Dequeue());
        }

        private static SelfStatsReading Reading(
            DateTimeOffset capturedAt,
            double processorSeconds,
            long received,
            long sent)
            => new()
            {
                CapturedAt = capturedAt,
                ProcessorTime = TimeSpan.FromSeconds(processorSeconds),
                WorkingSetBytes = 100,
                AvailableMemoryBytes = 1_000,
                DiskUsedBytes = 100,
                DiskTotalBytes = 200,
                StartedAt = capturedAt.AddSeconds(-900),
                BytesReceived = received,
                BytesSent = sent
            };

        private static Server Row(Guid id) => new()
        {
            Id = id,
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
            CreatedAt = Start
        };

        /// <summary>The remote probe: answers a declared sample and counts how often it was asked.</summary>
        private sealed class CountingProbe : IServerStatsProbe
        {
            public ServerStatsSample? Result { get; init; }

            public int Calls { get; private set; }

            public Task<ServerStatsSample?> ProbeAsync(
                Server server,
                CancellationToken cancellationToken = default)
            {
                Calls++;

                return Task.FromResult(Result);
            }
        }
    }
}
