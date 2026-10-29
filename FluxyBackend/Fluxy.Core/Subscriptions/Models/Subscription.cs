using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.Core.Subscriptions.Models
{
    /// <summary>
    /// Immutable business representation of one subscription - the row an operator issues to a
    /// user: who owns it, which services it delivers, what access it allows and when it lapses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The services are columns, not child rows.</b> <see cref="PlaylistId"/>,
    /// <see cref="MagMac"/> and <see cref="Enigma2Mac"/> are three nullable fields, because each
    /// kind is at most one per subscription - a playlist is one identifier, a device is one MAC
    /// - and a table of service rows would buy a join and the "unloaded collection reads as
    /// empty" bug class for a 1:0..1 field.
    /// </para>
    /// <para>
    /// <b>The tariff plan owns what it owns.</b> When <see cref="TariffPlanId"/> is set the
    /// plan is the single source of truth for the connection ceiling, restreaming, output
    /// formats, the STB lock, the country lock, the trial flag and which services are
    /// supported. The properties below still carry values - they are what the subscription
    /// applies again once the plan is cleared - but a save with a plan does not write them.
    /// </para>
    /// <para>
    /// <see cref="PlayToken"/> is stored and deliberately unused: the streaming subsystem that
    /// will verify a MAC against it owns its lifecycle, and nothing in this pass writes or
    /// reads it. It is a column here so the row exists before that subsystem does, not a value
    /// this domain has an opinion about.
    /// </para>
    /// </remarks>
    public sealed record Subscription
    {
        /// <summary>Identifier of the subscription.</summary>
        public required Guid Id { get; init; }

        /// <summary>Account the subscription belongs to. Set at creation and never changed.</summary>
        public required Guid UserId { get; init; }

        /// <summary>
        /// The playlist service, as the identifier a subscriber's player is pointed at, or null
        /// when the subscription delivers no playlist.
        /// </summary>
        public string? PlaylistId { get; init; }

        /// <summary>Moment the subscription lapses, or null for "never expires".</summary>
        public DateTimeOffset? ExpireAt { get; init; }

        /// <summary>
        /// The tariff plan the subscription is sold under, or null when it carries its own
        /// parameters instead.
        /// </summary>
        public Guid? TariffPlanId { get; init; }

        /// <summary>
        /// How many devices may watch at once, or null for unlimited. Only ever written while
        /// no plan is attached.
        /// </summary>
        public int? MaxConnections { get; init; }

        /// <summary>Whether the subscriber may restream. Only written without a plan.</summary>
        public bool IsRestreamer { get; init; }

        /// <summary>
        /// Addresses the subscription may be watched from, exact or CIDR. Empty means no
        /// restriction. Replaced wholesale on save.
        /// </summary>
        public required IReadOnlyList<string> IpWhitelist { get; init; }

        /// <summary>
        /// User agents the subscription may be watched with, as free text. Empty means no
        /// restriction. Replaced wholesale on save.
        /// </summary>
        public required IReadOnlyList<string> UserAgentWhitelist { get; init; }

        /// <summary>Whether the subscription is pinned to one provider. Only written without a plan.</summary>
        public bool IsIspLock { get; init; }

        /// <summary>Whether the subscription is pinned to one country. Only written without a plan.</summary>
        public bool IsCountryLock { get; init; }

        /// <summary>Whether the subscription is a trial. Only written without a plan.</summary>
        public bool IsTrial { get; init; }

        /// <summary>
        /// Account that issued the subscription - the administrator or reseller who created it,
        /// reassignable afterwards. Null when that account has since been deleted.
        /// </summary>
        public Guid? ResellerId { get; init; }

        /// <summary>The formats the subscription is delivered in. Only written without a plan.</summary>
        public required SubscriptionOutputFormat OutputFormats { get; init; }

        /// <summary>MAG device the subscription plays on, canonical <c>XX:XX:XX:XX:XX:XX</c>, or null.</summary>
        public string? MagMac { get; init; }

        /// <summary>Whether a MAG device's identifiers are checked strictly. Only written without a plan.</summary>
        public bool IsStbLock { get; init; }

        /// <summary>Enigma2 device the subscription plays on, canonical <c>XX:XX:XX:XX:XX:XX</c>, or null.</summary>
        public string? Enigma2Mac { get; init; }

        /// <summary>
        /// Reserved for the streaming subsystem that will verify MAC access. Nothing in this
        /// pass writes or reads it.
        /// </summary>
        public string? PlayToken { get; init; }

        /// <summary>
        /// The bouquets the subscription carries, used only while no plan is attached. With a
        /// plan the plan's bouquets are the source of truth and this list is kept but dormant.
        /// </summary>
        public required IReadOnlyList<Guid> BouquetIds { get; init; }

        /// <summary>Creation timestamp of the subscription.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>Last modification timestamp of the subscription.</summary>
        public required DateTimeOffset UpdatedAt { get; init; }
    }
}
