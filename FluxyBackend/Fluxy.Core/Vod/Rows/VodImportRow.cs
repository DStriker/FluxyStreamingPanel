namespace Fluxy.Core.Vod.Rows
{
    /// <summary>
    /// One existing item of a category, read only to decide whether a playlist entry is a
    /// new row or a name that is already taken.
    /// </summary>
    /// <remarks>
    /// A projection shaped for one query: it never reaches a response. The import needs the
    /// identifier only to report the row it skipped; the name is what the comparison is made
    /// on, compared case sensitively the same way the unique index is.
    /// </remarks>
    public sealed record VodImportRow
    {
        /// <summary>Identifier of the existing item.</summary>
        public required Guid Id { get; init; }

        /// <summary>Name the existing item holds.</summary>
        public required string Name { get; init; }
    }
}
