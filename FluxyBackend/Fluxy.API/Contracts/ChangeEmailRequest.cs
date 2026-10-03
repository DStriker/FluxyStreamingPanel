using System.ComponentModel.DataAnnotations;
using Fluxy.Application.Services.Registration;

namespace Fluxy.API.Contracts
{
    /// <summary>What a signed-in visitor submits to change their email address.</summary>
    /// <remarks>
    /// The address is not verified by the body being well formed - the code that arrives at it
    /// is the verification, which is the whole reason the change is not applied at once. See
    /// <see cref="ChangeUsernameRequest"/> for why <c>currentPassword</c> has no minimum length.
    /// </remarks>
    public sealed class ChangeEmailRequest
    {
        /// <summary>Password the account signs in with, proving the session is the owner's.</summary>
        [Required]
        [StringLength(RegistrationPolicy.PasswordMaxLength)]
        public string? CurrentPassword { get; init; }

        /// <summary>New address. Folded to lower case before anything is compared.</summary>
        [Required]
        [EmailAddress]
        [StringLength(RegistrationPolicy.EmailMaxLength)]
        public string? Email { get; init; }
    }
}
