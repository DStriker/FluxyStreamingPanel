using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.Core.Subscriptions.Commands
{
    /// <summary>
    /// What an administrator changes about an existing subscription. Every property is optional,
    /// and an absent one means "leave it alone".
    /// </summary>
    /// <remarks>
    /// <para>
    /// A nullable text field answers "keep" and "clear" without a second field: <c>null</c>
    /// keeps whatever the row holds, an empty string clears it, and anything else is the new
    /// value. <see cref="ExpireAt"/> follows the same shape and travels as text precisely so the
    /// two requests stay distinguishable - a null <see cref="DateTimeOffset"/> would be
    /// ambiguous between "leave the expiry" and "make it never expire".
    /// </para>
    /// <para>
    /// <see cref="TariffPlanId"/> needs one extra state, so it carries a companion flag: a
    /// non-null identifier attaches that plan, <see cref="ClearTariffPlan"/> nulls the reference
    /// and re-activates the subscription's own parameters, and neither keeps the current plan.
    /// </para>
    /// <para>
    /// The two lists follow the rule every other list on this API follows: absent keeps, an empty
    /// list clears, a list replaces wholesale.
    /// </para>
    /// </remarks>
    public sealed record SubscriptionPatch
    {
        /// <summary>New playlist id, empty to clear, null to keep.</summary>
        public string? PlaylistId { get; init; }

        /// <summary>New expiry as <c>yyyy-MM-dd</c>, empty to clear, null to keep.</summary>
        public string? ExpireAt { get; init; }

        /// <summary>The plan to attach, or null to leave the reference alone.</summary>
        public Guid? TariffPlanId { get; init; }

        /// <summary>
        /// True to null the plan reference and re-activate the subscription's own parameters.
        /// Ignored when <see cref="TariffPlanId"/> names a plan.
        /// </summary>
        public bool ClearTariffPlan { get; init; }

        /// <summary>New connection ceiling, or null to keep the current one.</summary>
        public int? MaxConnections { get; init; }

        /// <summary>True to store unlimited connections rather than a ceiling.</summary>
        public bool ClearMaxConnections { get; init; }

        /// <summary>New restreaming flag, or null to keep the current one.</summary>
        public bool? IsRestreamer { get; init; }

        /// <summary>New addresses, empty to clear, null to keep.</summary>
        public IReadOnlyList<string>? IpWhitelist { get; init; }

        /// <summary>New user agents, empty to clear, null to keep.</summary>
        public IReadOnlyList<string>? UserAgentWhitelist { get; init; }

        /// <summary>New provider lock flag, or null to keep the current one.</summary>
        public bool? IsIspLock { get; init; }

        /// <summary>New country lock flag, or null to keep the current one.</summary>
        public bool? IsCountryLock { get; init; }

        /// <summary>New trial flag, or null to keep the current one.</summary>
        public bool? IsTrial { get; init; }

        /// <summary>New output formats, or null to keep the current ones.</summary>
        public SubscriptionOutputFormat? OutputFormats { get; init; }

        /// <summary>New MAG MAC, empty to clear, null to keep.</summary>
        public string? MagMac { get; init; }

        /// <summary>New STB lock flag, or null to keep the current one.</summary>
        public bool? IsStbLock { get; init; }

        /// <summary>New Enigma2 MAC, empty to clear, null to keep.</summary>
        public string? Enigma2Mac { get; init; }

        /// <summary>New bouquets, empty to clear, null to keep.</summary>
        public IReadOnlyList<Guid>? BouquetIds { get; init; }

        /// <summary>
        /// New issuer, or null to keep the current one. Only ever read by the bulk
        /// <c>change-creator</c> operation, which is the one place the issuer changes.
        /// </summary>
        public Guid? ResellerId { get; init; }
    }
}
