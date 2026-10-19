using Fluxy.API.Contracts.Common;
using Fluxy.Core.Security.Enums;
using Fluxy.Core.Security.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Support
{
    /// <summary>
    /// The reCAPTCHA check the auth writes run after the antiforgery pair and before the
    /// attempt window.
    /// </summary>
    /// <remarks>
    /// Extracted from <c>AuthControllerBase</c> together with <see cref="AttemptLimiter"/>, for
    /// the same reason: the session history inherits the base and has no captcha of its own, so
    /// the check is composed into the four controllers that spend it instead.
    ///
    /// The check reads the token from the request rather than letting the caller pass one. A
    /// form that could choose its own header would be a second place for the spelling of
    /// <see cref="HeaderName"/> to live, and the two would eventually disagree.
    ///
    /// Anything other than an outright failure lets the request continue. That is what keeps an
    /// installation without a secret usable: <see cref="RecaptchaValidationOutcome.Skipped"/> is
    /// a deliberate bypass, not a refusal.
    /// </remarks>
    public sealed class CaptchaGuard
    {
        /// <summary>Header the reCAPTCHA token arrives in.</summary>
        public const string HeaderName = "X-Recaptcha-Token";

        /// <summary>Code reported when the reCAPTCHA check refuses a request.</summary>
        public const string InvalidCode = "captcha_invalid";

        private readonly IRecaptchaValidator _validator;

        /// <summary>
        /// Initializes a new instance of the <see cref="CaptchaGuard"/> class.
        /// </summary>
        /// <param name="validator">Check that a request came from a person.</param>
        public CaptchaGuard(IRecaptchaValidator validator)
        {
            _validator = validator;
        }

        /// <summary>
        /// Checks the token on the request against the action the endpoint requires.
        /// </summary>
        /// <param name="request">Request whose header carries the token.</param>
        /// <param name="action">
        /// Action the token has to have been minted for. Fixed per endpoint and never taken from
        /// the request: a token minted for one form must not be usable at another.
        /// </param>
        /// <param name="clientAddress">Address to report to the verification service.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Null when the request may continue, otherwise the refusal to answer with.</returns>
        public async Task<IActionResult?> RejectsAsync(
            HttpRequest request,
            string action,
            string clientAddress,
            CancellationToken cancellationToken)
        {
            var token = request.Headers[HeaderName].FirstOrDefault();

            var outcome = await _validator.ValidateAsync(
                token,
                action,
                clientAddress,
                cancellationToken);

            if (outcome is not RecaptchaValidationOutcome.Failed)
            {
                return null;
            }

            return new ObjectResult(
                new MessageResponse
                {
                    Code = InvalidCode,
                    Message = "The request could not be verified as coming from a person. Please try again."
                })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }
    }
}
