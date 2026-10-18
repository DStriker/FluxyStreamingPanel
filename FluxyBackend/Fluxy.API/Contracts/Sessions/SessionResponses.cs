using Fluxy.API.Contracts.Common;

namespace Fluxy.API.Contracts.Sessions
{
    /// <summary>
    /// The codes and bodies the session endpoints answer with, in one place.
    /// </summary>
    /// <remarks>
    /// The codes moved here the moment a second controller needed them: an operator ending a
    /// session from the admin area answers the same <c>session_revoked</c> the account's own
    /// button gets, because it is the same fact, and a client branching on one should not have
    /// to know which page produced it. The sentences differ where the endpoint differs - which
    /// session was not found is not the same statement as one about the caller's own account -
    /// so the not-found body takes its sentence from the caller rather than holding one.
    /// </remarks>
    internal static class SessionResponses
    {
        /// <summary>Code reported after a session was ended at its owner's request.</summary>
        public const string RevokedCode = "session_revoked";

        /// <summary>
        /// Code reported when a session id names nothing the caller may end - unknown, already
        /// ended, quietly expired, or belonging to an account the caller may not read. The
        /// cases are one answer on purpose: the id is a guessable value, and a difference in
        /// the refusal would turn the endpoint into a probe for other accounts' sessions.
        /// </summary>
        public const string NotFoundCode = "session_not_found";

        /// <summary>Code reported when the caller named the session the request arrived with.</summary>
        public const string CannotRevokeCurrentCode = "cannot_revoke_current";

        /// <summary>Code reported after every session but the current one was ended.</summary>
        public const string OtherSessionsRevokedCode = "other_sessions_revoked";

        /// <summary>
        /// The answer to an ending that happened, ready to be handed to <c>Ok</c>.
        /// </summary>
        public static MessageResponse Revoked()
            => new()
            {
                Code = RevokedCode,
                Message = "The session has been ended."
            };

        /// <summary>
        /// The answer to a session id that names nothing, with the sentence of the endpoint
        /// that asked - what the caller may not end differs between one's own account and one
        /// an operator manages.
        /// </summary>
        /// <param name="message">Sentence naming what was not found.</param>
        public static MessageResponse NotFound(string message)
            => new()
            {
                Code = NotFoundCode,
                Message = message
            };

        /// <summary>
        /// The refusal of an attempt to end the session the request arrived with. The page
        /// disables the button for its own row; this is what a direct call that ignored the UI
        /// gets instead of a silent no-op, so "I pressed it and nothing happened" cannot be the
        /// state a caller is left in.
        /// </summary>
        public static MessageResponse CannotRevokeCurrent()
            => new()
            {
                Code = CannotRevokeCurrentCode,
                Message = "The session you are using cannot be ended from here. Sign out instead."
            };

        /// <summary>
        /// The answer to ending every session but the current one, carrying how many there
        /// were.
        /// </summary>
        /// <param name="revokedCount">How many sessions were ended; zero when only the current remained.</param>
        public static RevokeOtherSessionsResponse OtherSessionsRevoked(int revokedCount)
            => new()
            {
                Code = OtherSessionsRevokedCode,
                Message = revokedCount == 0
                    ? "This account held no other sessions."
                    : "The other sessions of this account have been ended.",
                RevokedCount = revokedCount
            };
    }
}
