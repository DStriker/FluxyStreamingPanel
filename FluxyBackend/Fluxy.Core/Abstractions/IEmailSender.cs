namespace Fluxy.Core.Abstractions
{
    /// <summary>
    /// Delivers a message to a recipient.
    /// </summary>
    /// <remarks>
    /// Implementations never throw. A mail server that is absent, unreachable or refusing is
    /// reported through <see cref="EmailSendResult"/>, because losing the delivery is a fact
    /// the caller has to handle, not an exceptional condition that should unwind the request.
    /// </remarks>
    public interface IEmailSender
    {
        /// <summary>
        /// Whether the installation has enough settings to send anything at all. Callers check
        /// this before they do expensive work, so that a server without mail refuses a request
        /// immediately instead of after storing something it cannot follow up on.
        /// </summary>
        bool IsConfigured { get; }

        /// <summary>
        /// Delivers <paramref name="message"/>.
        /// </summary>
        /// <param name="message">Message to deliver.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// <see cref="EmailSendResult.Sent"/> on success,
        /// <see cref="EmailSendResult.NotConfigured"/> when nothing is configured, or
        /// <see cref="EmailSendResult.TransportFailed"/> when the mail server refused the
        /// message. The method does not throw.
        /// </returns>
        Task<EmailSendResult> SendAsync(
            EmailMessage message,
            CancellationToken cancellationToken = default);
    }
}
