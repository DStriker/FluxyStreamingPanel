using Fluxy.API.Contracts.Settings;
using Fluxy.API.Controllers;
using Fluxy.Core.Settings.Interfaces;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace Fluxy.API.Controllers.Auth
{
    /// <summary>
    /// The one settings fact a visitor who has not signed in may read: the public reCAPTCHA site
    /// key a guest form needs to ask Google for a token.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Anonymous, and deliberately tiny.</b> A registration form, a sign-in form and a password
    /// reset all need the site key before any session exists, so the endpoint carries no
    /// <c>[Authorize]</c> - the shape the other guest controllers have. Nothing else may travel
    /// here: the private key, the flood whitelist and the backup configuration all stay behind
    /// the admin grants.
    /// </para>
    /// <para>
    /// It carries <c>no-store</c> like every other private read, because an operator can change
    /// the key and a shared cache holding the old answer would hand a stale key to every visitor
    /// behind it.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("auth/public-settings")]
    [Produces("application/json")]
    public sealed class PublicSettingsController : ApiControllerBase
    {
        private readonly ISettingsSnapshotProvider _snapshots;

        /// <summary>
        /// Initializes a new instance of the <see cref="PublicSettingsController"/> class.
        /// </summary>
        /// <param name="snapshots">Cache the site key is read from.</param>
        /// <param name="antiforgery">Service the base class holds; this read never checks a token.</param>
        /// <param name="logger">Logger every controller reports through.</param>
        public PublicSettingsController(
            ISettingsSnapshotProvider snapshots,
            IAntiforgery antiforgery,
            ILogger<PublicSettingsController> logger)
            : base(antiforgery, logger)
        {
            _snapshots = snapshots;
        }

        /// <summary>Reports the public reCAPTCHA site key, and nothing else.</summary>
        /// <returns>200 with <c>recaptchaSiteKey</c>; empty when the check is not configured.</returns>
        /// <remarks>
        /// The snapshot is used rather than the service: the value is read on every guest form
        /// and is cached with the rest of the settings, and a database round trip per anonymous
        /// page load would make a public endpoint the busiest reader of the table.
        /// </remarks>
        [HttpGet]
        [ProducesResponseType<PublicSettingsResponse>(StatusCodes.Status200OK)]
        public IActionResult GetPublicSettings()
        {
            NoStore();

            return Ok(new PublicSettingsResponse
            {
                RecaptchaSiteKey = _snapshots.ApplicationSnapshot.RecaptchaSiteKey
            });
        }
    }
}
