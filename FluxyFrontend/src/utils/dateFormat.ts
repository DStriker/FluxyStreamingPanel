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
 * The three formatters the chat bubble's time line reads: a bare time, a short date with a
 * time, and a day key used to decide which of the two a message gets. Cached like the two
 * above, and for the same reason - a thread may hold hundreds of bubbles and is repainted on
 * every keystroke in the composer.
 */
const chatTimeFormatters = new Map<string, Intl.DateTimeFormat | null>()
const chatMomentFormatters = new Map<string, Intl.DateTimeFormat | null>()
const chatDayKeyFormatters = new Map<string, Intl.DateTimeFormat | null>()

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

/**
 * A cached formatter from one of the chat caches, built from the options it is given.
 *
 * The three chat caches are separate rather than one keyed by an option hash because each is
 * asked for one shape only, and one map's key is then the locale and the zone - the same
 * two facts `dateFormatterFor` remembers. A refusal is cached too, for the reason it is
 * above: a zone this ICU build cannot format in must not be re-discovered per bubble.
 */
function chatFormatterFor(
  cache: Map<string, Intl.DateTimeFormat | null>,
  locale: string,
  timeZone: string | undefined,
  options: Intl.DateTimeFormatOptions,
): Intl.DateTimeFormat | null {
  const key = `${locale}|${timeZone ?? ''}`
  const cached = cache.get(key)
  if (cached !== undefined) return cached

  let formatter: Intl.DateTimeFormat | null = null

  try {
    formatter = new Intl.DateTimeFormat(locale, {
      ...options,
      ...(timeZone ? { timeZone } : {}),
    })
  } catch {
    formatter = null
  }

  cache.set(key, formatter)
  return formatter
}

/**
 * The time line under a chat bubble: `HH:mm` for a message written today, a short date and a
 * time for anything older.
 *
 * "Today" is judged **in the viewer's zone**, not the browser's: the profile page promises
 * that dates and times are shown in the chosen zone, and a bubble that called a message from
 * 00:30 "yesterday" because the browser is one zone west would be that promise failing on
 * the one screen a person reads most carefully. The day comparison is a formatted key rather
 * than arithmetic on milliseconds, because a day is not twenty-four hours in a zone with a
 * daylight-saving shift.
 *
 * `reference` exists for the tests and for a future caller that renders a transcript at a
 * fixed moment; every real call site leaves it out.
 */
export function formatChatTime(
  value: string | null | undefined,
  locale: string,
  timeZone: string | null | undefined,
  reference: Date = new Date(),
): string {
  if (!value) return '—'

  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '—'

  const zone = timeZone ?? undefined

  /**
   * A comparison key for "which day is this", in one zone.
   *
   * `en-CA` is deliberate and replaces the page's language: its numeric date order is
   * `YYYY-MM-DD`, so two equal days produce equal keys whatever the reader's locale is. The
   * locale under which the *reader* sees the date is a different question and is answered by
   * the two formatters below.
   */
  const dayKeyFormatter =
    chatFormatterFor(chatDayKeyFormatters, 'en-CA', zone, CHAT_DAY_KEY_OPTIONS) ??
    chatFormatterFor(chatDayKeyFormatters, 'en-CA', undefined, CHAT_DAY_KEY_OPTIONS)

  const sameDay = dayKeyFormatter
    ? dayKeyFormatter.format(date) === dayKeyFormatter.format(reference)
    : false

  if (sameDay) {
    const time =
      chatFormatterFor(chatTimeFormatters, locale, zone, CHAT_TIME_OPTIONS) ??
      chatFormatterFor(chatTimeFormatters, locale, undefined, CHAT_TIME_OPTIONS)

    return time ? time.format(date) : '—'
  }

  const moment =
    chatFormatterFor(chatMomentFormatters, locale, zone, CHAT_MOMENT_OPTIONS) ??
    chatFormatterFor(chatMomentFormatters, locale, undefined, CHAT_MOMENT_OPTIONS)

  return moment ? moment.format(date) : '—'
}

/** The bare clock reading of a message written today - 24-hour, whatever the locale. */
const CHAT_TIME_OPTIONS: Intl.DateTimeFormatOptions = {
  hour: '2-digit',
  minute: '2-digit',
  // `HH:mm` is the spec's own spelling, and it is also the one clock reading that does not
  // change shape between a locale that writes 14:30 and one that writes 2:30 PM: a thread
  // whose times switched family halfway down would be harder to scan than a slightly foreign
  // convention. The earlier-day branch below carries the same `hourCycle` for that reason.
  hourCycle: 'h23',
}

/**
 * A short date and a short time, for a message from an earlier day.
 *
 * Written as explicit fields rather than `dateStyle`/`timeStyle` so it can carry `hourCycle`:
 * `dateStyle` cannot be combined with an hour field, and the point is that both branches of
 * `formatChatTime` print the same 24-hour clock. A thread whose "today" messages read `14:30`
 * and whose older ones read `2:30 PM` would be one screen speaking two conventions.
 */
const CHAT_MOMENT_OPTIONS: Intl.DateTimeFormatOptions = {
  year: '2-digit',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
}

/** The parts a day comparison key is built from. */
const CHAT_DAY_KEY_OPTIONS: Intl.DateTimeFormatOptions = {
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
}
