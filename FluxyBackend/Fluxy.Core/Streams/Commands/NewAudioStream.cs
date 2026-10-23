using Fluxy.Core.Streams.Enums;

namespace Fluxy.Core.Streams.Commands
{
    /// <summary>
    /// Everything an administrator supplies to create an audio stream in one request.
    /// </summary>
    /// <remarks>
    /// <b>Status is a field of the create.</b> An operator decides when adding the stream
    /// whether it should be active straight away; the API answers
    /// <see cref="AudioStreamStatus.Stopped"/> when the body omits it, which keeps the contract
    /// for callers that predate the selector. The status remains the same row fact the start
    /// and stop actions write - a create is simply allowed to set it once.
    /// </remarks>
    public sealed record NewAudioStream
    {
        /// <summary>Name the stream gets. Trimmed, compared case sensitively.</summary>
        public required string Name { get; init; }

        /// <summary>The kind of stream.</summary>
        public required AudioStreamType Type { get; init; }

        /// <summary>Whether the stream starts active or disabled.</summary>
        public required AudioStreamStatus Status { get; init; }

        /// <summary>Category the stream is filed under.</summary>
        public required Guid CategoryId { get; init; }

        /// <summary>Address the icon is fetched from, or null.</summary>
        public string? LogoUrl { get; init; }

        /// <summary>Enigma2 service reference as text, or null.</summary>
        public string? Enigma2Sid { get; init; }

        /// <summary>User agent the relayed requests carry, or null.</summary>
        public string? RestreamUserAgent { get; init; }

        /// <summary>Proxy the relay goes out through, or null.</summary>
        public string? RestreamProxy { get; init; }

        /// <summary>Cookies the relayed requests carry, or null.</summary>
        public string? RestreamCookies { get; init; }

        /// <summary>Extra headers the relayed requests carry, as typed.</summary>
        public string? RestreamHeaders { get; init; }

        /// <summary>Sources to relay from, in order, the first as the primary.</summary>
        public required IReadOnlyList<string> Sources { get; init; }
    }
}
