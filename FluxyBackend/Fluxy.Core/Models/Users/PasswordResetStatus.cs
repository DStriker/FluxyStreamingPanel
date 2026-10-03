namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// How a password reset request or its confirmation ended.
    /// </summary>
    /// <remarks>
    /// The pair check on the request has its own member rather than being folded into
    /// <see cref="InvalidCode"/>: unlike the registration endpoint, which reports every unknown
    /// address identically, this endpoint is deliberately allowed to say that the username and
    /// the email do not belong together, so the visitor with a typo is told so instead of
    /// waiting for a message that never comes. The cost of that choice - that the pair can be
    /// probed - is paid for by the attempt limit on the endpoint, not by a lie in the answer.
    /// </remarks>
    public enum PasswordResetStatus
    {
        /// <summary>A pending reset was stored and the code was mailed to the address.</summary>
        Submitted = 0,

        /// <summary>
        /// This installation has no mail server, so there is no way to send a code and nothing
        /// was stored.
        /// </summary>
        NotConfigured = 1,

        /// <summary>No account holds both the supplied username and the supplied email.</summary>
        InvalidPair = 2,

        /// <summary>At least one supplied value does not satisfy the registration policy.</summary>
        InvalidInput = 3,

        /// <summary>
        /// The pending reset was stored, but the mail server refused the message carrying the
        /// code.
        /// </summary>
        EmailDeliveryFailed = 4,

        /// <summary>No pending reset matches the account, or the code is not the right one.</summary>
        InvalidCode = 5,

        /// <summary>The code was the right one but its window has closed.</summary>
        CodeExpired = 6,

        /// <summary>The password was replaced and every session of the account was ended.</summary>
        Confirmed = 7
    }
}
