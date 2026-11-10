import { describe, expect, it } from 'vitest'
import {
  COLUMN_KEYS,
  COLUMN_WIDTHS_KEY,
  DEFAULT_COLUMN_WIDTHS,
  TABLE_WIDTH,
  columnWidthTotal,
  sortFieldOf,
} from '../../../src/features/stream-problems/layout'

/**
 * The stream problem journal's layout, pinned as numbers and as its one sort rule.
 *
 * Every fact here is invisible to `typecheck`, `lint` and the build: a width table that stops
 * summing to the administration's total is valid TypeScript, and a sorter that sends a field
 * the endpoint does not know compiles perfectly and is silently dropped.
 */
describe('the stream problem journal layout', () => {
  it('draws three columns, in the order the row reads', () => {
    expect([...COLUMN_KEYS]).toEqual(['stream', 'error', 'createdAt'])
  })

  it('sums the three widths to 1190, the total every table in this administration reaches', () => {
    expect(TABLE_WIDTH).toBe(1190)
    expect(DEFAULT_COLUMN_WIDTHS.stream).toBe(320)
    expect(DEFAULT_COLUMN_WIDTHS.error).toBe(570)
    expect(DEFAULT_COLUMN_WIDTHS.createdAt).toBe(300)
    expect(columnWidthTotal(DEFAULT_COLUMN_WIDTHS)).toBe(1190)
  })

  it('gives the error the widest column, because the producer message is the longest fact', () => {
    for (const key of ['stream', 'createdAt'] as const) {
      expect(DEFAULT_COLUMN_WIDTHS.error).toBeGreaterThan(DEFAULT_COLUMN_WIDTHS[key])
    }
  })

  it('maps every column key to the endpoint’s one sort field', () => {
    expect(sortFieldOf('createdAt')).toBe('createdAt')
    expect(sortFieldOf('stream')).toBe('createdAt')
    expect(sortFieldOf('error')).toBe('createdAt')
  })

  it('remembers the arrangement under a namespaced key of its own', () => {
    expect(COLUMN_WIDTHS_KEY).toBe('fluxy.streamProblems.columnWidths')
    expect(COLUMN_WIDTHS_KEY.startsWith('fluxy.streamProblems.')).toBe(true)
  })
})
