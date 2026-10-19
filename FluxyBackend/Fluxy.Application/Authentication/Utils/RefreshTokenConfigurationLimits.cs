using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Fluxy.Application.Authentication.Utils
{
    /// <summary>
    /// Column widths of the <c>refresh_tokens</c> table, mirrored here so the service that
    /// writes to it does not have to reference the data access layer's configuration types.
    /// </summary>
    /// <remarks>
    /// A duplicate of two constants, and a deliberate one: the alternative is for the token
    /// service to name <c>RefreshTokenConfiguration</c>, which would put a persistence
    /// decision in the middle of the business service. The mapping is the authority and the
    /// values below have to agree with it.
    /// </remarks>
    public static class RefreshTokenConfigurationLimits
    {
        /// <summary>Longest client address the column stores.</summary>
        public const int ClientAddress = 45;

        /// <summary>Longest user agent the column stores.</summary>
        public const int UserAgent = 255;
    }
}
