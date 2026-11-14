namespace Fluxy.API.Contracts.Tickets
{
    /// <summary>
    /// The body that opens a ticket: its subject line and nothing else.
    /// </summary>
    /// <remarks>
    /// The first message is deliberately absent. A ticket is created from the list, and the
    /// conversation is where a message is written - the endpoint takes the one field the dialog
    /// collects, and the thread the create answers into is where the text belongs. A body
    /// carrying both would make the two an ordered pair the server has to accept as one.
    ///
    /// The value is judged by the service rather than by a data annotation: an annotation runs
    /// before the action, and a body refused that way would be answered without ever reaching
    /// the antiforgery check or the service's own trim.
    /// </remarks>
    public sealed record CreateTicketRequest
    {
        /// <summary>Subject line, at most 200 characters after trimming.</summary>
        public string? Subject { get; init; }
    }
}
