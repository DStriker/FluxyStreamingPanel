using System.ComponentModel.DataAnnotations;

namespace Fluxy.API.Contracts
{
    /// <summary>
    /// What a visitor submits to confirm a pending registration.
    /// </summary>
    public sealed class ConfirmRegistrationRequest
    {
        /// <summary>Address the account was registered with, in any spelling.</summary>
        [Required]
        [EmailAddress]
        [StringLength(254)]
        public string? Email { get; init; }

        /// <summary>
        /// The code that was mailed to the address.
        /// </summary>
        /// <remarks>
        /// The length is deliberately not pinned to the configured code length. That value lives
        /// in configuration, an attribute cannot read it, and a code of the wrong length simply
        /// fails verification - which is the same answer a wrong code gets. The bound exists to
        /// keep an unbounded string away from the deliberately expensive comparison, not to
        /// describe the format.
        /// </remarks>
        [Required]
        [StringLength(32, MinimumLength = 1)]
        public string? Code { get; init; }
    }
}