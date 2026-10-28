import type { TariffPlanDurationUnit } from './types'

/**
 * The rules a tariff plan must satisfy, mirrored from the server so a form refuses before the
 * wire instead of explaining a refusal after one.
 *
 * The server is still the authority - every endpoint checks again - but a form that let an
 * operator fill in a trial of three months and only told them afterwards would be a form that
 * reported failure for choices it never questioned. The numbers live here as constants rather
 * than as literals at the call sites for the same reason `profile/loginGuardPolicy.ts` mirrors
 * its limits: a rule that changes on the server has one counterpart to change here.
 */

/** Longest display name a plan may carry (`TariffPlanLimits.NameMaxLength`). */
export const TARIFF_PLAN_NAME_MAX = 100

/** Cheapest plan, in credits. Zero is a free plan. */
export const TARIFF_PLAN_COST_MIN = 0

/** Shortest validity period a plan may name. */
export const TARIFF_PLAN_DURATION_MIN = 1

/** Fewest simultaneous connections a plan may allow. */
export const TARIFF_PLAN_CONNECTIONS_MIN = 1

/** Most simultaneous connections a plan may allow. */
export const TARIFF_PLAN_CONNECTIONS_MAX = 10000

/** Longest validity a trial may name, in days (`TariffPlanLimits.TrialMaxDays`). */
export const TRIAL_MAX_DAYS = 30

/** Longest validity a trial may name, in hours - the same thirty days stated in hours. */
export const TRIAL_MAX_HOURS = TRIAL_MAX_DAYS * 24

/**
 * Whether a validity period is inside the thirty-day ceiling a trial is held to.
 *
 * Only hours and days count as expressible: a month is not thirty days in every month, so a
 * trial stated in one would be a trial whose length depends on the calendar - which is exactly
 * what "at most thirty days" excludes. The server applies the same rule, and it reports the
 * refusal under `durationUnit`.
 */
export const isTrialDurationAllowed = (
  value: number,
  unit: TariffPlanDurationUnit,
): boolean => {
  if (unit === 'hours') return value <= TRIAL_MAX_HOURS
  if (unit === 'days') return value <= TRIAL_MAX_DAYS

  return false
}

/**
 * Whether at least one delivery method is chosen.
 *
 * A plan that delivers nothing is not a plan: it would be a price a subscriber could pay for
 * no service, and the server refuses it under `supportsM3u`.
 */
export const hasDeliveryMethod = (supports: {
  m3u: boolean
  mag: boolean
  enigma2: boolean
}): boolean => supports.m3u || supports.mag || supports.enigma2

/** Whether at least one output format is chosen. The server refuses under `outputMpegTs`. */
export const hasOutputFormat = (outputs: { mpegTs: boolean; hls: boolean }): boolean =>
  outputs.mpegTs || outputs.hls

/**
 * Whether a strict STB check is allowed beside the chosen delivery methods.
 *
 * The check is about a MAG device's identifier, so it only means something while MAG is
 * supported. The form disables the switch in the other state and clears it when MAG is turned
 * off; this function is what makes that clearing provable rather than remembered.
 */
export const isStrictStbAllowed = (supportsMag: boolean): boolean => supportsMag

/**
 * The units a trial may be expressed in, given the plan's current flag.
 *
 * Returned as a predicate over a unit rather than as a filtered array so a caller keeps one
 * list of units and asks a question about each, which is the shape a `Select`'s `options`
 * wants.
 */
export const isUnitOfferedToTrial = (unit: TariffPlanDurationUnit): boolean =>
  unit === 'hours' || unit === 'days'
