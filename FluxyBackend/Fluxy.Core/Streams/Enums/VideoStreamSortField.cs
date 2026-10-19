namespace Fluxy.Core.Streams.Enums
{
    /// <summary>
    /// Which column of the streams table a caller asked to order by.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The list is ordered by the server rather than by the browser, because the browser only
    /// ever holds one page: an order applied to the rows already on screen would leave the rows
    /// of the next page in whatever order the query happened to return them, so two pages could
    /// both contain a stream or both miss one.
    /// </para>
    /// <para>
    /// Every member is a value the query can honestly sort on. The identifier is deliberately
    /// absent - it is a random uuid, and an order on it is an order nothing asked for - and so
    /// is <c>ActiveSource</c>: it is what a process reports back, it is null for every stream
    /// that is not relaying right now, and ordering a table by it would lead with the streams
    /// nobody is watching.
    /// </para>
    /// </remarks>
    public enum VideoStreamSortField
    {
        /// <summary>Display name, compared case sensitively by the database collation.</summary>
        Name,

        /// <summary>
        /// The category's display name, which is what the column draws: an order on an
        /// identifier nobody sees would order an invisible value beside a visible one.
        /// </summary>
        Category,

        /// <summary>
        /// State of the stream: Stopped or Running. The members are not ordered, but the column
        /// is - an order on an enum stored as smallint is an order on its numbers.
        /// </summary>
        Status,

        /// <summary>
        /// Kind of stream. The members are not ordered either, and the same applies.
        /// </summary>
        Type,

        /// <summary>Position within the category, which is the order a category page is shown in.</summary>
        SortOrder
    }
}
