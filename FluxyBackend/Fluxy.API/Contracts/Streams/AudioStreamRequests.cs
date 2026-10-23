namespace Fluxy.API.Contracts.Streams
{
    /// <summary>
    /// What the add-audio-stream form sends to make a stream in one request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>status</c> <i>is</i> a field: the form chooses whether the stream starts active, and
    /// an absent value keeps the original contract and starts it stopped.
    /// </para>
    /// <para>
    /// <b>Both audio kinds take a list of <c>sources</c>.</b> Unlike a video stream, there is
    /// no payload a kind can switch off: on-demand and redirect are two ways of playing the
    /// same address list. The list is required and is stored in the order it is sent.
    /// </para>
    /// </remarks>
    public sealed record CreateAudioStreamRequest
    {
        /// <summary>Name of the stream, as typed. Unique within its category.</summary>
        public string? Name { get; init; }

        /// <summary>
        /// The kind of stream, as the name of the enum member compared case insensitively -
        /// <c>ondemand</c> or <c>redirect</c>.
        /// </summary>
        public string? Type { get; init; }

        /// <summary>
        /// Whether the stream starts active or disabled, as the name of the enum member.
        /// Absent means <c>stopped</c>.
        /// </summary>
        public string? Status { get; init; }

        /// <summary>Identifier of the category the stream is filed under, as text.</summary>
        public string? CategoryId { get; init; }

        /// <summary>Address the icon is fetched from, or none.</summary>
        public string? LogoUrl { get; init; }

        /// <summary>Enigma2 service reference as text, or none.</summary>
        public string? Enigma2Sid { get; init; }

        /// <summary>User agent the relayed requests carry, or none.</summary>
        public string? RestreamUserAgent { get; init; }

        /// <summary>Proxy the relay goes out through, or none.</summary>
        public string? RestreamProxy { get; init; }

        /// <summary>Cookies the relayed requests carry, or none.</summary>
        public string? RestreamCookies { get; init; }

        /// <summary>Extra headers the relayed requests carry, as typed.</summary>
        public string? RestreamHeaders { get; init; }

        /// <summary>
        /// Sources to relay from, in order, the first as the primary and the rest as the
        /// fallback.
        /// </summary>
        public IReadOnlyList<string>? Sources { get; init; }
    }

    /// <summary>
    /// What the edit-audio-stream form sends. Every property is optional, and an absent one
    /// means "leave it alone".
    /// </summary>
    /// <remarks>
    /// <para>
    /// A nullable text field answers "keep" and "clear" without a second field to tell them
    /// apart: <c>null</c> keeps whatever the row holds, an empty string clears it, and
    /// anything else is the new value.
    /// </para>
    /// <para>
    /// The source list follows the same shape: an absent list keeps the stored one, and a list
    /// with entries replaces it wholesale in the order they are named. An empty list is
    /// refused, because both kinds relay from a list and a stream with none could never play.
    /// </para>
    /// </remarks>
    public sealed record UpdateAudioStreamRequest
    {
        /// <summary>New name, or null to keep the one on the row.</summary>
        public string? Name { get; init; }

        /// <summary>New kind of stream, as the name of the enum member, or null to keep.</summary>
        public string? Type { get; init; }

        /// <summary>New status, as the name of the enum member, or null to keep the stored one.</summary>
        public string? Status { get; init; }

        /// <summary>New category, as text; null keeps.</summary>
        public string? CategoryId { get; init; }

        /// <summary>New icon address; null keeps, empty clears.</summary>
        public string? LogoUrl { get; init; }

        /// <summary>New service reference; null keeps, empty clears.</summary>
        public string? Enigma2Sid { get; init; }

        /// <summary>New user agent; null keeps, empty clears.</summary>
        public string? RestreamUserAgent { get; init; }

        /// <summary>New proxy; null keeps, empty clears.</summary>
        public string? RestreamProxy { get; init; }

        /// <summary>New cookies; null keeps, empty clears.</summary>
        public string? RestreamCookies { get; init; }

        /// <summary>New headers; null keeps, empty clears.</summary>
        public string? RestreamHeaders { get; init; }

        /// <summary>New source list; null keeps, a list replaces. An empty list is refused.</summary>
        public IReadOnlyList<string>? Sources { get; init; }
    }
}
