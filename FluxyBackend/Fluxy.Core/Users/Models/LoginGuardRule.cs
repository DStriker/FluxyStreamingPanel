using Fluxy.Core.Users.Enums;

namespace Fluxy.Core.Users.Models
{
    /// <summary>
    /// One stored entry of one account's login guard allow lists.
    /// </summary>
    /// <remarks>
    /// The read shape of a row, and deliberately without the account identifier: a caller asks
    /// for the rules of an account it has already named, so repeating the key on every row would
    /// be a second copy of the question rather than part of the answer.
    /// </remarks>
    public sealed record LoginGuardRule
    {
        /// <summary>Which allow list this rule belongs to.</summary>
        public required LoginGuardRuleKind Kind { get; init; }

        /// <summary>Canonical value: an address or CIDR, a country code, or an AS number.</summary>
        public required string Value { get; init; }
    }
}
