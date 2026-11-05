import dayjs from 'dayjs'
import timezone from 'dayjs/plugin/timezone'
import utc from 'dayjs/plugin/utc'
import type { Dayjs } from 'dayjs'

dayjs.extend(utc)
dayjs.extend(timezone)

/**
 * The start-time range: the two URL parameters, and the account-zone conversion between them
 * and the picker.
 *
 * **The picker speaks calendar days; the wire speaks instants.** An operator picks "1 May –
 * 3 May" and expects everything that started during those three days, in the timezone the page
 * prints timestamps in. The server compares `started_at` against an instant, so the two bounds
 * are written as that zone's first and last millisecond of the chosen days - and both ends are
 * inclusive on the server, matching the whole-day control. A naive `toISOString` of the picked
 * `Dayjs` would send the *browser's* midnight, which is a different day for a viewer whose
 * account zone differs from the machine's, and would silently drop or add rows at the edge.
 *
 * **The parameters are plain ISO 8601 instants**, the same spelling every other wire date here
 * uses, so a hand-edited link (or the API directly) is understandable without knowing this
 * module. Nothing is shifted silently: the instant that leaves the page is the instant the
 * account's zone named.
 *
 * The conversion is extracted from the page so it can be unit-tested under pinned non-UTC
 * zones: `probe:render` lands before any interaction and can never see this path.
 */

/** The query-string name of the lower bound, inclusive. */
export const STARTED_FROM_KEY = 'startedFrom'

/** The query-string name of the upper bound, inclusive. */
export const STARTED_TO_KEY = 'startedTo'

/** The two bounds as an address carries them, or null for a side that is not set. */
export interface StartRange {
  from: string | null
  to: string | null
}

/** Reads both bounds out of the address; an absent or empty value is null. */
export const startRangeParams = (params: URLSearchParams): StartRange => ({
  from: params.get(STARTED_FROM_KEY) || null,
  to: params.get(STARTED_TO_KEY) || null,
})

/** No bounds at all, as the mode that has no range control reads the address. */
export const NO_START_RANGE: StartRange = { from: null, to: null }

/**
 * The range **this mode** may apply.
 *
 * Only the activity view draws the range control, so only it may send the two bounds. The live
 * view pins `state=live` - "every session still open, whatever time it started" - and a bound
 * that arrived in a hand-edited or saved address would silently narrow that promise. **The
 * parameters are ignored rather than stripped**: the address is the visitor's, a stale link must
 * not be rewritten under them, and ignoring the two keys is what makes the live query the same
 * query whatever they hold.
 *
 * This is a function rather than a branch at the call site so the rule can be unit-tested: no
 * render can carry a live `?startedFrom=` without also carrying the query it feeds, and the
 * probe only sees the markup the gate produces.
 */
export const startRangeForMode = (
  params: URLSearchParams,
  hasControl: boolean,
): StartRange => (hasControl ? startRangeParams(params) : NO_START_RANGE)

/**
 * A wire instant as the picker's calendar day in the account's zone, or null when unreadable.
 *
 * The answer is a **calendar day at local midnight**, not the instant moved into another zone:
 * the picker is a calendar, so it must display the day the account's zone says, and its panel
 * must open on that day rather than on a shifted one. The instant itself is only needed when a
 * selection is converted back (`rangeFromPicker`), and that step reads this value's
 * `YYYY-MM-DD` fields and rebuilds the boundary in the account's zone.
 *
 * An unreadable value answers null rather than "Invalid Date": a hand-edited address must draw
 * an empty picker, not crash the toolbar. The server is the one that refuses a broken bound.
 */
export const pickerDay = (value: string | null, timeZone: string | null): Dayjs | null => {
  if (!value) return null

  const parsed = dayjs(value)
  if (!parsed.isValid()) return null

  // `format` in the account's zone answers the calendar day; parsing that day locally is what
  // makes the picker show it, whatever zone the browser itself is in.
  return dayjs(displayDay(parsed, timeZone))
}

/**
 * The two picker values for an address's range, or null when neither side is set.
 *
 * A single-sided range is returned with the other side empty: the page renders what the address
 * says, and the URL remains the source of truth.
 */
export const rangePickerValue = (
  range: StartRange,
  timeZone: string | null,
): [Dayjs | null, Dayjs | null] | null => {
  const from = pickerDay(range.from, timeZone)
  const to = pickerDay(range.to, timeZone)

  return from === null && to === null ? null : [from, to]
}

/** The instant of the chosen day's first millisecond in the account's zone. */
export const startOfDayIso = (day: Dayjs, timeZone: string | null): string =>
  dayBoundaryIso(day, timeZone, 'start')

/** The instant of the chosen day's last millisecond in the account's zone. */
export const endOfDayIso = (day: Dayjs, timeZone: string | null): string =>
  dayBoundaryIso(day, timeZone, 'end')

/**
 * The two wire values a completed picker selection means.
 *
 * Null answers "no bound" on that side, which is the same thing the endpoint reads a blank
 * parameter as. The page writes both in one navigation, so the range is one request.
 */
export const rangeFromPicker = (
  value: [Dayjs | null, Dayjs | null],
  timeZone: string | null,
): StartRange => ({
  from: value[0] ? startOfDayIso(value[0], timeZone) : null,
  to: value[1] ? endOfDayIso(value[1], timeZone) : null,
})

/**
 * The calendar day an instant falls on in the account's zone, as `YYYY-MM-DD`.
 *
 * A stored zone this ICU build does not carry makes `tz` throw, exactly as it does for
 * `formatTimestamp`; the page's own fallback is the browser's zone, so the same answer here
 * keeps the picker and the printed rows in one zone.
 */
const displayDay = (value: Dayjs, timeZone: string | null): string => {
  if (!timeZone) return value.format('YYYY-MM-DD')

  try {
    return value.tz(timeZone).format('YYYY-MM-DD')
  } catch {
    return value.format('YYYY-MM-DD')
  }
}

/**
 * One calendar day's first or last millisecond, read in the account's zone.
 *
 * The day is taken from the picker value's own `YYYY-MM-DD` fields rather than from its
 * instant: the picker is a calendar, and the reader chose that calendar day. Interpreting the
 * day in the account's zone is what makes "1 May 00:00" mean midnight where the page's rows
 * are printed. dayjs' timezone plugin builds that wall clock and resolves the offset for the
 * date, so a DST transition is the zone's own answer rather than a fixed offset.
 */
const dayBoundaryIso = (
  day: Dayjs,
  timeZone: string | null,
  boundary: 'start' | 'end',
): string => {
  const calendarDay = day.format('YYYY-MM-DD')
  const wallClock = boundary === 'start' ? '00:00:00.000' : '23:59:59.999'

  if (timeZone) {
    try {
      return dayjs
        .tz(`${calendarDay} ${wallClock}`, 'YYYY-MM-DD HH:mm:ss.SSS', timeZone)
        .toISOString()
    } catch {
      // The zone is unusable: the browser's own is the fallback the printed timestamps use.
    }
  }

  // No stored zone means "follow the browser", which is `formatTimestamp`'s `timeZone === null`.
  return (boundary === 'start' ? day.startOf('day') : day.endOf('day')).toISOString()
}
