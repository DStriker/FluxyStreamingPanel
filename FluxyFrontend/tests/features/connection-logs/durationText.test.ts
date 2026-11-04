import { describe, expect, it } from 'vitest'
import { durationText } from '../../../src/features/connection-logs/duration'
import type { DurationUnits } from '../../../src/features/connection-logs/duration'

/**
 * The duration helper the activity table prints beside an end time.
 *
 * It is the only arithmetic the connection log does, and it lives here rather than in the cell
 * for exactly that reason: a probe renders before any row arrives, so nothing that needs a row
 * can be reached from `probe:render`, and the cell is a component whose output is markup rather
 * than a sentence.
 *
 * The three properties worth pinning are the shape of each branch (hours, minutes, seconds),
 * the padding rule (a unit carries a leading zero only when a larger one precedes it), and the
 * dash a live session answers with - which is the branch the live page never reaches and the
 * activity page reaches on every row still running.
 */

const ENGLISH: DurationUnits = { hour: 'h', minute: 'min', second: 's' }

/** A start and an end exactly `seconds` apart, as ISO strings. */
const pair = (seconds: number): [string, string] => {
  const start = new Date('2026-05-01T10:00:00Z')
  const end = new Date(start.getTime() + seconds * 1000)

  return [start.toISOString(), end.toISOString()]
}

describe('durationText', () => {
  it('prints hours, minutes and seconds with the two smaller units padded', () => {
    // 1 h 04 min 12 s = 3852 s.
    expect(durationText(...pair(3852), ENGLISH)).toBe('1 h 04 min 12 s')
  })

  it('prints minutes and seconds, padding the seconds', () => {
    expect(durationText(...pair(247), ENGLISH)).toBe('4 min 07 s')
  })

  it('prints a sub-minute duration as seconds alone, unpadded', () => {
    expect(durationText(...pair(9), ENGLISH)).toBe('9 s')
    expect(durationText(...pair(0), ENGLISH)).toBe('0 s')
  })

  it('pads an hour count that is a single digit only where a smaller unit follows', () => {
    // 2 h 00 min 05 s - the minute is padded even though it is zero.
    expect(durationText(...pair(7205), ENGLISH)).toBe('2 h 00 min 05 s')
  })

  it('rounds to the nearest whole second rather than truncating', () => {
    const start = '2026-05-01T10:00:00.000Z'

    expect(durationText(start, '2026-05-01T10:00:00.600Z', ENGLISH)).toBe('1 s')
    expect(durationText(start, '2026-05-01T10:00:00.400Z', ENGLISH)).toBe('0 s')
  })

  it('never prints a negative duration for a pair that runs backwards', () => {
    const [start, end] = pair(30)

    expect(durationText(end, start, ENGLISH)).toBe('0 s')
  })

  it('answers a dash while the session is live', () => {
    const [start, end] = pair(120)

    expect(durationText(start, null, ENGLISH)).toBe('—')
    expect(durationText(null, end, ENGLISH)).toBe('—')
  })

  it('answers a dash for a pair it cannot read', () => {
    expect(durationText('not a date', '2026-05-01T10:00:00Z', ENGLISH)).toBe('—')
    expect(durationText('2026-05-01T10:00:00Z', 'not a date', ENGLISH)).toBe('—')
  })

  it('uses the unit words it is given, so the page language is the caller’s choice', () => {
    const RUSSIAN: DurationUnits = { hour: 'ч', minute: 'мин', second: 'с' }

    expect(durationText(...pair(3852), RUSSIAN)).toBe('1 ч 04 мин 12 с')
  })
})
