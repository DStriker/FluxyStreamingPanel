using System.Net;
using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.GeoIp;
using MaxMind.GeoIP2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fluxy.Application.Services.GeoIp
{
    /// <summary>
    /// Places a client address on the map using local MaxMind databases.
    /// </summary>
    /// <remarks>
    /// Local files rather than a lookup API, because this runs on the sign-in path: an HTTP
    /// call per login would add its latency to every sign-in and make signing in depend on a
    /// third party being up. The readers are held for the life of the process and are safe to
    /// share between requests; a missing file means <c>Unknown</c> for every address, never an
    /// exception, and the guard service is what turns that into a refusal.
    /// </remarks>
    public sealed class MaxMindGeoIpResolver : IGeoIpResolver, IDisposable
    {
        private readonly IOptionsMonitor<GeoIpOptions> _options;
        private readonly ILogger<MaxMindGeoIpResolver> _logger;
        private readonly object _sync = new();
        private DatabaseReader? _cityReader;
        private DatabaseReader? _asnReader;
        private bool _initialized;
        private bool _warned;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="MaxMindGeoIpResolver"/> class.
        /// </summary>
        /// <param name="options">Live database paths, so a configuration reload applies without a restart.</param>
        /// <param name="logger">Logger the missing database warning is reported to.</param>
        public MaxMindGeoIpResolver(
            IOptionsMonitor<GeoIpOptions> options,
            ILogger<MaxMindGeoIpResolver> logger)
        {
            _options = options;
            _logger = logger;
        }

        /// <inheritdoc />
        public Task<GeoIpInfo> ResolveAsync(string? clientAddress, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();

            var trimmed = clientAddress?.Trim();
            if (string.IsNullOrEmpty(trimmed)
                || !IPAddress.TryParse(trimmed, out var address)
                || LoginGuardPolicy.IsLocalAddress(trimmed))
            {
                // A private address is not on the public map by definition: there is nothing to
                // look up, and asking the database would only produce its miss path.
                return Task.FromResult(GeoIpInfo.Unknown(trimmed));
            }

            string? country = null;
            int? asn = null;
            string? organization = null;

            if (_cityReader is not null)
            {
                try
                {
                    country = _cityReader.City(address).Country.IsoCode;
                }
                catch (Exception exception)
                {
                    WarnOnce("city", exception);
                }
            }

            if (_asnReader is not null)
            {
                try
                {
                    var response = _asnReader.Asn(address);
                    asn = response.AutonomousSystemNumber is { } number && number <= int.MaxValue
                        ? (int)number
                        : null;
                    organization = string.IsNullOrWhiteSpace(response.AutonomousSystemOrganization)
                        ? null
                        : response.AutonomousSystemOrganization;
                }
                catch (Exception exception)
                {
                    WarnOnce("ASN", exception);
                }
            }

            if (string.IsNullOrWhiteSpace(country) && asn is null)
            {
                return Task.FromResult(GeoIpInfo.Unknown(trimmed));
            }

            return Task.FromResult(new GeoIpInfo
            {
                Ip = trimmed,
                CountryCode = string.IsNullOrWhiteSpace(country) ? null : country.ToUpperInvariant(),
                AutonomousSystemNumber = asn,
                Organization = organization,
                IsUnknown = false
            });
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cityReader?.Dispose();
            _asnReader?.Dispose();
        }

        /// <summary>
        /// Opens the database files once, on first use rather than at startup: a host without
        /// the files is a valid installation for every account with the guard off, and failing
        /// it at startup would make the files mandatory for accounts that never asked for them.
        /// </summary>
        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            lock (_sync)
            {
                if (_initialized)
                {
                    return;
                }

                _initialized = true;
                var options = _options.CurrentValue;

                _cityReader = Open(options.CityDbPath, "city");
                _asnReader = Open(options.AsnDbPath, "ASN");
            }
        }

        private DatabaseReader? Open(string? path, string what)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                _logger.LogWarning(
                    "No GeoIP {What} database is configured ('GeoIp' section), so addresses " +
                    "resolve as unknown and accounts with the login guard on cannot sign in. " +
                    "Install the MaxMind database files to enable the guard.",
                    what);

                return null;
            }

            // A bare file name means "next to the binaries", which is where the build copies
            // the solution level databases. Resolved here rather than left to the working
            // directory, because that one depends on where the host was started from - the
            // output folder does not.
            var rooted = Path.IsPathFullyQualified(path)
                ? path
                : Path.Combine(AppContext.BaseDirectory, path);

            try
            {
                return new DatabaseReader(rooted);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "The GeoIP {What} database at {Path} could not be opened, so addresses " +
                    "resolve as unknown and accounts with the login guard on cannot sign in.",
                    what,
                    rooted);

                return null;
            }
        }

        /// <summary>
        /// One warning per process for a lookup that fails at runtime. A database that answers
        /// every query with an exception would otherwise fill the log at the rate of the
        /// sign-in attempts.
        /// </summary>
        private void WarnOnce(string what, Exception exception)
        {
            if (_warned)
            {
                return;
            }

            lock (_sync)
            {
                if (_warned)
                {
                    return;
                }

                _warned = true;
            }

            _logger.LogWarning(
                exception,
                "A GeoIP {What} lookup failed; the address resolved as unknown.",
                what);
        }
    }
}
