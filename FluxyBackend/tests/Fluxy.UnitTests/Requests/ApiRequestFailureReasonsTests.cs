using Fluxy.Core.Requests.Enums;
using Fluxy.Core.Requests.Utils;

namespace Fluxy.UnitTests.Requests
{
    /// <summary>
    /// The wire vocabulary of the API request failure log: each reason has exactly one snake_case
    /// name, and that name is the only spelling a filter accepts.
    /// </summary>
    /// <remarks>
    /// The wire name is what a query string and a response carry, while the enum member is what
    /// the database stores. A name added on one side and forgotten on the other would make a filter
    /// silently refuse a value the screen offers, which is why the round trip is pinned for all six
    /// members rather than for a representative two.
    /// </remarks>
    public sealed class ApiRequestFailureReasonsTests
    {
        [Theory]
        [InlineData(ApiRequestFailureReason.UnknownId, "unknown_id")]
        [InlineData(ApiRequestFailureReason.GeoIpViolation, "geo_ip_violation")]
        [InlineData(ApiRequestFailureReason.AuthFailed, "auth_failed")]
        [InlineData(ApiRequestFailureReason.TooManyConnections, "too_many_connections")]
        [InlineData(ApiRequestFailureReason.SubscriptionExpired, "subscription_expired")]
        [InlineData(ApiRequestFailureReason.UserBlocked, "user_blocked")]
        public void EveryMemberHasOneWireNameAndParsesBackToItself(
            ApiRequestFailureReason reason,
            string wire)
        {
            Assert.Equal(wire, ApiRequestFailureReasons.WireName(reason));

            Assert.True(ApiRequestFailureReasons.TryParseWire(wire, out var parsed));
            Assert.Equal(reason, parsed);
        }

        [Theory]
        [InlineData("UNKNOWN_ID", ApiRequestFailureReason.UnknownId)]
        [InlineData(" Geo_Ip_Violation ", ApiRequestFailureReason.GeoIpViolation)]
        [InlineData("AUTH_FAILED", ApiRequestFailureReason.AuthFailed)]
        [InlineData("Too_Many_Connections", ApiRequestFailureReason.TooManyConnections)]
        [InlineData("SUBSCRIPTION_EXPIRED", ApiRequestFailureReason.SubscriptionExpired)]
        [InlineData(" user_blocked ", ApiRequestFailureReason.UserBlocked)]
        public void AWireNameIsReadCaseInsensitivelyAndTrimmed(
            string value,
            ApiRequestFailureReason expected)
        {
            Assert.True(ApiRequestFailureReasons.TryParseWire(value, out var parsed));
            Assert.Equal(expected, parsed);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("unknown")]
        [InlineData("unknown-id")]
        [InlineData("UnknownId")]
        [InlineData("auth failed")]
        [InlineData("blocked")]
        public void AnythingElseNamesNoReason(string? value)
        {
            Assert.False(ApiRequestFailureReasons.TryParseWire(value, out var parsed));
            Assert.Equal(default, parsed);
        }

        [Fact]
        public void TheCatalogHoldsExactlyTheSixMembersInSchemaOrder()
        {
            Assert.Equal(
                [
                    ApiRequestFailureReason.UnknownId,
                    ApiRequestFailureReason.GeoIpViolation,
                    ApiRequestFailureReason.AuthFailed,
                    ApiRequestFailureReason.TooManyConnections,
                    ApiRequestFailureReason.SubscriptionExpired,
                    ApiRequestFailureReason.UserBlocked
                ],
                ApiRequestFailureReasons.All);

            Assert.Equal(
                ApiRequestFailureReasons.All,
                Enum.GetValues<ApiRequestFailureReason>());
        }

        [Fact]
        public void AValueThatNamesNoMemberHasNoWireName()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ApiRequestFailureReasons.WireName((ApiRequestFailureReason)99));
        }

        [Fact]
        public void TheHintNamesEveryWireValue()
        {
            foreach (var reason in ApiRequestFailureReasons.All)
            {
                Assert.Contains(
                    ApiRequestFailureReasons.WireName(reason),
                    ApiRequestFailureReasons.UnknownReasonHint,
                    StringComparison.Ordinal);
            }
        }
    }
}
