import { describe, expect, it } from 'vitest'
import { actionsColumnWidth } from '../../../src/utils/tableWidths'
import {
  ACTIVITY_COLUMN_KEYS,
  ACTIVITY_COLUMN_WIDTHS,
  COLUMN_WIDTHS_KEYS,
  LIVE_COLUMN_KEYS,
  LIVE_COLUMN_WIDTHS,
  TABLE_WIDTH,
  columnWidthTotal,
} from '../../../src/features/connection-logs/layout'

/**
 * The connection log's two layouts, pinned as numbers.
 *
 * These are the owner's structural corrections, and every one of them is invisible to
 * `typecheck`, `lint` and the build: a width table that stops summing to the table's total is
 * perfectly valid TypeScript, and two storage keys that are accidentally the same string are
 * two `string`s. `probe:render` can see the widths a *fresh* render draws, but not the
 * relationship between the two modes - which is the thing that was wrong.
 */
describe('the connection log layout', () => {
  it('draws seven columns in each mode, with the end time and the actions cell never together', () => {
    expect(LIVE_COLUMN_KEYS).toHaveLength(7)
    expect(ACTIVITY_COLUMN_KEYS).toHaveLength(7)

    expect(LIVE_COLUMN_KEYS).toContain('actions')
    expect(LIVE_COLUMN_KEYS).not.toContain('endedAt')

    expect(ACTIVITY_COLUMN_KEYS).toContain('endedAt')
    expect(ACTIVITY_COLUMN_KEYS).not.toContain('actions')
  })

  it('no longer draws the log identifier in either mode', () => {
    // The owner replaced it with the subscription and the user as two columns; a key that came
    // back would be a column under the widths of a set that no longer has room for it.
    for (const keys of [LIVE_COLUMN_KEYS, ACTIVITY_COLUMN_KEYS]) {
      expect(keys).not.toContain('id')
      expect(keys).toContain('subscription')
      expect(keys).toContain('user')
    }
  })

  it('draws the user agent in both modes, beside the network cell', () => {
    for (const keys of [LIVE_COLUMN_KEYS, ACTIVITY_COLUMN_KEYS]) {
      expect(keys).toContain('agent')
      expect(keys.indexOf('agent')).toBe(keys.indexOf('network') + 1)
    }
  })

  it('sums each mode to 1190, the total every table in this administration reaches', () => {
    expect(TABLE_WIDTH).toBe(1190)
    expect(columnWidthTotal(LIVE_COLUMN_KEYS, LIVE_COLUMN_WIDTHS)).toBe(TABLE_WIDTH)
    expect(columnWidthTotal(ACTIVITY_COLUMN_KEYS, ACTIVITY_COLUMN_WIDTHS)).toBe(TABLE_WIDTH)
  })

  it("gives the actions column 45 more than its buttons need, and takes it from the stream", () => {
    // The owner's correction as an arithmetic: the kill stub's own measure plus 45, and the
    // title beside it 45 narrower than it was.
    expect(LIVE_COLUMN_WIDTHS.actions).toBe(actionsColumnWidth(1) + 45)
    expect(LIVE_COLUMN_WIDTHS.stream).toBe(344 - 45)
  })

  it('gives the start and end columns the same width, so the interval reads as one', () => {
    expect(ACTIVITY_COLUMN_WIDTHS.startedAt).toBe(ACTIVITY_COLUMN_WIDTHS.endedAt)
    // And the same start width in the live mode, because it prints the same timestamp.
    expect(LIVE_COLUMN_WIDTHS.startedAt).toBe(ACTIVITY_COLUMN_WIDTHS.startedAt)
  })

  it('keeps every content column but the stream at the same width in both modes', () => {
    // The two screens read the same row; a column that changed width between them would move
    // the table's content sideways on a switch the visitor did not ask for. The stream is the
    // one exception, and it is the column that pays for the end time on the activity view.
    for (const key of ['subscription', 'user', 'network', 'agent'] as const) {
      expect(LIVE_COLUMN_WIDTHS[key]).toBe(ACTIVITY_COLUMN_WIDTHS[key])
    }

    expect(LIVE_COLUMN_WIDTHS.stream).not.toBe(ACTIVITY_COLUMN_WIDTHS.stream)
  })

  it("gives the address the owner's round-4 width and takes it from the user agent", () => {
    // The owner's round-4 correction: the address column starts at 160 (it was 120) and the
    // user agent at 170 (it was 210), so the pair still spends 330 and each table still sums to
    // 1190. Both are the same in both modes, because the two screens read the same row.
    for (const widths of [LIVE_COLUMN_WIDTHS, ACTIVITY_COLUMN_WIDTHS]) {
      expect(widths.network).toBe(160)
      expect(widths.agent).toBe(170)
      expect(widths.network + widths.agent).toBe(330)

      // The address is the field an operator reads first, and it is no longer the narrowest
      // text in the row (the subscription column is 130). What must still hold is that the
      // provider sentence has room and that neither of the two columns beside it was widened:
      // the stream column pays for the timeline, and the pair's own total is unchanged.
      expect(widths.network).toBeLessThan(widths.stream)
      expect(widths.network).toBeLessThan(widths.agent)
    }
  })

  it('remembers each mode under its own storage key', () => {
    expect(COLUMN_WIDTHS_KEYS.live).not.toBe(COLUMN_WIDTHS_KEYS.activity)

    // The two are namespaced under the same prefix, so a reader looking for "where does this
    // page store widths" finds both - and a future third mode has an obvious place to go.
    for (const key of Object.values(COLUMN_WIDTHS_KEYS)) {
      expect(key.startsWith('fluxy.connectionLogs.')).toBe(true)
    }

    // The mode is in the key, so a reset pressed on one page cannot name the other's table.
    expect(COLUMN_WIDTHS_KEYS.live).toContain('.live.')
    expect(COLUMN_WIDTHS_KEYS.activity).toContain('.activity.')
  })

  it('bumps the storage key version with the round-3 column set', () => {
    // The keys are what a remembered arrangement is filed under, and the arrangement changed
    // shape: the id column is gone and the subscription cell now holds one fact. A stored
    // width from before this correction would describe columns that no longer exist.
    for (const key of Object.values(COLUMN_WIDTHS_KEYS)) {
      expect(key.endsWith('.v2')).toBe(true)
    }
  })

  it('carries no width for the column a mode does not draw', () => {
    // Zero rather than a real number, so a stale arrangement can never contribute pixels to a
    // sum the mode does not draw - the bug the separate storage keys exist to prevent.
    expect(LIVE_COLUMN_WIDTHS.endedAt).toBe(0)
    expect(ACTIVITY_COLUMN_WIDTHS.actions).toBe(0)
  })
})
