namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// How a profile change or its confirmation ended.
    /// </summary>
    /// <remarks>
    /// The same shape as <see cref="RegistrationStatus"/> for the same reason it exists there:
    /// every member maps to one machine readable code and one HTTP status in the transport
    /// layer, and the service never learns either of them.
    /// </remarks>
    public enum ProfileChangeStatus
    {
        /// <summary>
        /// The change was applied at once, because this installation has no mail server and
        /// therefore nothing to confirm with.
        /// </summary>
        Applied = 0,

        /// <summary>
        /// The change is stored as a pending row and the code that applies it was mailed out.
        /// </summary>
        Submitted = 1,

        /// <summary>The supplied password is not the one the account signs in with.</summary>
        InvalidCurrentPassword = 2,

        /// <summary>At least one supplied value does not satisfy the registration policy.</summary>
        InvalidInput = 3,

        /// <summary>The username or the email is already held by another account.</summary>
        AlreadyExists = 4,

        /// <summary>No pending change matches the account, or the code is not the right one.</summary>
        InvalidCode = 5,

        /// <summary>The code was the right one but its window has closed.</summary>
        CodeExpired = 6,

        /// <summary>
        /// The pending row was stored, but the mail server refused the message carrying the
        /// code. A new request replaces the code once it is made.
        /// </summary>
        EmailDeliveryFailed = 7,

        /// <summary>
        /// The account is missing, blocked or otherwise not <see cref="UserStatus.Registered"/>,
        /// so it may not change itself.
        /// </summary>
        AccountNotActive = 8,

        /// <summary>
        /// The code was right, the pending row is gone and the change is on the account. Kept
        /// apart from <see cref="Applied"/> so the two moments a change can land - at once, and
        /// after the code - stay distinguishable for the client even though both end well.
        /// </summary>
        Confirmed = 9
    }
}
