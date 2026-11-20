using Fluxy.Core.Servers.Enums;
using Fluxy.Core.Servers.Models;
using Fluxy.Core.Servers.Utils;

namespace Fluxy.UnitTests.Servers
{
    /// <summary>
    /// The one rule the main server owns: which identifier and which row name it, and the
    /// starting values the seeder writes.
    /// </summary>
    public sealed class MainServerTests
    {
        [Fact]
        public void TheIdentifierIsTheDocumentedConstant()
        {
            Assert.Equal(
                Guid.Parse("00000000-0000-0000-0000-000000000401"),
                MainServer.Id);
        }

        [Fact]
        public void IsMainAcceptsTheConstantAndRejectsEveryOtherIdentifier()
        {
            Assert.True(MainServer.IsMain(MainServer.Id));
            Assert.False(MainServer.IsMain(Guid.NewGuid()));
            Assert.False(MainServer.IsMain(Guid.Empty));
        }

        [Fact]
        public void IsMainReadsTheIdentifierOfARowRatherThanItsFlag()
        {
            Assert.True(MainServer.IsMain(Row(MainServer.Id)));

            // A row that claims the flag without carrying the identifier is not the main one:
            // the flag is a column the seeder writes, and the rule must not depend on it.
            Assert.False(MainServer.IsMain(Row(Guid.NewGuid())));
        }

        [Fact]
        public void TheDefaultsAreTheDocumentedValues()
        {
            Assert.Equal("Main server", MainServer.DefaultName);
            Assert.Equal(1000, MainServer.DefaultClientCount);
            Assert.Equal("eth0", MainServer.DefaultNetworkInterface);
            Assert.Equal("127.0.0.1", MainServer.FallbackIp);
            Assert.Equal(5159, MainServer.FallbackPort);
            Assert.Equal("unknown", MainServer.FallbackOsName);
        }

        private static Server Row(Guid id) => new()
        {
            Id = id,
            Name = "Anything",
            Ip = "127.0.0.1",
            Port = 5159,
            ClientCount = 0,
            OsName = "linux",
            NetworkInterface = "eth0",
            Latency = 0,
            Status = ServerStatus.Active,
            GuaranteedSpeedKbps = 0,
            TimeshiftOnly = false,
            CountryCodes = [],
            IsMain = true,
            CreatedAt = DateTimeOffset.UnixEpoch
        };
    }
}
