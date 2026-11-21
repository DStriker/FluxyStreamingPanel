namespace Fluxy.API.Contracts.Settings
{
    /// <summary>
    /// The one fact a guest page needs before it can draw a form: the public reCAPTCHA site key.
    /// </summary>
    /// <remarks>
    /// <b>Nothing else may travel here.</b> The endpoint is anonymous, so a field added to this
    /// record is a field the whole internet can read. The private key, the flood whitelist and the
    /// backup configuration all stay behind the admin grants; the site key is public by
    /// definition - it is handed to every browser that shows the reCAPTCHA widget.
    /// </remarks>
    public sealed record PublicSettingsResponse
    {
        /// <summary>Public reCAPTCHA site key; empty when the check is not configured.</summary>
        public required string RecaptchaSiteKey { get; init; }
    }
}
