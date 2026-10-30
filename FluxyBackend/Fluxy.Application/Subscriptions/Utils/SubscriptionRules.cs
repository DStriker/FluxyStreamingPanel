using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Rows;
using Fluxy.Core.Subscriptions.Utils;

namespace Fluxy.Application.Subscriptions.Utils
{
    /// <summary>
    /// The rules every write to a subscription is judged by, the sentences that describe breaking
    /// them, and the two calculations a row derives rather than stores.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A create and a change reach the same row through different requests, so the rules that are
    /// a fact about the row rather than about the request live here once. What is not here is what
    /// a request alone decides: a create resolves its defaults and a patch resolves "absent means
    /// keep", and both stay with the service.
    /// </para>
    /// <para>
    /// Every rule below is refused rather than corrected, except the one the specification
    /// explicitly says corrects: <b>a service the selected plan does not support is cleared</b>
    /// rather than refused, because the plan is the operator's own statement about what the
    /// subscription may deliver. Only the case where clearing leaves nothing at all is refused -
    /// a subscription with no service is not a subscription.
    /// </para>
    /// <para>
    /// The keys of the maps are the C# names of the fields on the commands, the convention every
    /// other service here follows; the transport turns them into the camelCase names a form reads.
    /// </para>
    /// </remarks>
    public static class SubscriptionRules
    {
        /// <summary>The partial unique index making a playlist id unique across subscriptions.</summary>
        public const string PlaylistIdIndexName = "IX_subscriptions_playlist_id";

        /// <summary>The partial unique index making a MAG MAC unique across subscriptions.</summary>
        public const string MagMacIndexName = "IX_subscriptions_mag_mac";

        /// <summary>The partial unique index making an Enigma2 MAC unique across subscriptions.</summary>
        public const string Enigma2MacIndexName = "IX_subscriptions_enigma2_mac";

        /// <summary>The sentence a playlist id of the wrong shape gets.</summary>
        public static readonly string[] PlaylistIdRule =
        [
            "A playlist id must be " + SubscriptionLimits.PlaylistIdLength +
                " lower-case hexadecimal characters."
        ];

        /// <summary>The sentence a MAC that is not six hex pairs gets.</summary>
        public static readonly string[] MacRule =
        [
            "A MAC address must be six pairs of hexadecimal digits, with ':' or '-' between them."
        ];

        /// <summary>The sentence a whitelist entry that is neither an address nor a CIDR gets.</summary>
        public static readonly string[] IpWhitelistRule =
        [
            "Every entry must be an IP address or a CIDR range, IPv4 or IPv6."
        ];

        /// <summary>The sentence an IP whitelist longer than the bound gets.</summary>
        public static readonly string[] IpWhitelistCountRule =
        [
            "No more than " + SubscriptionLimits.WhitelistMaxEntries + " addresses may be listed."
        ];

        /// <summary>The sentence an empty user-agent entry gets.</summary>
        public static readonly string[] UserAgentWhitelistRule =
        [
            "Every user agent must be between 1 and " +
                SubscriptionLimits.WhitelistEntryMaxLength + " characters."
        ];

        /// <summary>The sentence a user-agent whitelist longer than the bound gets.</summary>
        public static readonly string[] UserAgentWhitelistCountRule =
        [
            "No more than " + SubscriptionLimits.WhitelistMaxEntries + " user agents may be listed."
        ];

        /// <summary>The sentence a connection ceiling outside its range gets.</summary>
        public static readonly string[] MaxConnectionsRule =
        [
            "The number of connections must be between " + SubscriptionLimits.ConnectionsMin +
                " and " + SubscriptionLimits.ConnectionsMax + "."
        ];

        /// <summary>The sentence a save that would leave no service gets.</summary>
        public static readonly string[] ServiceRequiredRule =
        [
            "A subscription must deliver at least one service: a playlist, a MAG device or an " +
                "Enigma2 device."
        ];

        /// <summary>The sentence a plan-less save with no bouquet gets.</summary>
        public static readonly string[] BouquetsRequiredRule =
        [
            "A subscription without a tariff plan must carry at least one bouquet."
        ];

        /// <summary>The sentence a bouquet identifier that names nothing gets.</summary>
        public static readonly string[] BouquetIdsRule =
        [
            "Every identifier must be the uuid of an existing bouquet."
        ];

        /// <summary>The sentence an output-format set that names nothing gets.</summary>
        public static readonly string[] OutputFormatsRule =
        [
            "A subscription must be delivered in at least one format: MPEG-TS or HLS."
        ];

        /// <summary>The sentence a plan whose groups do not include the owner's group gets.</summary>
        public static readonly string[] PlanUnavailableRule =
        [
            "That tariff plan is not available to this subscription's owner."
        ];

        /// <summary>The sentence a plan identifier that names no plan gets.</summary>
        public static readonly string[] PlanNotFoundRule =
        [
            "There is no tariff plan with that identifier."
        ];

        /// <summary>The sentence an owner that names no account gets.</summary>
        public static readonly string[] OwnerNotFoundRule =
        [
            "There is no account with that identifier."
        ];

        /// <summary>The sentence an issuer that names no account gets.</summary>
        public static readonly string[] CreatorNotFoundRule =
        [
            "There is no account with that identifier."
        ];

        /// <summary>
        /// The sentence an issuer whose role is neither administrator nor reseller gets.
        /// </summary>
        public static readonly string[] CreatorRoleRule =
        [
            "A subscription may only be issued by an administrator or a reseller."
        ];

        /// <summary>The sentence a prolongation of a plan-less subscription gets.</summary>
        /// <remarks>
        /// Keyed on the plan field rather than the expiry, because the plan is the thing that is
        /// missing: the duration to extend by comes from it, and an operator told "the expiry is
        /// wrong" would go looking at a date that is not the problem.
        /// </remarks>
        public static readonly string[] ProlongRequiresPlanRule =
        [
            "A subscription can only be prolonged while a tariff plan is attached: " +
                "the plan supplies the length to extend it by."
        ];

        /// <summary>
        /// Whether a tariff plan's parameters let it be attached to a subscription whose owner
        /// belongs to <paramref name="ownerGroupId"/>.
        /// </summary>
        /// <param name="plan">The plan, with the groups it may be sold to.</param>
        /// <param name="ownerGroupId">Group the owner belongs to.</param>
        public static bool IsAvailableToOwner(SubscriptionPlanContext plan, Guid ownerGroupId)
            => plan.UserGroupIds.Contains(ownerGroupId);

        /// <summary>
        /// The formats a plan delivers, as the flag set a subscription row carries.
        /// </summary>
        /// <param name="plan">The plan.</param>
        /// <remarks>
        /// The one place the plan's two booleans become the subscription's one flag set, so a
        /// create, a change and the bulk plan change cannot disagree about it.
        /// </remarks>
        public static SubscriptionOutputFormat OutputFormatsOf(SubscriptionPlanContext plan)
        {
            var formats = SubscriptionOutputFormat.None;

            if (plan.OutputMpegTs)
            {
                formats |= SubscriptionOutputFormat.MpegTs;
            }

            if (plan.OutputHls)
            {
                formats |= SubscriptionOutputFormat.Hls;
            }

            return formats;
        }

        /// <summary>
        /// The subscription's expiry while a plan is attached: its creation date plus the plan's
        /// duration, calendar-aware for months and years.
        /// </summary>
        /// <param name="createdAt">Moment the subscription was issued.</param>
        /// <param name="plan">The plan whose length is applied.</param>
        /// <remarks>
        /// <b>The creation date, not "now".</b> The owner's explicit choice: a subscription's
        /// lifetime is a fact about the moment it was issued, so re-saving it never extends it. An
        /// old subscription given a plan therefore gets a past date and reads as expired, which is
        /// the honest answer rather than a silent renewal.
        /// </remarks>
        public static DateTimeOffset ExpiryOf(DateTimeOffset createdAt, SubscriptionPlanContext plan)
            => plan.DurationUnit switch
            {
                TariffPlanDurationUnit.Hours => createdAt.AddHours(plan.DurationValue),
                TariffPlanDurationUnit.Days => createdAt.AddDays(plan.DurationValue),
                TariffPlanDurationUnit.Months => createdAt.AddMonths(plan.DurationValue),
                TariffPlanDurationUnit.Years => createdAt.AddYears(plan.DurationValue),

                // Unreachable: the plan service refuses a unit that names no member, so a stored
                // row cannot carry one. Thrown rather than treated as days, because a silent
                // fallback would compute a lifetime nobody asked for.
                _ => throw new ArgumentOutOfRangeException(
                    nameof(plan),
                    plan.DurationUnit,
                    "Unknown tariff plan duration unit.")
            };

        /// <summary>
        /// Whether a set of services clears to nothing once the plan's support is applied.
        /// </summary>
        /// <param name="playlistId">Playlist the row would carry, if any.</param>
        /// <param name="magMac">MAG MAC the row would carry, if any.</param>
        /// <param name="enigma2Mac">Enigma2 MAC the row would carry, if any.</param>
        /// <param name="supportsPlaylist">Whether the effective plan allows a playlist.</param>
        /// <param name="supportsMag">Whether the effective plan allows a MAG device.</param>
        /// <param name="supportsEnigma2">Whether the effective plan allows an Enigma2 device.</param>
        /// <remarks>
        /// The rule the specification states as "services a selected plan does not support shall
        /// be cleared on save; if clearing would leave no service, the save is refused". The
        /// <i>clearing</i> happens in the service; this answers only the question the refusal is
        /// made of, so the clear and the refusal can never disagree about which is which.
        /// </remarks>
        public static bool LeavesNoService(
            string? playlistId,
            string? magMac,
            string? enigma2Mac,
            bool supportsPlaylist,
            bool supportsMag,
            bool supportsEnigma2)
            => !(supportsPlaylist && playlistId is not null)
                && !(supportsMag && magMac is not null)
                && !(supportsEnigma2 && enigma2Mac is not null);

        /// <summary>
        /// Whether a set of output formats names at least one this system delivers.
        /// </summary>
        /// <param name="formats">Value from a body.</param>
        public static bool IsUsableOutputFormats(SubscriptionOutputFormat formats)
            => formats != SubscriptionOutputFormat.None
                && (formats & ~(SubscriptionOutputFormat.MpegTs | SubscriptionOutputFormat.Hls))
                    == SubscriptionOutputFormat.None;

        /// <summary>Whether a connection ceiling is inside its range.</summary>
        /// <param name="connections">Ceiling to judge.</param>
        public static bool IsUsableConnections(int connections)
            => connections is >= SubscriptionLimits.ConnectionsMin
                and <= SubscriptionLimits.ConnectionsMax;

        /// <summary>
        /// Canonical forms of an IP whitelist plus everything wrong with the list.
        /// </summary>
        /// <param name="entries">Values as the caller spelled them.</param>
        /// <returns>
        /// The entries to store - always produced, so a caller that only wants to render them never
        /// has to check the errors - and the errors, empty when the list is acceptable.
        /// </returns>
        public static (IReadOnlyList<string> Value, Dictionary<string, string[]> Errors) NormalizeIpWhitelist(
            IReadOnlyList<string>? entries)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var normalized = (entries ?? [])
                .Select(SubscriptionNormalizer.NormalizeIpEntry)
                .ToList();

            if (normalized.Any(entry => entry is null))
            {
                errors["IpWhitelist"] = IpWhitelistRule;
            }

            var canonical = normalized
                .Where(entry => entry is not null)
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (canonical.Count > SubscriptionLimits.WhitelistMaxEntries)
            {
                errors["IpWhitelist"] = IpWhitelistCountRule;
            }

            return (canonical, errors);
        }

        /// <summary>
        /// Canonical forms of a user-agent whitelist plus everything wrong with the list.
        /// </summary>
        /// <param name="entries">Values as the caller spelled them.</param>
        public static (IReadOnlyList<string> Value, Dictionary<string, string[]> Errors)
            NormalizeUserAgentWhitelist(IReadOnlyList<string>? entries)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var normalized = (entries ?? [])
                .Select(SubscriptionNormalizer.NormalizeUserAgent)
                .ToList();

            if (normalized.Any(entry =>
                entry is null || entry.Length > SubscriptionLimits.WhitelistEntryMaxLength))
            {
                errors["UserAgentWhitelist"] = UserAgentWhitelistRule;
            }

            var canonical = normalized
                .Where(entry => entry is not null)
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (canonical.Count > SubscriptionLimits.WhitelistMaxEntries)
            {
                errors["UserAgentWhitelist"] = UserAgentWhitelistCountRule;
            }

            return (canonical, errors);
        }

        /// <summary>
        /// The moment a prolongation moves the expiry to: the later of now and the current expiry,
        /// plus the plan's duration.
        /// </summary>
        /// <param name="now">The moment the prolongation runs.</param>
        /// <param name="currentExpiry">
        /// The stored expiry, or null. Null is read as <paramref name="now"/>, so a subscription
        /// that never lapsed but somehow carries no expiry gets a full period rather than none.
        /// </param>
        /// <param name="plan">The plan whose duration is added.</param>
        /// <remarks>
        /// <b>From the end, never from now.</b> A subscription with twenty days left gains the
        /// period on top of those twenty days rather than restarting from today - the subscriber
        /// paid for both, and a prolongation that reset the clock would quietly take the remainder
        /// away. The <c>max</c> is the same rule for the other side: an expired subscription has
        /// nothing left, so it extends from now, which is what "revives it" means.
        /// </remarks>
        public static DateTimeOffset ProlongedExpiry(
            DateTimeOffset now,
            DateTimeOffset? currentExpiry,
            SubscriptionPlanContext plan)
        {
            var from = currentExpiry is { } existing && existing > now ? existing : now;

            return plan.DurationUnit switch
            {
                TariffPlanDurationUnit.Hours => from.AddHours(plan.DurationValue),
                TariffPlanDurationUnit.Days => from.AddDays(plan.DurationValue),
                TariffPlanDurationUnit.Months => from.AddMonths(plan.DurationValue),
                TariffPlanDurationUnit.Years => from.AddYears(plan.DurationValue),

                // Unreachable: the plan service refuses a unit that names no member, so a stored
                // row cannot carry one.
                _ => throw new ArgumentOutOfRangeException(
                    nameof(plan),
                    plan.DurationUnit,
                    "Unknown tariff plan duration unit.")
            };
        }

        /// <summary>
        /// The values a plan derives for a subscription: everything the plan owns, which a save
        /// with that plan writes instead of the request's values.
        /// </summary>
        /// <param name="plan">The attached plan.</param>
        /// <param name="expireAt">
        /// The expiry this save stores. A create or a plan change computes it with
        /// <see cref="ExpiryOf"/>; a save that keeps its plan passes the stored one through, which
        /// is what lets a prolongation survive a later edit.
        /// </param>
        /// <remarks>
        /// One function rather than six lines repeated in the create and the change: the two must
        /// derive the same row from the same plan, and the third caller - the bulk plan change -
        /// must derive it the same way again. The expiry travels in rather than being computed here
        /// because the two callers disagree about where it comes from, and folding that decision
        /// into this method would hide the rule this change is about.
        /// </remarks>
        public static SubscriptionDerived Derive(SubscriptionPlanContext plan, DateTimeOffset expireAt)
            => new(
                plan.MaxConnections,
                plan.AllowRestream,
                OutputFormatsOf(plan),
                plan.StrictStbDeviceId,
                !plan.AllowMultipleCountries,
                plan.IsTrial,
                plan.SupportsM3u,
                plan.SupportsMag,
                plan.SupportsEnigma2,
                expireAt);
    }
}
