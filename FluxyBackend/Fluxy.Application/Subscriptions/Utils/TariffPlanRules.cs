using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Utils;

namespace Fluxy.Application.Subscriptions.Utils
{
    /// <summary>
    /// The rules every write to a tariff plan is judged by, and the sentences that describe
    /// breaking them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A create and a patch reach the same row through different requests, so the rules that are
    /// a fact about the row rather than about the request live here once. What is not here is what
    /// a request alone decides: a create resolves its defaults and a patch resolves "absent means
    /// keep", and both stay with the service.
    /// </para>
    /// <para>
    /// Every rule below is refused rather than corrected. A plan is a price list - silently
    /// lowering a price, clearing a flag or forcing a length would write a row nobody asked for,
    /// and the operator would only find out when a subscriber was charged something else.
    /// </para>
    /// <para>
    /// The keys of <see cref="Validate"/> are the C# names of the fields on
    /// <see cref="NewTariffPlan"/> and <see cref="TariffPlanPatch"/>, which spell the same fields
    /// the same way; the transport turns them into the camelCase names a form reads.
    /// </para>
    /// </remarks>
    public static class TariffPlanRules
    {
        /// <summary>The unique index making a plan's name unique across the installation.</summary>
        public const string NameIndexName = "IX_tariff_plans_name_lower";

        /// <summary>The sentence a name that is blank or too long gets.</summary>
        public static readonly string[] NameRule =
        [
            "The name must be between 1 and " + TariffPlanLimits.NameMaxLength + " characters."
        ];

        /// <summary>The sentence a negative price gets.</summary>
        public static readonly string[] CostRule =
        [
            "The cost must be zero or a positive number of credits."
        ];

        /// <summary>The sentence a validity period shorter than one unit gets.</summary>
        public static readonly string[] DurationValueRule =
        [
            "The validity period must be at least one unit."
        ];

        /// <summary>The sentence a unit that names no member gets.</summary>
        public static readonly string[] DurationUnitRule =
        [
            "The validity unit must be one of: hours, days, months, years."
        ];

        /// <summary>The sentence a trial longer than the ceiling gets.</summary>
        public static readonly string[] TrialDurationRule =
        [
            "A trial plan may be at most " + TariffPlanLimits.TrialMaxDays + " days long, " +
                "so it may only be expressed in hours (at most " +
                TariffPlanLimits.TrialMaxHours + ") or days (at most " +
                TariffPlanLimits.TrialMaxDays + ")."
        ];

        /// <summary>The sentence a plan offering no delivery method gets.</summary>
        public static readonly string[] SupportsRule =
        [
            "A plan must support at least one delivery method: an M3U playlist, a MAG device " +
                "or an Enigma2 device."
        ];

        /// <summary>The sentence a plan with no output format gets.</summary>
        public static readonly string[] OutputFormatsRule =
        [
            "A plan must deliver at least one output format: MPEG-TS or HLS."
        ];

        /// <summary>The sentence a strict STB check on a plan without MAG support gets.</summary>
        public static readonly string[] StrictStbDeviceIdRule =
        [
            "The strict STB device identifier check can only be enabled for a plan that " +
                "supports MAG devices."
        ];

        /// <summary>The sentence a connection ceiling outside its range gets.</summary>
        public static readonly string[] MaxConnectionsRule =
        [
            "The number of connections must be between " + TariffPlanLimits.ConnectionsMin +
                " and " + TariffPlanLimits.ConnectionsMax + "."
        ];

        /// <summary>The sentence a group identifier that names nothing gets.</summary>
        public static readonly string[] UserGroupIdsRule =
        [
            "Every identifier must be the uuid of an existing user group."
        ];

        /// <summary>The sentence a plan offered to no group gets.</summary>
        /// <remarks>
        /// A plan nobody may be sold is a price list entry that cannot be bought, which is the
        /// same kind of absence as a plan that delivers nothing - so it is refused rather than
        /// stored. A create never reaches this rule: an absent group list is resolved to the
        /// three base groups, which is what "by default every base group" means. It bites on a
        /// change that would clear the last group, and on a create whose list named something
        /// that no longer exists.
        /// </remarks>
        public static readonly string[] UserGroupsRequiredRule =
        [
            "A plan must be offered to at least one user group."
        ];

        /// <summary>The sentence a bouquet identifier that names nothing gets.</summary>
        public static readonly string[] BouquetIdsRule =
        [
            "Every identifier must be the uuid of an existing bouquet."
        ];

        /// <summary>The sentence a plan that delivers no content gets.</summary>
        /// <remarks>
        /// Unlike the group list there is no sensible default: which packages a plan delivers is
        /// a decision only the operator can take, and picking one for them would be inventing a
        /// product. A subscription on such a plan would be a subscription to nothing.
        /// </remarks>
        public static readonly string[] BouquetsRequiredRule =
        [
            "A plan must deliver at least one bouquet."
        ];

        /// <summary>Whether a name is non-blank and short enough to store.</summary>
        /// <param name="name">Name to judge.</param>
        public static bool IsUsableName(string name)
            => name.Trim().Length is >= 1 and <= TariffPlanLimits.NameMaxLength;

        /// <summary>Whether a price is one this domain has.</summary>
        /// <param name="cost">Cost in credits.</param>
        public static bool IsUsableCost(int cost) => cost >= TariffPlanLimits.CostMin;

        /// <summary>Whether a validity period is at least one unit long.</summary>
        /// <param name="value">Amount in the unit.</param>
        public static bool IsUsableDurationValue(int value)
            => value >= TariffPlanLimits.DurationValueMin;

        /// <summary>Whether a connection ceiling is inside its range.</summary>
        /// <param name="connections">Ceiling to judge.</param>
        public static bool IsUsableConnections(int connections)
            => connections is >= TariffPlanLimits.ConnectionsMin
                and <= TariffPlanLimits.ConnectionsMax;

        /// <summary>
        /// Whether a trial's length is inside the thirty-day ceiling.
        /// </summary>
        /// <remarks>
        /// Only hours and days are accepted, because those are the two units the ceiling can be
        /// stated in exactly. A month is not thirty days in every month and a year is not twelve
        /// months in every year, so a trial expressed in either would be a trial whose real
        /// length depends on the calendar - which is exactly what "at most thirty days" is meant
        /// to exclude.
        /// </remarks>
        /// <param name="value">Amount in the unit.</param>
        /// <param name="unit">Unit the amount is counted in.</param>
        public static bool IsTrialDurationAllowed(int value, TariffPlanDurationUnit unit)
            => unit switch
            {
                TariffPlanDurationUnit.Hours => value <= TariffPlanLimits.TrialMaxHours,
                TariffPlanDurationUnit.Days => value <= TariffPlanLimits.TrialMaxDays,
                _ => false
            };

        /// <summary>
        /// Checks everything about a plan that can be judged without asking the database.
        /// </summary>
        /// <param name="name">Display name.</param>
        /// <param name="isTrial">Whether the plan is a trial.</param>
        /// <param name="cost">Cost in credits.</param>
        /// <param name="durationValue">Amount of the validity period.</param>
        /// <param name="durationUnit">Unit the period is counted in.</param>
        /// <param name="supportsM3u">Whether an M3U playlist is delivered.</param>
        /// <param name="supportsMag">Whether a MAG device is supported.</param>
        /// <param name="supportsEnigma2">Whether an Enigma2 device is supported.</param>
        /// <param name="outputMpegTs">Whether MPEG-TS is delivered.</param>
        /// <param name="outputHls">Whether HLS is delivered.</param>
        /// <param name="strictStbDeviceId">Whether the MAG STB identifier is checked strictly.</param>
        /// <param name="maxConnections">Connection ceiling.</param>
        public static Dictionary<string, string[]> Validate(
            string name,
            bool isTrial,
            int cost,
            int durationValue,
            TariffPlanDurationUnit durationUnit,
            bool supportsM3u,
            bool supportsMag,
            bool supportsEnigma2,
            bool outputMpegTs,
            bool outputHls,
            bool strictStbDeviceId,
            int maxConnections)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (!IsUsableName(name))
            {
                errors[nameof(NewTariffPlan.Name)] = NameRule;
            }

            if (!IsUsableCost(cost))
            {
                errors[nameof(NewTariffPlan.Cost)] = CostRule;
            }

            if (!IsUsableDurationValue(durationValue))
            {
                errors[nameof(NewTariffPlan.DurationValue)] = DurationValueRule;
            }

            if (!Enum.IsDefined(durationUnit))
            {
                errors[nameof(NewTariffPlan.DurationUnit)] = DurationUnitRule;
            }
            else if (isTrial && !IsTrialDurationAllowed(durationValue, durationUnit))
            {
                // Reported under the unit rather than under the amount, because with a month or
                // a year the unit is the part that cannot be held inside the ceiling at all.
                errors[nameof(NewTariffPlan.DurationUnit)] = TrialDurationRule;
            }

            if (!supportsM3u && !supportsMag && !supportsEnigma2)
            {
                errors[nameof(NewTariffPlan.SupportsM3u)] = SupportsRule;
            }

            if (!outputMpegTs && !outputHls)
            {
                errors[nameof(NewTariffPlan.OutputMpegTs)] = OutputFormatsRule;
            }

            if (strictStbDeviceId && !supportsMag)
            {
                errors[nameof(NewTariffPlan.StrictStbDeviceId)] = StrictStbDeviceIdRule;
            }

            if (!IsUsableConnections(maxConnections))
            {
                errors[nameof(NewTariffPlan.MaxConnections)] = MaxConnectionsRule;
            }

            return errors;
        }
    }
}
