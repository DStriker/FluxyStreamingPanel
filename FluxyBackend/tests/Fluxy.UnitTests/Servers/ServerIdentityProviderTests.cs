using Fluxy.Application.Servers.Services;
using Fluxy.Core.Servers.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fluxy.UnitTests.Servers
{
    /// <summary>
    /// The identity chain: configuration first, the host second, the documented fallback last.
    /// </summary>
    /// <remarks>
    /// Only the branches that are deterministic are proved here - a configured value, and the
    /// <c>Urls</c> parsing the host's own launch profiles produce. The host probe itself is a
    /// fact about the machine the test runs on, so its fallback is the integration suite's fake,
    /// not an assertion about a developer's network.
    /// </remarks>
    public sealed class ServerIdentityProviderTests
    {
        [Fact]
        public void AConfiguredAddressAndPortWin()
        {
            var identity = Provider(new Dictionary<string, string?>
            {
                ["Servers:MainIp"] = "203.0.113.9",
                ["Servers:MainPort"] = "6000",
                ["Urls"] = "http://localhost:5159"
            }).Read();

            Assert.Equal("203.0.113.9", identity.Ip);
            Assert.Equal(6000, identity.Port);
            Assert.Equal(MainServer.DefaultNetworkInterface, identity.NetworkInterface);
        }

        [Fact]
        public void TheOperatingSystemIsTheRuntimeDescriptionOrTheFallback()
        {
            var identity = Provider(new Dictionary<string, string?>
            {
                ["Servers:MainIp"] = "203.0.113.9",
                ["Servers:MainPort"] = "6000"
            }).Read();

            Assert.False(string.IsNullOrWhiteSpace(identity.OperatingSystem));
            Assert.True(identity.OperatingSystem.Length <= ServerLimits.OsNameMaxLength);
        }

        [Fact]
        public void AnUnparseableConfiguredPortFallsThroughToUrls()
        {
            var identity = Provider(new Dictionary<string, string?>
            {
                ["Servers:MainIp"] = "203.0.113.9",
                ["Servers:MainPort"] = "not a port",
                ["Urls"] = "http://localhost:5159"
            }).Read();

            Assert.Equal(5159, identity.Port);
        }

        [Theory]
        [InlineData("http://localhost:5159", 5159)]
        [InlineData("http://localhost:5159;https://localhost:7221", 5159)]
        [InlineData("http://0.0.0.0:5159", 5159)]
        [InlineData("http://*:5159", 5159)]
        [InlineData("http://+:5159", 5159)]
        [InlineData("https://[::]:7221", 7221)]
        public void TheFirstPortOfUrlsIsTheFirstEntryThatNamesOne(string urls, int expected)
        {
            Assert.Equal(expected, ServerIdentityProvider.FirstPortOf(urls));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("*")]
        [InlineData("+")]
        [InlineData("http://localhost")]
        public void UrlsWithoutAPortNameNone(string? urls)
        {
            Assert.Null(ServerIdentityProvider.FirstPortOf(urls));
        }

        /// <summary>A provider over an in-memory configuration and a logger that discards.</summary>
        private static ServerIdentityProvider Provider(Dictionary<string, string?> settings)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            return new ServerIdentityProvider(
                configuration,
                NullLogger<ServerIdentityProvider>.Instance);
        }
    }
}
