using System.ComponentModel.DataAnnotations;
using Fluxy.Application.Services.Registration;

namespace Fluxy.API.Contracts
{
    /// <summary>
    /// What a visitor submits to apply the password reset whose code was mailed out.
    /// </summary>
    /// <remarks>
    /// The login name travels with the code because a code is not an identifier: it is stored as
    /// a hash, it cannot be looked up backwards, and one account's code must not be spendable by
    /// naming another. The name picks the pending row; the code decides whether it opens.
    ///
    /// The account is not in a session either - this endpoint is reached by somebody who, by
    /// definition, cannot sign in.
    /// </remarks>
    public sealed class ResetPasswordRequest
    {
        /// <summary>Login name the reset was requested for, compared case sensitively.</summary>
        [Required]
        [StringLength(
            RegistrationPolicy.UsernameMaxLength,
            MinimumLength = 1)]
        public string? Username { get; init; }

        /// <summary>The code that was mailed to the address.</summary>
        /// <remarks>
        /// The length is not pinned to the configured code length, for the reason
        /// <see cref="ConfirmRegistrationRequest.Code"/> gives.
        /// </remarks>
        [Required]
        [StringLength(32, MinimumLength = 1)]
        public string? Code { get; init; }
    }
}
