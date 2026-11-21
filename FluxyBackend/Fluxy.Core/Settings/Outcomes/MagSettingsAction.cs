namespace Fluxy.Core.Settings.Outcomes
{
    /// <summary>
    /// How an admin operation on the MAG settings ended.
    /// </summary>
    /// <remarks>
    /// The service knows only these words; which of them is a 200 and which a 400 is the
    /// transport's answer, stated beside the response table in the API layer.
    /// </remarks>
    public enum MagSettingsAction
    {
        /// <summary>The settings were written.</summary>
        Updated,

        /// <summary>One or more values did not pass validation; which one is in the errors.</summary>
        Invalid
    }
}
