import dayjs from 'dayjs'
import timezone from 'dayjs/plugin/timezone'
import utc from 'dayjs/plugin/utc'
import type { Dayjs } from 'dayjs'

dayjs.extend(utc)
dayjs.extend(timezone)

/**
 * One inclusive date range as two query-string parameters, and the account-zone conversion
 * between those parameters and a picker.
 *
 * **The picker speaks calendar days; the wire speaks instants.** An operator picks "1 May –
 * 3 May" and expects everything that happened during those three days, in the timezone the
 * page prints timestamps in. The server compares a stored instant against the bound, so the
 * two ends are written as that zone's first and last millisecond of the chosen days - and
 * both ends are inclusive on the server, matching the whole-day control. A naive
 * `toISOString` of the picked `Dayjs` would send the *browser's* midnight, which is a
 * different day for a viewer whose account zone differs from the machine's, and would
 * silently drop or add rows at the edge.
 *
 * **The parameters are plain ISO 8601 instants**, the same spelling every other wire date
 * here uses, so a hand-edited link (or the API directly) is understandable without knowing
 * this module. Nothing is shifted silently: the instant that leaves the page is the instant
 * the account's zone named.
 *
 * The two parameter names arrive as arguments because two tables filter on a date range of
 * their own: the connection log on `startedFrom`/`startedTo`, the credit log on
 * `dateFrom`/`dateTo`. The arithmetic is one rule, and this is where it is written once.
 *
 * The conversion is extracted from the page so it can be unit-tested under pinned non-UTC
 * zones: `probe:render` lands before any interaction and can never see this path.
 */

/** The two bounds as an address carries them, or null for a side that is not set. */
export interface DateRange {
  from: string | null
  to: string | null
}

/** Reads both bounds out of the address; an absent or empty value is null. */
export const dateRangeParams = (
  params: URLSearchParams,
  fromKey: string,
  toKey: string,
): DateRange => ({
  from: params.get(fromKey) || null,
  to: params.get(toKey) || null,
})

/** No bounds at all, for a table read with a range that is not set. */
export const NO_DATE_RANGE: DateRange = { from: null, to: null }

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
  range: DateRange,
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
): DateRange => ({
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
