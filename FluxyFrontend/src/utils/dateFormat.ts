/**
 * Dates, by locale and zone, remembered.
 *
 * Building an `Intl.DateTimeFormat` is the expensive part - eight milliseconds for a hundred
 * rows on one machine, which is half of a frame once a table repaints on every step of a
 * column drag. Looking a date up in an existing one is two orders of magnitude cheaper, so the
 * expensive half happens once per locale and zone instead of once per cell per frame.
 *
 * `null` is a remembered *refusal* as much as a formatter is a remembered answer: a zone this
 * ICU build does not carry would otherwise be re-discovered by every row of every repaint.
 *
 * Here rather than inside the visit history because two tables print timestamps - the history
 * and the admin's user table - and each of them reads the *viewer's* time zone from
 * `getProfile()`, since neither page makes a promise about the zone of the accounts it lists.
 * Two copies of the cache would be two answers to one question whenever they disagreed.
 */
const dateFormatters = new Map<string, Intl.DateTimeFormat | null>()

/**
 * Day formatters, cached for the same reason the timestamp ones are: a toolbar filter tag is
 * rebuilt on every list answer, and the expensive half is the constructor rather than the
 * format.
 */
const dayFormatters = new Map<string, Intl.DateTimeFormat | null>()

/**
 * A formatter for this locale and zone, or `null` when the zone cannot be formatted in.
 *
 * The refusal is cached as well as the answer: a zone stored by an older build, or one this
 * ICU build does not carry, makes `Intl.DateTimeFormat` throw before it returns anything - and
 * an exception thrown while rendering a row would take the whole page down over one timestamp.
 */
export function dateFormatterFor(
  locale: string,
  timeZone: string | undefined,
): Intl.DateTimeFormat | null {
  const key = `${locale}|${timeZone ?? ''}`
  const cached = dateFormatters.get(key)
  if (cached !== undefined) return cached

  let formatter: Intl.DateTimeFormat | null = null

  try {
    formatter = new Intl.DateTimeFormat(locale, {
      dateStyle: 'medium',
      timeStyle: 'medium',
      ...(timeZone ? { timeZone } : {}),
    })
  } catch {
    formatter = null
  }

  dateFormatters.set(key, formatter)
  return formatter
}

/**
 * One timestamp as a table cell shows it: medium date and time, in the page's language and in
 * the zone of the account reading it.
 *
 * Three fallbacks, in the order a viewer would meet them. An absent or unparseable value is a
 * dash rather than "Invalid Date" - a row whose last visit is unknown is a fact, not a
 * failure. A zone the stored value cannot be formatted in falls back to the browser's own,
 * which is what `timeZone === null` means anyway: the profile page's "automatic" is the
 * absence of a zone. And a locale with no full-ICU build answers `null` from the constructor,
 * which is a dash as well - a page that cannot date a row still has to draw the row.
 */
export function formatTimestamp(
  value: string | null | undefined,
  locale: string,
  timeZone: string | null | undefined,
): string {
  if (!value) return '—'

  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '—'

  const formatter =
    dateFormatterFor(locale, timeZone ?? undefined) ?? dateFormatterFor(locale, undefined)

  return formatter ? formatter.format(date) : '—'
}

/**
 * One instant as the day it falls on, in the page's language and the account's zone - the label
 * a date-range filter tag carries, where a time of day would be noise.
 *
 * The same two fallbacks as `formatTimestamp`, in the same order: a zone this ICU build rejects
 * falls back to the browser's, and an unreadable value is answered as itself rather than as a
 * dash, so a hand-edited tag still names what the address holds.
 */
export function formatDay(
  value: string | null | undefined,
  locale: string,
  timeZone: string | null | undefined,
): string {
  if (!value) return ''

  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value

  const formatter =
    dayFormatterFor(locale, timeZone ?? undefined) ?? dayFormatterFor(locale, undefined)

  return formatter ? formatter.format(date) : value
}

/** A date-only formatter for this locale and zone, or `null` when the zone cannot be used. */
function dayFormatterFor(
  locale: string,
  timeZone: string | undefined,
): Intl.DateTimeFormat | null {
  const key = `${locale}|${timeZone ?? ''}`
  const cached = dayFormatters.get(key)
  if (cached !== undefined) return cached

  let formatter: Intl.DateTimeFormat | null = null

  try {
    formatter = new Intl.DateTimeFormat(locale, {
      dateStyle: 'medium',
      ...(timeZone ? { timeZone } : {}),
    })
  } catch {
    formatter = null
  }

  dayFormatters.set(key, formatter)
  return formatter
}
