using System.Net;
using Fluxy.Application.Users.Utils;
using Fluxy.Core.GeoIp.ReadModels;

namespace Fluxy.UnitTests.Users
{
    public sealed class LoginGuardPolicyTests
    {
        [Theory]
        [InlineData("127.0.0.1", "127.0.0.1")]
        [InlineData(" 192.168.1.10 ", "192.168.1.10")]
        [InlineData("2001:0db8:0:0:0:0:0:1", "2001:db8::1")]
        [InlineData("10.0.0.0/8", "10.0.0.0/8")]
        [InlineData("2001:db8::/32", "2001:db8::/32")]
        public void NormalizeIpEntryReturnsCanonicalValue(string input, string expected)
        {
            Assert.Equal(expected, LoginGuardPolicy.NormalizeIpEntry(input));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not-an-address")]
        [InlineData("10.0.0.0/33")]
        [InlineData("2001:db8::/129")]
        [InlineData("10.0.0.0/-1")]
        [InlineData("/24")]
        public void NormalizeIpEntryRejectsMalformedValues(string? input)
        {
            Assert.Null(LoginGuardPolicy.NormalizeIpEntry(input));
        }

        [Fact]
        public void AddressesEqualComparesParsedAddressesRatherThanText()
        {
            Assert.True(LoginGuardPolicy.AddressesEqual("::1", "0:0:0:0:0:0:0:1"));
            Assert.True(LoginGuardPolicy.AddressesEqual(" 10.0.0.1 ", "10.0.0.1"));
            Assert.False(LoginGuardPolicy.AddressesEqual("10.0.0.1", "10.0.0.2"));
        }

        [Fact]
        public void AddressesEqualFallsBackToTextForUnparseableValues()
        {
            Assert.True(LoginGuardPolicy.AddressesEqual("unknown", " unknown "));
            Assert.False(LoginGuardPolicy.AddressesEqual("unknown", "other"));
            Assert.True(LoginGuardPolicy.AddressesEqual(null, null));
        }

        [Theory]
        [InlineData("10.1.2.3", "10.0.0.0/8", true)]
        [InlineData("192.168.50.1", "192.168.0.0/16", true)]
        [InlineData("192.169.0.1", "192.168.0.0/16", false)]
        [InlineData("10.0.0.1", "10.0.0.1", true)]
        [InlineData("10.0.0.2", "10.0.0.1", false)]
        public void MatchesEntryHandlesIpv4CidrAndExactAddresses(
            string address,
            string entry,
            bool expected)
        {
            Assert.Equal(
                expected,
                LoginGuardPolicy.MatchesEntry(entry, IPAddress.Parse(address)));
        }

        [Fact]
        public void MatchesEntryHandlesIpv6CidrAndFamilyMismatch()
        {
            Assert.True(LoginGuardPolicy.MatchesEntry(
                "2001:db8::/32",
                IPAddress.Parse("2001:db8:1234::1")));
            Assert.False(LoginGuardPolicy.MatchesEntry(
                "2001:db9::/32",
                IPAddress.Parse("2001:db8:1234::1")));
            Assert.False(LoginGuardPolicy.MatchesEntry(
                "10.0.0.0/8",
                IPAddress.Parse("2001:db8::1")));
        }

        [Fact]
        public void AllowsAddressTreatsAnEmptyListAsUnrestricted()
        {
            var unknown = GeoIpInfo.Unknown(null);

            Assert.True(LoginGuardPolicy.AllowsAddress([], unknown));
            Assert.False(LoginGuardPolicy.AllowsAddress(["10.0.0.0/8"], unknown));
        }

        [Fact]
        public void AllowsAddressMatchesAnyEntryAndRefusesUnparseableAddress()
        {
            Assert.True(LoginGuardPolicy.AllowsAddress(
                ["10.0.0.0/8", "192.168.0.0/16"],
                new GeoIpInfo { Ip = "192.168.1.1" }));
            Assert.False(LoginGuardPolicy.AllowsAddress(
                ["10.0.0.0/8"],
                new GeoIpInfo { Ip = "203.0.113.7" }));
            Assert.False(LoginGuardPolicy.AllowsAddress(
                ["10.0.0.0/8"],
                new GeoIpInfo { Ip = "not-an-address" }));
        }

        [Fact]
        public void AllowsCountryIgnoresCaseAndRequiresAKnownCountryForANonEmptyList()
        {
            Assert.True(LoginGuardPolicy.AllowsCountry([], GeoIpInfo.Unknown("203.0.113.7")));
            Assert.True(LoginGuardPolicy.AllowsCountry(
                ["US"],
                new GeoIpInfo { Ip = "203.0.113.7", CountryCode = "us" }));
            Assert.False(LoginGuardPolicy.AllowsCountry(
                ["US"],
                GeoIpInfo.Unknown("203.0.113.7")));
        }

        [Fact]
        public void AllowsProviderDecidesOnlyByAutonomousSystemNumber()
        {
            Assert.True(LoginGuardPolicy.AllowsProvider([], GeoIpInfo.Unknown("203.0.113.7")));
            Assert.True(LoginGuardPolicy.AllowsProvider(
                [15169],
                new GeoIpInfo
                {
                    Ip = "203.0.113.7",
                    AutonomousSystemNumber = 15169,
                    Organization = "Some other name"
                }));
            Assert.False(LoginGuardPolicy.AllowsProvider(
                [15169],
                new GeoIpInfo
                {
                    Ip = "203.0.113.7",
                    AutonomousSystemNumber = 12345,
                    Organization = "Google LLC"
                }));
            Assert.False(LoginGuardPolicy.AllowsProvider(
                [15169],
                GeoIpInfo.Unknown("203.0.113.7")));
        }

        [Theory]
        [InlineData("us", "US")]
        [InlineData(" gb ", "GB")]
        public void NormalizeCountryReturnsAnUpperCaseKnownCode(string input, string expected)
        {
            Assert.Equal(expected, LoginGuardPolicy.NormalizeCountry(input));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("XX")]
        [InlineData("USA")]
        public void NormalizeCountryRejectsUnknownCodes(string? input)
        {
            Assert.Null(LoginGuardPolicy.NormalizeCountry(input));
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(64512, true)]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        public void IsAutonomousSystemNumberAcceptsPositiveValuesOnly(int value, bool expected)
        {
            Assert.Equal(expected, LoginGuardPolicy.IsAutonomousSystemNumber(value));
        }
    }
}
