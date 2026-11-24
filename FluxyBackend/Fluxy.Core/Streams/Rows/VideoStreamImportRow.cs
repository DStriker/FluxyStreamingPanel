using Fluxy.Core.Streams.Enums;

namespace Fluxy.Core.Streams.Rows
{
    /// <summary>
    /// One existing stream an import matched by name, shaped for the batch import query.
    /// </summary>
    /// <remarks>
    /// A projection only - it never reaches an HTTP response. The service uses it to decide
    /// which entries create a new stream and which update an existing one.
    /// </remarks>
    public sealed record VideoStreamImportRow
    {
        /// <summary>Identifier of the existing stream.</summary>
        public required Guid Id { get; init; }

        /// <summary>Name the stream already carries.</summary>
        public required string Name { get; init; }

        /// <summary>Kind the stream already is - decides whether a source may be appended.</summary>
        public required VideoStreamType Type { get; init; }

        /// <summary>
        /// Server the stream is already bound to for transcoding, or null for none.
        /// </summary>
        /// <remarks>
        /// The <b>stored</b> binding, read only to decide whether an overwrite would leave a
        /// permanent stream on a timeshift-only transcoder. The import never writes it: the two
        /// server references are batch settings for the rows the batch creates.
        /// </remarks>
        public Guid? TranscodingServerId { get; init; }
    }
}
