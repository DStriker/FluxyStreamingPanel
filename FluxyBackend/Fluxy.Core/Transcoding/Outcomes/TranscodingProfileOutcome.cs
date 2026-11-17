using Fluxy.Core.Transcoding.Enums;

namespace Fluxy.Core.Transcoding.Outcomes
{
    /// <summary>
    /// The outcome of one admin operation on a transcoding profile: which
    /// <see cref="TranscodingProfileAction"/> ended it, which profile it was about, and what was
    /// refused when the input was the problem.
    /// </summary>
    /// <remarks>
    /// <see cref="Errors"/> is keyed by the C# name of the property the service rejected, the
    /// convention every other service here follows; the transport turns those into the
    /// camelCase names a form reads.
    /// </remarks>
    public sealed record TranscodingProfileOutcome
    {
        /// <summary>How the operation ended.</summary>
        public required TranscodingProfileAction Action { get; init; }

        /// <summary>The profile the operation was about.</summary>
        public required Guid ProfileId { get; init; }

        /// <summary>How many rows of the four referencing tables point at it, when it is in use.</summary>
        public int ReferenceCount { get; init; }

        /// <summary>Rejected fields, when the input was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
