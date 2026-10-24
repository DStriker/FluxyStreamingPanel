using Fluxy.Core.Vod.Enums;

namespace Fluxy.Core.Vod.Outcomes
{
    /// <summary>
    /// The outcome of one admin operation on an on-demand item: which
    /// <see cref="VodAction"/> ended it, which item it was about, and which fields were refused
    /// when the input was the problem.
    /// </summary>
    /// <remarks>
    /// <see cref="Errors"/> is keyed by the C# name of the property the service rejected - the
    /// convention every other service here follows, and the transport turns those into the
    /// camelCase names a form reads.
    /// </remarks>
    public sealed record VodOutcome
    {
        /// <summary>How the operation ended.</summary>
        public required VodAction Action { get; init; }

        /// <summary>The item the operation was about.</summary>
        public required Guid VodId { get; init; }

        /// <summary>Rejected fields, when the input was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
