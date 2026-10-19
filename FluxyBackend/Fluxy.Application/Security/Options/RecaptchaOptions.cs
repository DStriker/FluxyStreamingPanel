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
        /// Secret shared with the site key the browser uses. Absent means the check is bypassed.
        /// </summary>
        /// <remarks>
        /// Read from the gitignored <c>.env</c> file as <c>RECAPTCHA_SECRET_KEY</c>, never from
        /// <c>appsettings.json</c>: a key that exists in appsettings shadows the file, so even an
        /// empty placeholder there would silently win over the real secret.
        /// </remarks>
        public string? SecretKey { get; set; }

        /// <summary>
        /// Lowest score a reCAPTCHA v3 token may carry. The service grades a token from 0 to 1
        /// and this is the cut-off below which the request is treated as a bot.
        /// </summary>
        public double MinimumScore { get; set; } = 0.5;
    }

    /// <summary>
    /// Verifies reCAPTCHA tokens against Google's siteverify endpoint.
    /// </summary>
    /// <remarks>
    /// An installation without a secret key skips the check. That is what makes a development
    /// machine usable - the frontend does not even ask for a token there - but it also means a
    /// production deployment that forgot the key has quietly lost the protection, so the
    /// situation is logged as a warning rather than passed over.
    /// </remarks>
}
