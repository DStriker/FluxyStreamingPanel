using Fluxy.Application.Users.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fluxy.UnitTests.Users
{
    public sealed class DevelopmentGuardBypassTests
    {
        [Theory]
        [InlineData("127.0.0.1")]
        [InlineData("::1")]
        [InlineData("10.0.0.1")]
        [InlineData("192.168.0.10")]
        public void BypassesEnforcementInDevelopmentForALocalAddress(string address)
        {
            Assert.True(DevelopmentGuardBypass.BypassesEnforcement(
                address,
                Environment("Development"),
                NullLogger.Instance));
        }

        [Theory]
        [InlineData("8.8.8.8")]
        [InlineData("2001:4860:4860::8888")]
        [InlineData("not-an-address")]
        public void BypassesEnforcementNeverSkipsAPublicAddress(string address)
        {
            Assert.False(DevelopmentGuardBypass.BypassesEnforcement(
                address,
                Environment("Development"),
                NullLogger.Instance));
        }

        [Theory]
        [InlineData("Production")]
        [InlineData("Staging")]
        [InlineData("Testing")]
        public void BypassesEnforcementNeverSkipsOutsideDevelopment(string environmentName)
        {
            Assert.False(DevelopmentGuardBypass.BypassesEnforcement(
                "127.0.0.1",
                Environment(environmentName),
                NullLogger.Instance));
        }

        [Fact]
        public void BypassesAntiLockUsesTheSameRule()
        {
            Assert.True(DevelopmentGuardBypass.BypassesAntiLock(
                "127.0.0.1",
                Environment("Development"),
                NullLogger.Instance));
            Assert.False(DevelopmentGuardBypass.BypassesAntiLock(
                "8.8.8.8",
                Environment("Development"),
                NullLogger.Instance));
            Assert.False(DevelopmentGuardBypass.BypassesAntiLock(
                "127.0.0.1",
                Environment("Production"),
                NullLogger.Instance));
        }

        private static IHostEnvironment Environment(string name) => new HostingEnvironment
        {
            EnvironmentName = name,
            ApplicationName = "Fluxy.UnitTests",
            ContentRootPath = AppContext.BaseDirectory
        };
    }
}
