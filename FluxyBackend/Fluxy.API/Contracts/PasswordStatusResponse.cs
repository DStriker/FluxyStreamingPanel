namespace Fluxy.API.Contracts
{
    /// <summary>
    /// Whether this installation can send a code at all, so a page can decide what to show
    /// before anybody types into a form that could never be finished.
    /// </summary>
    public sealed record PasswordStatusResponse
    {
        /// <summary>
        /// True when a mail server is configured and the reset form is worth rendering.
        /// </summary>
        public required bool Configured { get; init; }
    }
}
