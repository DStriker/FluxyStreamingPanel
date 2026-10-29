using Fluxy.Core.Subscriptions.Utils;

namespace Fluxy.API.Contracts.Subscriptions
{
    /// <summary>The answer to a playlist-id generator request.</summary>
    /// <remarks>
    /// A body of its own rather than a full subscription, because the request writes nothing: the
    /// form asks what identifier it is about to store, and a resource write would be a lie about
    /// what happened.
    /// </remarks>
    public sealed record SubscriptionPlaylistIdResponse
    {
        /// <summary>A fresh 32-hex identifier no subscription holds.</summary>
        public required string PlaylistId { get; init; }

        /// <summary>Length of a playlist id, so a client can validate without its own constant.</summary>
        public static int Length => SubscriptionLimits.PlaylistIdLength;
    }
}
