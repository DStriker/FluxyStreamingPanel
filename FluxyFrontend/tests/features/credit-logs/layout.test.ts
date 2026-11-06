import { describe, expect, it } from 'vitest'
import {
  COLUMN_KEYS,
  COLUMN_WIDTHS_KEY,
  DEFAULT_COLUMN_WIDTHS,
  TABLE_WIDTH,
  columnWidthTotal,
  sortFieldOf,
} from '../../../src/features/credit-logs/layout'

/**
 * The credit log's layout, pinned as numbers and as its one key-to-field rule.
 *
 * Every fact here is invisible to `typecheck`, `lint` and the build: a width table that stops
 * summing to the administration's total is valid TypeScript, and a sorter that sends a field
 * the endpoint does not know compiles perfectly and is silently dropped.
 */
describe('the credit log layout', () => {
  it('draws five columns, in the order the row reads', () => {
    expect([...COLUMN_KEYS]).toEqual([
      'reseller',
      'credits',
      'issuedBy',
      'description',
      'createdAt',
    ])
  })

  it('sums the five widths to 1190, the total every table in this administration reaches', () => {
    expect(TABLE_WIDTH).toBe(1190)
    expect(columnWidthTotal(DEFAULT_COLUMN_WIDTHS)).toBe(TABLE_WIDTH)
  })

  it('gives the two login names the same width and the description the wide column', () => {
    // The reseller and the issuer are the same kind of fact: a difference between the two
    // would make one of them look more important than the other.
    expect(DEFAULT_COLUMN_WIDTHS.reseller).toBe(DEFAULT_COLUMN_WIDTHS.issuedBy)

    // The description is the row's own sentence and the column the search box matches.
    for (const key of ['reseller', 'credits', 'issuedBy', 'createdAt'] as const) {
      expect(DEFAULT_COLUMN_WIDTHS.description).toBeGreaterThan(DEFAULT_COLUMN_WIDTHS[key])
    }

    // The date column is the width every table here gives a formatted timestamp.
    expect(DEFAULT_COLUMN_WIDTHS.createdAt).toBe(200)
  })

  it('maps the credits header to the amount sort field and nothing else', () => {
    // The header is the word the table draws; the endpoint orders by `amount`. This is the one
    // column where the two differ, and `useServerTable` hands a sorter's key over as `sortBy`.
    expect(sortFieldOf('credits')).toBe('amount')

    // Every other key is `createdAt` - including the date column itself, whose key and field
    // are the same string.
    expect(sortFieldOf('createdAt')).toBe('createdAt')
    expect(sortFieldOf('reseller')).toBe('createdAt')
    expect(sortFieldOf('issuedBy')).toBe('createdAt')
    expect(sortFieldOf('description')).toBe('createdAt')
  })

  it('remembers the arrangement under a namespaced key of its own', () => {
    expect(COLUMN_WIDTHS_KEY).toBe('fluxy.creditLogs.columnWidths')
    expect(COLUMN_WIDTHS_KEY.startsWith('fluxy.creditLogs.')).toBe(true)
  })
})
