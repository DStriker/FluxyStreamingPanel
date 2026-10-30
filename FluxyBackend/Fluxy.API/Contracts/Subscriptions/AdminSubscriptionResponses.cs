namespace Fluxy.API.Contracts.Subscriptions
{
    /// <summary>
    /// One row of the admin subscriptions table: what the main row and its service subrows draw.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The effective values travel, not the stored ones: a subscription with a plan shows the
    /// plan's connection ceiling, trial flag and service support, because that is what it would
    /// actually deliver. A row whose stored values were dormant behind a plan would describe a
    /// subscription that does not exist.
    /// </para>
    /// <para>
    /// The three support flags are what the subrows' "add a service" line is gated on, so the
    /// page can decide which buttons to draw without a second call per row.
    /// </para>
    /// </remarks>
    public sealed record AdminSubscriptionItemResponse
    {
        /// <summary>Identifier of the subscription, as text.</summary>
        public required string Id { get; init; }

        /// <summary>Identifier of the owner, as text.</summary>
        public required string UserId { get; init; }

        /// <summary>Login name of the owner.</summary>
        public required string Username { get; init; }

        /// <summary>Whether the owner's effective status is Blocked.</summary>
        public required bool OwnerBlocked { get; init; }

        /// <summary>Login name of the issuer, or null when that account has been deleted.</summary>
        public string? CreatorUsername { get; init; }

        /// <summary>Identifier of the tariff plan, as text, or null.</summary>
        public string? TariffPlanId { get; init; }

        /// <summary>Name of that plan, or null.</summary>
        public string? TariffPlanName { get; init; }

        /// <summary>
        /// How many <see cref="TariffPlanDurationUnit"/> the attached plan lasts for, or null when
        /// there is no plan. Read by the prolongation confirmation, which projects the new expiry
        /// with the server's own rule.
        /// </summary>
        public int? TariffPlanDurationValue { get; init; }

        /// <summary>
        /// Unit of that length - <c>hours</c>, <c>days</c>, <c>months</c> or <c>years</c> - or null
        /// when there is no plan.
        /// </summary>
        public string? TariffPlanDurationUnit { get; init; }

        /// <summary>When the subscription was issued.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>When it lapses, or null for "never expires".</summary>
        public DateTimeOffset? ExpireAt { get; init; }

        /// <summary>Whether the subscription counts as a trial - the plan's answer when one is attached.</summary>
        public required bool IsTrial { get; init; }

        /// <summary>The computed state, as the name of the enum member.</summary>
        public required string Status { get; init; }

        /// <summary>Effective connection ceiling, or null for unlimited.</summary>
        public int? MaxConnections { get; init; }

        /// <summary>The playlist service's identifier, or null.</summary>
        public string? PlaylistId { get; init; }

        /// <summary>The MAG device's MAC, or null.</summary>
        public string? MagMac { get; init; }

        /// <summary>The Enigma2 device's MAC, or null.</summary>
        public string? Enigma2Mac { get; init; }

        /// <summary>Whether the effective plan allows a playlist.</summary>
        public required bool SupportsPlaylist { get; init; }

        /// <summary>Whether the effective plan allows a MAG device.</summary>
        public required bool SupportsMag { get; init; }

        /// <summary>Whether the effective plan allows an Enigma2 device.</summary>
        public required bool SupportsEnigma2 { get; init; }
    }

    /// <summary>
    /// One page of the admin subscriptions table, with the count the pager is built from.
    /// </summary>
    public sealed record AdminSubscriptionPageResponse
    {
        /// <summary>The subscriptions this page holds, in the requested order.</summary>
        public required IReadOnlyList<AdminSubscriptionItemResponse> Items { get; init; }

        /// <summary>How many subscriptions the same filter admits, all pages together.</summary>
        public required int Total { get; init; }

        /// <summary>One-based page this answer is.</summary>
        public required int Page { get; init; }

        /// <summary>How many subscriptions one page holds.</summary>
        public required int PageSize { get; init; }
    }

    /// <summary>
    /// What the attached tariff plan decides for one subscription, as the form draws it disabled.
    /// </summary>
    public sealed record AdminSubscriptionDerivedResponse
    {
        /// <summary>Connection ceiling the plan allows, or null for unlimited.</summary>
        public required int? MaxConnections { get; init; }

        /// <summary>Whether the plan allows restreaming.</summary>
        public required bool IsRestreamer { get; init; }

        /// <summary>The formats the plan delivers, as enum member names.</summary>
        public required IReadOnlyList<string> OutputFormats { get; init; }

        /// <summary>Whether the plan's MAG identifiers are checked strictly.</summary>
        public required bool IsStbLock { get; init; }

        /// <summary>Whether the plan pins the subscription to one country.</summary>
        public required bool IsCountryLock { get; init; }

        /// <summary>Whether the plan is a trial.</summary>
        public required bool IsTrial { get; init; }

        /// <summary>Whether the plan delivers a playlist.</summary>
        public required bool SupportsPlaylist { get; init; }

        /// <summary>Whether the plan delivers to a MAG device.</summary>
        public required bool SupportsMag { get; init; }

        /// <summary>Whether the plan delivers to an Enigma2 device.</summary>
        public required bool SupportsEnigma2 { get; init; }

        /// <summary>The bouquets the plan delivers, as text.</summary>
        public required IReadOnlyList<string> BouquetIds { get; init; }
    }

    /// <summary>
    /// One subscription in full, for the edit form.
    /// </summary>
    public sealed record AdminSubscriptionDetailResponse
    {
        /// <summary>Identifier of the subscription, as text.</summary>
        public required string Id { get; init; }

        /// <summary>Identifier of the owner, as text.</summary>
        public required string UserId { get; init; }

        /// <summary>Login name of the owner.</summary>
        public required string Username { get; init; }

        /// <summary>Identifier of the issuer, as text, or null.</summary>
        public string? ResellerId { get; init; }

        /// <summary>Login name of the issuer, or null.</summary>
        public string? CreatorUsername { get; init; }

        /// <summary>The playlist service's identifier, or null.</summary>
        public string? PlaylistId { get; init; }

        /// <summary>When it lapses, or null for "never expires".</summary>
        public DateTimeOffset? ExpireAt { get; init; }

        /// <summary>Identifier of the tariff plan, as text, or null.</summary>
        public string? TariffPlanId { get; init; }

        /// <summary>Name of the tariff plan, or null.</summary>
        public string? TariffPlanName { get; init; }

        /// <summary>The computed state, as the name of the enum member.</summary>
        public required string Status { get; init; }

        /// <summary>Stored connection ceiling, or null for unlimited.</summary>
        public int? MaxConnections { get; init; }

        /// <summary>Stored restreaming flag.</summary>
        public required bool IsRestreamer { get; init; }

        /// <summary>Stored addresses. Empty means no restriction.</summary>
        public required IReadOnlyList<string> IpWhitelist { get; init; }

        /// <summary>Stored user agents. Empty means no restriction.</summary>
        public required IReadOnlyList<string> UserAgentWhitelist { get; init; }

        /// <summary>Stored provider lock flag.</summary>
        public required bool IsIspLock { get; init; }

        /// <summary>Stored country lock flag.</summary>
        public required bool IsCountryLock { get; init; }

        /// <summary>Stored trial flag.</summary>
        public required bool IsTrial { get; init; }

        /// <summary>Stored formats, as enum member names.</summary>
        public required IReadOnlyList<string> OutputFormats { get; init; }

        /// <summary>Stored MAG MAC, or null.</summary>
        public string? MagMac { get; init; }

        /// <summary>Stored STB lock flag.</summary>
        public required bool IsStbLock { get; init; }

        /// <summary>Stored Enigma2 MAC, or null.</summary>
        public string? Enigma2Mac { get; init; }

        /// <summary>The subscription's own bouquets, as text.</summary>
        public required IReadOnlyList<string> BouquetIds { get; init; }

        /// <summary>When the subscription was issued.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>The plan-derived values while a plan is attached, or null.</summary>
        public AdminSubscriptionDerivedResponse? Derived { get; init; }
    }
}
