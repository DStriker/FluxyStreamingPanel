/**
 * How long a finished session lasted, as the activity table's end cell shows it.
 *
 * The units arrive from the page rather than from a table in here, because this application
 * has a language switcher and the page is where the translated words live. The shape of the
 * answer is language-neutral: the numbers carry a leading zero only where a smaller unit
 * follows them, so `1 h 04 min 12 s` reads as a clock rather than as three unrelated numbers.
 *
 * The helper is pure - two timestamps in, one string out - and it is the reason there is no
 * duration column on the server: the two timestamps are the facts, and this is the reader's
 * arithmetic written once instead of once per cell.
 *
 * A live session (`endedAt` null) and an unreadable pair both answer a dash. Neither is an
 * error worth a cell of its own: the row still reads, and the fact that it has no end is what
 * the dash says.
 */
export interface DurationUnits {
  /** Short word for an hour, in the page's language: `h`, `ч`. */
  hour: string
  /** Short word for a minute, in the page's language: `min`, `мин`. */
  minute: string
  /** Short word for a second, in the page's language: `s`, `с`. */
  second: string
}

/** The dash both a live session and an unreadable pair answer with. */
const DASH = '—'

/** Two digits, for a unit a larger one precedes: `04` minutes, `12` seconds. */
const pad = (value: number): string => String(value).padStart(2, '0')

/**
 * The duration between two timestamps, in short units.
 *
 * @param startedAt ISO timestamp the session opened at.
 * @param endedAt ISO timestamp it finished at, or null while it is still live.
 * @param units The page's own short unit words, already translated.
 * @returns `1 h 04 min 12 s`, `4 min 07 s`, `9 s`, or `—` when there is no end.
 */
export function durationText(
  startedAt: string | null | undefined,
  endedAt: string | null | undefined,
  units: DurationUnits,
): string {
  if (!startedAt || !endedAt) return DASH

  const start = new Date(startedAt).getTime()
  const end = new Date(endedAt).getTime()

  if (Number.isNaN(start) || Number.isNaN(end)) return DASH

  // Rounded rather than truncated, and floored at zero: two rows written in the same moment
  // must not read as a negative duration, and half a second is a second on a screen that
  // prints whole ones.
  const total = Math.max(0, Math.round((end - start) / 1000))

  const hours = Math.floor(total / 3600)
  const minutes = Math.floor((total % 3600) / 60)
  const seconds = total % 60

  if (hours > 0) {
    return `${hours} ${units.hour} ${pad(minutes)} ${units.minute} ${pad(seconds)} ${units.second}`
  }

  if (minutes > 0) {
    return `${minutes} ${units.minute} ${pad(seconds)} ${units.second}`
  }

  return `${seconds} ${units.second}`
}
