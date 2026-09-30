namespace Fluxy.Core.Abstractions
{
    /// <summary>
    /// Verifies the token a browser obtained from reCAPTCHA against the verification service.
    /// </summary>
    public interface IRecaptchaValidator
    {
        /// <summary>
        /// Checks a token that a client produced with the reCAPTCHA script.
        /// </summary>
        /// <param name="token">
        /// Token to verify. May be null or empty, which is a failed check rather than an error,
        /// because that is what a development client without a site key sends.
        /// </param>
        /// <param name="expectedAction">
        /// Action the token was requested for. The verification service echoes the action back,
        /// and a token minted for a different action belongs to another form and must not be
        /// accepted here.
        /// </param>
        /// <param name="remoteAddress">Address the token was produced from, if available.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The outcome of the check. <see cref="RecaptchaValidationOutcome.Skipped"/> means no
        /// secret is configured and the check was bypassed. A verification service that cannot
        /// be reached is reported as <see cref="RecaptchaValidationOutcome.Failed"/> rather
        /// than thrown, so an outage of a third party cannot turn into a crash.
        /// </returns>
        Task<RecaptchaValidationOutcome> ValidateAsync(
            string? token,
            string? expectedAction,
            string? remoteAddress = null,
            CancellationToken cancellationToken = default);
    }
}
