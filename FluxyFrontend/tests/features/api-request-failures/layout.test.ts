import { describe, expect, it } from 'vitest'
import {
  COLUMN_KEYS,
  COLUMN_WIDTHS_KEY,
  DEFAULT_COLUMN_WIDTHS,
  TABLE_WIDTH,
  columnWidthTotal,
  sortFieldOf,
} from '../../../src/features/api-request-failures/layout'

/**
 * The API request failure log's layout, pinned as numbers and as its one key-to-field rule.
 *
 * Every fact here is invisible to `typecheck`, `lint` and the build: a width table that stops
 * summing to the administration's total is valid TypeScript, and a sorter that sends a field
 * the endpoint does not know compiles perfectly and is silently dropped.
 */
describe('the API request failure log layout', () => {
  it('draws five columns, in the order the row reads', () => {
    expect([...COLUMN_KEYS]).toEqual([
      'subscription',
      'content',
      'reason',
      'network',
      'createdAt',
    ])
  })

  it('sums the five widths to 1190, the total every table in this administration reaches', () => {
    expect(TABLE_WIDTH).toBe(1190)
    expect(columnWidthTotal(DEFAULT_COLUMN_WIDTHS)).toBe(1190)
  })

  it('gives the content the widest column and the network the second widest', () => {
    // The content cell carries a stored name with its opaque identifier underneath.
    for (const key of ['subscription', 'reason', 'network', 'createdAt'] as const) {
      expect(DEFAULT_COLUMN_WIDTHS.content).toBeGreaterThan(DEFAULT_COLUMN_WIDTHS[key])
    }

    // The network cell carries a flag, an address and a provider on two lines.
    for (const key of ['subscription', 'reason', 'createdAt'] as const) {
      expect(DEFAULT_COLUMN_WIDTHS.network).toBeGreaterThan(DEFAULT_COLUMN_WIDTHS[key])
    }
  })

  it('maps the reason header to the reason field and every other key to createdAt', () => {
    expect(sortFieldOf('reason')).toBe('reason')
    expect(sortFieldOf('createdAt')).toBe('createdAt')
    expect(sortFieldOf('subscription')).toBe('createdAt')
    expect(sortFieldOf('content')).toBe('createdAt')
    expect(sortFieldOf('network')).toBe('createdAt')
  })

  it('remembers the arrangement under a namespaced key of its own', () => {
    expect(COLUMN_WIDTHS_KEY).toBe('fluxy.apiRequestFailures.columnWidths')
    expect(COLUMN_WIDTHS_KEY.startsWith('fluxy.apiRequestFailures.')).toBe(true)
  })
})
