using Fluxy.Core.Models.Users;

namespace Fluxy.Core.Models.Authentication
{
    /// <summary>
    /// The state of an account as it stands right now, which is not necessarily what a token
    /// says about it.
    /// </summary>
    /// <remarks>
    /// A signed token is a statement about the moment it was issued, and it stays true for its
    /// whole life. That is what makes it cheap to check and also what makes it wrong on its own:
    /// an account can be blocked, deleted or promoted after the token was written, and the token
    /// cannot know. Reading the row back is what closes the gap, which is why this type exists
    /// and why it is read on every authorized request rather than only when one is issued.
    ///
    /// <see cref="Status"/> is null rather than a fourth state when the row is gone, because
    /// "no such account" is the absence of the thing this type describes and not another kind
    /// of it. The three states that do exist are the project's own
    /// <see cref="UserStatus"/>, used here only for the one question that matters: may this
    /// account act at all right now.
    /// </remarks>
    public sealed record AccountStanding
    {
        /// <summary>Current state of the account, or null when no row carries the identifier.</summary>
        public UserStatus? Status { get; init; }

        /// <summary>
        /// Role the account holds now. Only meaningful together with
        /// <see cref="IsActive"/>, and deliberately left at its default for a missing row so a
        /// caller cannot read a role out of an account it did not find.
        /// </summary>
        public UserRole Role { get; init; } = UserRole.Client;

        /// <summary>
        /// Whether the account may act: it exists and it is
        /// <see cref="UserStatus.Registered"/>.
        /// </summary>
        public bool IsActive => Status is UserStatus.Registered;
    }
}
