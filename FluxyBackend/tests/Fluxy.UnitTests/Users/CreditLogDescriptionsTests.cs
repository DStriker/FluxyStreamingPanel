using Fluxy.Core.Users.Utils;

namespace Fluxy.UnitTests.Users
{
    /// <summary>
    /// The two sentences a credit log row carries: the exact wording, and the fact that a zero
    /// initial balance is a sentence and not an omission.
    /// </summary>
    public sealed class CreditLogDescriptionsTests
    {
        [Fact]
        public void CreationNamesTheInitialBalanceIncludingZero()
        {
            Assert.Equal(
                "Reseller created with initial credits: 0",
                CreditLogDescriptions.ResellerCreated(0));

            Assert.Equal(
                "Reseller created with initial credits: 25",
                CreditLogDescriptions.ResellerCreated(25));
        }

        [Fact]
        public void AChangeNamesBothSidesWithARightwardsArrow()
        {
            Assert.Equal(
                "Credits changed: 5 \u2192 15",
                CreditLogDescriptions.CreditsChanged(5, 15));

            Assert.Equal(
                "Credits changed: 15 \u2192 5",
                CreditLogDescriptions.CreditsChanged(15, 5));
        }

        [Fact]
        public void TheArrowIsTheSingleCodePointTheContractNames()
        {
            var description = CreditLogDescriptions.CreditsChanged(1, 2);

            Assert.Contains('\u2192', description);
            Assert.DoesNotContain("->", description);
        }

        [Fact]
        public void ADecreaseIsNamedByItsTwoBalancesRatherThanASignedAmount()
        {
            // The row's Amount column is where the sign lives; the sentence names the two
            // balances, so a reader sees what moved without reading a minus sign - and a client
            // that renders the sentence and the amount side by side cannot double the sign.
            var description = CreditLogDescriptions.CreditsChanged(20, 8);

            Assert.DoesNotContain('-', description);
            Assert.DoesNotContain('+', description);
            Assert.Equal("Credits changed: 20 \u2192 8", description);
        }
    }
}
