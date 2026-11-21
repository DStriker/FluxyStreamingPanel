namespace Fluxy.Core.Settings.Enums
{
    /// <summary>
    /// Theme a MAG portal would be shown with.
    /// </summary>
    /// <remarks>
    /// Nothing in this pass produces a MAG portal: the value is stored, validated and read by an
    /// operator only. <see cref="Cappucino"/> keeps the owner's spelling deliberately; it is not
    /// a misspelling to correct.
    /// </remarks>
    public enum MagTheme
    {
        /// <summary>The portal's own default theme.</summary>
        Default = 0,

        /// <summary>Digital theme.</summary>
        Digital = 1,

        /// <summary>Emerald theme.</summary>
        Emerald = 2,

        /// <summary>Cappucino theme, spelled as the owner spells it.</summary>
        Cappucino = 3,

        /// <summary>Ocean blue theme.</summary>
        OceanBlue = 4
    }
}
