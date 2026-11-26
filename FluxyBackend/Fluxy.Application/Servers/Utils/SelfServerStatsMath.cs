namespace Fluxy.Application.Servers.Utils
{
    /// <summary>
    /// The arithmetic that turns two raw readings of this process into the percentages the
    /// dashboard draws.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Pure functions on purpose.</b> The only inputs are numbers, so every rule - the CPU
    /// clamp, the zero-denominator refusals, the counter-reset refusal, the floored uptime - is
    /// tested without mocking a process, a disk or an interface. The sampler owns the clock and the
    /// operating system; this type owns only the sums.
    /// </para>
    /// <para>
    /// <b>A window must be positive.</b> Two readings taken in the same clock tick say nothing
    /// about a rate, and a zero or negative wall-clock delta would divide a real byte counter into
    /// <c>Infinity</c>. Every windowed function answers null in that case, which the sampler
    /// reports as "no sample" rather than as a zero that would read as an idle machine.
    /// </para>
    /// </remarks>
    public static class SelfServerStatsMath
    {
        /// <summary>
        /// Processor load as a percentage of the whole machine, clamped to <c>0..100</c>.
        /// </summary>
        /// <param name="previousProcessorTime">Cumulative processor time at the earlier reading.</param>
        /// <param name="processorTime">Cumulative processor time at the later reading.</param>
        /// <param name="window">Wall-clock time between the two readings.</param>
        /// <param name="processorCount">Number of logical processors the runtime sees.</param>
        /// <returns>The percentage, or null when the window or the processor count is not usable.</returns>
        /// <remarks>
        /// <see cref="System.Diagnostics.Process.TotalProcessorTime"/> is a sum over every core, so
        /// the elapsed processor time is divided by the wall clock and by the processor count
        /// before it becomes a percentage of the machine. A result above 100 is clamped: the two
        /// counters are read microseconds apart from the clock and a busy machine can briefly
        /// compute slightly over 100, which is a measurement artefact rather than a real load.
        /// </remarks>
        public static double? CpuPercent(
            TimeSpan previousProcessorTime,
            TimeSpan processorTime,
            TimeSpan window,
            int processorCount)
        {
            if (window <= TimeSpan.Zero || processorCount <= 0)
            {
                return null;
            }

            var elapsed = (processorTime - previousProcessorTime).TotalMilliseconds;

            if (elapsed < 0)
            {
                return null;
            }

            var percent = elapsed / window.TotalMilliseconds / processorCount * 100;

            return Math.Clamp(percent, 0, 100);
        }

        /// <summary>
        /// <paramref name="used"/> as a percentage of <paramref name="total"/>, clamped to
        /// <c>0..100</c>.
        /// </summary>
        /// <param name="used">Space in use.</param>
        /// <param name="total">Space the measurement is against.</param>
        /// <returns>The percentage, or null when the denominator is zero or negative.</returns>
        /// <remarks>
        /// A zero denominator is a failure rather than a percentage: dividing by it would answer
        /// <c>NaN</c> or <c>Infinity</c>, and the dashboard's wire contract carries real numbers.
        /// The same rule covers the process's memory budget (which a runtime may not describe) and
        /// the volume's total size (which a drive may report as zero).
        /// </remarks>
        public static double? Percent(long used, long total)
        {
            if (total <= 0 || used < 0)
            {
                return null;
            }

            return Math.Clamp((double)used / total * 100, 0, 100);
        }

        /// <summary>Seconds from <paramref name="startedAt"/> to <paramref name="now"/>, floored at 0.</summary>
        /// <param name="startedAt">When the process started.</param>
        /// <param name="now">The instant the sample is taken.</param>
        /// <returns>A non-negative number of whole seconds.</returns>
        /// <remarks>
        /// A clock that moved backwards would answer a negative uptime, which is not a fact any
        /// screen should be able to draw; it is floored at zero instead.
        /// </remarks>
        public static long UptimeSeconds(DateTimeOffset startedAt, DateTimeOffset now)
        {
            var seconds = (now - startedAt).TotalSeconds;

            return seconds <= 0 ? 0 : (long)seconds;
        }

        /// <summary>
        /// A byte-counter delta as megabits per second.
        /// </summary>
        /// <param name="previousBytes">Counter at the earlier reading.</param>
        /// <param name="bytes">Counter at the later reading.</param>
        /// <param name="window">Wall-clock time between the two readings.</param>
        /// <returns>The rate, or null when the window is not usable or the counter went backwards.</returns>
        /// <remarks>
        /// A counter that decreased means the interface was reset or replaced between the two
        /// readings, and the delta is not a measurement of traffic - so it is refused rather than
        /// turned into a huge number. Decimal megabits (10^6 bits) are used because the dashboard's
        /// existing remote contract names <c>Mbps</c>, not mebibits.
        /// </remarks>
        public static double? MegabitsPerSecond(long previousBytes, long bytes, TimeSpan window)
        {
            if (window <= TimeSpan.Zero || bytes < previousBytes)
            {
                return null;
            }

            var delta = bytes - previousBytes;

            return delta * 8d / window.TotalSeconds / 1_000_000;
        }
    }
}
