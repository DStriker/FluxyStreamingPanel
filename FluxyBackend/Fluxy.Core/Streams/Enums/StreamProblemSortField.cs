namespace Fluxy.Core.Streams.Enums
{
    /// <summary>
    /// The stored columns the stream problem journal may be ordered by.
    /// </summary>
    /// <remarks>
    /// <b><see cref="CreatedAt"/> is the only column.</b> The message is free text nobody groups a
    /// journal by, and the stream name is resolved by a join, so ordering on it would sort rows the
    /// server only has after the page has already been cut - the lie a client-side sort tells.
    /// </remarks>
    public enum StreamProblemSortField
    {
        /// <summary>When the problem was recorded. The default, and the newest first.</summary>
        CreatedAt = 0
    }
}
