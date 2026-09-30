namespace Fluxy.Core.Abstractions
{
    /// <summary>
    /// Outcome of a delivery attempt. It is a result rather than an exception because a
    /// missing or unreachable mail server is a configuration fact of the installation, not
    /// a fault of the code that tried to send.
    /// </summary>
    public enum EmailSendResult
    {
        /// <summary>The message was handed over to the mail server.</summary>
        Sent = 0,

        /// <summary>
        /// The installation has no usable mail server configured, so the message was not sent.
        /// </summary>
        NotConfigured = 1,

        /// <summary>
        /// A mail server is configured but the message could not be delivered to it, for
        /// example because it refused the connection or rejected the sender.
        /// </summary>
        TransportFailed = 2
    }
}
