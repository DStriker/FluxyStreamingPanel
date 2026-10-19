using Fluxy.Core.Persistence.Base;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// One address a stream relays from. It is mutable, has no behaviour beyond mapping, and
    /// knows nothing about HTTP.
    /// </summary>
    /// <remarks>
    /// One row is one entry of the list rather than a list stored in a column, for the reason
    /// the login guard rules are rows: the database can enforce "one position per stream" with
    /// a unique index instead of the application remembering to check, and an order can be
    /// read with plain <c>ORDER BY position</c>.
    ///
    /// The list is the stream's failover chain: the first row is the source that is used while
    /// it answers, and the rows after it are what is tried when it stops. Only the order
    /// matters here - which one is currently answering is <see cref="VideoStreamEntity.ActiveSource"/>,
    /// and that is a fact the relaying process reports rather than one this table decides.
    /// </remarks>
    public sealed class VideoStreamSourceEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VideoStreamSourceEntity"/> class for a
        /// source that is about to be inserted. The identifier and both audit timestamps are
        /// generated.
        /// </summary>
        public VideoStreamSourceEntity()
        {
        }

        /// <summary>Stream this source belongs to. Deleted with the stream.</summary>
        public Guid StreamId { get; set; }

        /// <summary>That stream.</summary>
        public VideoStreamEntity? Stream { get; set; }

        /// <summary>
        /// Position in the list, zero based. Written by the service when the whole list is
        /// replaced, never by anything else - a position is not a field an operator types.
        /// </summary>
        public int Position { get; set; }

        /// <summary>The address itself, at most 2000 characters.</summary>
        public string Url { get; set; } = string.Empty;
    }
}
