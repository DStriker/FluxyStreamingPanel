namespace Fluxy.API.Contracts.Tickets
{
    /// <summary>
    /// The body that answers a ticket: the message and nothing else.
    /// </summary>
    /// <remarks>
    /// The side of the message is not in the body; it is the entrance the request arrived
    /// through. The self-service reply is always the account's, the administrator's reply is
    /// always the operator's, and letting a body name the side would let a caller write into a
    /// conversation as somebody else.
    /// </remarks>
    public sealed record CreateTicketReplyRequest
    {
        /// <summary>Message body, at most 4000 characters after trimming.</summary>
        public string? Message { get; init; }
    }
}
