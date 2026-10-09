namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// The one place where an account's own state and its group's state become the state
    /// everybody else reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both are <see cref="UserStatus"/>, and the enum is deliberately <b>not</b> an ordered
    /// scale - there is no <c>&gt;=</c> here to reach for. What the product means by an
    /// account's status is "what may this account do right now", and the two states answer it
    /// together: blocking a group is blocking everyone in it, and leaving a group
    /// <see cref="UserStatus.Unregistered"/> means its members are waiting on that group
    /// whatever their own row says. So the stricter of the two wins, by name rather than by
    /// number.
    /// </para>
    /// <para>
    /// The asymmetry that <i>is</i> allowed is deliberate: <c>Blocked</c> beats
    /// <c>Unregistered</c>. Blocking an account that never finished registration is a
    /// legitimate move against a spammer, and a group whose members are all blocked but
    /// whose own row says <c>Unregistered</c> must not read as "waiting for confirmation" -
    /// it reads as "shut off".
    /// </para>
    /// <para>
    /// Only this and <see cref="AccessChecker"/> may combine the two. A second copy of the
    /// rule would be a second source of truth about an account's state, and the day they
    /// disagree the answer depends on which page the visitor happened to open.
    /// </para>
    /// </remarks>
    public static class UserStatusComposition
    {
        /// <summary>
        /// Every state a row can hold, in no particular order. The enum is not an ordered
        /// scale, so this is a list to loop over rather than a range to walk: it is what lets
        /// a caller that has to answer the same question in SQL ask <see cref="Combine"/> it
        /// instead of writing the rule a second time.
        /// </summary>
        public static IReadOnlyList<UserStatus> All { get; } =
        [
            UserStatus.Unregistered,
            UserStatus.Registered,
            UserStatus.Blocked
        ];

        /// <summary>
        /// The state an account actually is, given its own row and the group it belongs to.
        /// </summary>
        /// <param name="userStatus">State stored on the account itself.</param>
        /// <param name="groupStatus">State stored on the group the account belongs to.</param>
        /// <returns>The stricter of the two.</returns>
        public static UserStatus Combine(UserStatus userStatus, UserStatus groupStatus)
        {
            if (userStatus == UserStatus.Blocked || groupStatus == UserStatus.Blocked)
            {
                return UserStatus.Blocked;
            }

            if (userStatus == UserStatus.Unregistered || groupStatus == UserStatus.Unregistered)
            {
                return UserStatus.Unregistered;
            }

            return UserStatus.Registered;
        }
    }
}
