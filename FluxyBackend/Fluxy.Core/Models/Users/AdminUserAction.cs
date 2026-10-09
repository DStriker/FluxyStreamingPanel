namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// How an admin operation on an account ended.
    /// </summary>
    /// <remarks>
    /// The service knows only these words. Which of them is a 201, which is a 409 and which
    /// code a client branches on is the transport's answer, stated in the API layer beside the
    /// other response tables - so a status can change without touching a rule.
    ///
    /// The members that begin with <c>Cannot</c> are refusals rather than failures: the
    /// request was well formed and the account exists, but answering it would take away the
    /// access of the very account that asked. They are refused by the service rather than by
    /// the controller so that no future caller can reach the same action without them.
    /// </summary>
    public enum AdminUserAction
    {
        /// <summary>The account was created.</summary>
        Created,

        /// <summary>The change was written to the row.</summary>
        Updated,

        /// <summary>The account was deleted, along with everything that references it.</summary>
        Removed,

        /// <summary>The account left Unregistered for Registered.</summary>
        RegistrationConfirmed,

        /// <summary>The account entered Blocked.</summary>
        Blocked,

        /// <summary>The account left Blocked, back to whatever it was before.</summary>
        Unblocked,

        /// <summary>No account with that identifier.</summary>
        NotFound,

        /// <summary>The username or the address is already held by another account.</summary>
        AlreadyExists,

        /// <summary>One of the values did not pass validation; which one is in the errors.</summary>
        InvalidInput,

        /// <summary>
        /// The action does not apply to the state the account is in - unblocking an account
        /// that is not blocked, or confirming one that is not waiting for a confirmation.
        /// </summary>
        InvalidStatus,

        /// <summary>The account asked to delete itself.</summary>
        CannotDeleteSelf,

        /// <summary>The account asked to block itself.</summary>
        CannotBlockSelf,

        /// <summary>The account asked to take its own access level down.</summary>
        CannotDemoteSelf,

        /// <summary>
        /// The account looks blocked only because the group it belongs to is blocked, so
        /// there is nothing on the row for an unblock to clear.
        /// </summary>
        /// <remarks>
        /// Not the same as <see cref="InvalidStatus"/>, which says the row is not blocked -
        /// and here the row genuinely is not, while the account on the screen plainly is.
        /// Answering with <see cref="InvalidStatus"/> would tell an operator who read
        /// "Blocked" in the list that they misread it. The honest answer names what is
        /// actually blocking the account: the group, whose own row is where the button that
        /// helps lives.
        /// </remarks>
        BlockedByGroup
    }
}
