using Fluxy.Core.Persistence.Base;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// One on-demand video a playlist stream plays, with its position in the playlist. It is
    /// mutable, has no behaviour beyond mapping, and knows nothing about HTTP.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A row per entry rather than a list in a column, for the reason
    /// <see cref="VideoStreamSourceEntity"/> gives: the database enforces "one position per
    /// stream" with a unique index, and the order is read with a plain <c>ORDER BY position</c>
    /// instead of the application remembering to keep a column in step.
    /// </para>
    /// <para>
    /// Unlike the series reference, this one **is** a foreign key: a video named here is a row
    /// of <c>vod_items</c> in this same database, so the database itself can refuse a link to
    /// something that does not exist, and deleting the video takes the playlist entry with it
    /// rather than leaving a misplaced step in a sequence.
    /// </para>
    /// </remarks>
    public sealed class VideoStreamVodItemEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VideoStreamVodItemEntity"/> class for an
        /// entry that is about to be inserted. The identifier and both audit timestamps are
        /// generated.
        /// </summary>
        public VideoStreamVodItemEntity()
        {
        }

        /// <summary>Stream this entry belongs to. Deleted with the stream.</summary>
        public Guid StreamId { get; set; }

        /// <summary>That stream.</summary>
        public VideoStreamEntity? Stream { get; set; }

        /// <summary>On-demand video this entry plays.</summary>
        public Guid VodItemId { get; set; }

        /// <summary>
        /// Position in the playlist, zero based. Written by the service when the whole list is
        /// replaced, never by anything else - a position is not a field an operator types.
        /// </summary>
        public int Position { get; set; }
    }
}
