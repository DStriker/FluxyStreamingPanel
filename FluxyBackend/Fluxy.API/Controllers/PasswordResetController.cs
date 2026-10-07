using Fluxy.API.Configuration;
using Fluxy.API.Contracts;
using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Users;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Fluxy.API.Controllers
{
    /// <summary>
    /// The password reset flow: whether it exists on this installation, asking for a code, and
    /// applying it.
    /// </summary>
    /// <remarks>
    /// Nothing here requires a session - by definition the visitor cannot sign in, which is why
    /// they are here - so the three guards in <see cref="AuthControllerBase"/> are all this
    /// controller has, and it runs them in the order that base class states.
    ///
    /// The limits are the sign-in ones, deliberately: this endpoint is a way to make the server
    /// compare a value it should not be comparing, exactly like a sign-in, and it gets the same
    /// five attempts per window for the same reason. The window is keyed separately from the
    /// sign-in ones, so resetting a password does not spend the attempts of somebody trying to
    /// sign in and the other way round.
    ///
    /// The reCAPTCHA options are the sign-in ones too - they are one set for the whole
    /// installation - but the action names are this controller's own. An action is fixed by the
    /// endpoint and never taken from the request, so a token minted for the sign-in form is not
    /// spendable here, the same way a registration token is not spendable on a sign-in.
    ///
    /// The route carries no <c>api</c> segment, for the reason the registration controller
    /// gives, and stays under <c>auth</c> so the frontend's dev proxy reaches it unchanged.
    /// </remarks>
    [ApiController]
    [Route("auth")]
    [Produces("application/json")]
    public sealed class PasswordResetController : AuthControllerBase
    {
        /// <summary>
        /// Action a reset request token has to have been minted for. A frontend asks for it by
        /// name, so the two have to agree, and this is the backend half of that agreement.
        /// </summary>
        public const string RequestCaptchaAction = "password_reset";

        /// <summary>Action a reset confirmation token has to have been minted for.</summary>
        public const string ConfirmCaptchaAction = "password_reset_confirm";

        /// <summary>Code reported when a client spent its reset requests for this window.</summary>
        public const string RequestThrottledCode = "password_reset_rate_limited";

        /// <summary>Code reported when a client spent its reset confirmations for this window.</summary>
        public const string ConfirmThrottledCode = "password_reset_confirmation_rate_limited";

        private readonly IPasswordResetService _passwordResetService;
        private readonly IRecaptchaValidator _captchaValidator;
        private readonly ITokenService _tokenService;

        /// <summary>
        /// Initializes a new instance of the <see cref="PasswordResetController"/> class.
        /// </summary>
        /// <param name="passwordResetService">Service that owns the reset rules.</param>
        /// <param name="captchaValidator">Check that a request came from a person.</param>
        /// <param name="tokenService">Service that ends the sessions a changed password leaves behind.</param>
        /// <param name="antiforgery">Service that builds and checks the CSRF token.</param>
        /// <param name="throttle">Counter that limits how often one client may try.</param>
        /// <param name="rateLimits">Live limits, so a configuration reload applies without a restart.</param>
        /// <param name="logger">Logger the refusals worth remembering are reported to.</param>
        public PasswordResetController(
            IPasswordResetService passwordResetService,
            IRecaptchaValidator captchaValidator,
            ITokenService tokenService,
            IAntiforgery antiforgery,
            IAttemptThrottle throttle,
            IOptionsMonitor<RateLimitOptions> rateLimits,
            ILogger<PasswordResetController> logger)
            : base(antiforgery, throttle, rateLimits, logger)
        {
            _passwordResetService = passwordResetService;
            _captchaValidator = captchaValidator;
            _tokenService = tokenService;
        }

        /// <summary>
        /// Reports whether this installation can send a code at all.
        /// </summary>
        /// <remarks>
        /// It exists so a page can decide what to render before anybody types into a form that
        /// could never be finished. It costs one property read and no lookup, is not rate
        /// limited and carries no secret: whether a mail server is configured is already
        /// disclosed by the answer to the request itself, only later and after the visitor had
        /// done the work.
        ///
        /// The check is read from the sender rather than from configuration, because the sender
        /// is what decides - a partially configured section, a user without a password, all of
        /// it resolves to the same answer here and to the same refusal there.
        /// </remarks>
        /// <returns>200 with whether the reset flow is available.</returns>
        [HttpGet("password-resets/status")]
        [ProducesResponseType<PasswordStatusResponse>(StatusCodes.Status200OK)]
        public IActionResult Status()
            => Ok(new PasswordStatusResponse
            {
                Configured = _passwordResetService.IsConfigured
            });

        /// <summary>
        /// Checks the username and email pair, stores the requested password and mails the code
        /// that applies it.
        /// </summary>
        /// <param name="request">The pair and the new password.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// 200 when the code is on its way, 400 when the pair does not belong to one account or
        /// a value is invalid, 429 once the attempts are spent, 502 when the code could not be
        /// mailed, 503 on an installation with no mail server.
        /// </returns>
        [HttpPost("password-resets")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status502BadGateway)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Forgot(
            [FromBody] ForgotPasswordRequest request,
            CancellationToken cancellationToken)
        {
            var clientAddress = ClientAddress;
            var limits = RateLimits.CurrentValue;
            var policy = new AttemptPolicy(limits.LoginLimit, limits.LoginWindow);

            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (!await PassesCaptchaAsync(_captchaValidator, RequestCaptchaAction, clientAddress, cancellationToken))
            {
                return CaptchaRefused();
            }

            // Recorded before the pair is looked up, unlike a registration, and that difference
            // is the point. A registration rejects a mistyped form for free because the work it
            // exists to cap is the hashing. This endpoint exists to cap something else: a pair
            // of values that belong together is worth knowing, and charging for the failures is
            // what keeps that knowledge to the same handful of guesses a sign-in allows. It also
            // means a visitor who mistypes the address waits out a window - the same price the
            // sign-in form has always charged, which is why the limits are shared.
            if (Throttle.RecordAttempt(RequestThrottleKey(clientAddress), policy) > limits.LoginLimit)
            {
                Logger.LogInformation(
                    "Refused a password reset request from {ClientAddress}: the limit of {Limit} " +
                    "attempts per window is spent.",
                    clientAddress,
                    limits.LoginLimit);

                return Throttled(RequestThrottledCode, limits.LoginWindow);
            }

            var outcome = await _passwordResetService.RequestAsync(
                request.Username ?? string.Empty,
                request.Email ?? string.Empty,
                request.Password ?? string.Empty,
                cancellationToken);

            return StatusCode(
                PasswordResetResponses.StatusCodeOf(outcome.Status),
                PasswordResetResponses.Describe(
                    outcome.Status,
                    FieldErrorKeys.FromPropertyNames(outcome.Errors)));
        }

        /// <summary>
        /// Applies the password whose code was mailed out, and ends every session the account
        /// was holding.
        /// </summary>
        /// <param name="request">Login name the reset was asked for, and the code.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>200 once the password is replaced, 400 for a wrong or expired code, 429 once the attempts are spent.</returns>
        /// <remarks>
        /// The sessions end here rather than being left to expire. Somebody asked for a new
        /// password, which means either the owner forgot theirs or somebody else has been trying
        /// - and in the second case the sessions opened with the old password are exactly what
        /// has to stop working. Ending them is free in the first case: the visitor cannot be
        /// signed in anyway, and the devices that are signed in are the ones being defended
        /// against.
        /// </remarks>
        [HttpPost("password-resets/confirm")]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<MessageResponse>(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Reset(
            [FromBody] ResetPasswordRequest request,
            CancellationToken cancellationToken)
        {
            var clientAddress = ClientAddress;
            var limits = RateLimits.CurrentValue;
            var policy = new AttemptPolicy(limits.LoginLimit, limits.LoginWindow);

            if (await RejectsCsrfAsync() is { } csrfFailure)
            {
                return csrfFailure;
            }

            if (!await PassesCaptchaAsync(_captchaValidator, ConfirmCaptchaAction, clientAddress, cancellationToken))
            {
                return CaptchaRefused();
            }

            // Recorded before the code is compared, for the reason the profile confirmation
            // gives: verifying a code costs as much as hashing a password does, and a six digit
            // code is a small enough space to guess at.
            if (Throttle.RecordAttempt(ConfirmThrottleKey(clientAddress), policy) > limits.LoginLimit)
            {
                Logger.LogInformation(
                    "Refused a password reset confirmation from {ClientAddress}: the limit of " +
                    "{Limit} attempts per window is spent.",
                    clientAddress,
                    limits.LoginLimit);

                return Throttled(ConfirmThrottledCode, limits.LoginWindow);
            }

            var outcome = await _passwordResetService.ConfirmAsync(
                request.Username ?? string.Empty,
                request.Code ?? string.Empty,
                cancellationToken);

            if (outcome.Status is PasswordResetStatus.Confirmed && outcome.UserId is { } userId)
            {
                await _tokenService.RevokeAllSessionsAsync(userId, cancellationToken);

                Logger.LogInformation(
                    "Completed a password reset for account {UserId} and ended its sessions.",
                    userId);
            }

            return StatusCode(
                PasswordResetResponses.StatusCodeOf(outcome.Status),
                PasswordResetResponses.Describe(
                    outcome.Status,
                    FieldErrorKeys.FromPropertyNames(outcome.Errors)));
        }

        private static string RequestThrottleKey(string clientAddress)
            => $"password_reset:{clientAddress}";

        private static string ConfirmThrottleKey(string clientAddress)
            => $"password_reset_confirm:{clientAddress}";
    }
}
