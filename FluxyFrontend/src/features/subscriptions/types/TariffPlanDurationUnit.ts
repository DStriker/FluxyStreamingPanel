/**
 * The unit a tariff plan's validity period is expressed in, spelled as the server names it.
 *
 * A local mirror rather than an import of the tariff plans feature's own: a feature imports
 * neither another feature nor a page, and this is the same kind of copy `SubscriptionStatus` and
 * `SubscriptionOutputFormat` already are. It travels on a subscription row only because the
 * prolongation dialog projects the new expiry with it - the subscriptions screens never show a
 * plan's length any other way, so borrowing the plan feature's type would make one feature depend
 * on another to read a field of its own response.
 *
 * The four members are the server's `TariffPlanDurationUnit`, and the conversion to a date is a
 * calendar one for `months` and `years`: "1 month" is not 30 days.
 */
export type TariffPlanDurationUnit = 'hours' | 'days' | 'months' | 'years'
