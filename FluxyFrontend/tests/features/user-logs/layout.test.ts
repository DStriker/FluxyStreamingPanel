import { describe, expect, it } from 'vitest'
import {
  COLUMN_KEYS,
  COLUMN_WIDTHS_KEY,
  DEFAULT_COLUMN_WIDTHS,
  TABLE_WIDTH,
  columnWidthTotal,
  sortFieldOf,
} from '../../../src/features/user-logs/layout'

/**
 * The user log's layout, pinned as numbers and as its one key-to-field rule.
 *
 * Every fact here is invisible to `typecheck`, `lint` and the build: a width table that stops
 * summing to the administration's total is valid TypeScript, and a sorter that sends a field
 * the endpoint does not know compiles perfectly and is silently dropped.
 */
describe('the user log layout', () => {
  it('draws five columns, in the order the row reads', () => {
    expect([...COLUMN_KEYS]).toEqual([
      'username',
      'recordType',
      'description',
      'network',
      'createdAt',
    ])
  })

  it('sums the five widths to 1190, the total every table in this administration reaches', () => {
    expect(TABLE_WIDTH).toBe(1190)
    expect(columnWidthTotal(DEFAULT_COLUMN_WIDTHS)).toBe(TABLE_WIDTH)
  })

  it('gives the description the widest column and the network the second widest', () => {
    // The description is the row's own sentence and the column the search box matches.
    for (const key of ['username', 'recordType', 'network', 'createdAt'] as const) {
      expect(DEFAULT_COLUMN_WIDTHS.description).toBeGreaterThan(DEFAULT_COLUMN_WIDTHS[key])
    }

    // The network cell carries a flag, an address and a provider on two lines.
    for (const key of ['username', 'recordType', 'createdAt'] as const) {
      expect(DEFAULT_COLUMN_WIDTHS.network).toBeGreaterThan(DEFAULT_COLUMN_WIDTHS[key])
    }
  })

  it('maps the record-type header to the recordType field and every other key to createdAt', () => {
    expect(sortFieldOf('recordType')).toBe('recordType')
    expect(sortFieldOf('createdAt')).toBe('createdAt')
    expect(sortFieldOf('username')).toBe('createdAt')
    expect(sortFieldOf('description')).toBe('createdAt')
    expect(sortFieldOf('network')).toBe('createdAt')
  })

  it('remembers the arrangement under a namespaced key of its own', () => {
    expect(COLUMN_WIDTHS_KEY).toBe('fluxy.userLogs.columnWidths')
    expect(COLUMN_WIDTHS_KEY.startsWith('fluxy.userLogs.')).toBe(true)
  })
})
