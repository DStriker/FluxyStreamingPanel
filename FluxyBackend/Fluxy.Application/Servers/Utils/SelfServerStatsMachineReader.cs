using System.Diagnostics;
using System.Net.NetworkInformation;

namespace Fluxy.Application.Servers.Utils
{
    /// <summary>
    /// Reads one raw snapshot of the machine this process runs on, for
    /// <see cref="SelfServerStatsSampler"/> to turn into a sample.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every figure is deliberately local.</b> Processor time is this process's own
    /// <see cref="Process.TotalProcessorTime"/>; memory is its <see cref="Process.WorkingSet64"/>
    /// against the runtime's <see cref="GC.GetGCMemoryInfo"/> budget; disk is the volume under the
    /// working directory; the network counters are the sum over every interface the machine
    /// reports. The sampler's remarks say what each of those means on screen, because the names the
    /// wire contract uses are broader than the facts behind them.
    /// </para>
    /// <para>
    /// <b>Nothing here throws.</b> A drive that cannot be described, a runtime that reports no
    /// memory budget, a platform whose process start time is unavailable or a machine with no
    /// usable interface answers false, and the sampler reports the main row as unavailable. The
    /// exception is swallowed on purpose: this runs inside a dashboard build, and one unreadable
    /// fact must not turn the whole screen into a 500.
    /// </para>
    /// </remarks>
    public static class SelfServerStatsMachineReader
    {
        /// <summary>Reads one raw snapshot, or false when a required fact cannot be read.</summary>
        /// <param name="time">Clock the reading is stamped with.</param>
        /// <param name="reading">The reading when this method returns true.</param>
        /// <returns>True when every required fact was read.</returns>
        public static bool TryRead(TimeProvider time, out SelfStatsReading reading)
        {
            reading = default;

            try
            {
                using var process = Process.GetCurrentProcess();

                var processorTime = process.TotalProcessorTime;
                var workingSet = process.WorkingSet64;

                // Throws on platforms and in situations where the start time is not available;
                // the sample then fails rather than reporting an uptime computed from a guess.
                var startedAt = new DateTimeOffset(process.StartTime.ToUniversalTime());

                var availableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

                if (!TryReadDisk(out var diskUsed, out var diskTotal))
                {
                    return false;
                }

                if (!TryReadNetwork(out var received, out var sent))
                {
                    return false;
                }

                reading = new SelfStatsReading
                {
                    CapturedAt = time.GetUtcNow(),
                    ProcessorTime = processorTime,
                    WorkingSetBytes = workingSet,
                    AvailableMemoryBytes = availableMemory,
                    DiskUsedBytes = diskUsed,
                    DiskTotalBytes = diskTotal,
                    StartedAt = startedAt,
                    BytesReceived = received,
                    BytesSent = sent
                };

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Space in use and the total size of the volume the process can reach.
        /// </summary>
        /// <remarks>
        /// <b>One volume, not every mounted disk.</b> The root is
        /// <see cref="Path.GetPathRoot(string)"/> of <see cref="Environment.CurrentDirectory"/>, so
        /// on a machine with several disks the figure describes the one the application runs from.
        /// A drive whose total size is zero is refused by the sampler's arithmetic.
        /// </remarks>
        private static bool TryReadDisk(out long used, out long total)
        {
            used = 0;
            total = 0;

            try
            {
                var root = Path.GetPathRoot(Environment.CurrentDirectory);

                if (string.IsNullOrEmpty(root))
                {
                    return false;
                }

                var drive = new DriveInfo(root);

                total = drive.TotalSize;
                used = total - drive.AvailableFreeSpace;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Cumulative bytes received and sent over every interface the machine reports.
        /// </summary>
        /// <remarks>
        /// <b>Every interface, so the figure is the machine's traffic, not the relayed media.</b>
        /// Database, cache and management traffic count too: they cross the same interfaces. An
        /// interface that throws <see cref="NetworkInformationException"/> is skipped rather than
        /// failing the whole reading - a disconnected or virtual adapter is common on a server
        /// that is otherwise healthy - but a machine where no interface could be read fails the
        /// sample.
        /// </remarks>
        private static bool TryReadNetwork(out long received, out long sent)
        {
            received = 0;
            sent = 0;

            try
            {
                var usable = false;

                foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    try
                    {
                        var statistics = networkInterface.GetIPStatistics();

                        received += statistics.BytesReceived;
                        sent += statistics.BytesSent;
                        usable = true;
                    }
                    catch (NetworkInformationException)
                    {
                        // A single unusable adapter is not a machine without a network.
                    }
                }

                return usable;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
