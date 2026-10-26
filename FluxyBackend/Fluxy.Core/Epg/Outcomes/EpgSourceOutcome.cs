using Fluxy.Core.Epg.Enums;

namespace Fluxy.Core.Epg.Outcomes
{
    /// <summary>
    /// The outcome of one admin operation on a guide source: which
    /// <see cref="EpgSourceAction"/> ended it, which source it was about, and what was refused
    /// when the input was the problem.
    /// </summary>
    /// <remarks>
    /// <see cref="Errors"/> is keyed by the C# name of the property the service rejected, the
    /// convention every other service here follows; the transport turns those into the
    /// camelCase names a form reads.
    /// </remarks>
    public sealed record EpgSourceOutcome
    {
        /// <summary>How the operation ended.</summary>
        public required EpgSourceAction Action { get; init; }

        /// <summary>The source the operation was about.</summary>
        public required Guid SourceId { get; init; }

        /// <summary>Rejected fields, when the input was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

        /// <summary>
        /// How many configured streams point at the source. Set only by an
        /// <see cref="EpgSourceAction.InUse"/> refusal, and used for the sentence that tells
        /// the operator what is standing in the way.
        /// </summary>
        public int StreamsUsing { get; init; }
    }
}
