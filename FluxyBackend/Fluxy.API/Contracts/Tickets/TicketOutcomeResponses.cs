using Fluxy.API.Contracts.Authentication;
using Fluxy.API.Contracts.Common;
using Fluxy.Core.Tickets.Enums;
using Fluxy.Core.Tickets.Outcomes;
using Microsoft.AspNetCore.Http;

namespace Fluxy.API.Contracts.Tickets
{
    /// <summary>
    /// Turns what a ticket operation reported into what a client is told: an HTTP status, a
    /// machine readable code and an English fallback.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The whole table lives in the transport layer, for the reason the account and profile
    /// tables do: the service knows only <see cref="TicketAction"/>, so which of those is a 201,
    /// a 404 or a 409 can change without touching a rule - and a rule can change without anybody
    /// having to decide what status code it should start returning.
    /// </para>
    /// <para>
    /// <c>permission_denied</c> is the policy's own code, restated rather than invented: the
    /// coarse gate on the way into an action answers with it, and the per-target check inside the
    /// service produces the other half of the same gate. A client shown two words for "your group
    /// does not grant this" would have no way to tell them apart.
    /// </para>
    /// </remarks>
    internal static class TicketOutcomeResponses
    {
        /// <summary>Answer describing <paramref name="outcome"/>.</summary>
        /// <param name="outcome">What the service reported.</param>
        /// <returns>The status and the body, with the rejected fields when the input was the problem.</returns>
        public static (int Status, MessageResponse Body) Describe(TicketOutcome outcome)
        {
            var (status, code, message) = Map(outcome.Action);

            return (status, new MessageResponse
            {
                Code = code,
                Message = message,
                Errors = FieldErrorKeys.FromPropertyNames(outcome.Errors)
            });
        }

        /// <summary>Answer describing one message write.</summary>
        /// <param name="outcome">What the service reported.</param>
        /// <returns>The status and the body, with the rejected fields when the input was the problem.</returns>
        public static (int Status, MessageResponse Body) Describe(TicketReplyOutcome outcome)
        {
            var (status, code, message) = Map(outcome.Action);

            return (status, new MessageResponse
            {
                Code = code,
                Message = message,
                Errors = FieldErrorKeys.FromPropertyNames(outcome.Errors)
            });
        }

        /// <summary>
        /// The one place each action becomes a status, a code and a sentence.
        /// </summary>
        /// <remarks>
        /// Every successful action answers its own word rather than a generic "ticket updated":
        /// the client translates a code, and one code per transition is what lets it say what
        /// happened instead of guessing from a status.
        /// </remarks>
        private static (int Status, string Code, string Message) Map(TicketAction action) =>
            action switch
            {
                TicketAction.Created =>
                    (StatusCodes.Status201Created,
                        TicketResponses.CreatedCode,
                        "The ticket has been created."),

                TicketAction.Replied =>
                    (StatusCodes.Status201Created,
                        TicketResponses.ReplyCreatedCode,
                        "The message has been sent."),

                TicketAction.MarkedRead =>
                    (StatusCodes.Status200OK,
                        TicketResponses.MarkedReadCode,
                        "The ticket has been marked as read."),

                TicketAction.MarkedUnread =>
                    (StatusCodes.Status200OK,
                        TicketResponses.MarkedUnreadCode,
                        "The ticket has been marked as unread."),

                TicketAction.Closed =>
                    (StatusCodes.Status200OK,
                        TicketResponses.ClosedCode,
                        "The ticket has been closed."),

                TicketAction.Reopened =>
                    (StatusCodes.Status200OK,
                        TicketResponses.ReopenedCode,
                        "The ticket has been reopened."),

                TicketAction.Deleted =>
                    (StatusCodes.Status200OK,
                        TicketResponses.DeletedCode,
                        "The ticket has been deleted."),

                TicketAction.NotFound =>
                    (StatusCodes.Status404NotFound,
                        TicketResponses.NotFoundCode,
                        "There is no ticket with that identifier."),

                TicketAction.Invalid =>
                    (StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Some of the values you entered are not valid."),

                TicketAction.PermissionDenied =>
                    (StatusCodes.Status403Forbidden,
                        AuthenticationResponses.PermissionDeniedCode,
                        "Your account does not have permission for this."),

                TicketAction.InvalidStatus =>
                    (StatusCodes.Status409Conflict,
                        TicketResponses.InvalidStatusCode,
                        "That action does not apply to the ticket's current state. Reload the ticket."),

                TicketAction.ReplyToClosed =>
                    (StatusCodes.Status409Conflict,
                        TicketResponses.IsClosedCode,
                        "This ticket is closed, so it cannot accept a reply."),

                TicketAction.ReplyLimitReached =>
                    (StatusCodes.Status409Conflict,
                        TicketResponses.ReplyLimitReachedCode,
                        "This ticket has reached its limit of messages."),

                // Unreachable: the service reports nothing outside this enum. Thrown rather than
                // answered as a success, because a code a client has never seen is a silent
                // failure somewhere above this line.
                _ => throw new ArgumentOutOfRangeException(
                    nameof(action),
                    action,
                    "Unknown ticket outcome.")
            };
    }
}
