using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Utils;
using Xunit;

namespace Fluxy.UnitTests.Subscriptions
{
    /// <summary>
    /// The column widths and list bounds of the connection log, and the fact that the table's enum
    /// values are the schema.
    /// </summary>
    /// <remarks>
    /// The widths are shared by the configuration and the transport, so a test here is what keeps
    /// them from drifting apart silently. The enum values are pinned for the same reason every
    /// other enum's are: they are already written into rows, and renumbering one would reinterpret
    /// every stored connection as the other medium without a single query being made.
    /// </remarks>
    public sealed class ConnectionLogListLimitsTests
    {
        [Fact]
        public void TheAddressColumnHoldsTheLongestTextualIpv6Form()
        {
            // The full form with an embedded IPv4 suffix is 45 characters.
            const string longest = "0000:0000:0000:0000:0000:0000:255.255.255.255";

            Assert.Equal(45, ConnectionLogListLimits.IpMaxLength);
            Assert.Equal(45, longest.Length);
        }

        [Fact]
        public void ThePageDefaultsAndCeilingAreTheSharedOnes()
        {
            Assert.Equal(20, ConnectionLogListLimits.DefaultPageSize);
            Assert.Equal(1000, ConnectionLogListLimits.MaxPageSize);
            Assert.Equal(200, ConnectionLogListLimits.MaxSearchLength);
        }

        [Fact]
        public void TheStoredWidthsAreTheOnesTheColumnsDeclare()
        {
            Assert.Equal(512, ConnectionLogListLimits.UserAgentMaxLength);
            Assert.Equal(2, ConnectionLogListLimits.CountryCodeLength);
            Assert.Equal(255, ConnectionLogListLimits.IspMaxLength);
        }

        [Theory]
        [InlineData(ConnectionStreamKind.VideoStream, 1)]
        [InlineData(ConnectionStreamKind.AudioStream, 2)]
        public void TheStreamKindNumbersAreTheSchema(ConnectionStreamKind kind, int stored)
        {
            Assert.Equal(stored, (int)kind);
        }
    }
}
