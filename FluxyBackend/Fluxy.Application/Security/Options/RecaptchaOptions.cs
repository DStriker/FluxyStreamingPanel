using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Fluxy.Application.Security.Options
{
    /// <summary>
    /// Settings of the reCAPTCHA check. Bound from the <c>Recaptcha</c> section.
    /// </summary>
    public sealed class RecaptchaOptions
    {
        /// <summary>Configuration section this type is bound from.</summary>
        public const string SectionName = "Recaptcha";

        /// <summary>
        /// Verification endpoint. Overridable so the check can be pointed at a stub without
        /// touching the code.
        /// </summary>
        public string VerifyUrl { get; set; } = "https://www.google.com/recaptcha/api/siteverify";

        /// <summary>
        /// Secret shared with the site key the browser uses. <b>Read once, at startup, to seed
        /// the stored settings row; not read by the check itself.</b>
        /// </summary>
        /// <remarks>
        /// <para>
        /// Read from the gitignored <c>.env</c> file as <c>RECAPTCHA_SECRET_KEY</c>, never from
        /// <c>appsettings.json</c>: a key that exists in appsettings shadows the file, so even an
        /// empty placeholder there would silently win over the real secret.
        /// </para>
        /// <para>
        /// The private key now lives in the <c>application_settings</c> row, where an operator
        /// edits it on <c>/admin/settings</c>. This configuration value is copied into the row on
        /// its first creation only - so an installation that already had a key in <c>.env</c>
        /// keeps working after the upgrade - and is ignored afterwards. Editing <c>.env</c> later
        /// changes nothing: an operator who cleared the key in the panel must not have it silently
        /// restored by a restart.
        /// </para>
        /// </remarks>
        public string? SecretKey { get; set; }

        /// <summary>
        /// Lowest score a reCAPTCHA v3 token may carry. The service grades a token from 0 to 1
        /// and this is the cut-off below which the request is treated as a bot.
        /// </summary>
        public double MinimumScore { get; set; } = 0.5;
    }
}
