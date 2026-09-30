namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// How a registration or a confirmation ended. The set is deliberately narrow: every
    /// member maps to one machine readable code and one HTTP status, so the text a client sees
    /// is a presentation concern and stays in the API layer.
    /// </summary>
    public enum RegistrationStatus
    {
        /// <summary>The account was created and a registration code was sent to it.</summary>
        Submitted = 0,

        /// <summary>
        /// The installation has no mail server configured, so registration cannot be completed
        /// and nothing was stored.
        /// </summary>
        RegistrationNotConfigured = 1,

        /// <summary>At least one supplied value does not satisfy the registration policy.</summary>
        InvalidInput = 2,

        /// <summary>
        /// The username or the email is already held by an account that cannot be registered
        /// again. The message is deliberately the same for both, so the form cannot be used to
        /// find out which accounts exist.
        /// </summary>
        AlreadyExists = 3,

        /// <summary>
        /// The account was confirmed and is now <see cref="UserStatus.Registered"/>.
        /// </summary>
        Confirmed = 4,

        /// <summary>
        /// No pending code matches the supplied address, or the code is not the right one. The
        /// two are reported identically, so the endpoint cannot confirm that an address is
        /// registered.
        /// </summary>
        InvalidCode = 5,

        /// <summary>The code was the right one but its window has closed.</summary>
        CodeExpired = 6,

        /// <summary>
        /// The account was stored, but the mail server refused the message carrying the code.
        /// The account is therefore stuck in <see cref="UserStatus.Unregistered"/> until a new
        /// request replaces the pending code.
        /// </summary>
        EmailDeliveryFailed = 7
    }
}
