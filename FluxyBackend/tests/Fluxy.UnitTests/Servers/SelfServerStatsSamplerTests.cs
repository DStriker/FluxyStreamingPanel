using Fluxy.Application.Servers.Utils;
using Microsoft.Extensions.Time.Testing;

namespace Fluxy.UnitTests.Servers
{
    /// <summary>
    /// The sampler's window policy: a cold start answers over a short window, a stale reading is
    /// replaced rather than averaged over, a burst of callers pays one wait, and a cancellation
    /// answers null.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reader is a declared function, so every raw fact is the test's. The arithmetic itself is
    /// proved in <see cref="SelfServerStatsMathTests"/>; this file proves when the sampler takes a
    /// fresh window instead of using the gap, and how it behaves under concurrency.
    /// </para>
    /// <para>
    /// The internal wait is real elapsed time (<see cref="Task.Delay(TimeSpan)"/>), so a cold-start
    /// case costs the 250 ms it documents. The injected clock dates the readings and expires the
    /// memo; a test that needs a second computed sample advances it past
    /// <see cref="SelfServerStatsSampler.MinimumWindow"/>.
    /// </para>
    /// </remarks>
    public sealed class SelfServerStatsSamplerTests
    {
        private static readonly DateTimeOffset Start =
            new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public async Task TheFirstCallAfterStartAnswersOverAShortWindow()
        {
            // A cold process has no previous reading, so the sampler measures its own window: 250 ms
            // of wall clock, one million bytes, a quarter of a processor-second.
            var sampler = new SelfServerStatsSampler(
                Clock(),
                Queue(
                    Reading(Start, processorSeconds: 1, received: 0, sent: 0),
                    Reading(
                        Start + SelfServerStatsSampler.MinimumWindow,
                        processorSeconds: 1.25,
                        received: 1_000_000,
                        sent: 500_000)));

            var sample = await sampler.ReadAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(sample);
            Assert.Equal(1000000 * 8d / 0.25 / 1_000_000, sample!.NetworkInMbps, precision: 9);
            Assert.Equal(500000 * 8d / 0.25 / 1_000_000, sample.NetworkOutMbps, precision: 9);
            Assert.Equal(10, sample.RamPercent);
            Assert.Equal(50, sample.DiskPercent);
            Assert.Equal(900, sample.UptimeSeconds);

            // A quarter of a processor-second over a quarter-second window is one busy core, so
            // the percentage of the whole machine is 100 divided by the logical processor count.
            Assert.Equal(100d / Environment.ProcessorCount, sample.CpuPercent, precision: 6);
        }

        [Fact]
        public async Task AReadingOlderThanTheMaximumWindowIsReplacedRatherThanAveragedOverTheGap()
        {
            var clock = Clock();

            var sampler = new SelfServerStatsSampler(
                clock,
                Queue(
                    // The cold start's short window.
                    Reading(Start, processorSeconds: 1, received: 0, sent: 0),
                    Reading(Start.AddMilliseconds(250), processorSeconds: 1.25, received: 1_000, sent: 0),
                    // The reading taken after the idle gap: the counters moved a lot over the gap,
                    // which must not be what the sample reports.
                    Reading(Start.AddMinutes(30), processorSeconds: 31, received: 300_000_000, sent: 0),
                    // The second reading of the fresh window.
                    Reading(Start.AddMinutes(30).AddMilliseconds(250), processorSeconds: 31.25, received: 301_000_000, sent: 0)));

            var cold = await sampler.ReadAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(cold);

            // 1000 bytes over the cold 250 ms window.
            Assert.Equal(1_000 * 8d / 0.25 / 1_000_000, cold!.NetworkInMbps, precision: 9);

            clock.Advance(TimeSpan.FromMinutes(30));

            var afterIdle = await sampler.ReadAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(afterIdle);

            // One million bytes over the fresh 250 ms window is 32 Mbps. Averaging over the
            // thirty-minute gap would have read 0.00444 Mbps instead, so this asserts the guard.
            Assert.Equal(32, afterIdle!.NetworkInMbps, precision: 9);
            Assert.NotEqual(1_000_000 * 8d / 1800 / 1_000_000, afterIdle.NetworkInMbps, precision: 6);
        }

        [Fact]
        public async Task AGapInsideTheBoundsIsUsedAsTheNaturalWindow()
        {
            var clock = Clock();

            var sampler = new SelfServerStatsSampler(
                clock,
                Queue(
                    Reading(Start, processorSeconds: 1, received: 0, sent: 0),
                    Reading(Start.AddMilliseconds(250), processorSeconds: 1.25, received: 1_000, sent: 0),
                    Reading(Start.AddSeconds(10), processorSeconds: 11.25, received: 11_000, sent: 0)));

            Assert.NotNull(await sampler.ReadAsync(TestContext.Current.CancellationToken));

            clock.Advance(TimeSpan.FromSeconds(10));

            var sample = await sampler.ReadAsync(TestContext.Current.CancellationToken);

            // Ten seconds and ten thousand bytes: the natural gap, with no second wait and no
            // second reading - the queue would have thrown if a reading had been taken.
            Assert.NotNull(sample);
            Assert.Equal(10_000 * 8d / 9.75 / 1_000_000, sample!.NetworkInMbps, precision: 9);
        }

        [Fact]
        public async Task ABurstOfColdStartCallersPaysTheWaitOnce()
        {
            var clock = Clock();
            var reads = 0;

            var sampler = new SelfServerStatsSampler(
                clock,
                () =>
                {
                    var index = Interlocked.Increment(ref reads);

                    return index switch
                    {
                        1 => Reading(Start, processorSeconds: 1, received: 0, sent: 0),
                        2 => Reading(
                            Start + SelfServerStatsSampler.MinimumWindow,
                            processorSeconds: 1.25,
                            received: 1_000_000,
                            sent: 0),
                        _ => throw new InvalidOperationException(
                            "a call inside the memo window read the machine again")
                    };
                });

            var samples = await Task.WhenAll(
                Enumerable.Range(0, 8)
                    .Select(_ => sampler.ReadAsync(TestContext.Current.CancellationToken)));

            Assert.All(samples, sample => Assert.NotNull(sample));
            Assert.Equal(2, reads);
            Assert.Single(samples.Select(sample => sample!.NetworkInMbps).Distinct());
        }

        [Fact]
        public async Task ACancelledCallerAnswersNullAndTheGateIsReleased()
        {
            var sampler = new SelfServerStatsSampler(
                Clock(),
                Queue(
                    Reading(Start, processorSeconds: 1, received: 0, sent: 0),
                    Reading(Start.AddMilliseconds(250), processorSeconds: 1.25, received: 1_000, sent: 0)));

            using var cancelled = new CancellationTokenSource();

            cancelled.Cancel();

            Assert.Null(await sampler.ReadAsync(cancelled.Token));

            // The already-cancelled caller never took the gate, so the next call still works.
            Assert.NotNull(await sampler.ReadAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task ACancellationDuringTheWaitAnswersNullWithoutThrowing()
        {
            var sampler = new SelfServerStatsSampler(
                Clock(),
                Queue(
                    Reading(Start, processorSeconds: 1, received: 0, sent: 0),
                    Reading(Start.AddMilliseconds(250), processorSeconds: 1.25, received: 1_000, sent: 0)));

            using var cancelled = new CancellationTokenSource();

            var pending = sampler.ReadAsync(cancelled.Token);

            cancelled.Cancel();

            Assert.Null(await pending);
        }

        [Fact]
        public async Task AReaderThatAnswersNothingReportsNoSample()
        {
            var sampler = new SelfServerStatsSampler(Clock(), () => null);

            Assert.Null(await sampler.ReadAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task AReaderThatThrowsPropagatesToTheCallerAndReleasesTheGate()
        {
            var reads = 0;

            var sampler = new SelfServerStatsSampler(
                Clock(),
                () =>
                {
                    var index = Interlocked.Increment(ref reads);

                    return index switch
                    {
                        1 => throw new InvalidOperationException("the platform refused a fact"),
                        2 => Reading(Start.AddSeconds(1), processorSeconds: 1, received: 0, sent: 0),
                        _ => Reading(
                            Start.AddSeconds(1) + SelfServerStatsSampler.MinimumWindow,
                            processorSeconds: 1.25,
                            received: 1_000,
                            sent: 0)
                    };
                });

            // The exception is the decorator's business: it decides what an unreadable main row
            // means. What this test pins is that the failed path did not leave the gate taken.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => sampler.ReadAsync(TestContext.Current.CancellationToken));

            var sample = await sampler.ReadAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(sample);
            Assert.Equal(1_000 * 8d / 0.25 / 1_000_000, sample!.NetworkInMbps, precision: 9);
        }

        [Fact]
        public async Task AMemoryBudgetOfZeroFailsTheSample()
        {
            var sampler = new SelfServerStatsSampler(
                Clock(),
                Queue(
                    Reading(Start, processorSeconds: 1, received: 0, sent: 0, availableMemory: 0),
                    Reading(
                        Start.AddMilliseconds(250),
                        processorSeconds: 1.25,
                        received: 0,
                        sent: 0,
                        availableMemory: 0)));

            Assert.Null(await sampler.ReadAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task AVolumeThatReportsNoTotalFailsTheSample()
        {
            var sampler = new SelfServerStatsSampler(
                Clock(),
                Queue(
                    Reading(Start, processorSeconds: 1, received: 0, sent: 0, diskTotal: 0),
                    Reading(Start.AddMilliseconds(250), processorSeconds: 1.25, received: 0, sent: 0, diskTotal: 0)));

            Assert.Null(await sampler.ReadAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task ANetworkCounterThatWentBackwardsFailsTheSample()
        {
            var sampler = new SelfServerStatsSampler(
                Clock(),
                Queue(
                    Reading(Start, processorSeconds: 1, received: 5_000, sent: 0),
                    Reading(Start.AddMilliseconds(250), processorSeconds: 1.25, received: 1_000, sent: 0)));

            Assert.Null(await sampler.ReadAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task AUptimeIsNeverNegativeEvenWhenTheStartIsAheadOfTheReading()
        {
            var sampler = new SelfServerStatsSampler(
                Clock(),
                Queue(
                    Reading(Start, processorSeconds: 1, received: 0, sent: 0, startedAt: Start.AddMinutes(5)),
                    Reading(
                        Start.AddMilliseconds(250),
                        processorSeconds: 1.25,
                        received: 0,
                        sent: 0,
                        startedAt: Start.AddMinutes(5))));

            var sample = await sampler.ReadAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(sample);
            Assert.Equal(0, sample!.UptimeSeconds);
        }

        private static FakeTimeProvider Clock() => new(Start);

        private static Func<SelfStatsReading?> Queue(params SelfStatsReading?[] readings)
        {
            var queue = new Queue<SelfStatsReading?>(readings);

            return () => queue.Count == 0
                ? throw new InvalidOperationException("the sampler took more readings than the test declared")
                : queue.Dequeue();
        }

        private static SelfStatsReading Reading(
            DateTimeOffset capturedAt,
            double processorSeconds,
            long received,
            long sent,
            long availableMemory = 1_000,
            long diskTotal = 200,
            DateTimeOffset? startedAt = null)
            => new()
            {
                CapturedAt = capturedAt,
                ProcessorTime = TimeSpan.FromSeconds(processorSeconds),
                WorkingSetBytes = 100,
                AvailableMemoryBytes = availableMemory,
                DiskUsedBytes = diskTotal / 2,
                DiskTotalBytes = diskTotal,
                StartedAt = startedAt ?? capturedAt.AddSeconds(-900),
                BytesReceived = received,
                BytesSent = sent
            };
    }
}
