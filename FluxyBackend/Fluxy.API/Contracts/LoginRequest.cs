using System.ComponentModel.DataAnnotations;
using Fluxy.Application.Services.Registration;

namespace Fluxy.API.Contracts
{
    /// <summary>
    /// What a sign-in form submits.
    /// </summary>
    /// <remarks>
    /// The attributes are bounds, not the registration policy, and the difference is deliberate.
    /// Registration checks complexity because it is choosing a password; a sign-in is not. The
    /// password that reaches this endpoint is one the account already has, and the account may
    /// well have been created before a complexity rule existed. Rejecting such a password here
    /// would lock its owner out of their own account permanently, and it would also tell an
    /// attacker something: a 400 for a password that fails a shape check and a 401 for one that
    /// does not is a free oracle for "this password is worth guessing".
    ///
    /// So the only checks here are the ones that reject a body which is not a credential at
    /// all - a missing name, a name that cannot exist, a password too long to be anything but a
    /// denial of service. Everything else is answered with the single refusal that a wrong
    /// password gets, because from the caller's side they are the same event.
    ///
    /// The length bounds are the same numbers <c>RegistrationPolicy</c> uses, read from it
    /// rather than repeated, so the two cannot drift into different limits.
    /// </remarks>
    public sealed class LoginRequest
    {
        /// <summary>
        /// Login name as typed. Trimmed before it is compared, and compared case sensitively like
        /// every other username in the system.
        /// </summary>
        [Required]
        [StringLength(
            RegistrationPolicy.UsernameMaxLength,
            MinimumLength = RegistrationPolicy.UsernameMinLength)]
        public string? Username { get; init; }

        /// <summary>
        /// Clear text password as typed. Verified against a hash and never stored, logged or
        /// echoed back.
        /// </summary>
        /// <remarks>
        /// The minimum is the same as at registration, and it is there to bound a body rather
        /// than to enforce a rule - a shorter string cannot be a stored password, so answering
        /// it as invalid costs nothing and tells nobody anything they could not already guess.
        /// </remarks>
        [Required]
        [StringLength(
            RegistrationPolicy.PasswordMaxLength,
            MinimumLength = RegistrationPolicy.PasswordMinLength)]
        public string? Password { get; init; }
    }
}
