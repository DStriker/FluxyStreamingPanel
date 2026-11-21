using Fluxy.Core.Settings.Enums;

namespace Fluxy.Core.Settings.Utils
{
    /// <summary>
    /// Turns the text of a MAG theme into the member it names, and names a member back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The vocabulary is the owner's, spelled out here rather than derived from the enum member
    /// names: <c>ocean blue</c> carries a space, and <c>cappucino</c> is deliberately not
    /// corrected to <c>cappuccino</c>. The two directions live together so they cannot disagree.
    /// </para>
    /// <para>
    /// Comparison is case-insensitive and trimmed, because a caller spelling <c>Ocean Blue</c> is
    /// naming the same theme a form offered as <c>ocean blue</c>.
    /// </para>
    /// </remarks>
    public static class MagThemeCatalog
    {
        /// <summary>Reads a theme name, or null when the text names no theme.</summary>
        /// <param name="value">Value as sent, possibly null.</param>
        public static MagTheme? Parse(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "default" => MagTheme.Default,
                "digital" => MagTheme.Digital,
                "emerald" => MagTheme.Emerald,
                "cappucino" => MagTheme.Cappucino,
                "ocean blue" => MagTheme.OceanBlue,
                _ => null
            };

        /// <summary>The text a client reads a stored theme by.</summary>
        /// <param name="theme">Stored theme.</param>
        public static string NameOf(MagTheme theme) => theme switch
        {
            MagTheme.Default => "default",
            MagTheme.Digital => "digital",
            MagTheme.Emerald => "emerald",
            MagTheme.Cappucino => "cappucino",
            MagTheme.OceanBlue => "ocean blue",
            _ => throw new ArgumentOutOfRangeException(
                nameof(theme),
                theme,
                "Unknown MAG theme.")
        };

        /// <summary>The sentence a theme name that names no member gets.</summary>
        public static string Hint =>
            "The theme must be one of: default, digital, emerald, cappucino, ocean blue.";
    }
}
