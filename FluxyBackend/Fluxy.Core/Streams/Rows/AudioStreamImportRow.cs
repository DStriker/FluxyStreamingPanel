using Fluxy.Core.Streams.Enums;

namespace Fluxy.Core.Streams.Rows
{
    /// <summary>
    /// One existing audio stream an import matched by name, shaped for the batch import query.
    /// </summary>
    /// <remarks>
    /// A projection only - it never reaches an HTTP response. The service uses it to decide
    /// which entries create a new stream and which update an existing one.
    /// </remarks>
    public sealed record AudioStreamImportRow
    {
        /// <summary>Identifier of the existing stream.</summary>
        public required Guid Id { get; init; }

        /// <summary>Name the stream already carries.</summary>
        public required string Name { get; init; }

        /// <summary>Kind the stream already is.</summary>
        public required AudioStreamType Type { get; init; }
    }
}
