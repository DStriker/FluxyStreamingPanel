using Fluxy.Core.Persistence.Base;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// One audio stream a bouquet holds, with its position inside that bouquet.
    /// </summary>
    /// <remarks>
    /// The sibling of <see cref="BouquetVideoStreamEntity"/>, and a table of its own for the
    /// reason that one states: the audio streams are ordered apart from the video ones, and a
    /// foreign key to <c>audio_streams</c> is what makes a deleted audio stream leave no link
    /// behind.
    /// </remarks>
    public sealed class BouquetAudioStreamEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BouquetAudioStreamEntity"/> class for a
        /// membership that is about to be inserted. The identifier and both audit timestamps are
        /// generated.
        /// </summary>
        public BouquetAudioStreamEntity()
        {
        }

        /// <summary>Bouquet this row helps make up.</summary>
        public Guid BouquetId { get; set; }

        /// <summary>That bouquet.</summary>
        public BouquetEntity? Bouquet { get; set; }

        /// <summary>Audio stream the bouquet holds. Removed from every bouquet when it goes.</summary>
        public Guid AudioStreamId { get; set; }

        /// <summary>Position of the stream among the bouquet's audio streams, lowest first.</summary>
        public int SortOrder { get; set; }
    }
}
