import { describe, expect, it } from 'vitest'
import { networkLabel } from '../../src/utils/networkLabel'

/**
 * The second line of the shared network cell, pinned where a probe cannot reach it.
 *
 * A network cell's provider line is drawn by a column `render`, which needs a row - and every
 * render in `probe:render` lands before the first answer arrives. The two tables that draw the
 * cell also store the provider differently, and this function is the one place that difference
 * is decided; a `??` that fell through the wrong way would draw a dash in one of them, or a
 * visit's organisation on a connection log row that never had one.
 */
describe('networkLabel', () => {
  it("prefers the connection log's stored provider, including when it is null", () => {
    // `isp` present and null means "the session recorded no provider" - a dash, not a fall
    // through to an organisation this row does not have. That is why presence is tested with
    // `!== undefined` rather than for truthiness.
    expect(networkLabel({ ip: '203.0.113.9', countryCode: 'DE', isp: 'Example ISP' })).toBe(
      'Example ISP',
    )
    expect(networkLabel({ ip: '203.0.113.9', countryCode: 'DE', isp: null })).toBeNull()
  })

  it("builds a visit's provider from the organisation and the AS number", () => {
    expect(
      networkLabel({
        ip: '203.0.113.9',
        countryCode: 'DE',
        organization: 'Example ISP',
        autonomousSystemNumber: 15169,
      }),
    ).toBe('Example ISP · AS15169')
  })

  it('states whichever half of a visit network the server knows', () => {
    expect(
      networkLabel({
        ip: '203.0.113.9',
        countryCode: 'DE',
        organization: 'Example ISP',
        autonomousSystemNumber: null,
      }),
    ).toBe('Example ISP')

    expect(
      networkLabel({
        ip: '203.0.113.9',
        countryCode: 'DE',
        organization: null,
        autonomousSystemNumber: 15169,
      }),
    ).toBe('AS15169')
  })

  it('answers null when a visit has neither half, so the cell draws its dash', () => {
    // A loopback address and a missing `.mmdb` both arrive here: unknown, not broken.
    expect(
      networkLabel({
        ip: '::1',
        countryCode: null,
        organization: null,
        autonomousSystemNumber: null,
      }),
    ).toBeNull()
  })
})
