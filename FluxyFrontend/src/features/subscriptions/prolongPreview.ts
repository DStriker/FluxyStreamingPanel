import dayjs from 'dayjs'
import utc from 'dayjs/plugin/utc'
import type { Dayjs } from 'dayjs'
import type { Subscription, TariffPlanDurationUnit } from './types'

dayjs.extend(utc)

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
 * **Everything here runs in UTC, and that is not a detail.** The server adds to a
 * `DateTimeOffset` whose values are the stored UTC instant, so its "one month" is a month on the
 * UTC calendar. dayjs in its default (local) mode means a month on the *viewer's* calendar, which
 * is a different answer twice a year and at every month end:
 *
 * - `2027-01-30T23:00:00Z` viewed from UTC+3 is 31 January locally, so "+1 month" clamps to the
 *   last day of February *there* and lands on `2027-02-27T23:00:00Z`; the server's UTC calendar
 *   sees 30 January and answers `2027-02-28T23:00:00Z`. The dialog would promise a date the
 *   server does not store.
 * - Any add that crosses the viewer's own DST transition shifts by an hour, because a local
 *   "add 10 days" is 10×24h only on days with no clock change.
 *
 * So the stored expiry and `now` are both read as UTC (`dayjs.utc`), and `addPlanDuration` adds
 * to that UTC-mode value: dayjs' `$set` then calls `setUTCMonth`/`setUTCFullYear` and clamps with
 * `daysInMonth()` over UTC fields, which is exactly `DateTimeOffset.AddMonths`/`AddYears`.
 *
 * **Why a projection rather than the server's answer.** A dialog that asked and then displayed the
 * result would have already written the change; the question is what the button is for. So the
 * preview is client-side, and how close it lands depends on which branch the row is in:
 *
 * - An **active** row previews *exactly*. The floor is the stored expiry - a value both sides
 *   already hold - so the only variable is the plan's duration, and both add it to the same
 *   instant.
 * - An **expired** row previews from the moment this dialog computed `now`. The server's floor is
 *   *its* request-time now, a few moments later, so the stored value lands later by however long
 *   the operator leaves the dialog open - unbounded, and visible, because the formatter prints
 *   seconds.
 *
 * The duration travels on the list row (`tariffPlanDurationValue`/`tariffPlanDurationUnit`) rather
 * than being fetched per dialog open: asking the plan endpoint each time would be a request whose
 * only purpose is a number the row could have carried.
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
 * **`from` must be a UTC-mode dayjs** (`dayjs.utc`), or the clamping and the DST behaviour below
 * happen on the viewer's calendar instead of the server's. `projectedExpiry` is the only caller
 * and it guarantees that; this is a plain function so the unit tests can drive each unit directly.
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
 *
 * The returned value is UTC-mode. Formatting it is the caller's business and must go through
 * `formatTimestamp`, which applies the account's zone; `.format()` on this value would print UTC
 * fields.
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

  // `new Date(...)` first, so a stored value carrying a non-zero offset is read as the instant it
  // names rather than re-parsed as bare fields; `dayjs.utc` then puts that instant on the UTC
  // calendar the server adds on.
  const stored =
    subscription.expireAt === null ? null : dayjs.utc(new Date(subscription.expireAt))

  const floor = now.utc()

  // The stored expiry, when it is still ahead of now. An expired or absent one reads as "now", so
  // the prolongation revives the row with a full period rather than adding it to a date in the
  // past - the server's `max(now, expiry)`.
  const from = stored !== null && stored.isValid() && stored.isAfter(floor) ? stored : floor

  return addPlanDuration(from, value, unit)
}
