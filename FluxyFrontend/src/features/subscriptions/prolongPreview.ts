import dayjs from 'dayjs'
import type { Dayjs } from 'dayjs'
import type { Subscription, TariffPlanDurationUnit } from './types'

/**
 * The date a prolongation will move a subscription's expiry to.
 *
 * The confirmation dialog has to say what is about to happen, not merely ask whether the operator
 * meant to press the button. Saying it means computing the same answer the server is about to
 * store, so this module mirrors `SubscriptionRules.ProlongedExpiry` on the backend rather than
 * approximating it. The formula is `max(now, the stored expiry) + the plan's duration`, and the
 * duration is calendar-aware for months and years - the half a hand-written "milliseconds in a
 * month" gets wrong every February.
 *
 * **Why a projection rather than the server's answer.** A dialog that asked and then displayed the
 * result would have already written the change; the question is what the button is for. So the
 * preview is client-side, and the two are allowed to differ by the seconds between the preview and
 * the request - the answer is shown to the minute, so a drift of under a minute is invisible. The
 * duration travels on the list row (`tariffPlanDurationValue`/`tariffPlanDurationUnit`) for the
 * same reason: asking the plan endpoint per dialog open would be a request whose only purpose is a
 * number the row could have carried.
 *
 * Deliberately absent from this file: formatting. A moment is shown in the *account's* time zone
 * and in the page's language, which is `formatTimestamp`'s job (`utils/dateFormat`) and not
 * dayjs' global locale - a preview printed in the browser's zone would disagree with the expiry
 * cell two lines above it in the dialog.
 */

/** The units a plan's length may be expressed in, as the projection understands them. */
const UNITS: readonly TariffPlanDurationUnit[] = ['hours', 'days', 'months', 'years']

/**
 * Adds a plan's duration to a starting moment, calendar-aware.
 *
 * `days` and `hours` are exact arithmetic; `months` and `years` are calendar ones, and dayjs
 * clamps the day of the month the way .NET's `AddMonths` does - 31 January plus one month is the
 * last day of February, not the 3rd of March. That clamping is the reason this cannot be
 * `start + value * 86400000`.
 *
 * A unit outside the four is impossible in a stored row (the plan service refuses one), so the
 * fallthrough returns the start unchanged rather than inventing a length. The server throws in the
 * same place; a throw during render would take the page down over a dialog that can simply decline
 * to show a date, which is the worse failure of the two.
 */
export function addPlanDuration(
  from: Dayjs,
  value: number,
  unit: TariffPlanDurationUnit,
): Dayjs {
  switch (unit) {
    case 'hours':
      return from.add(value, 'hour')
    case 'days':
      return from.add(value, 'day')
    case 'months':
      return from.add(value, 'month')
    case 'years':
      return from.add(value, 'year')
    default:
      return from
  }
}

/**
 * The expiry a prolongation of this row will produce, or null when it cannot be projected.
 *
 * Null means "no plan attached, or the plan's length is missing" - also the state in which the
 * row's prolong button is disabled, so a caller that draws the dialog only for a projectable row
 * never has to render a sentence with no date in it.
 *
 * `now` is a parameter rather than a call to `Date.now()` inside: the unit tests pin both branches
 * (an expiry in the future is extended from itself, an expired one from now) without moving a
 * clock, and the dialog passes one value so a re-render cannot show a date that moved.
 */
export function projectedExpiry(
  subscription: Pick<
    Subscription,
    'expireAt' | 'tariffPlanDurationValue' | 'tariffPlanDurationUnit'
  >,
  now: Dayjs,
): Dayjs | null {
  const value = subscription.tariffPlanDurationValue
  const unit = subscription.tariffPlanDurationUnit

  if (value === null || unit === null || !UNITS.includes(unit)) return null

  // The stored expiry, when it is still ahead of now. An expired or absent one reads as "now", so
  // the prolongation revives the row with a full period rather than adding it to a date in the
  // past - the server's `max(now, expiry)`.
  const stored = subscription.expireAt === null ? null : dayjs(subscription.expireAt)
  const from = stored !== null && stored.isValid() && stored.isAfter(now) ? stored : now

  return addPlanDuration(from, value, unit)
}
