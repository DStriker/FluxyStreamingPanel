using Fluxy.Core.GeoIp.Interfaces;
using Fluxy.Core.GeoIp.ReadModels;

namespace Fluxy.Application.Authentication.Utils
{
    /// <summary>
    /// The lookups of one request, kept so the same address is not looked up twice.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three services describe sessions - the visit history, the list of what is still signed
    /// in, and the operator's view of the same rows - and each of them walked its rows through
    /// its own dictionary, with the same eight lines in each copy. A page of visits from one
    /// home connection is the same address twenty times over, and the lookup is not free: it
    /// reads a file on disk and hands back a country and a provider.
    /// </para>
    /// <para>
    /// The dictionary lives here rather than in the caller, so the three services cannot each
    /// spell the memo their own way, and the service is scoped rather than singleton: the
    /// answers describe the request being served, and a memo that outlived the request would
    /// outlive the data it was made for.
    /// </para>
    /// </remarks>
    public sealed class GeoIpMemo
    {
        private readonly IGeoIpResolver _geoIp;
        private readonly Dictionary<string, GeoIpInfo> _byAddress = new(StringComparer.Ordinal);

        /// <summary>
        /// Initializes a new instance of the <see cref="GeoIpMemo"/> class.
        /// </summary>
        /// <param name="geoIp">The resolver that answers what the database knows.</param>
        public GeoIpMemo(IGeoIpResolver geoIp)
        {
            _geoIp = geoIp;
        }

        /// <summary>
        /// Places an address on the map, looking it up the first time it is seen.
        /// </summary>
        /// <param name="clientAddress">Address as the server saw it, may be null or malformed.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// What the database knows. Never null and never thrown: anything unresolvable comes
        /// back as <see cref="GeoIpInfo.IsUnknown"/>.
        /// </returns>
        public async Task<GeoIpInfo> ResolveAsync(
            string? clientAddress,
            CancellationToken cancellationToken = default)
        {
            // The null address is a key of its own: a row without an address is a row the
            // database cannot place, and two such rows must not turn into two lookups.
            var key = clientAddress ?? string.Empty;

            if (!_byAddress.TryGetValue(key, out var geo))
            {
                geo = await _geoIp.ResolveAsync(clientAddress, cancellationToken);
                _byAddress[key] = geo;
            }

            return geo;
        }
    }
}
