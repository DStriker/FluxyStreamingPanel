using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.Core.Subscriptions.Commands
{
    /// <summary>
    /// Everything an administrator supplies to issue a subscription.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The owner and the issuer are not optional. A subscription without an owner is a row
    /// nobody can be told about, and the issuer is the account that took the decision - the
    /// transport stamps it from the session, so a body never names it.
    /// </para>
    /// <para>
    /// <b>Every dependent field is carried even though a plan may override it.</b> A create with
    /// a plan ignores them; a create without one stores them; and a later save that clears the
    /// plan needs something real to fall back on. Dropping them from the request when a plan is
    /// attached would leave a subscription whose own parameters were never entered, which is
    /// exactly the state the "clearing the plan re-activates the fields" rule promises not to
    /// produce.
    /// </para>
    /// </remarks>
    public sealed record NewSubscription
    {
        /// <summary>Account the subscription belongs to. Never changed afterwards.</summary>
        public required Guid UserId { get; init; }

        /// <summary>Account issuing it, stamped from the session.</summary>
        public required Guid ResellerId { get; init; }

        /// <summary>The playlist service's identifier, or null to leave it off.</summary>
        public string? PlaylistId { get; init; }

        /// <summary>Moment it lapses, or null for "never expires".</summary>
        public DateTimeOffset? ExpireAt { get; init; }

        /// <summary>The tariff plan it is sold under, or null to carry its own parameters.</summary>
        public Guid? TariffPlanId { get; init; }

        /// <summary>Connection ceiling, or null for unlimited.</summary>
        public int? MaxConnections { get; init; }

        /// <summary>Whether the subscriber may restream.</summary>
        public bool IsRestreamer { get; init; }

        /// <summary>Addresses it may be watched from. Empty means no restriction.</summary>
        public IReadOnlyList<string> IpWhitelist { get; init; } = [];

        /// <summary>User agents it may be watched with. Empty means no restriction.</summary>
        public IReadOnlyList<string> UserAgentWhitelist { get; init; } = [];

        /// <summary>Whether it is pinned to one provider.</summary>
        public bool IsIspLock { get; init; }

        /// <summary>Whether it is pinned to one country.</summary>
        public bool IsCountryLock { get; init; }

        /// <summary>Whether it is a trial.</summary>
        public bool IsTrial { get; init; }

        /// <summary>The formats it is delivered in. At least one.</summary>
        public SubscriptionOutputFormat OutputFormats { get; init; } = SubscriptionOutputFormat.MpegTs;

        /// <summary>MAG device's MAC, any case and either separator, or null.</summary>
        public string? MagMac { get; init; }

        /// <summary>Whether MAG identifiers are checked strictly.</summary>
        public bool IsStbLock { get; init; }

        /// <summary>Enigma2 device's MAC, any case and either separator, or null.</summary>
        public string? Enigma2Mac { get; init; }

        /// <summary>
        /// The bouquets it carries, or null to preselect every existing bouquet.
        /// </summary>
        /// <remarks>
        /// A null is a decision rather than a missing value: a create that names no bouquet is a
        /// create by an operator who has not chosen yet, and the domain's answer is every
        /// bouquet the installation has. An empty list is the caller saying "none", which the
        /// minimum-one rule refuses while no plan is attached.
        /// </remarks>
        public IReadOnlyList<Guid>? BouquetIds { get; init; }
    }
}
