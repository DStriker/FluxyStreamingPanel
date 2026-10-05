using Fluxy.Core.Models.GeoIp;

namespace Fluxy.Core.Abstractions
{
    /// <summary>
    /// Places a client address on the map: country and provider.
    /// </summary>
    /// <remarks>
    /// The implementation lives in <c>Fluxy.Application</c>, the interface lives here, for the
    /// same reason every other business interface does: the API layer may ask, but it may never
    /// name the database that answers.
    /// </remarks>
    public interface IGeoIpResolver
    {
        /// <summary>
        /// Resolves a client address to its country and provider.
        /// </summary>
        /// <param name="clientAddress">Address as the server saw it, may be null or malformed.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// What the database knows. Never null and never thrown: anything unresolvable comes
        /// back as <see cref="GeoIpInfo.IsUnknown"/>.
        /// </returns>
        Task<GeoIpInfo> ResolveAsync(string? clientAddress, CancellationToken cancellationToken = default);
    }
}
