using Fluxy.Application.Servers.Utils;

namespace Fluxy.UnitTests.Servers
{
    /// <summary>
    /// The arithmetic behind the local sample: every percentage is clamped and every unusable
    /// window or denominator is refused rather than answered with a plausible number.
    /// </summary>
    /// <remarks>
    /// The functions take numbers only, which is why the sampler's rules can be proved without a
    /// process, a disk or a network interface. The sampler itself is tested separately for the
    /// window it keeps between calls.
    /// </remarks>
    public sealed class SelfServerStatsMathTests
    {
        [Fact]
        public void AFullCoreBusyForTheWholeWindowIsOneHundredPercent()
        {
            var percent = SelfServerStatsMath.CpuPercent(
                TimeSpan.Zero,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1),
                processorCount: 1);

            Assert.Equal(100, percent);
        }

        [Fact]
        public void ProcessorTimeAboveTheWindowIsClampedToOneHundred()
        {
            // The counters and the clock are read a few microseconds apart, so a busy machine can
            // compute slightly over 100 on its own; a corrupted delta must not reach the wire.
            var percent = SelfServerStatsMath.CpuPercent(
                TimeSpan.Zero,
                TimeSpan.FromSeconds(50),
                TimeSpan.FromSeconds(1),
                processorCount: 4);

            Assert.Equal(100, percent);
        }

        [Fact]
        public void ACpuWindowThatIsZeroOrNegativeIsRefused()
        {
            Assert.Null(SelfServerStatsMath.CpuPercent(
                TimeSpan.Zero,
                TimeSpan.FromMilliseconds(10),
                TimeSpan.Zero,
                processorCount: 4));

            Assert.Null(SelfServerStatsMath.CpuPercent(
                TimeSpan.FromMilliseconds(10),
                TimeSpan.Zero,
                TimeSpan.FromMilliseconds(-5),
                processorCount: 4));
        }

        [Fact]
        public void ACpuWindowWithNoProcessorCountIsRefused()
        {
            Assert.Null(SelfServerStatsMath.CpuPercent(
                TimeSpan.Zero,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1),
                processorCount: 0));
        }

        [Fact]
        public void ProcessorTimeThatWentBackwardsIsRefused()
        {
            Assert.Null(SelfServerStatsMath.CpuPercent(
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1),
                processorCount: 1));
        }

        [Theory]
        [InlineData(50, 100, 50)]
        [InlineData(1, 4, 25)]
        [InlineData(0, 100, 0)]
        public void AShareBecomesItsPercentage(long used, long total, double expected)
        {
            Assert.Equal(expected, SelfServerStatsMath.Percent(used, total));
        }

        [Fact]
        public void AShareAboveItsTotalIsClampedToOneHundred()
        {
            Assert.Equal(100, SelfServerStatsMath.Percent(150, 100));
        }

        [Theory]
        [InlineData(10, 0)]
        [InlineData(10, -1)]
        [InlineData(-1, 100)]
        public void AShareWithAnUnusableDenominatorOrNegativeUseIsRefused(long used, long total)
        {
            Assert.Null(SelfServerStatsMath.Percent(used, total));
        }

        [Fact]
        public void UptimeIsWholeSecondsAndNeverNegative()
        {
            var now = new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

            Assert.Equal(90, SelfServerStatsMath.UptimeSeconds(now.AddSeconds(-90), now));

            // A clock that moved backwards must not draw a negative uptime.
            Assert.Equal(0, SelfServerStatsMath.UptimeSeconds(now.AddSeconds(10), now));
        }

        [Fact]
        public void AByteDeltaBecomesDecimalMegabitsPerSecond()
        {
            // 1,000,000 bytes in one second is 8,000,000 bits, i.e. 8 Mbps.
            Assert.Equal(8, SelfServerStatsMath.MegabitsPerSecond(
                previousBytes: 0,
                bytes: 1_000_000,
                window: TimeSpan.FromSeconds(1)));

            Assert.Equal(4, SelfServerStatsMath.MegabitsPerSecond(
                previousBytes: 0,
                bytes: 1_000_000,
                window: TimeSpan.FromSeconds(2)));
        }

        [Fact]
        public void ANetworkCounterThatWentBackwardsOrAWindowOfZeroIsRefused()
        {
            Assert.Null(SelfServerStatsMath.MegabitsPerSecond(
                previousBytes: 1000,
                bytes: 500,
                window: TimeSpan.FromSeconds(1)));

            Assert.Null(SelfServerStatsMath.MegabitsPerSecond(
                previousBytes: 0,
                bytes: 1000,
                window: TimeSpan.Zero));
        }
    }
}
