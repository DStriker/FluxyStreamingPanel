import { describe, expect, it } from 'vitest'
import {
  networkFilterParams,
  type UserLogNetworkFilterKey,
} from '../../../src/features/user-logs/networkFilters'
import { withFilter } from '../../../src/utils/tableParams'

/**
 * The user log's three network filters, pinned as the endpoint's own spellings.
 *
 * The response row answers `countryCode` and the request accepts `country`, so the one mistake
 * this file exists for is a reader that reaches for the response field: the server ignores the
 * misspelled parameter and answers the unfiltered list, while the tag on screen says otherwise.
 * The keys are asserted as strings because no type in this tree can check them against the
 * controller.
 */
describe('the user log network filters', () => {
  it('uses the endpoint ip/country/isp spellings', () => {
    const keys: UserLogNetworkFilterKey[] = ['country', 'ip', 'isp']

    expect(keys.join(',')).toBe('country,ip,isp')
  })

  it('reads the three single-value filters out of an address', () => {
    const params = new URLSearchParams(
      'ip=203.0.113.9&country=DE&isp=Example%20ISP&page=2&search=alice',
    )

    expect(networkFilterParams(params)).toEqual({
      country: 'DE',
      ip: '203.0.113.9',
      isp: 'Example ISP',
    })
  })

  it('answers null for an absent or empty value, so no tag claims a filter the request omits', () => {
    expect(networkFilterParams(new URLSearchParams(''))).toEqual({
      country: null,
      ip: null,
      isp: null,
    })
    expect(networkFilterParams(new URLSearchParams('ip=&country=&isp='))).toEqual({
      country: null,
      ip: null,
      isp: null,
    })
  })

  it('does not mistake the response field countryCode for the wire parameter', () => {
    // A row answers `countryCode`; a hand-built address naming it is not the filter the cell
    // writes, and treating it as one would send a parameter the server does not accept.
    const params = new URLSearchParams('countryCode=DE')

    expect(networkFilterParams(params).country).toBeNull()
  })

  it('round-trips a click through withFilter: the value is written back and page is dropped', () => {
    // The write half is the shared `withFilter` the page hands the cell; the read half is this
    // module. Together they are one single-value filter, covered the way the account table's
    // `status` filter already is.
    const written = withFilter(new URLSearchParams('page=4&userIds=x'), 'ip', '203.0.113.9')
    const read = networkFilterParams(written)

    expect(read.ip).toBe('203.0.113.9')
    expect(written.get('page')).toBeNull()
    expect(written.getAll('userIds')).toEqual(['x'])

    // Closing the tag writes null, which removes the key and leaves the rest of the address.
    const cleared = withFilter(written, 'ip', null)

    expect(networkFilterParams(cleared).ip).toBeNull()
  })
})
