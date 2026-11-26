import { describe, expect, it } from 'vitest'
import {
  clampPercent,
  formatMbps,
  formatUptime,
  RING_CRITICAL_PERCENT,
  RING_WARN_PERCENT,
  ringTier,
} from '../../../src/features/dashboard/format'

/**
 * The three display helpers and the ring thresholds the admin dashboard draws with.
 *
 * A `probe:render` lands before the first answer arrives, so every number here is
 * unreachable from a render; these are the boundaries where a helper goes quietly wrong -
 * the unit a value drops, the decimal a pair of statistics must share, and the exact
 * breakpoints where a ring changes colour.
 */

describe('formatUptime', () => {
  it('answers <1m below a minute, at zero and for a value that is negative', () => {
    expect(formatUptime(0)).toBe('<1m')
    expect(formatUptime(59)).toBe('<1m')
    expect(formatUptime(-5)).toBe('<1m')
  })

  it('prints minutes alone once there is at least one', () => {
    expect(formatUptime(60)).toBe('1m')
    expect(formatUptime(61)).toBe('1m')
  })

  it('drops the leading zero units and keeps the ones that follow', () => {
    // 1 d 1 h 1 min = 90061 s. A day is shown with its hours and minutes; an hour is shown
    // without a 0d, and a minute without a 0d 0h.
    expect(formatUptime(90061)).toBe('1d 1h 1m')
    expect(formatUptime(3720)).toBe('1h 2m')
    // Exactly one day: the hours and minutes are still drawn, at zero.
    expect(formatUptime(86400)).toBe('1d 0h 0m')
  })

  it('prints the owner-read shape for a long uptime', () => {
    // 5 d 1 h 1 min = 435660 s.
    expect(formatUptime(435660)).toBe('5d 1h 1m')
  })

  it('never prints NaN or throws for a value that is not a finite number', () => {
    expect(formatUptime(Infinity)).toBe('<1m')
    expect(formatUptime(Number.NaN)).toBe('<1m')
  })
})

describe('formatMbps', () => {
  it('always prints exactly one decimal', () => {
    expect(formatMbps(12.34)).toBe('12.3')
    expect(formatMbps(0)).toBe('0.0')
    expect(formatMbps(7)).toBe('7.0')
  })

  it('answers 0.0 for a value that is not a finite number, and never prints NaN', () => {
    expect(formatMbps(Number.NaN)).toBe('0.0')
    expect(formatMbps(Infinity)).toBe('0.0')
  })

  it('never throws on a negative reading and still prints a number', () => {
    expect(() => formatMbps(-1)).not.toThrow()
    expect(formatMbps(-1)).toBe('-1.0')
    expect(formatMbps(-1)).not.toContain('NaN')
  })
})

describe('clampPercent', () => {
  it('keeps a value inside 0-100 and clamps the two ends', () => {
    expect(clampPercent(-5)).toBe(0)
    expect(clampPercent(0)).toBe(0)
    expect(clampPercent(42.5)).toBe(42.5)
    expect(clampPercent(100)).toBe(100)
    expect(clampPercent(150)).toBe(100)
  })

  it('answers 0 for a value it cannot read', () => {
    expect(clampPercent(Number.NaN)).toBe(0)
    expect(clampPercent(Infinity)).toBe(0)
  })
})

describe('ringTier', () => {
  it('switches at the two documented thresholds, inclusive on both', () => {
    expect(ringTier(69.9)).toBe('normal')
    expect(ringTier(70)).toBe('warn')
    expect(ringTier(89.9)).toBe('warn')
    expect(ringTier(90)).toBe('critical')
  })

  it('clamps first, so an out-of-range reading lands in a tier rather than out of one', () => {
    expect(ringTier(150)).toBe('critical')
    expect(ringTier(-5)).toBe('normal')
  })

  it('pins the two constants the page and the helper share', () => {
    expect(RING_WARN_PERCENT).toBe(70)
    expect(RING_CRITICAL_PERCENT).toBe(90)
  })
})
