using System.Collections.Concurrent;
using Fluxy.Core.GeoIp.Interfaces;
using Fluxy.Core.GeoIp.ReadModels;

namespace Fluxy.IntegrationTests.Infrastructure.Fakes
{
    /// <summary>
    /// Answers GeoIP lookups from a table the tests control.
    /// </summary>
    public sealed class FakeGeoIpResolver : IGeoIpResolver
    {
        private readonly ConcurrentDictionary<string, GeoIpInfo> _entries =
            new(StringComparer.OrdinalIgnoreCase);

        public Task<GeoIpInfo> ResolveAsync(
            string? clientAddress,
            CancellationToken cancellationToken = default)
        {
            var ip = clientAddress?.Trim();

            if (ip is null || !_entries.TryGetValue(ip, out var info))
            {
                return Task.FromResult(GeoIpInfo.Unknown(ip));
            }

            return Task.FromResult(info with { Ip = ip });
        }

        public void Set(string ip, string? countryCode, int? autonomousSystemNumber, string? organization = null)
        {
            _entries[ip] = new GeoIpInfo
            {
                Ip = ip,
                CountryCode = countryCode,
                AutonomousSystemNumber = autonomousSystemNumber,
                Organization = organization
            };
        }

        public void Clear() => _entries.Clear();
    }
}
