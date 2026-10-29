namespace Fluxy.Core.Subscriptions.Enums
{
    /// <summary>
    /// The state a subscription is in right now, as the screen shows it and as the list is
    /// filtered by.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Computed, never stored.</b> The row carries the facts the answer is made of - the
    /// expiry, the trial flag - and the owner's own state is a fact about the account, so a
    /// status column would be a third copy that could disagree with both. The precedence is
    /// <see cref="Expired"/> &gt; <see cref="Blocked"/> &gt; <see cref="Trial"/> &gt;
    /// <see cref="Active"/>, decided once in <c>SubscriptionStatusComposition</c>.
    /// </para>
    /// <para>
    /// Like <c>UserStatus</c> these are states of one subscription rather than levels of
    /// access, so the numeric order carries no meaning: compare for the exact member, never
    /// with <c>&gt;=</c>. The values are fixed because a client matches on the name and the
    /// list filter parses one, not because anything stores the number.
    /// </para>
    /// </remarks>
    public enum SubscriptionStatus
    {
        /// <summary>Nothing is wrong: not expired, the owner may sign in, and it is not a trial.</summary>
        Active = 1,

        /// <summary>The subscription is a trial and has not expired.</summary>
        Trial = 2,

        /// <summary>The owner's effective status is Blocked.</summary>
        Blocked = 3,

        /// <summary>The expiry is in the past.</summary>
        Expired = 4
    }
}
