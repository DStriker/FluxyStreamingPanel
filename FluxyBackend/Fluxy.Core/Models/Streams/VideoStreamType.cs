namespace Fluxy.Core.Models.Streams
{
    /// <summary>
    /// The six kinds of stream this installation manages, each with its own answer to the
    /// question "what does this stream broadcast?".
    /// </summary>
    /// <remarks>
    /// <para>
    /// The numeric values are fixed and must never be renumbered: they are what the
    /// <c>stream_type</c> column stores, so a value already in the database is a fact about
    /// the past as much as about the code. A new member is added at the end.
    /// </para>
    /// <para>
    /// Like <see cref="Fluxy.Core.Users.Enums.UserStatus"/> and <see cref="Fluxy.Core.Models.Content.ContentType"/>, this is not an
    /// ordered scale - the members are kinds of stream, not levels of anything - so nothing
    /// anywhere may compare two of them with <c>&gt;</c> or <c>&lt;</c>.
    /// </para>
    /// <para>
    /// The type is what decides which payload the stream carries: a URL list for
    /// <see cref="Permanent"/>, <see cref="OnDemand"/> and <see cref="Redirect"/>, the
    /// identifier of a series for <see cref="OnlineSeries"/>, the identifier of one
    /// on-demand item for <see cref="VodPlaylist"/>, and a list of file paths for
    /// <see cref="FilePlaylist"/>. That rule lives in <c>VideoStreamService</c> and nowhere
    /// else, so a payload cannot be written for a stream that does not use it.
    /// </para>
    /// </remarks>
    public enum VideoStreamType
    {
        /// <summary>A permanent stream: always available, and the only kind that may keep an archive.</summary>
        Permanent = 1,

        /// <summary>A stream started on request rather than running on its own schedule.</summary>
        OnDemand = 2,

        /// <summary>A redirection to somewhere else, with the same URL list as a permanent stream.</summary>
        Redirect = 3,

        /// <summary>The episodes of one series, one after another.</summary>
        OnlineSeries = 4,

        /// <summary>A list of on-demand items played on request.</summary>
        VodPlaylist = 5,

        /// <summary>A list of files played one after another.</summary>
        FilePlaylist = 6
    }
}
