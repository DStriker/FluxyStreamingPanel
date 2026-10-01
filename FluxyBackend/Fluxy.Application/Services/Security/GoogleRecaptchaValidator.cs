using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Fluxy.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fluxy.Application.Services.Security
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
    public sealed class GoogleRecaptchaValidator : IRecaptchaValidator
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptionsMonitor<RecaptchaOptions> _options;
        private readonly ILogger<GoogleRecaptchaValidator> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleRecaptchaValidator"/> class.
        /// </summary>
        /// <param name="httpClientFactory">Factory producing the client used for the check.</param>
        /// <param name="options">Live settings, so a configuration reload applies without a restart.</param>
        /// <param name="logger">Logger problems with the verification service are reported to.</param>
        public GoogleRecaptchaValidator(
            IHttpClientFactory httpClientFactory,
            IOptionsMonitor<RecaptchaOptions> options,
            ILogger<GoogleRecaptchaValidator> logger)
        {
            _httpClientFactory = httpClientFactory;
            _options = options;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<RecaptchaValidationOutcome> ValidateAsync(
            string? token,
            string? expectedAction,
            string? remoteAddress = null,
            CancellationToken cancellationToken = default)
        {
            var options = _options.CurrentValue;

            if (string.IsNullOrWhiteSpace(options.SecretKey))
            {
                _logger.LogWarning(
                    "reCAPTCHA is not configured, so the check for action '{Action}' was skipped " +
                    "entirely. Put the private key in .env as {EnvKey} to enforce it.",
                    expectedAction,
                    "RECAPTCHA_SECRET_KEY");

                return RecaptchaValidationOutcome.Skipped;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                // Not an error: this is what a client that has no site key sends. When a secret is
                // configured, a missing token means the request did not come through the form.
                return RecaptchaValidationOutcome.Failed;
            }

            var form = new Dictionary<string, string>
            {
                ["secret"] = options.SecretKey,
                ["response"] = token
            };

            if (!string.IsNullOrWhiteSpace(remoteAddress))
            {
                form["remoteip"] = remoteAddress;
            }

            try
            {
                var client = _httpClientFactory.CreateClient(nameof(GoogleRecaptchaValidator));
                using var content = new FormUrlEncodedContent(form);
                using var response = await client.PostAsync(options.VerifyUrl, content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "The reCAPTCHA verification service answered {StatusCode} for action '{Action}'. " +
                        "The request is treated as failed.",
                        (int)response.StatusCode,
                        expectedAction);

                    return RecaptchaValidationOutcome.Failed;
                }

                var verification = await response.Content
                    .ReadFromJsonAsync<SiteVerifyResponse>(cancellationToken);

                if (verification is null || !verification.Success)
                {
                    _logger.LogWarning(
                        "reCAPTCHA rejected the token for action '{Action}'. Errors: {Errors}.",
                        expectedAction,
                        verification?.ErrorCodes is { Length: > 0 } codes
                            ? string.Join(", ", codes)
                            : "none reported");

                    return RecaptchaValidationOutcome.Failed;
                }

                // The service echoes the action back. A token minted for a different form has a
                // different action, so accepting it here would let one form's token be spent on
                // another - and a token from the login form would confirm a registration.
                if (!string.IsNullOrWhiteSpace(expectedAction)
                    && !string.Equals(verification.Action, expectedAction, StringComparison.Ordinal))
                {
                    _logger.LogWarning(
                        "reCAPTCHA accepted a token minted for action '{ActualAction}' where " +
                        "'{ExpectedAction}' was expected.",
                        verification.Action,
                        expectedAction);

                    return RecaptchaValidationOutcome.Failed;
                }

                if (verification.Score is { } score && score < options.MinimumScore)
                {
                    _logger.LogWarning(
                        "reCAPTCHA scored {Score} for action '{Action}', below the accepted {Minimum}.",
                        score,
                        expectedAction,
                        options.MinimumScore);

                    return RecaptchaValidationOutcome.Failed;
                }

                return RecaptchaValidationOutcome.Passed;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // An unreachable third party must not become a 500, and it must not become a pass
                // either: failing closed keeps the form closed until the service answers.
                _logger.LogError(
                    exception,
                    "The reCAPTCHA verification service could not be reached. The request for " +
                    "action '{Action}' is treated as failed.",
                    expectedAction);

                return RecaptchaValidationOutcome.Failed;
            }
        }

        /// <summary>
        /// The answer of the siteverify endpoint. Only the parts this check relies on are
        /// modelled, and the score is a nullable double because the field is absent for
        /// reCAPTCHA v2 tokens.
        /// </summary>
        private sealed class SiteVerifyResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; init; }

            [JsonPropertyName("score")]
            public double? Score { get; init; }

            [JsonPropertyName("action")]
            public string? Action { get; init; }

            [JsonPropertyName("error-codes")]
            public string[]? ErrorCodes { get; init; }
        }
    }
}
