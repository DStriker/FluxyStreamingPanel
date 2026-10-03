using System.ComponentModel.DataAnnotations;
using Fluxy.Application.Services.Registration;

namespace Fluxy.API.Contracts
{
    /// <summary>What a signed-in visitor submits to change their password.</summary>
    /// <remarks>
    /// There is no second field repeating the new password. The form asks for a confirmation,
    /// and it checks it before anything is sent - one field in the body means a client cannot
    /// submit two different values and a server cannot have an opinion about which of them wins.
    /// The registration form is built the same way.
    /// </remarks>
    public sealed class ChangePasswordRequest
    {
        /// <summary>Password the account signs in with, proving the session is the owner's.</summary>
        [Required]
        [StringLength(RegistrationPolicy.PasswordMaxLength)]
        public string? CurrentPassword { get; init; }

        /// <summary>
        /// Replacement password in clear text. It is never echoed back and never stored as given.
        /// </summary>
        [Required]
        [StringLength(
            RegistrationPolicy.PasswordMaxLength,
            MinimumLength = RegistrationPolicy.PasswordMinLength)]
        public string? NewPassword { get; init; }
    }
}
