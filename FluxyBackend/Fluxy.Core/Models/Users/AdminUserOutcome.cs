namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// The outcome of one admin operation: which <see cref="Action"/> ended it, which account
    /// it was about, and which fields were refused when the input was the problem.
    /// </summary>
    /// <remarks>
    /// <see cref="Errors"/> is keyed by the C# name of the property the service rejected, the
    /// same convention every other service here follows - the transport turns those into the
    /// camelCase names a form reads, in one place, rather than each service spelling them its
    /// own way.
    /// </remarks>
    public sealed record AdminUserOutcome
    {
        /// <summary>How the operation ended.</summary>
        public required AdminUserAction Action { get; init; }

        /// <summary>
        /// Account the operation was about. <see cref="Guid.Empty"/> for an outcome that never
        /// reached an account, such as a refusal made from the body alone.
        /// </summary>
        public Guid UserId { get; init; }

        /// <summary>Rejected fields, when the input was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
