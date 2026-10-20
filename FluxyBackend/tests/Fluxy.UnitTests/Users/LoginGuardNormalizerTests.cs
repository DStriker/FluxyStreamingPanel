using Fluxy.Application.Users.Utils;
using Fluxy.Core.Users.Models;

namespace Fluxy.UnitTests.Users
{
    public sealed class LoginGuardNormalizerTests
    {
        [Fact]
        public void NormalizeProducesCanonicalValuesWithoutErrors()
        {
            var settings = new LoginGuardSettings
            {
                GeoProtectionEnabled = true,
                BindSessionToIp = true,
                AllowedIps = [" 10.0.0.0/8 ", "10.0.0.0/8", "2001:db8::1"],
                AllowedCountry = "us",
                AllowedAutonomousSystemNumber = 15169
            };

            var (value, errors) = LoginGuardNormalizer.Normalize(settings);

            Assert.Empty(errors);
            Assert.True(value.GeoProtectionEnabled);
            Assert.True(value.BindSessionToIp);
            Assert.Equal(["10.0.0.0/8", "2001:db8::1"], value.AllowedIps);
            Assert.Equal("US", value.AllowedCountry);
            Assert.Equal(15169, value.AllowedAutonomousSystemNumber);
        }

        [Fact]
        public void NormalizeAcceptsAnEmptyGuard()
        {
            var (value, errors) = LoginGuardNormalizer.Normalize(new LoginGuardSettings());

            Assert.Empty(errors);
            Assert.Empty(value.AllowedIps);
            Assert.Null(value.AllowedCountry);
            Assert.Null(value.AllowedAutonomousSystemNumber);
        }

        [Fact]
        public void NormalizeNamesAnInvalidAddressAndDropsItFromTheStoredValue()
        {
            var (value, errors) = LoginGuardNormalizer.Normalize(new LoginGuardSettings
            {
                AllowedIps = ["10.0.0.1", "not-an-address"]
            });

            Assert.Contains(nameof(LoginGuardSettings.AllowedIps), errors.Keys);
            Assert.Equal(["10.0.0.1"], value.AllowedIps);
        }

        [Fact]
        public void NormalizeRefusesMoreThanFiveDistinctAddresses()
        {
            var (_, errors) = LoginGuardNormalizer.Normalize(new LoginGuardSettings
            {
                AllowedIps =
                [
                    "10.0.0.1",
                    "10.0.0.2",
                    "10.0.0.3",
                    "10.0.0.4",
                    "10.0.0.5",
                    "10.0.0.6"
                ]
            });

            Assert.Contains(nameof(LoginGuardSettings.AllowedIps), errors.Keys);
        }

        [Fact]
        public void NormalizeDoesNotCountDuplicatesTwice()
        {
            var (_, errors) = LoginGuardNormalizer.Normalize(new LoginGuardSettings
            {
                AllowedIps =
                [
                    "10.0.0.1",
                    "10.0.0.1/32",
                    "10.0.0.1",
                    "10.0.0.1/32",
                    "10.0.0.1"
                ]
            });

            Assert.Empty(errors);
        }

        [Theory]
        [InlineData("XX")]
        [InlineData("USA")]
        public void NormalizeNamesAnUnknownCountry(string country)
        {
            var (value, errors) = LoginGuardNormalizer.Normalize(new LoginGuardSettings
            {
                AllowedCountry = country
            });

            Assert.Contains(nameof(LoginGuardSettings.AllowedCountry), errors.Keys);
            Assert.Null(value.AllowedCountry);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-100)]
        public void NormalizeNamesAnInvalidAutonomousSystemNumber(int asn)
        {
            var (value, errors) = LoginGuardNormalizer.Normalize(new LoginGuardSettings
            {
                AllowedAutonomousSystemNumber = asn
            });

            Assert.Contains(nameof(LoginGuardSettings.AllowedAutonomousSystemNumber), errors.Keys);
            Assert.Null(value.AllowedAutonomousSystemNumber);
        }
    }
}
