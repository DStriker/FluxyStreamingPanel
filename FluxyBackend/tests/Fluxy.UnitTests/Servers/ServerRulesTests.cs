using Fluxy.Application.Servers.Utils;
using Fluxy.Core.Servers.Commands;
using Fluxy.Core.Servers.Enums;
using Fluxy.Core.Servers.Utils;

namespace Fluxy.UnitTests.Servers
{
    /// <summary>
    /// Every refusal and every normalization the server rules own, one fact per rule.
    /// </summary>
    /// <remarks>
    /// The rules are static and judge one write at a time, so every fact here builds a write,
    /// breaks one field and reads the map back. The service tests below prove what the service
    /// does with a map; these prove the map itself.
    /// </remarks>
    public sealed class ServerRulesTests
    {
        [Fact]
        public void AValidMinimalRowIsAccepted()
        {
            var write = Valid();

            Assert.Empty(ServerRules.Validate(write));
        }

        [Fact]
        public void NormalizeTrimsEveryTextAndTurnsABlankOptionalOneIntoNull()
        {
            var write = ServerRules.Normalize(Valid() with
            {
                Name = "  Edge one  ",
                Ip = "  203.0.113.7  ",
                OsName = "  Debian 12  ",
                NetworkInterface = "  eth0  ",
                Hostname = "  edge.example.test  ",
                VpnIp = "  10.0.0.2  "
            });

            Assert.Equal("Edge one", write.Name);
            Assert.Equal("203.0.113.7", write.Ip);
            Assert.Equal("Debian 12", write.OsName);
            Assert.Equal("eth0", write.NetworkInterface);
            Assert.Equal("edge.example.test", write.Hostname);
            Assert.Equal("10.0.0.2", write.VpnIp);
        }

        [Fact]
        public void NormalizeTurnsABlankHostnameAndVpnIntoNull()
        {
            var write = ServerRules.Normalize(Valid() with
            {
                Hostname = "   ",
                VpnIp = "   "
            });

            Assert.Null(write.Hostname);
            Assert.Null(write.VpnIp);
        }

        [Fact]
        public void NormalizeKeepsTheIdentifierTheSeederSets()
        {
            var write = ServerRules.Normalize(Valid() with { Id = MainServer.Id });

            Assert.Equal(MainServer.Id, write.Id);
        }

        [Fact]
        public void ANameAtTheMinimumLengthIsAccepted()
        {
            Assert.Empty(ServerRules.Validate(Valid() with { Name = new string('a', 1) }));
        }

        [Fact]
        public void ANameAtTheMaximumLengthIsAccepted()
        {
            Assert.Empty(ServerRules.Validate(
                Valid() with { Name = new string('a', ServerLimits.NameMaxLength) }));
        }

        [Fact]
        public void ANameOneCharacterTooLongIsRefused()
        {
            var errors = ServerRules.Validate(
                Valid() with { Name = new string('a', ServerLimits.NameMaxLength + 1) });

            Assert.True(errors.ContainsKey(nameof(ServerWrite.Name)));
        }

        [Fact]
        public void ABlankNameIsRefusedEvenWhenItIsOnlySpaces()
        {
            var errors = ServerRules.Validate(Valid() with { Name = "     " });

            Assert.True(errors.ContainsKey(nameof(ServerWrite.Name)));
        }

        [Fact]
        public void AnIpv4AddressIsAccepted()
        {
            Assert.Empty(ServerRules.Validate(Valid() with { Ip = "192.0.2.10" }));
        }

        [Fact]
        public void AnIpv6AddressIsAccepted()
        {
            Assert.Empty(ServerRules.Validate(Valid() with { Ip = "2001:db8::1" }));
        }

        [Fact]
        public void AnIpThatIsNotALiteralIsRefused()
        {
            var errors = ServerRules.Validate(Valid() with { Ip = "example.test" });

            Assert.True(errors.ContainsKey(nameof(ServerWrite.Ip)));
        }

        [Fact]
        public void AnIpWithoutAnAddressIsRefused()
        {
            var errors = ServerRules.Validate(Valid() with { Ip = "" });

            Assert.True(errors.ContainsKey(nameof(ServerWrite.Ip)));
        }

        [Fact]
        public void AnIpLongerThanTheColumnIsRefused()
        {
            var errors = ServerRules.Validate(
                Valid() with { Ip = new string('1', ServerLimits.IpMaxLength + 1) });

            Assert.True(errors.ContainsKey(nameof(ServerWrite.Ip)));
        }

        [Fact]
        public void AVpnAddressThatIsNotALiteralIsRefused()
        {
            var errors = ServerRules.Validate(Valid() with { VpnIp = "vpn.example.test" });

            Assert.True(errors.ContainsKey(nameof(ServerWrite.VpnIp)));
        }

        [Fact]
        public void AnAbsentVpnAddressIsAccepted()
        {
            Assert.Empty(ServerRules.Validate(Valid() with { VpnIp = null }));
        }

        [Fact]
        public void TheLowestPortIsAccepted()
        {
            Assert.Empty(ServerRules.Validate(Valid() with { Port = ServerLimits.PortMin }));
        }

        [Fact]
        public void TheHighestPortIsAccepted()
        {
            Assert.Empty(ServerRules.Validate(Valid() with { Port = ServerLimits.PortMax }));
        }

        [Fact]
        public void APortBelowTheRangeIsRefused()
        {
            var errors = ServerRules.Validate(Valid() with { Port = ServerLimits.PortMin - 1 });

            Assert.True(errors.ContainsKey(nameof(ServerWrite.Port)));
        }

        [Fact]
        public void APortAboveTheRangeIsRefused()
        {
            var errors = ServerRules.Validate(Valid() with { Port = ServerLimits.PortMax + 1 });

            Assert.True(errors.ContainsKey(nameof(ServerWrite.Port)));
        }

        [Fact]
        public void AClientCountOfZeroIsAcceptedAsUnlimited()
        {
            Assert.Empty(ServerRules.Validate(Valid() with { ClientCount = 0 }));
        }

        [Fact]
        public void AClientCountAtTheMaximumIsAccepted()
        {
            Assert.Empty(ServerRules.Validate(
                Valid() with { ClientCount = ServerLimits.ClientCountMax }));
        }

        [Fact]
        public void AClientCountBelowZeroIsRefused()
        {
            var errors = ServerRules.Validate(Valid() with { ClientCount = -1 });

            Assert.True(errors.ContainsKey(nameof(ServerWrite.ClientCount)));
        }

        [Fact]
        public void AClientCountOnePastTheMaximumIsRefused()
        {
            var errors = ServerRules.Validate(
                Valid() with { ClientCount = ServerLimits.ClientCountMax + 1 });

            Assert.True(errors.ContainsKey(nameof(ServerWrite.ClientCount)));
        }

        [Fact]
        public void ALatencyOfZeroIsAcceptedAsNotMeasured()
        {
            Assert.Empty(ServerRules.Validate(Valid() with { Latency = 0 }));
        }

        [Fact]
        public void ANegativeLatencyIsRefused()
        {
            var errors = ServerRules.Validate(Valid() with { Latency = -0.1 });

            Assert.True(errors.ContainsKey(nameof(ServerWrite.Latency)));
        }

        [Fact]
        public void AGuaranteedSpeedOfZeroIsAcceptedAsUnknown()
        {
            Assert.Empty(ServerRules.Validate(Valid() with { GuaranteedSpeedKbps = 0 }));
        }

        [Fact]
        public void AGuaranteedSpeedAtTheMaximumIsAccepted()
        {
            Assert.Empty(ServerRules.Validate(
                Valid() with { GuaranteedSpeedKbps = ServerLimits.GuaranteedSpeedMaxKbps }));
        }

        [Fact]
        public void AGuaranteedSpeedBelowZeroIsRefused()
        {
            var errors = ServerRules.Validate(Valid() with { GuaranteedSpeedKbps = -1 });

            Assert.True(errors.ContainsKey(nameof(ServerWrite.GuaranteedSpeedKbps)));
        }

        [Fact]
        public void AGuaranteedSpeedOnePastTheMaximumIsRefused()
        {
            var errors = ServerRules.Validate(
                Valid() with { GuaranteedSpeedKbps = ServerLimits.GuaranteedSpeedMaxKbps + 1 });

            Assert.True(errors.ContainsKey(nameof(ServerWrite.GuaranteedSpeedKbps)));
        }

        [Fact]
        public void AnOperatingSystemAtTheMaximumLengthIsAccepted()
        {
            Assert.Empty(ServerRules.Validate(
                Valid() with { OsName = new string('a', ServerLimits.OsNameMaxLength) }));
        }

        [Fact]
        public void AnOperatingSystemOneCharacterTooLongIsRefused()
        {
            var errors = ServerRules.Validate(
                Valid() with { OsName = new string('a', ServerLimits.OsNameMaxLength + 1) });

            Assert.True(errors.ContainsKey(nameof(ServerWrite.OsName)));
        }

        [Fact]
        public void ABlankOperatingSystemIsRefused()
        {
            var errors = ServerRules.Validate(Valid() with { OsName = " " });

            Assert.True(errors.ContainsKey(nameof(ServerWrite.OsName)));
        }

        [Fact]
        public void ANetworkInterfaceAtTheMaximumLengthIsAccepted()
        {
            Assert.Empty(ServerRules.Validate(
                Valid() with
                {
                    NetworkInterface = new string('a', ServerLimits.NetworkInterfaceMaxLength)
                }));
        }

        [Fact]
        public void ANetworkInterfaceOneCharacterTooLongIsRefused()
        {
            var errors = ServerRules.Validate(
                Valid() with
                {
                    NetworkInterface = new string('a', ServerLimits.NetworkInterfaceMaxLength + 1)
                });

            Assert.True(errors.ContainsKey(nameof(ServerWrite.NetworkInterface)));
        }

        [Fact]
        public void ABlankNetworkInterfaceIsRefused()
        {
            var errors = ServerRules.Validate(Valid() with { NetworkInterface = "" });

            Assert.True(errors.ContainsKey(nameof(ServerWrite.NetworkInterface)));
        }

        [Fact]
        public void AHostnameAtTheMaximumLengthIsAccepted()
        {
            Assert.Empty(ServerRules.Validate(
                Valid() with { Hostname = new string('a', ServerLimits.HostnameMaxLength) }));
        }

        [Fact]
        public void AHostnameOneCharacterTooLongIsRefused()
        {
            var errors = ServerRules.Validate(
                Valid() with { Hostname = new string('a', ServerLimits.HostnameMaxLength + 1) });

            Assert.True(errors.ContainsKey(nameof(ServerWrite.Hostname)));
        }

        [Fact]
        public void CountryCodesAreUppercased()
        {
            var countries = ServerRules.NormalizeCountries(["de", "at"]);

            Assert.Equal(["DE", "AT"], countries);
        }

        [Fact]
        public void ALowerCaseCountryIsAccepted()
        {
            Assert.Empty(ServerRules.Validate(Valid() with { CountryCodes = ["de"] }));
        }

        [Fact]
        public void ACountryThatNamesNoCountryIsRefused()
        {
            var errors = ServerRules.Validate(Valid() with { CountryCodes = ["ZZ"] });

            Assert.True(errors.ContainsKey(nameof(ServerWrite.CountryCodes)));
        }

        [Fact]
        public void ABlankCountryElementIsRefused()
        {
            var errors = ServerRules.Validate(Valid() with { CountryCodes = ["DE", "  "] });

            Assert.True(errors.ContainsKey(nameof(ServerWrite.CountryCodes)));
        }

        [Fact]
        public void DuplicateCountriesCollapsePreservingFirstOccurrence()
        {
            var countries = ServerRules.NormalizeCountries(["de", "FR", "DE", "fr"]);

            Assert.Equal(["DE", "FR"], countries);
        }

        [Fact]
        public void ANullCountryListBecomesEmpty()
        {
            var countries = ServerRules.NormalizeCountries(null);

            Assert.Empty(countries);
        }

        [Fact]
        public void AnEmptyCountryListIsAccepted()
        {
            Assert.Empty(ServerRules.Validate(Valid() with { CountryCodes = [] }));
        }

        [Fact]
        public void TheCountryCapIsInclusive()
        {
            Assert.True(ServerRules.IsWithinCountryCap(ServerLimits.CountryCodeCountMax));
            Assert.False(ServerRules.IsWithinCountryCap(ServerLimits.CountryCodeCountMax + 1));
        }

        [Fact]
        public void EveryRefusalIsKeyedByTheWritePropertyItRefused()
        {
            var errors = ServerRules.Validate(new ServerWrite
            {
                Name = "",
                Ip = "nowhere",
                Port = 0,
                ClientCount = -1,
                OsName = "",
                NetworkInterface = "",
                Latency = -1,
                Status = ServerStatus.Active,
                GuaranteedSpeedKbps = -1,
                TimeshiftOnly = false,
                CountryCodes = ["NOPE"]
            });

            Assert.Equal(
                [
                    nameof(ServerWrite.ClientCount),
                    nameof(ServerWrite.CountryCodes),
                    nameof(ServerWrite.GuaranteedSpeedKbps),
                    nameof(ServerWrite.Ip),
                    nameof(ServerWrite.Latency),
                    nameof(ServerWrite.Name),
                    nameof(ServerWrite.NetworkInterface),
                    nameof(ServerWrite.OsName),
                    nameof(ServerWrite.Port)
                ],
                errors.Keys.Order(StringComparer.Ordinal));
        }

        /// <summary>A row every fact starts from: valid, ordinary, and not the main server.</summary>
        private static ServerWrite Valid() => new()
        {
            Name = "Edge one",
            Ip = "203.0.113.7",
            Port = 5159,
            ClientCount = 1000,
            OsName = "Debian 12",
            NetworkInterface = "eth0",
            Latency = 0,
            Status = ServerStatus.Active,
            GuaranteedSpeedKbps = 0,
            TimeshiftOnly = false,
            Hostname = null,
            VpnIp = null,
            CountryCodes = []
        };
    }
}
