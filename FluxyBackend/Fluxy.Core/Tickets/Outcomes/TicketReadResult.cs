using Fluxy.Core.Tickets.Enums;
using Fluxy.Core.Tickets.ReadModels;

namespace Fluxy.Core.Tickets.Outcomes
{
    /// <summary>
    /// What reading one ticket came back with: the ticket and its conversation, or the word for
    /// why there is nothing to show.
    /// </summary>
    /// <remarks>
    /// A read has two ways of coming back empty and they are not the same answer. There may be no
    /// ticket with that identifier, or the operator's group may not read the accounts of the role
    /// that opened it. Both arrive here as <see cref="Detail"/> equal to null, and
    /// <see cref="Action"/> says which - the transport turns each into its own status rather than
    /// the service learning what a status is.
    /// </remarks>
    public sealed record TicketReadResult
    {
        /// <summary>The ticket, when it is there and this caller may read it.</summary>
        public TicketDetail? Detail { get; init; }

        /// <summary>
        /// Why <see cref="Detail"/> is null. Carries no meaning beside a non-null detail, and left
        /// at its default so the one case that has an answer cannot forget to state it.
        /// </summary>
        public TicketAction Action { get; init; } = TicketAction.NotFound;

        /// <summary>The ticket, read successfully.</summary>
        /// <param name="detail">What the row holds.</param>
        /// <returns>A result carrying it.</returns>
        public static TicketReadResult Found(TicketDetail detail) => new() { Detail = detail };

        /// <summary>Nothing to show, for the reason <paramref name="action"/> names.</summary>
        /// <param name="action">
        /// <see cref="TicketAction.NotFound"/> or <see cref="TicketAction.PermissionDenied"/>.
        /// </param>
        /// <returns>A result carrying the refusal.</returns>
        public static TicketReadResult Refused(TicketAction action) => new() { Action = action };
    }
}
