using System.ComponentModel.DataAnnotations;
using Fluxy.Application.Services.Registration;

namespace Fluxy.API.Contracts
{
    /// <summary>
    /// What a signed-in visitor submits to change their login name.
    /// </summary>
    /// <remarks>
    /// The attributes are the cheap half of the policy, exactly as on
    /// <see cref="RegisterRequest"/>: they reject a malformed body before it reaches a service
    /// and list every offending field at once. <c>RegistrationPolicy</c> is applied again by the
    /// service, because a client is free to ignore whatever the server advertises.
    ///
    /// <c>currentPassword</c> is required on all three change bodies. It has no minimum length
    /// of its own - only the maximum - because the account may predate the policy that a new
    /// password has to satisfy, and refusing to read a password the account actually has would
    /// lock that account out of its own settings.
    /// </remarks>
    public sealed class ChangeUsernameRequest
    {
        /// <summary>Password the account signs in with, proving the session is the owner's.</summary>
        [Required]
        [StringLength(RegistrationPolicy.PasswordMaxLength)]
        public string? CurrentPassword { get; init; }

        /// <summary>New login name. Trimmed, and compared case sensitively.</summary>
        [Required]
        [StringLength(
            RegistrationPolicy.UsernameMaxLength,
            MinimumLength = RegistrationPolicy.UsernameMinLength)]
        public string? Username { get; init; }
    }
}
