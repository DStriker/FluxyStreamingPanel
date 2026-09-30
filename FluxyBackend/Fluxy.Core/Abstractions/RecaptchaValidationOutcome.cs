namespace Fluxy.Core.Abstractions
{
    /// <summary>
    /// Why a reCAPTCHA check ended the way it did.
    /// </summary>
    public enum RecaptchaValidationOutcome
    {
        /// <summary>The token was accepted by the verification service.</summary>
        Passed = 0,

        /// <summary>
        /// No secret key is configured, so the check was not performed at all. This is what
        /// keeps a development machine usable, and it is also the case that silently removes
        /// the protection from an installation that was meant to have it.
        /// </summary>
        Skipped = 1,

        /// <summary>The token was rejected, expired, or scored below the accepted threshold.</summary>
        Failed = 2
    }
}
