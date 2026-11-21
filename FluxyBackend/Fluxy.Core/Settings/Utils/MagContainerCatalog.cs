using Fluxy.Core.Settings.Enums;

namespace Fluxy.Core.Settings.Utils
{
    /// <summary>
    /// Turns the text of a MAG container into the member it names, and names a member back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The accepted vocabulary matches the subscription output formats' own: <c>mpegts</c> and
    /// <c>mpeg-ts</c> name the same container, and the answer is always the canonical
    /// <c>mpeg-ts</c>. <c>hls</c> is its own name in both directions.
    /// </para>
    /// <para>
    /// Comparison is case-insensitive and trimmed, so <c>MPEG-TS</c> is the same request as
    /// <c>mpeg-ts</c>.
    /// </para>
    /// </remarks>
    public static class MagContainerCatalog
    {
        /// <summary>Reads a container name, or null when the text names no container.</summary>
        /// <param name="value">Value as sent, possibly null.</param>
        public static MagContainer? Parse(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "mpeg-ts" or "mpegts" => MagContainer.MpegTs,
                "hls" => MagContainer.Hls,
                _ => null
            };

        /// <summary>The canonical text a client reads a stored container by.</summary>
        /// <param name="container">Stored container.</param>
        public static string NameOf(MagContainer container) => container switch
        {
            MagContainer.MpegTs => "mpeg-ts",
            MagContainer.Hls => "hls",
            _ => throw new ArgumentOutOfRangeException(
                nameof(container),
                container,
                "Unknown MAG container.")
        };

        /// <summary>The sentence a container name that names no member gets.</summary>
        public static string Hint =>
            "The container must be either mpeg-ts or hls.";
    }
}
