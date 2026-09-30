using System.ComponentModel.DataAnnotations;
using Fluxy.Application.Services.Registration;

namespace Fluxy.API.Contracts
{
    /// <summary>
    /// What a visitor submits to ask for a registration.
    /// </summary>
    /// <remarks>
    /// The attributes on this type are the cheap half of the registration policy: they reject a
    /// malformed body before it reaches a service, and they produce a list of every offending
    /// field at once instead of the first one. They are not the authority -
    /// <c>RegistrationService</c> applies the same numbers as the binding source, because a
    /// client is free to ignore whatever the server advertises and post whatever it likes.
    ///
    /// A body may also carry <c>captchaAction</c> and <c>captchaToken</c>. They are ignored on
    /// purpose: the token is read from the request header, and the action is fixed by the
    /// endpoint rather than chosen by the caller, so a client cannot ask for a weaker check.
    /// Unknown properties are dropped without an error.
    /// </remarks>
    public sealed class RegisterRequest
    {
        /// <summary>Login name the visitor wants. Trimmed, and compared case sensitively.</summary>
        [Required]
        [StringLength(
            RegistrationPolicy.UsernameMaxLength,
            MinimumLength = RegistrationPolicy.UsernameMinLength)]
        public string? Username { get; init; }

        /// <summary>
        /// Address the registration code is sent to. Trimmed and folded to lower case before
        /// anything is compared, so two spellings of one mailbox cannot become two accounts.
        /// </summary>
        [Required]
        [EmailAddress]
        [StringLength(RegistrationPolicy.EmailMaxLength)]
        public string? Email { get; init; }

        /// <summary>
        /// Clear text password. It is never echoed back and never stored as given.
        /// </summary>
        [Required]
        [StringLength(
            RegistrationPolicy.PasswordMaxLength,
            MinimumLength = RegistrationPolicy.PasswordMinLength)]
        public string? Password { get; init; }
    }
}