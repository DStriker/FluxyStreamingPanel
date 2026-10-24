namespace Fluxy.Core.Vod.Enums
{
    /// <summary>
    /// How an on-demand item is delivered to the client that plays it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two members are not two kinds of file - both point at one source - but two answers
    /// to "who carries the bytes". A <see cref="Relay"/> item is served by this installation:
    /// the play endpoint proxies a remote address or streams a local file, so the client never
    /// talks to the origin. A <see cref="Redirect"/> item is a pointer: the play endpoint
    /// answers with a redirect and the client fetches the source itself.
    /// </para>
    /// <para>
    /// The numeric values are fixed because they are what the <c>vod_type</c> column stores.
    /// </para>
    /// </remarks>
    public enum VodType
    {
        /// <summary>The installation serves the source on the client's behalf.</summary>
        Relay = 1,

        /// <summary>The client is redirected to the source.</summary>
        Redirect = 2
    }
}
