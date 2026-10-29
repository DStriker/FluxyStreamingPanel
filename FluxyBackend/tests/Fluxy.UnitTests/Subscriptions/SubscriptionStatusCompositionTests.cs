using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Utils;

namespace Fluxy.UnitTests.Subscriptions
{
    /// <summary>
    /// The computed status of a subscription: expired &gt; blocked &gt; trial &gt; active, with
    /// a null expiry never having passed.
    /// </summary>
    /// <remarks>
    /// Every precedence pair is exercised on its own, because the whole value of the rule is
    /// that the pairs disagree: a trial whose owner is blocked is <c>Blocked</c>, and a blocked
    /// trial that has lapsed is <c>Expired</c>. A test that only checked one fact at a time
    /// would pass over an implementation that returned the first thing it noticed.
    /// </remarks>
    public sealed class SubscriptionStatusCompositionTests
    {
        private static readonly DateTimeOffset Now = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public void AnUnexpiredNonTrialSubscriptionOfAPermittedOwnerIsActive()
        {
            Assert.Equal(
                SubscriptionStatus.Active,
                SubscriptionStatusComposition.Compose(Now.AddDays(1), false, false, Now));
        }

        [Fact]
        public void AnUnexpiredTrialIsATrial()
        {
            Assert.Equal(
                SubscriptionStatus.Trial,
                SubscriptionStatusComposition.Compose(Now.AddDays(1), false, true, Now));
        }

        [Fact]
        public void BlockedBeatsTrial()
        {
            // The case the requirement names: an unexpired trial whose owner becomes blocked.
            Assert.Equal(
                SubscriptionStatus.Blocked,
                SubscriptionStatusComposition.Compose(Now.AddDays(1), true, true, Now));
        }

        [Fact]
        public void BlockedBeatsActive()
        {
            Assert.Equal(
                SubscriptionStatus.Blocked,
                SubscriptionStatusComposition.Compose(Now.AddDays(1), true, false, Now));
        }

        [Fact]
        public void ExpiredBeatsBlockedAndTrial()
        {
            // The other case the requirement names: a trial of a blocked owner that has passed
            // its expiry reads as expired, so an operator is not invited to unblock the owner
            // and expect the subscription back.
            Assert.Equal(
                SubscriptionStatus.Expired,
                SubscriptionStatusComposition.Compose(Now.AddDays(-1), true, true, Now));
        }

        [Fact]
        public void ExpiredBeatsActive()
        {
            Assert.Equal(
                SubscriptionStatus.Expired,
                SubscriptionStatusComposition.Compose(Now.AddDays(-1), false, false, Now));
        }

        [Fact]
        public void ASubscriptionWithNoExpiryNeverExpires()
        {
            Assert.Equal(
                SubscriptionStatus.Active,
                SubscriptionStatusComposition.Compose(null, false, false, Now));
        }

        [Fact]
        public void ASubscriptionWithNoExpiryIsStillTrialWhenItIsOne()
        {
            Assert.Equal(
                SubscriptionStatus.Trial,
                SubscriptionStatusComposition.Compose(null, false, true, Now));
        }

        [Fact]
        public void AnExpiryEqualToTheMomentHasPassed()
        {
            // The boundary is the moment itself rather than the moment after it: a subscription
            // that lapses "now" is over, and the list tag and the filter must agree about that
            // exact instant.
            Assert.True(SubscriptionStatusComposition.IsExpired(Now, Now));
            Assert.Equal(
                SubscriptionStatus.Expired,
                SubscriptionStatusComposition.Compose(Now, false, false, Now));
        }

        [Fact]
        public void ASubscriptionOneTickPastTheMomentHasNotExpired()
        {
            Assert.False(SubscriptionStatusComposition.IsExpired(Now.AddTicks(1), Now));
        }

        [Fact]
        public void EveryStateTheCompositionCanAnswerIsNamedInAll()
        {
            // A caller that loops over the states - the filter's options, a test that wants
            // every case - must not be missing one that Compose can produce.
            Assert.Equal(
                SubscriptionStatusComposition.All.Order(),
                new[]
                {
                    SubscriptionStatus.Active,
                    SubscriptionStatus.Trial,
                    SubscriptionStatus.Blocked,
                    SubscriptionStatus.Expired
                }.Order());
        }
    }
}
