using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.Core.Subscriptions.Utils
{
    /// <summary>
    /// The one place where a subscription's stored facts become the state everybody reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The precedence is <see cref="SubscriptionStatus.Expired"/> &gt;
    /// <see cref="SubscriptionStatus.Blocked"/> &gt; <see cref="SubscriptionStatus.Trial"/> &gt;
    /// <see cref="SubscriptionStatus.Active"/>, by name rather than by number - the enum is not
    /// an ordered scale, so a <c>&gt;=</c> here would be a bug waiting for the day somebody
    /// renumbers a member.
    /// </para>
    /// <para>
    /// <b>Expired first, and that is the point of an expiry.</b> A lapsed subscription is
    /// lapsed whatever the owner's account is doing and whether or not it was a trial; saying
    /// "blocked" about it would invite an operator to unblock the owner and expect the
    /// subscription back. Blocked comes next because an account that may not sign in is a
    /// subscription that cannot be watched, and trial last because a trial is a fact about the
    /// subscription rather than a fault.
    /// </para>
    /// <para>
    /// The same rule is written as a SQL predicate by the repository, for the list's status
    /// filter, and the two must agree: the filter is what the table is told to show, and the tag
    /// is what it draws. <see cref="IsExpired"/> and <see cref="IsTrial"/> are the halves that
    /// predicate needs, so the two cannot drift on the boundary.
    /// </para>
    /// </remarks>
    public static class SubscriptionStatusComposition
    {
        /// <summary>Every state a subscription can be in, in no particular order.</summary>
        public static IReadOnlyList<SubscriptionStatus> All { get; } =
        [
            SubscriptionStatus.Active,
            SubscriptionStatus.Trial,
            SubscriptionStatus.Blocked,
            SubscriptionStatus.Expired
        ];

        /// <summary>
        /// The state of a subscription, given the three facts it is made of.
        /// </summary>
        /// <param name="expireAt">Moment it lapses, or null for "never expires".</param>
        /// <param name="ownerBlocked">Whether the owner's effective status is Blocked.</param>
        /// <param name="isTrial">Whether the subscription counts as a trial.</param>
        /// <param name="now">The moment the question is asked about.</param>
        /// <returns>The state, by the precedence above.</returns>
        public static SubscriptionStatus Compose(
            DateTimeOffset? expireAt,
            bool ownerBlocked,
            bool isTrial,
            DateTimeOffset now)
        {
            if (IsExpired(expireAt, now))
            {
                return SubscriptionStatus.Expired;
            }

            if (ownerBlocked)
            {
                return SubscriptionStatus.Blocked;
            }

            return isTrial ? SubscriptionStatus.Trial : SubscriptionStatus.Active;
        }

        /// <summary>Whether an expiry has passed. A null expiry never has.</summary>
        /// <param name="expireAt">Moment it lapses, or null for "never expires".</param>
        /// <param name="now">The moment the question is asked about.</param>
        public static bool IsExpired(DateTimeOffset? expireAt, DateTimeOffset now)
            => expireAt is { } at && at <= now;
    }
}
