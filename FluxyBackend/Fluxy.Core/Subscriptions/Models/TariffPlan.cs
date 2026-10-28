using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.Core.Subscriptions.Models
{
    /// <summary>
    /// Immutable business representation of one tariff plan - the package a subscription is sold
    /// under.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A plan carries what the plan itself decides: its name, its price, its length, what it
    /// delivers and how many devices may watch at once. Which user groups may buy it and which
    /// bouquets it delivers are relations rather than fields, so they live in junction tables
    /// and are read through the detail model.
    /// </para>
    /// <para>
    /// The name is unique for the installation <i>without regard to case</i>: "Пробный" and
    /// "пробный" are one plan, because an operator scanning the list cannot tell them apart and
    /// a subscriber shown two spellings is being shown one product twice. A unique index over
    /// <c>lower(name)</c> enforces it in the database as well as in the service, so a race
    /// between two operators cannot slip a second spelling in.
    /// </para>
    /// </remarks>
    public sealed record TariffPlan
    {
        /// <summary>Identifier of the plan.</summary>
        public required Guid Id { get; init; }

        /// <summary>Display name, unique for the installation when compared without regard to case.</summary>
        public required string Name { get; init; }

        /// <summary>
        /// Whether this is the free trial plan. A trial is capped at
        /// <see cref="Utils.TariffPlanLimits.TrialMaxDays"/> days and may only state its length
        /// in hours or days.
        /// </summary>
        public required bool IsTrial { get; init; }

        /// <summary>Cost of the plan in installation credits. Zero is a free plan.</summary>
        public required int Cost { get; init; }

        /// <summary>How much of <see cref="DurationUnit"/> the plan lasts for.</summary>
        public required int DurationValue { get; init; }

        /// <summary>The unit <see cref="DurationValue"/> is counted in.</summary>
        public required TariffPlanDurationUnit DurationUnit { get; init; }

        /// <summary>Whether the plan may be delivered as an M3U playlist.</summary>
        public required bool SupportsM3u { get; init; }

        /// <summary>Whether the plan may be delivered to a MAG set-top box.</summary>
        public required bool SupportsMag { get; init; }

        /// <summary>Whether the plan may be delivered to an Enigma2 receiver.</summary>
        public required bool SupportsEnigma2 { get; init; }

        /// <summary>Whether the plan may be delivered as an MPEG-TS stream.</summary>
        public required bool OutputMpegTs { get; init; }

        /// <summary>Whether the plan may be delivered as an HLS stream.</summary>
        public required bool OutputHls { get; init; }

        /// <summary>
        /// Whether a MAG device's STB identifier is checked strictly. Only meaningful when
        /// <see cref="SupportsMag"/> is true, which the service enforces.
        /// </summary>
        public required bool StrictStbDeviceId { get; init; }

        /// <summary>Whether a subscriber may restream the subscription through their own servers.</summary>
        public required bool AllowRestream { get; init; }

        /// <summary>How many devices may be connected at the same time.</summary>
        public required int MaxConnections { get; init; }

        /// <summary>Whether connections from different countries are allowed at the same time.</summary>
        public required bool AllowMultipleCountries { get; init; }
    }
}
