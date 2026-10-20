using Fluxy.Core.GeoIp.Utils;

namespace Fluxy.UnitTests.GeoIp
{
    public sealed class GeoIpAddressTests
    {
        [Theory]
        [InlineData("127.0.0.1")]
        [InlineData("::1")]
        [InlineData("0:0:0:0:0:0:0:1")]
        [InlineData("10.0.0.1")]
        [InlineData("172.16.0.1")]
        [InlineData("172.31.255.255")]
        [InlineData("192.168.1.1")]
        [InlineData("169.254.1.1")]
        [InlineData("fc00::1")]
        [InlineData("fd12:3456::1")]
        [InlineData("fe80::1")]
        public void IsLocalRecognizesLoopbackPrivateAndLinkLocalAddresses(string address)
        {
            Assert.True(GeoIpAddress.IsLocal(address));
        }

        [Theory]
        [InlineData("8.8.8.8")]
        [InlineData("172.32.0.1")]
        [InlineData("192.169.1.1")]
        [InlineData("2001:4860:4860::8888")]
        [InlineData("not-an-address")]
        [InlineData("")]
        [InlineData(null)]
        public void IsLocalRejectsPublicAndMalformedAddresses(string? address)
        {
            Assert.False(GeoIpAddress.IsLocal(address));
        }

        [Fact]
        public void IsLocalTrimsTheAddressBeforeParsing()
        {
            Assert.True(GeoIpAddress.IsLocal(" 127.0.0.1 "));
        }
    }
}
