namespace Fluxy.Core.Series.Enums
{
    /// <summary>
    /// The operations one bulk request may apply to a set of episodes of one series.
    /// </summary>
    public enum BulkEpisodeAction
    {
        /// <summary>Publish or disable every named episode.</summary>
        ChangeStatus,

        /// <summary>Change how every named episode is delivered.</summary>
        ChangeType,

        /// <summary>Delete every named episode.</summary>
        Delete
    }
}
