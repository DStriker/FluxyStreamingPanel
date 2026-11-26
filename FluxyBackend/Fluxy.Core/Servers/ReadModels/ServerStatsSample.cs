namespace Fluxy.Core.Servers.ReadModels
{
    /// <summary>
    /// One reading of a server's machine: processor, memory, disk, uptime and the two network
    /// rates, as the server itself reported them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every field is a real measurement, never a sentinel.</b> A server that could not be
    /// read answers null from the probe instead of this type filled with zeros, so the dashboard
    /// can tell "an idle machine" from "a machine that did not answer".
    /// </para>
    /// <para>
    /// The payload this is read from is an assumption recorded as A1 in
    /// <c>docs/admin-dashboard.md</c>: one adapter type owns it, so a real agent payload later is
    /// a one-type change.
    /// </para>
    /// </remarks>
    public sealed record ServerStatsSample
    {
        /// <summary>Processor load, as a percentage.</summary>
        public required double CpuPercent { get; init; }

        /// <summary>Memory in use, as a percentage.</summary>
        public required double RamPercent { get; init; }

        /// <summary>Disk in use, as a percentage.</summary>
        public required double DiskPercent { get; init; }

        /// <summary>How long the measured instance has been up, in seconds.</summary>
        public required long UptimeSeconds { get; init; }

        /// <summary>Inbound rate, in megabits per second.</summary>
        public required double NetworkInMbps { get; init; }

        /// <summary>Outbound rate, in megabits per second.</summary>
        public required double NetworkOutMbps { get; init; }
    }
}
