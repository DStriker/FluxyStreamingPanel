using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.Core.Subscriptions.Commands
{
    /// <summary>
    /// Everything an administrator supplies to create a tariff plan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two switches that have a sensible default are optional here and resolved by the
    /// service rather than by the column - this project declares no database defaults, so a
    /// value the application forgot would surface as an error rather than be invented silently.
    /// <see cref="AllowMultipleCountries"/> defaults to true, because a plan that forbids a
    /// subscriber from travelling is the exception; <see cref="IsTrial"/> defaults to false.
    /// </para>
    /// <para>
    /// The group and bouquet identifiers are the plan's two relations. An empty set is the
    /// statement "related to nothing", not an omission: a plan with no group may not be sold to
    /// anybody yet, and a plan with no bouquet delivers a subscription with no content. Both are
    /// legitimate while an operator is still building the catalogue.
    /// </para>
    /// </remarks>
    public sealed record NewTariffPlan
    {
        /// <summary>Display name. Trimmed, compared without regard to case.</summary>
        public required string Name { get; init; }

        /// <summary>Whether the plan is a free trial. False when the body names none.</summary>
        public bool IsTrial { get; init; }

        /// <summary>Cost in credits. Zero is a free plan.</summary>
        public required int Cost { get; init; }

        /// <summary>How much of <see cref="DurationUnit"/> the plan lasts for.</summary>
        public required int DurationValue { get; init; }

        /// <summary>The unit the validity period is counted in.</summary>
        public required TariffPlanDurationUnit DurationUnit { get; init; }

        /// <summary>Whether the plan may be delivered as an M3U playlist.</summary>
        public bool SupportsM3u { get; init; }

        /// <summary>Whether the plan may be delivered to a MAG set-top box.</summary>
        public bool SupportsMag { get; init; }

        /// <summary>Whether the plan may be delivered to an Enigma2 receiver.</summary>
        public bool SupportsEnigma2 { get; init; }

        /// <summary>Whether the plan may be delivered as an MPEG-TS stream.</summary>
        public bool OutputMpegTs { get; init; }

        /// <summary>Whether the plan may be delivered as an HLS stream.</summary>
        public bool OutputHls { get; init; }

        /// <summary>Whether a MAG device's STB identifier is checked strictly.</summary>
        public bool StrictStbDeviceId { get; init; }

        /// <summary>Whether a subscriber may restream the subscription through their own servers.</summary>
        public bool AllowRestream { get; init; }

        /// <summary>How many devices may be connected at the same time.</summary>
        public required int MaxConnections { get; init; }

        /// <summary>Whether connections from different countries are allowed at the same time.</summary>
        public bool AllowMultipleCountries { get; init; } = true;

        /// <summary>
        /// Groups that may be sold the plan, or null to offer it to every base group.
        /// </summary>
        /// <remarks>
        /// The null is a decision rather than a missing value: a create that names no group is a
        /// create by an operator who has not thought about the audience yet, and the domain's
        /// answer is the three base groups. An empty list is the caller saying "nobody", which the
        /// service refuses - a plan that cannot be bought is not a plan.
        /// </remarks>
        public IReadOnlyList<Guid>? UserGroupIds { get; init; }

        /// <summary>Bouquets the plan delivers. At least one, and there is no default.</summary>
        public IReadOnlyList<Guid>? BouquetIds { get; init; }
    }
}
