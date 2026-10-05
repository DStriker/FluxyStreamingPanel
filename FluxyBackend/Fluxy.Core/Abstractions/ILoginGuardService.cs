using Fluxy.Core.Models.GeoIp;

namespace Fluxy.Core.Abstractions
{
    /// <summary>
    /// Decides whether a network may open or keep a session of one account.
    /// </summary>
    /// <remarks>
    /// The enforcement half of the login guard. The settings themselves are read and written
    /// through <see cref="IProfileService"/>; this service answers one question on the sign-in
    /// and refresh paths, and knows nothing about HTTP, passwords or confirmation codes.
    /// </remarks>
    public interface ILoginGuardService
    {
        /// <summary>
        /// Whether the network <paramref name="where"/> describes may act for the account.
        /// </summary>
        /// <param name="userId">Account whose lists are checked.</param>
        /// <param name="where">Resolved network the request came from.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// True when the guard is off or every non-empty list allows the network. An unknown
        /// network on an account with the guard on is refused - failing open would silently
        /// switch the protection off the moment the database went stale.
        /// </returns>
        Task<bool> IsAllowedAsync(Guid userId, GeoIpInfo where, CancellationToken cancellationToken = default);
    }
}
