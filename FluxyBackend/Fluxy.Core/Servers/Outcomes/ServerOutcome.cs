using Fluxy.Core.Servers.Enums;

namespace Fluxy.Core.Servers.Outcomes
{
    /// <summary>
    /// The outcome of one admin operation on a server: which <see cref="ServerAction"/> ended it,
    /// which server it was about, and what was refused when the input was the problem.
    /// </summary>
    /// <remarks>
    /// <see cref="Errors"/> is keyed by the C# name of the property the service rejected, the
    /// convention every other service here follows; the transport turns those into the camelCase
    /// names a form reads.
    /// </remarks>
    public sealed record ServerOutcome
    {
        /// <summary>How the operation ended.</summary>
        public required ServerAction Action { get; init; }

        /// <summary>The server the operation was about.</summary>
        public required Guid ServerId { get; init; }

        /// <summary>How many rows point at the server, when it is still in use.</summary>
        public int ReferenceCount { get; init; }

        /// <summary>Rejected fields, when the input was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
