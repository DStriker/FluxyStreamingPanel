namespace Fluxy.Core.Models.Authentication
{
    /// <summary>
    /// The freshest live refresh token of one session, from which an active session is drawn.
    /// </summary>
    /// <remarks>
    /// A projection rather than the whole row: the active-sessions page needs the address, the
    /// client and the moment, and reading the token hash as well would put a credential-shaped
    /// value into a service that has no use for it.
    /// </remarks>
    public sealed record StoredActiveSession
    {
        /// <summary>Session the row belongs to.</summary>
        public required Guid Id { get; init; }

        /// <summary>Address the session was last seen at, if one was stored.</summary>
        public string? ClientAddress { get; init; }

        /// <summary>User agent the session was last seen with, if one was stored.</summary>
        public string? UserAgent { get; init; }

        /// <summary>Moment the live row was written.</summary>
        public required DateTimeOffset LastSeenAt { get; init; }
    }
}
