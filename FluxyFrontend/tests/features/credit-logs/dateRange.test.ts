import { describe, expect, it } from 'vitest'
import { DATE_FROM_KEY, DATE_TO_KEY } from '../../../src/features/credit-logs/dateRange'
import { dateRangeParams } from '../../../src/utils/dateRange'

/**
 * The credit log's parameter names. The arithmetic itself is shared and pinned in
 * `tests/utils/dateRange.test.ts`; what this file owes is that the two keys this table reads
 * and writes are the endpoint's own spellings - a typo here would send a filter the server
 * silently ignores, with the tag on screen saying it applied.
 */
describe('the credit log date range keys', () => {
  it('uses the endpoint dateFrom/dateTo spellings', () => {
    expect(DATE_FROM_KEY).toBe('dateFrom')
    expect(DATE_TO_KEY).toBe('dateTo')
  })

  it('reads its own two parameters out of an address', () => {
    const params = new URLSearchParams(
      'dateFrom=2026-05-01T00:00:00.000Z&dateTo=2026-05-03T23:59:59.999Z',
    )

    expect(dateRangeParams(params, DATE_FROM_KEY, DATE_TO_KEY)).toEqual({
      from: '2026-05-01T00:00:00.000Z',
      to: '2026-05-03T23:59:59.999Z',
    })
  })

  it('leaves a single-sided range single-sided', () => {
    const params = new URLSearchParams('dateFrom=2026-05-01T00:00:00.000Z')

    expect(dateRangeParams(params, DATE_FROM_KEY, DATE_TO_KEY)).toEqual({
      from: '2026-05-01T00:00:00.000Z',
      to: null,
    })
  })
})
