namespace Fluxy.Core.Series.Enums
{
    /// <summary>Whether an episode is published, or disabled and kept.</summary>
    /// <remarks>
    /// Two states of one episode, not two levels of anything. The numeric values are fixed
    /// because they are what the <c>status</c> column stores.
    /// </remarks>
    public enum EpisodeStatus
    {
        /// <summary>The episode is disabled: kept, but not offered to clients.</summary>
        Inactive = 0,

        /// <summary>The episode is published.</summary>
        Active = 1
    }
}
