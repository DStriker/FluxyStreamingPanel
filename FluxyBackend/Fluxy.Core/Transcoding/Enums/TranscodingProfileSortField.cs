namespace Fluxy.Core.Transcoding.Enums
{
    /// <summary>
    /// Which column of the transcoding profile table a caller asked to order by.
    /// </summary>
    /// <remarks>
    /// The name is the only column offered. Every other field is an optional ffmpeg option
    /// whose order a reader does not compare down the table, and the identifier is a random
    /// uuid. A single member looks odd until the alternative is considered: a sort dropdown
    /// over nineteen nullable numbers is a control nobody can use.
    /// </remarks>
    public enum TranscodingProfileSortField
    {
        /// <summary>Display name, compared by the database collation.</summary>
        Name
    }
}
