/**
 * The three pure display helpers the admin dashboard draws with, plus the two ring
 * thresholds.
 *
 * They live here rather than inside the page because a static render lands before the first
 * answer arrives, so nothing that needs a value can be reached from `probe:render` - and
 * these are exactly the branches that go wrong at a boundary. The unit tests under
 * `tests/features/dashboard/` pin them without a DOM.
 */

/**
 * A period in seconds as the card's compact uptime line: `12d 4h 31m`, `4h 31m`, `31m`.
 *
 * **Leading zero units are dropped, lower ones are kept.** A server up for twelve days
 * should not read `12d 4h 0m` when the minute is the interesting tail, and one up for an
 * hour should not read `0d 1h 0m`. Below a minute - and for anything the server could not
 * sensibly have meant (a negative value, `Infinity`, `NaN`) - the answer is `<1m`; the line
 * always says something rather than printing `NaN`.
 */
export function formatUptime(seconds: number): string {
  if (!Number.isFinite(seconds) || seconds < 60) return '<1m'

  const totalMinutes = Math.floor(seconds / 60)
  const days = Math.floor(totalMinutes / 1440)
  const hours = Math.floor((totalMinutes % 1440) / 60)
  const minutes = totalMinutes % 60

  if (days > 0) return `${days}d ${hours}h ${minutes}m`
  if (hours > 0) return `${hours}h ${minutes}m`

  return `${minutes}m`
}

/**
 * A throughput as the card's one-decimal string: `'12.3'`, `'0.0'`.
 *
 * Always one decimal, because the network card's two statistics sit beside each other and a
 * pair reading `'12'` and `'3.4'` would look like different units. A value that is not a
 * finite number is drawn as `'0.0'` - the alternative is `NaN` on a screenshot.
 */
export function formatMbps(value: number): string {
  return (Number.isFinite(value) ? value : 0).toFixed(1)
}

/** A percentage clamped to the ring's own 0-100 range; anything unreadable is 0. */
export function clampPercent(value: number): number {
  if (!Number.isFinite(value)) return 0
  return Math.min(100, Math.max(0, value))
}

/** Below this a ring is the primary colour. */
export const RING_WARN_PERCENT = 70

/** At or above this a ring is the error colour and is drawn as an exception. */
export const RING_CRITICAL_PERCENT = 90

/**
 * Which of the three ring colours a load belongs in, after clamping.
 *
 * The thresholds are inclusive on the warn side and on the critical side: exactly 70 is a
 * warning and exactly 90 is critical, because the number is a measurement and a boundary
 * should not read better than the value it names.
 */
export function ringTier(percent: number): 'normal' | 'warn' | 'critical' {
  const clamped = clampPercent(percent)

  if (clamped >= RING_CRITICAL_PERCENT) return 'critical'
  if (clamped >= RING_WARN_PERCENT) return 'warn'

  return 'normal'
}
