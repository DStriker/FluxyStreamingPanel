namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// State of a user account.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="UserRole"/>, these are states of one and the same user and not
    /// levels of access, so the numeric order carries no meaning - <c>Blocked = 2</c> does not
    /// mean "more" than <c>Registered = 1</c>. Comparing statuses with <c>&gt;=</c> is a bug;
    /// test for the exact member instead.
    /// </remarks>
    public enum UserStatus
    {
        /// <summary>
        /// The account exists but never confirmed the one-time code sent to its email address,
        /// so it cannot be used yet. <see cref="User.RegisteredAt"/> is null.
        /// </summary>
        Unregistered = 0,

        /// <summary>The account confirmed its email address and may sign in.</summary>
        Registered = 1,

        /// <summary>
        /// The account completed registration but access to it is suspended by an operator.
        /// <see cref="User.RegisteredAt"/> keeps the moment of registration and is not cleared.
        /// </summary>
        Blocked = 2
    }
}