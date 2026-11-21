using Fluxy.Application.Security.Options;
using Fluxy.Core.Security.Enums;
using Fluxy.Core.Security.Interfaces;
using Fluxy.Core.Settings.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Fluxy.Application.Security.Services
{
    /// <summary>
    /// Verifies reCAPTCHA tokens against Google's siteverify endpoint.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The private key comes from the stored settings, not from configuration.</b> An empty
    /// stored secret skips the check, which is the explicit default a fresh installation starts
    /// with and the state an operator clears the field to reach. The configuration's
    /// <c>Recaptcha:SecretKey</c> is copied into the row once, on its first creation, and is not
    /// read here at all - otherwise an operator who cleared the key in the panel would still be
    /// enforcing an old one from <c>.env</c>.
    /// </para>
    /// <para>
    /// <see cref="RecaptchaOptions.VerifyUrl"/> and <see cref="RecaptchaOptions.MinimumScore"/>
    /// stay configuration: they describe the integration, not the installation's state.
    /// </para>
    /// </remarks>
    public sealed class GoogleRecaptchaValidator : IRecaptchaValidator
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptionsMonitor<RecaptchaOptions> _options;
        private readonly ISettingsSnapshotProvider _settings;
        private readonly ILogger<GoogleRecaptchaValidator> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleRecaptchaValidator"/> class.
        /// </summary>
        /// <param name="httpClientFactory">Factory producing the client used for the check.</param>
        /// <param name="options">Live options for the endpoint and the score cut-off.</param>
        /// <param name="settings">Stored settings the private key is read from.</param>
        /// <param name="logger">Logger problems with the verification service are reported to.</param>
        public GoogleRecaptchaValidator(
            IHttpClientFactory httpClientFactory,
            IOptionsMonitor<RecaptchaOptions> options,
            ISettingsSnapshotProvider settings,
            ILogger<GoogleRecaptchaValidator> logger)
        {
            _httpClientFactory = httpClientFactory;
            _options = options;
            _settings = settings;
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
            var secretKey = _settings.ApplicationSnapshot.RecaptchaSecretKey;

            if (string.IsNullOrWhiteSpace(secretKey))
            {
                _logger.LogWarning(
                    "reCAPTCHA is not configured, so the check for action '{Action}' was skipped " +
                    "entirely. Set the site key and the private key in /admin/settings to enforce it.",
                    expectedAction);

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
                ["secret"] = secretKey,
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
