/**
 * The unit a tariff plan's validity period is counted in, spelled the way the server parses
 * it.
 *
 * A union rather than `string` for the reason every other API enum here is one: a value typed
 * here is checked where it is written, while a plan typed as `string` would accept `weeks` and
 * the server would answer `validation_failed` naming a field the form never offered.
 *
 * **Only hours and days are offered to a trial.** A month is not thirty days in every month,
 * so a trial cannot be expressed in one; the form narrows the select to these two while the
 * trial switch is on, and the server refuses any other combination regardless.
 */
export type TariffPlanDurationUnit = 'hours' | 'days' | 'months' | 'years'

/** Every unit, in the order the select draws them - shortest first. */
export const TARIFF_PLAN_DURATION_UNITS: readonly TariffPlanDurationUnit[] = [
  'hours',
  'days',
  'months',
  'years',
]

/**
 * The units a trial may use, which is the set the form keeps offered while the trial switch is
 * on. The server's rule is the same pair; see `tariffPlanRules.ts`.
 */
export const TRIAL_DURATION_UNITS: readonly TariffPlanDurationUnit[] = ['hours', 'days']
