namespace Fluxy.Application.Servers.Options
{
    /// <summary>
    /// Configuration of the server statistics probe.
    /// </summary>
    /// <remarks>
    /// The section is <c>Servers</c> because that is where the identity of the installation's main
    /// row already lives (<c>Servers:MainIp</c>, <c>Servers:MainPort</c>); the token is one more
    /// fact about the same servers. <see cref="StatsToken"/> is optional: when it is absent the
    /// probe sends no <c>Authorization</c> header, which is the shape a server with no agent
    /// secret answers.
    /// </remarks>
    public sealed class ServerStatsOptions
    {
        /// <summary>Name of the configuration section these values are bound from.</summary>
        public const string SectionName = "Servers";

        /// <summary>
        /// Bearer token every statistics request carries, or null when the servers expect none.
        /// </summary>
        public string? StatsToken { get; set; }
    }
}
