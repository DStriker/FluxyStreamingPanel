namespace Fluxy.Core.Subscriptions.Enums
{
    /// <summary>
    /// The unit a tariff plan's validity period is expressed in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A plan stores a number and one of these four, rather than a number of days: "1 месяц" and
    /// "30 дней" are the same length only until February arrives, and an operator selling a
    /// month means a calendar month. The conversion to an expiry date belongs to the subscription
    /// domain that will consume the plan, not to the plan row itself, which is why nothing here
    /// computes one.
    /// </para>
    /// <para>
    /// The numeric values are stored as <c>smallint</c> and are part of the schema, so they are
    /// assigned explicitly and must not be renumbered. <see cref="Hours"/> and <see cref="Days"/>
    /// are the only two a trial may use - a trial is limited to thirty days, and a month or a
    /// year cannot be held under that ceiling without a conversion nobody asked for.
    /// </para>
    /// </remarks>
    public enum TariffPlanDurationUnit
    {
        /// <summary>Hours. The shortest period a plan may name, and the only one a trial may use below a day.</summary>
        Hours = 1,

        /// <summary>Days. A trial may name at most thirty of them.</summary>
        Days = 2,

        /// <summary>Calendar months. Refused for a trial.</summary>
        Months = 3,

        /// <summary>Calendar years. Refused for a trial.</summary>
        Years = 4
    }
}
