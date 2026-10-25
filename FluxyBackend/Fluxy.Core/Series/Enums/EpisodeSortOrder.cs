namespace Fluxy.Core.Series.Enums
{
    /// <summary>Which way the episode table runs.</summary>
    public enum EpisodeSortOrder
    {
        /// <summary>Smallest first: season 1 before season 2, episode 1 before episode 2.</summary>
        Ascending,

        /// <summary>Largest first: the reverse of <see cref="Ascending"/>.</summary>
        Descending
    }
}
