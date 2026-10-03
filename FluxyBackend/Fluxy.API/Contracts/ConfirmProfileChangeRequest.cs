using System.ComponentModel.DataAnnotations;

namespace Fluxy.API.Contracts
{
    /// <summary>
    /// What a signed-in visitor submits to finish a change that was mailed a code.
    /// </summary>
    /// <remarks>
    /// No identifier travels with it. The account comes from the access token, so the code only
    /// ever confirms what the session it belongs to asked for - a code is not a credential on
    /// its own and cannot be spent by whoever holds it.
    /// </remarks>
    public sealed class ConfirmProfileChangeRequest
    {
        /// <summary>
        /// The code that was mailed to the account.
        /// </summary>
        /// <remarks>
        /// The length is not pinned to the configured code length for the reason
        /// <see cref="ConfirmRegistrationRequest.Code"/> gives: the value lives in configuration,
        /// an attribute cannot read it, and a code of the wrong length simply fails
        /// verification. The bound keeps an unbounded string away from the expensive
        /// comparison rather than describing the format.
        /// </remarks>
        [Required]
        [StringLength(32, MinimumLength = 1)]
        public string? Code { get; init; }
    }
}
