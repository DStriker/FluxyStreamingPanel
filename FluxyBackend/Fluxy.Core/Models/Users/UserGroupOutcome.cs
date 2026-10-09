namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// How an admin operation on a group ended.
    /// </summary>
    /// <remarks>
    /// The service knows only these words. Which of them is a 201, which is a 409 and which
    /// code a client branches on is the transport's answer, stated beside the other response
    /// tables in the API layer - so a status can change without touching a rule, and a rule
    /// can change without anybody having to decide what status code it should start
    /// returning.
    /// </remarks>
    public enum UserGroupAction
    {
        /// <summary>The group was created.</summary>
        Created,

        /// <summary>The change was written to the row.</summary>
        Updated,

        /// <summary>The group was deleted, along with its permissions.</summary>
        Removed,

        /// <summary>No group with that identifier.</summary>
        NotFound,

        /// <summary>Another group already holds that name.</summary>
        AlreadyExists,

        /// <summary>One of the values did not pass validation; which one is in the errors.</summary>
        Invalid,

        /// <summary>
        /// The group is one of the three base groups and the request changed something other
        /// than its name. Refused here rather than by a hidden control, so that an endpoint
        /// cannot be asked twice in two shapes.
        /// </summary>
        ImmutableBase,

        /// <summary>The group still has members, so deleting it would strand them.</summary>
        InUse
    }

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
