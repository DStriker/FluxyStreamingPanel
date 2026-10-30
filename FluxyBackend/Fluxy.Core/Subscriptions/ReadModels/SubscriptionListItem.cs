using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.Core.Subscriptions.ReadModels
{
    /// <summary>
    /// One subscription as a row of the table, and everything its subrows need to decide what to
    /// draw.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The list feeds the whole screen on purpose: the main row draws the owner, the plan, the
    /// creation date, the status and the expiry, and the service subrows below it draw the
    /// playlist and the two MACs. Shipping the <i>effective</i> support flags with each row
    /// means the "add a service" line can decide which buttons are allowed without a second
    /// call per row.
    /// </para>
    /// <para>
    /// <b>Effective, not stored.</b> <see cref="MaxConnections"/>, <see cref="IsTrial"/> and the
    /// three support flags are the plan's values while a plan is attached, and the row's own
    /// values otherwise. A table that showed the dormant stored values beside a plan would
    /// describe a subscription that does not exist.
    /// </para>
    /// </remarks>
    public sealed record SubscriptionListItem
    {
        /// <summary>Identifier of the subscription.</summary>
        public required Guid Id { get; init; }

        /// <summary>Identifier of the owner, for the owner filter and the owner cell's link.</summary>
        public required Guid UserId { get; init; }

        /// <summary>Login name of the owner.</summary>
        public required string Username { get; init; }

        /// <summary>Whether the owner's effective status is Blocked.</summary>
        public required bool OwnerBlocked { get; init; }

        /// <summary>Login name of the issuer, or null when that account has been deleted.</summary>
        public string? CreatorUsername { get; init; }

        /// <summary>Identifier of the tariff plan, or null when the subscription carries its own parameters.</summary>
        public Guid? TariffPlanId { get; init; }

        /// <summary>Name of that plan, or null when there is none.</summary>
        public string? TariffPlanName { get; init; }

        /// <summary>
        /// How many <see cref="TariffPlanDurationUnit"/> the attached plan lasts for, or null when
        /// there is no plan.
        /// </summary>
        /// <remarks>
        /// Travels for one reader: the prolongation confirmation, which has to show the date the
        /// row is about to move to and must compute it with the server's own rule -
        /// <c>max(now, expiry) + the plan's duration</c>. Sending the two numbers with the page the
        /// table already reads is what keeps that preview from needing a second request per dialog
        /// open, and from needing the whole plan list on a screen that only ever shows one plan's
        /// length at a time.
        /// </remarks>
        public int? TariffPlanDurationValue { get; init; }

        /// <summary>The unit that length is counted in, or null when there is no plan.</summary>
        public TariffPlanDurationUnit? TariffPlanDurationUnit { get; init; }

        /// <summary>When the subscription was issued.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>When it lapses, or null for "never expires".</summary>
        public DateTimeOffset? ExpireAt { get; init; }

        /// <summary>Whether the subscription counts as a trial - the plan's answer when one is attached.</summary>
        public required bool IsTrial { get; init; }

        /// <summary>The computed state: expired &gt; blocked &gt; trial &gt; active.</summary>
        public required SubscriptionStatus Status { get; init; }

        /// <summary>Effective connection ceiling, or null for unlimited.</summary>
        public int? MaxConnections { get; init; }

        /// <summary>The playlist service's identifier, or null when none is attached.</summary>
        public string? PlaylistId { get; init; }

        /// <summary>The MAG device's MAC, or null when none is attached.</summary>
        public string? MagMac { get; init; }

        /// <summary>The Enigma2 device's MAC, or null when none is attached.</summary>
        public string? Enigma2Mac { get; init; }

        /// <summary>Whether the effective plan allows a playlist.</summary>
        public required bool SupportsPlaylist { get; init; }

        /// <summary>Whether the effective plan allows a MAG device.</summary>
        public required bool SupportsMag { get; init; }

        /// <summary>Whether the effective plan allows an Enigma2 device.</summary>
        public required bool SupportsEnigma2 { get; init; }
    }
}
