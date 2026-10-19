namespace Fluxy.Core.Users.Enums
{
    /// <summary>
    /// The outcome of one admin operation on a group: which <see cref="Action"/> ended it,
    /// which group it was about, and which fields were refused when the input was the
    /// problem.
    /// </summary>
    /// <remarks>
    /// <see cref="Errors"/> is keyed by the C# name of the property the service rejected, the
    /// convention every other service here follows - the transport turns those into the
    /// camelCase names a form reads, in one place.
    /// </remarks>
    public sealed record UserGroupOutcome
    {
        /// <summary>How the operation ended.</summary>
        public required UserGroupAction Action { get; init; }

        /// <summary>
        /// Group the operation was about. <see cref="Guid.Empty"/> for an outcome that never
        /// reached a group, such as a refusal made from the body alone.
        /// </summary>
        public Guid GroupId { get; init; }

        /// <summary>Rejected fields, when the input was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
