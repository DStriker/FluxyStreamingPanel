using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.Core.Subscriptions.ReadModels
{
    /// <summary>
    /// One subscription in full, as the edit form reads it: every stored parameter plus the
    /// plan-derived values the form draws disabled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stored values and the effective ones are both present, and that is deliberate: the
    /// form has to <i>show</i> what the plan decides while the plan is attached, and it also has
    /// to know what the row carries underneath so clearing the plan restores something real
    /// rather than a guess.
    /// </para>
    /// <para>
    /// <see cref="BouquetIds"/> is the subscription's own list, which is used only while no plan
    /// is attached. With a plan the form draws the plan's bouquets instead and this list lies
    /// dormant, exactly as the stored values above do.
    /// </para>
    /// </remarks>
    public sealed record SubscriptionDetail
    {
        /// <summary>Identifier of the subscription.</summary>
        public required Guid Id { get; init; }

        /// <summary>Identifier of the owner, as text for the form's owner cell.</summary>
        public required Guid UserId { get; init; }

        /// <summary>Login name of the owner.</summary>
        public required string Username { get; init; }

        /// <summary>Identifier of the issuer, or null when that account has been deleted.</summary>
        public Guid? ResellerId { get; init; }

        /// <summary>Login name of the issuer, or null when that account has been deleted.</summary>
        public string? CreatorUsername { get; init; }

        /// <summary>The playlist service's identifier, or null.</summary>
        public string? PlaylistId { get; init; }

        /// <summary>When the subscription lapses, or null for "never expires".</summary>
        public DateTimeOffset? ExpireAt { get; init; }

        /// <summary>Identifier of the tariff plan, or null.</summary>
        public Guid? TariffPlanId { get; init; }

        /// <summary>Name of the tariff plan, or null.</summary>
        public string? TariffPlanName { get; init; }

        /// <summary>The computed state.</summary>
        public required SubscriptionStatus Status { get; init; }

        /// <summary>Stored connection ceiling, or null for unlimited.</summary>
        public int? MaxConnections { get; init; }

        /// <summary>Stored restreaming flag.</summary>
        public required bool IsRestreamer { get; init; }

        /// <summary>Stored addresses, exact or CIDR. Empty means no restriction.</summary>
        public required IReadOnlyList<string> IpWhitelist { get; init; }

        /// <summary>Stored user agents. Empty means no restriction.</summary>
        public required IReadOnlyList<string> UserAgentWhitelist { get; init; }

        /// <summary>Stored provider lock flag.</summary>
        public required bool IsIspLock { get; init; }

        /// <summary>Stored country lock flag.</summary>
        public required bool IsCountryLock { get; init; }

        /// <summary>Stored trial flag.</summary>
        public required bool IsTrial { get; init; }

        /// <summary>Stored output formats.</summary>
        public required SubscriptionOutputFormat OutputFormats { get; init; }

        /// <summary>Stored MAG MAC, or null.</summary>
        public string? MagMac { get; init; }

        /// <summary>Stored STB lock flag.</summary>
        public required bool IsStbLock { get; init; }

        /// <summary>Stored Enigma2 MAC, or null.</summary>
        public string? Enigma2Mac { get; init; }

        /// <summary>The subscription's own bouquets, dormant while a plan is attached.</summary>
        public required IReadOnlyList<Guid> BouquetIds { get; init; }

        /// <summary>When the subscription was issued.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// The plan-derived values the form draws while a plan is attached, or null when no plan
        /// is. These are the values a save with that plan would write.
        /// </summary>
        public SubscriptionDerivedValues? Derived { get; init; }
    }
}
