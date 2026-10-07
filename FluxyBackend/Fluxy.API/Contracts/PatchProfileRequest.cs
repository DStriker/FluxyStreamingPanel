using System.ComponentModel.DataAnnotations;
using Fluxy.Application.Services.Registration;

namespace Fluxy.API.Contracts
{
    /// <summary>
    /// What a signed-in visitor submits to change their own profile — one change per request.
    /// </summary>
    /// <remarks>
    /// This body replaced the five <c>POST /auth/profile/*</c> endpoints in the May 2026 REST
    /// pass: a profile is one resource, so its edits are one <c>PATCH</c> whose shape says which
    /// change is being asked for, not five paths the caller picks between.
    ///
    /// <b>Absent means "leave it alone".</b> Every property is nullable and none carries
    /// <c>[Required]</c>, because a partial update that changed a field left out of the body
    /// would not be partial — the endpoint counts the kinds present (see <c>ProfileController</c>)
    /// and refuses a body naming none or more than one. The one exception in shape is
    /// <see cref="TimeZone"/>, where an explicit empty string clears the preference and a null
    /// (absent) leaves it untouched.
    ///
    /// The attributes are the cheap half of the policy, exactly as on <see cref="RegisterRequest"/>:
    /// they reject a malformed body before it reaches a service and list every offending field at
    /// once. <c>RegistrationPolicy</c> is applied again by the service, because a client is free
    /// to ignore whatever the server advertises.
    ///
    /// <see cref="CurrentPassword"/> has no minimum length of its own — only the maximum —
    /// because the account may predate the policy a new password has to satisfy, and refusing to
    /// read a password the account actually has would lock that account out of its own settings.
    /// It is required by the endpoint (not by an attribute) for every change but the time zone,
    /// for the same reason the old per-field bodies required it: the session alone may set a
    /// rendering preference, but changing what the account <em>is</em> needs the password.
    /// </remarks>
    public sealed class PatchProfileRequest
    {
        /// <summary>Password the account signs in with, proving the session is the owner's.</summary>
        [StringLength(RegistrationPolicy.PasswordMaxLength)]
        public string? CurrentPassword { get; init; }

        /// <summary>New login name. Trimmed, and compared case sensitively.</summary>
        [StringLength(
            RegistrationPolicy.UsernameMaxLength,
            MinimumLength = RegistrationPolicy.UsernameMinLength)]
        public string? Username { get; init; }

        /// <summary>New address. Folded to lower case before anything is compared.</summary>
        [EmailAddress]
        [StringLength(RegistrationPolicy.EmailMaxLength)]
        public string? Email { get; init; }

        /// <summary>
        /// Replacement password in clear text. It is never echoed back and never stored as given.
        /// There is no second field repeating it — the form checks the confirmation before
        /// anything is sent, so the body cannot carry two values a server would have to rank.
        /// </summary>
        [StringLength(
            RegistrationPolicy.PasswordMaxLength,
            MinimumLength = RegistrationPolicy.PasswordMinLength)]
        public string? NewPassword { get; init; }

        /// <summary>
        /// IANA identifier to store (`Europe/Moscow`), an empty string to clear the choice and
        /// fall back to whatever the browser reports, or null to leave the preference alone.
        /// </summary>
        /// <remarks>
        /// The bound keeps an unbounded string away from the identifier lookup rather than
        /// describing the format: every real zone id is far shorter, and one that is not simply
        /// fails the lookup the service does. The JSON spelling is <c>timeZone</c> on both halves
        /// of the feature — this body and the <c>ProfileResponse</c> — because one fact with two
        /// spellings is a typo waiting to be read as a missing field.
        /// </remarks>
        [StringLength(64)]
        public string? TimeZone { get; init; }

        /// <summary>
        /// The new login guard — the two switches and the three allow lists — or null to leave
        /// the guard untouched.
        /// </summary>
        /// <remarks>
        /// A nested object rather than five loose properties: the guard is replaced as a whole by
        /// the service, so presenting it has to be one present/absent choice rather than a guess
        /// built from whichever of the five happened to be in the body. See
        /// <see cref="PatchLoginGuardRequest"/> for its limits.
        /// </remarks>
        public PatchLoginGuardRequest? LoginGuard { get; init; }
    }
}
