using Fluxy.Core.Abstractions;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// One file a stream plays. It is mutable, has no behaviour beyond mapping, and knows
    /// nothing about HTTP.
    /// </summary>
    /// <remarks>
    /// One row is one entry of the list rather than a list stored in a column, for the reason
    /// <see cref="VideoStreamSourceEntity"/> gives: the database enforces "one position per
    /// stream" with a unique index and the order is read with plain <c>ORDER BY position</c>.
    ///
    /// A path is stored rather than a URL, because this list names files on the machine that
    /// relays them - the counterpart of <see cref="VideoStreamSourceEntity.Url"/>, which names
    /// an address on the network.
    /// </remarks>
    public sealed class VideoStreamFileEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VideoStreamFileEntity"/> class for a
        /// file that is about to be inserted. The identifier and both audit timestamps are
        /// generated.
        /// </summary>
        public VideoStreamFileEntity()
        {
        }

        /// <summary>Stream this file belongs to. Deleted with the stream.</summary>
        public Guid StreamId { get; set; }

        /// <summary>That stream.</summary>
        public VideoStreamEntity? Stream { get; set; }

        /// <summary>
        /// Position in the list, zero based. Written by the service when the whole list is
        /// replaced, never by anything else.
        /// </summary>
        public int Position { get; set; }

        /// <summary>The path itself, at most 1000 characters.</summary>
        public string Path { get; set; } = string.Empty;
    }
}
