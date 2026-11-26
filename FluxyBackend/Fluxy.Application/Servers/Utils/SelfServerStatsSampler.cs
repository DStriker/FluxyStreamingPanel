using Fluxy.Core.Servers.ReadModels;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxy.Application.Servers.Utils
{
    /// <summary>
    /// Answers the statistics of the machine this process runs on, read locally instead of over
    /// HTTP.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The figures describe this process and the volume it can reach, not the host.</b> The
    /// details are on <see cref="ReadAsync"/>: processor time and network counters are deltas
    /// against an earlier reading, memory is this process's working set against the runtime's
    /// available memory, disk is the volume under the working directory, and uptime is how long
    /// this process has been running - a restart resets it.
    /// </para>
    /// <para>
    /// <b>It is registered as a singleton and it remembers one reading.</b> The processor and the
    /// network figures are rates, so a single snapshot of the counters cannot produce them: the
    /// type keeps an earlier cumulative reading and computes the next sample against it. A burst of
    /// dashboard builds is serialised through one gate and answered from a short memo, so the cost
    /// of a fresh window is paid once rather than once per caller.
    /// </para>
    /// <para>
    /// <b>A failed read is "unavailable", never a zero.</b> A missing network interface, a drive
    /// that reports no total, a runtime that cannot describe its memory budget or a platform where
    /// the process start time throws all answer null, so the caller reports the main row as
    /// unavailable rather than as an idle machine. The exception text never leaves this type.
    /// </para>
    /// </remarks>
    public sealed class SelfServerStatsSampler
    {
        /// <summary>
        /// The shortest window a rate is computed over, and the interval a computed sample is
        /// served from the memo.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Two readings taken closer together than this turn scheduler jitter into a violent rate
        /// spike: a few milliseconds of extra processor time over a five-millisecond window reads as
        /// a busy machine, and a handful of bytes reads as a burst of traffic. A quarter of a
        /// second is short enough to still describe "now" and long enough for the counters to mean
        /// something. The same interval is the memo lifetime, so a burst of concurrent callers pays
        /// one wait and the rest are answered immediately.
        /// </para>
        /// <para>
        /// The bound lives here rather than in <see cref="SelfServerStatsMath"/> because the math
        /// accepts any positive window - it is pure arithmetic - while these two values are a
        /// sampling policy: how long the sampler is willing to wait, and how old an average may be
        /// before it stops being an answer.
        /// </para>
        /// </remarks>
        public static readonly TimeSpan MinimumWindow = TimeSpan.FromMilliseconds(250);

        /// <summary>The longest gap two readings may be averaged over before a fresh window is taken.</summary>
        /// <remarks>
        /// Beyond a minute the average stops describing "now": a dashboard opened after an idle hour
        /// would otherwise show the traffic of that hour, which on a quiet installation is near
        /// zero, while the machine may be busy at the moment the screen is opened. A gap longer
        /// than this is not refused - the main row must always answer - it is replaced by a short
        /// window measured now.
        /// </remarks>
        public static readonly TimeSpan MaximumWindow = TimeSpan.FromSeconds(60);

        private readonly TimeProvider _time;
        private readonly Func<SelfStatsReading?> _read;
        private readonly SemaphoreSlim _gate = new(1, 1);

        private SelfStatsReading? _previous;
        private ComputedSample? _memo;

        /// <summary>
        /// Initializes a new instance of the <see cref="SelfServerStatsSampler"/> class over the
        /// real process, disk and network interfaces.
        /// </summary>
        /// <param name="time">Clock the window and the memo are measured with.</param>
        [ActivatorUtilitiesConstructor]
        public SelfServerStatsSampler(TimeProvider time)
        {
            _time = time;
            _read = () => SelfServerStatsMachineReader.TryRead(time, out var reading)
                ? reading
                : null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SelfServerStatsSampler"/> class over a
        /// declared reader.
        /// </summary>
        /// <param name="time">Clock the window and the memo are measured with.</param>
        /// <param name="read">Reader that answers one raw reading, or null to fail the sample.</param>
        /// <remarks>
        /// The seam the unit tests use: <see cref="SelfServerStatsMath"/> proves the arithmetic and
        /// this constructor proves the window policy and the concurrency without touching a real
        /// process. It is not used by the composition root.
        /// </remarks>
        public SelfServerStatsSampler(TimeProvider time, Func<SelfStatsReading?> read)
        {
            _time = time;
            _read = read;
        }

        /// <summary>Reads one sample of this process, or null when it cannot be measured now.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The sample, or null when a fact could not be read or the caller cancelled.</returns>
        /// <remarks>
        /// <para>
        /// <b>The window is bounded on both sides, and a cold start answers.</b> If an earlier
        /// reading exists and the gap since it is inside
        /// <see cref="MinimumWindow"/>..<see cref="MaximumWindow"/>, the rates are computed over
        /// that natural gap. Otherwise - no earlier reading (the first request after a restart), a
        /// gap too short to trust, or one too old to describe "now" - a reading is taken, the
        /// method waits <see cref="MinimumWindow"/>, takes a second reading, and computes over that
        /// short window. Every call therefore produces a sample, and the figures always describe
        /// the last at most a minute rather than an average over an idle hour.
        /// </para>
        /// <para>
        /// <b>One wait, not one per caller.</b> The work runs behind a
        /// <see cref="SemaphoreSlim"/>, and the last computed sample is memoised for
        /// <see cref="MinimumWindow"/>. A caller that arrives while the memo is fresh answers
        /// immediately; one that waited on the gate re-checks the memo before doing any work. No
        /// <c>lock</c> is held across an <c>await</c>, and the gate is released in a
        /// <c>finally</c> on every path - including a throw from a reader that does not follow the
        /// "answer null" convention, which propagates to the decorator that decides what an
        /// unreadable main row means.
        /// </para>
        /// <para>
        /// <b>A cancelled request answers null for that request only.</b> The internal wait takes
        /// the caller's token; cancellation is not an error about the machine and never reaches the
        /// dashboard as an exception from this type.
        /// </para>
        /// <para>
        /// The wait is real elapsed time (a <see cref="Task.Delay(TimeSpan, CancellationToken)"/>),
        /// not the injected clock: the clock is what dates the readings and expires the memo, and a
        /// test clock that had to be advanced to release the wait would be a second, unrelated
        /// mechanism.
        /// </para>
        /// </remarks>
        public async Task<ServerStatsSample?> ReadAsync(CancellationToken cancellationToken = default)
        {
            // Checked before the memo so a cancelled request is answered the same way whether or
            // not a fresh sample happens to be available: null for this request, not an exception.
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            if (TryServeMemo(_time.GetUtcNow()) is { } cached)
            {
                return cached;
            }

            try
            {
                await _gate.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            try
            {
                // Re-checked inside the gate: another caller may have computed while this one
                // waited, and serving that answer is the point of the memo.
                if (TryServeMemo(_time.GetUtcNow()) is { } fresh)
                {
                    return fresh;
                }

                var current = _read();

                if (current is not { } first)
                {
                    return null;
                }

                ServerStatsSample? sample;

                if (_previous is { } earlier)
                {
                    var gap = first.CapturedAt - earlier.CapturedAt;

                    if (gap >= MinimumWindow && gap <= MaximumWindow)
                    {
                        sample = Compute(earlier, first);
                        _previous = first;
                    }
                    else
                    {
                        // The gap is either too short to trust or too old to describe "now": the
                        // reading above is this window's start.
                        sample = await ReadShortWindowAsync(first, cancellationToken);
                    }
                }
                else
                {
                    // A cold start has no earlier reading at all, so the reading above is the
                    // start of its own short window.
                    sample = await ReadShortWindowAsync(first, cancellationToken);
                }

                if (sample is null)
                {
                    return null;
                }

                Volatile.Write(ref _memo, new ComputedSample(sample, _time.GetUtcNow()));

                return sample;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>Waits a minimum window from <paramref name="start"/> and computes over it.</summary>
        /// <param name="start">The reading the window begins at.</param>
        /// <param name="cancellationToken">Token to cancel the wait.</param>
        /// <returns>The sample over the short window, or null when the second reading or the wait failed.</returns>
        /// <remarks>
        /// <b>Called only while the gate is held.</b> That is what makes one burst pay one wait. The
        /// start reading is stored even when the rest fails, so the next call measures a natural
        /// gap from it instead of paying the wait again.
        /// </remarks>
        private async Task<ServerStatsSample?> ReadShortWindowAsync(
            SelfStatsReading start,
            CancellationToken cancellationToken)
        {
            _previous = start;

            try
            {
                await Task.Delay(MinimumWindow, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            var next = _read();

            if (next is not { } second)
            {
                return null;
            }

            var sample = Compute(start, second);

            _previous = second;

            return sample;
        }

        /// <summary>The memoised sample when it is younger than the minimum window, else null.</summary>
        private ServerStatsSample? TryServeMemo(DateTimeOffset now)
        {
            var memo = Volatile.Read(ref _memo);

            return memo is not null && now - memo.ComputedAt < MinimumWindow
                ? memo.Sample
                : null;
        }

        /// <summary>
        /// Turns two readings into one sample, or null when a field could not be measured.
        /// </summary>
        /// <remarks>
        /// The denominator refusals and the counter-reset refusal live in
        /// <see cref="SelfServerStatsMath"/>; this method only feeds it and refuses to publish a
        /// partial reading, because the wire contract carries six real numbers.
        /// </remarks>
        private static ServerStatsSample? Compute(SelfStatsReading earlier, SelfStatsReading later)
        {
            var window = later.CapturedAt - earlier.CapturedAt;

            var cpu = SelfServerStatsMath.CpuPercent(
                earlier.ProcessorTime,
                later.ProcessorTime,
                window,
                Environment.ProcessorCount);

            var ram = SelfServerStatsMath.Percent(
                later.WorkingSetBytes,
                later.AvailableMemoryBytes);

            var disk = SelfServerStatsMath.Percent(
                later.DiskUsedBytes,
                later.DiskTotalBytes);

            var networkIn = SelfServerStatsMath.MegabitsPerSecond(
                earlier.BytesReceived,
                later.BytesReceived,
                window);

            var networkOut = SelfServerStatsMath.MegabitsPerSecond(
                earlier.BytesSent,
                later.BytesSent,
                window);

            if (cpu is not { } cpuPercent
                || ram is not { } ramPercent
                || disk is not { } diskPercent
                || networkIn is not { } inMbps
                || networkOut is not { } outMbps)
            {
                return null;
            }

            return new ServerStatsSample
            {
                CpuPercent = cpuPercent,
                RamPercent = ramPercent,
                DiskPercent = diskPercent,
                UptimeSeconds = SelfServerStatsMath.UptimeSeconds(later.StartedAt, later.CapturedAt),
                NetworkInMbps = inMbps,
                NetworkOutMbps = outMbps
            };
        }

        /// <summary>One computed sample and the instant it was computed, served briefly to callers.</summary>
        private sealed record ComputedSample(ServerStatsSample Sample, DateTimeOffset ComputedAt);
    }
}
