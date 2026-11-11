import { describe, expect, it } from 'vitest'
import {
  COLUMN_KEYS,
  COLUMN_WIDTHS_KEY,
  DEFAULT_COLUMN_WIDTHS,
  TABLE_WIDTH,
  columnWidthTotal,
  sortFieldOf,
} from '../../../src/features/mag-events/layout'

/**
 * The MAG event journal's layout, pinned as numbers and as its one sort rule.
 *
 * Every fact here is invisible to `typecheck`, `lint` and the build: a width table that stops
 * summing to the administration's total is valid TypeScript, and a sorter that sends a field
 * the endpoint does not know compiles perfectly and is silently dropped.
 */
describe('the MAG event journal layout', () => {
  it('draws five columns, in the order the row reads', () => {
    expect([...COLUMN_KEYS]).toEqual(['mac', 'event', 'message', 'status', 'date'])
  })

  it('sums the five widths to 1190, the total every table in this administration reaches', () => {
    expect(TABLE_WIDTH).toBe(1190)
    expect(DEFAULT_COLUMN_WIDTHS.mac).toBe(180)
    expect(DEFAULT_COLUMN_WIDTHS.event).toBe(170)
    expect(DEFAULT_COLUMN_WIDTHS.status).toBe(120)
    expect(DEFAULT_COLUMN_WIDTHS.date).toBe(200)
    expect(DEFAULT_COLUMN_WIDTHS.message).toBe(520)
    expect(columnWidthTotal(DEFAULT_COLUMN_WIDTHS)).toBe(1190)
  })

  it('gives the message the widest column, because the payload is the longest fact', () => {
    for (const key of ['mac', 'event', 'status', 'date'] as const) {
      expect(DEFAULT_COLUMN_WIDTHS.message).toBeGreaterThan(DEFAULT_COLUMN_WIDTHS[key])
    }
  })

  it('maps every column key to the endpoint’s one sort field', () => {
    expect(sortFieldOf('date')).toBe('createdAt')
    expect(sortFieldOf('createdAt')).toBe('createdAt')
    expect(sortFieldOf('mac')).toBe('createdAt')
    expect(sortFieldOf('event')).toBe('createdAt')
    expect(sortFieldOf('message')).toBe('createdAt')
    expect(sortFieldOf('status')).toBe('createdAt')
  })

  it('remembers the arrangement under a namespaced key of its own', () => {
    expect(COLUMN_WIDTHS_KEY).toBe('fluxy.magEvents.columnWidths')
    expect(COLUMN_WIDTHS_KEY.startsWith('fluxy.magEvents.')).toBe(true)
  })
})
