namespace Fluxy.API.Contracts.Subscriptions
{
    /// <summary>
    /// What the add-subscription form sends to issue one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No data annotations on purpose: framework validation runs before the action, so a body
    /// that failed one would never reach the antiforgery check. Every field is checked in the
    /// service after the CSRF pair has been accepted.
    /// </para>
    /// <para>
    /// <b>The owner and the issuer travel as text, and the issuer is ignored.</b> The owner is an
    /// identifier the form picked; the issuer is stamped from the session by the controller, so
    /// a body naming one is not obeyed - an operator cannot issue a subscription in somebody
    /// else's name by editing a request.
    /// </para>
    /// <para>
    /// <see cref="ExpireAt"/> travels as <c>yyyy-MM-dd</c> rather than as a timestamp: an expiry
    /// is a calendar date the operator chose, and a timestamp would invite a time-of-day nobody
    /// meant. <see cref="OutputFormats"/> travels as the names of the enum members, so a client
    /// sends <c>["mpegTs","hls"]</c> and reads the same names back.
    /// </para>
    /// </remarks>
    public sealed record CreateSubscriptionRequest
    {
        /// <summary>Account the subscription belongs to, as a uuid.</summary>
        public string? UserId { get; init; }

        /// <summary>The playlist service's identifier, or null to leave it off.</summary>
        public string? PlaylistId { get; init; }

        /// <summary>Expiry as <c>yyyy-MM-dd</c>, or null for "never expires".</summary>
        public string? ExpireAt { get; init; }

        /// <summary>The tariff plan it is sold under, as a uuid, or null.</summary>
        public string? TariffPlanId { get; init; }

        /// <summary>Connection ceiling, or null for unlimited.</summary>
        public int? MaxConnections { get; init; }

        /// <summary>Whether the subscriber may restream.</summary>
        public bool IsRestreamer { get; init; }

        /// <summary>Addresses it may be watched from. Empty means no restriction.</summary>
        public IReadOnlyList<string>? IpWhitelist { get; init; }

        /// <summary>User agents it may be watched with. Empty means no restriction.</summary>
        public IReadOnlyList<string>? UserAgentWhitelist { get; init; }

        /// <summary>Whether it is pinned to one provider.</summary>
        public bool IsIspLock { get; init; }

        /// <summary>Whether it is pinned to one country.</summary>
        public bool IsCountryLock { get; init; }

        /// <summary>Whether it is a trial.</summary>
        public bool IsTrial { get; init; }

        /// <summary>The formats it is delivered in, as enum member names.</summary>
        public IReadOnlyList<string>? OutputFormats { get; init; }

        /// <summary>MAG device's MAC, any case and either separator, or null.</summary>
        public string? MagMac { get; init; }

        /// <summary>Whether MAG identifiers are checked strictly.</summary>
        public bool IsStbLock { get; init; }

        /// <summary>Enigma2 device's MAC, any case and either separator, or null.</summary>
        public string? Enigma2Mac { get; init; }

        /// <summary>
        /// The bouquets it carries, or null to preselect every one the installation has. Only
        /// used while no plan is attached.
        /// </summary>
        public IReadOnlyList<string>? BouquetIds { get; init; }
    }
}
