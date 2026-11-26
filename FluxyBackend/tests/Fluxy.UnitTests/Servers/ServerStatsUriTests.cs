using Fluxy.Application.Servers.Utils;

namespace Fluxy.UnitTests.Servers
{
    /// <summary>
    /// The address a server's statistics are read from: IPv4 as stored, IPv6 bracketed, and
    /// anything that is not an address or a port answered as unbuildable rather than thrown.
    /// </summary>
    /// <remarks>
    /// The builder is where the A1 assumption about plain HTTP lives, so it is tested without a
    /// host: one test per shape the stored columns can actually hold.
    /// </remarks>
    public sealed class ServerStatsUriTests
    {
        [Fact]
        public void AnIpv4AddressBecomesAPlainHttpUri()
        {
            Assert.True(ServerStatsUri.TryBuild("203.0.113.7", 6200, out var uri));

            Assert.Equal("http://203.0.113.7:6200/api/stats", uri.ToString());
        }

        [Fact]
        public void AnIpv6LiteralIsBracketed()
        {
            Assert.True(ServerStatsUri.TryBuild("::1", 6200, out var uri));

            Assert.Equal("http://[::1]:6200/api/stats", uri.ToString());
        }

        [Fact]
        public void AnExpandedIpv6LiteralIsBracketedToo()
        {
            Assert.True(ServerStatsUri.TryBuild("2001:db8::1", 5159, out var uri));

            Assert.Equal("http://[2001:db8::1]:5159/api/stats", uri.ToString());
        }

        [Theory]
        [InlineData("not-an-address")]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("203.0.113.7:5159")]
        [InlineData("localhost")]
        public void AnythingThatIsNotAnAddressAnswersFalse(string ip)
        {
            Assert.False(ServerStatsUri.TryBuild(ip, 6200, out var uri));
            Assert.Null(uri);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(65536)]
        public void ADescriptionOutsideThePortRangeAnswersFalse(int port)
        {
            Assert.False(ServerStatsUri.TryBuild("203.0.113.7", port, out var uri));
            Assert.Null(uri);
        }

        [Fact]
        public void BothEndsOfThePortRangeBuild()
        {
            Assert.True(ServerStatsUri.TryBuild("203.0.113.7", 1, out _));
            Assert.True(ServerStatsUri.TryBuild("203.0.113.7", 65535, out _));
        }
    }
}
