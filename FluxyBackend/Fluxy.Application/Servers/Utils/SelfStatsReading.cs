namespace Fluxy.Application.Servers.Utils
{
    /// <summary>
    /// One raw reading of the machine this process runs on, taken by
    /// <see cref="SelfServerStatsMachineReader"/> before the window arithmetic turns it into a
    /// <see cref="Fluxy.Core.Servers.ReadModels.ServerStatsSample"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>These are facts, not percentages.</b> The processor time and the two byte counters are
    /// cumulative totals that only mean something against an earlier reading; the memory, disk and
    /// start-time figures are instantaneous. Keeping the two kinds apart is what lets the sampler
    /// fail a window without discarding the readings that do not need one.
    /// </para>
    /// <para>
    /// <b><see cref="AvailableMemoryBytes"/> is the runtime's budget, not the host's physical
    /// memory.</b> It is <c>GC.GetGCMemoryInfo().TotalAvailableMemoryBytes</c> - the amount the
    /// runtime will let this process use, which inside a container is the container's limit.
    /// <see cref="WorkingSetBytes"/> is the process's residential set. The ratio is therefore
    /// "this process against what the runtime allows", not "the machine's RAM".
    /// </para>
    /// <para>
    /// The type carries no behaviour: the arithmetic lives in <see cref="SelfServerStatsMath"/>, so
    /// it can be tested without a machine. This is the seam a unit test hands over instead of
    /// reading a real process, disk or network interface.
    /// </para>
    /// </remarks>
    public readonly record struct SelfStatsReading
    {
        /// <summary>The instant the reading was taken, used to size the window.</summary>
        public required DateTimeOffset CapturedAt { get; init; }

        /// <summary>Cumulative processor time the process has consumed since it started.</summary>
        public required TimeSpan ProcessorTime { get; init; }

        /// <summary>The process's working set, in bytes.</summary>
        public required long WorkingSetBytes { get; init; }

        /// <summary>
        /// The runtime's available-memory budget for this process, in bytes; zero or negative when
        /// the runtime could not describe it.
        /// </summary>
        public required long AvailableMemoryBytes { get; init; }

        /// <summary>Space in use on the volume under the working directory, in bytes.</summary>
        public required long DiskUsedBytes { get; init; }

        /// <summary>Total size of that volume, in bytes; zero when it reported none.</summary>
        public required long DiskTotalBytes { get; init; }

        /// <summary>When this process started, in UTC.</summary>
        public required DateTimeOffset StartedAt { get; init; }

        /// <summary>Cumulative bytes the machine's interfaces have received.</summary>
        public required long BytesReceived { get; init; }

        /// <summary>Cumulative bytes the machine's interfaces have sent.</summary>
        public required long BytesSent { get; init; }
    }
}
