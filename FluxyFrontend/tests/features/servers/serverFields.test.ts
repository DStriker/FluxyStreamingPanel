import { describe, expect, it } from 'vitest'
import { kbpsToMbps, mbpsToKbps } from '../../../src/features/servers/serverFields'

/**
 * The throughput conversion between the wire's kbps and the form's Mbps.
 *
 * `0` is a real state on both sides - "unknown" on the API - so the boundary that matters
 * most is the one where a careless `||` or a divide would turn it into something else. The
 * fractional and large values pin the rounding, which is what decides whether a row
 * survives a round trip unchanged.
 */

describe('kbpsToMbps', () => {
  it('keeps the unknown sentinel at zero', () => {
    expect(kbpsToMbps(0)).toBe(0)
  })

  it('divides by a thousand', () => {
    expect(kbpsToMbps(1)).toBe(0.001)
    expect(kbpsToMbps(1000)).toBe(1)
    expect(kbpsToMbps(100_000)).toBe(100)
  })
})

describe('mbpsToKbps', () => {
  it('keeps the unknown sentinel at zero', () => {
    expect(mbpsToKbps(0)).toBe(0)
  })

  it('multiplies by a thousand', () => {
    expect(mbpsToKbps(1)).toBe(1000)
    expect(mbpsToKbps(100)).toBe(100_000)
  })

  it('rounds a fractional amount to the nearest whole kilobit', () => {
    expect(mbpsToKbps(0.5)).toBe(500)
    expect(mbpsToKbps(1.2345)).toBe(1235)
    expect(mbpsToKbps(1.2344)).toBe(1234)
  })

  it('takes the server ceiling without losing a digit', () => {
    expect(mbpsToKbps(1000)).toBe(1_000_000)
  })
})
