using System.ComponentModel.DataAnnotations;
using Fluxy.Application.Services.Registration;

namespace Fluxy.API.Contracts
{
    /// <summary>
    /// What a visitor submits to ask for a password reset.
    /// </summary>
    /// <remarks>
    /// The pair is the whole first half of the proof: a username and an email that have to
    /// belong to the same account, both supplied by somebody who is not signed in. The second
    /// half - the code - is what actually authorizes the change, which is why the password here
    /// is validated, hashed and held until then.
    ///
    /// There is no <c>confirmPassword</c> field, the same reason there is none on
    /// <see cref="PatchProfileRequest"/>: the form checks it before sending. The password is
    /// spelled <c>password</c> rather than <c>newPassword</c> so that it lands on the same
    /// antd field name the registration form uses - the form that renders it is that form,
    /// reused, and a rejection reported under a name the form has no input for would reach the
    /// visitor as a bare toast instead of as a reason under the input.
    /// </remarks>
    public sealed class ForgotPasswordRequest
    {
        /// <summary>Login name of the account, compared case sensitively.</summary>
        [Required]
        [StringLength(
            RegistrationPolicy.UsernameMaxLength,
            MinimumLength = RegistrationPolicy.UsernameMinLength)]
        public string? Username { get; init; }

        /// <summary>Address the account receives mail at. Folded to lower case.</summary>
        [Required]
        [EmailAddress]
        [StringLength(RegistrationPolicy.EmailMaxLength)]
        public string? Email { get; init; }

        /// <summary>
        /// Replacement password in clear text. It is never echoed back and never stored as
        /// given - the pending row holds a hash of it.
        /// </summary>
        [Required]
        [StringLength(
            RegistrationPolicy.PasswordMaxLength,
            MinimumLength = RegistrationPolicy.PasswordMinLength)]
        public string? Password { get; init; }
    }
}
