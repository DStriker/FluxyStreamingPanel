namespace Fluxy.Core.Series.Enums
{
    /// <summary>
    /// How an episode is delivered to the client that plays it.
    /// </summary>
    /// <remarks>
    /// The two members are two answers to "who carries the bytes". A <see cref="Relay"/>
    /// episode is served by this installation; a <see cref="Redirect"/> episode is a pointer
    /// the client follows itself. The numeric values are fixed because they are what the
    /// <c>episode_type</c> column stores.
    /// </remarks>
    public enum EpisodeType
    {
        /// <summary>The installation serves the source on the client's behalf.</summary>
        Relay = 1,

        /// <summary>The client is redirected to the source.</summary>
        Redirect = 2
    }
}
