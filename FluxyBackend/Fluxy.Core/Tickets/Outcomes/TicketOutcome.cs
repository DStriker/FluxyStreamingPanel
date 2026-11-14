using Fluxy.Core.Tickets.Enums;

namespace Fluxy.Core.Tickets.Outcomes
{
    /// <summary>
    /// How an operation on a support ticket ended: which action it was, which ticket it named and
    /// which fields were refused when the input was the problem.
    /// </summary>
    /// <remarks>
    /// <see cref="Errors"/> is keyed by the C# name of the property the service rejected, the
    /// convention every other service here follows - the transport turns those into the camelCase
    /// names a form reads, in one place. It is null on a successful operation.
    /// </remarks>
    public sealed record TicketOutcome
    {
        /// <summary>How the operation ended.</summary>
        public required TicketAction Action { get; init; }

        /// <summary>Ticket the operation was about, when one was written or found.</summary>
        public Guid? TicketId { get; init; }

        /// <summary>Rejected fields, keyed by their C# property name, or null.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
