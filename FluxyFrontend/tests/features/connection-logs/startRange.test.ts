import dayjs from 'dayjs'
import timezone from 'dayjs/plugin/timezone'
import utc from 'dayjs/plugin/utc'
import { describe, expect, it, vi } from 'vitest'
import {
  NO_START_RANGE,
  STARTED_FROM_KEY,
  STARTED_TO_KEY,
  endOfDayIso,
  pickerDay,
  rangeFromPicker,
  rangePickerValue,
  startOfDayIso,
  startRangeForMode,
  startRangeParams,
} from '../../../src/features/connection-logs/startRange'
import { formatDay } from '../../../src/utils/dateFormat'

dayjs.extend(utc)
dayjs.extend(timezone)

/**
 * The start-time range's two conversions, asserted in **pinned non-UTC zones**.
 *
 * Every interesting decision here is invisible to a UTC machine: whether the picked day is read
 * in the account's zone or the browser's, and whether the two ends are inclusive. A test that
 * inherited the machine's zone would pass over the very bug it exists to catch, so each case
 * states the zone it needs - and the assertions are UTC instants, which no zone can move.
 * Vitest isolates every file, so a stubbed zone cannot leak into another suite.
 */

const useZone = (zone: string) => vi.stubEnv('TZ', zone)

describe('the start-time range', () => {
  it('reads both bounds out of the address, absent and empty alike meaning no bound', () => {
    expect(
      startRangeParams(
        new URLSearchParams(
          'startedFrom=2026-05-01T00:00:00.000Z&startedTo=2026-05-01T23:59:59.999Z',
        ),
      ),
    ).toEqual({
      from: '2026-05-01T00:00:00.000Z',
      to: '2026-05-01T23:59:59.999Z',
    })

    expect(startRangeParams(new URLSearchParams(''))).toEqual({ from: null, to: null })
    expect(
      startRangeParams(new URLSearchParams('startedFrom=&startedTo=')),
    ).toEqual({ from: null, to: null })
    expect(STARTED_FROM_KEY).toBe('startedFrom')
    expect(STARTED_TO_KEY).toBe('startedTo')
  })

  it('reads no range at all on the mode that draws no range control', () => {
    // The live page pins `state=live`, and a bound from a hand-edited or saved address would
    // narrow that promise on a screen with no control showing the narrowing. The keys are
    // ignored rather than stripped: the address stays the visitor's, the query stays the same.
    const address = new URLSearchParams(
      'startedFrom=2026-05-01T00:00:00.000Z&startedTo=2026-05-03T23:59:59.999Z',
    )

    expect(startRangeForMode(address, false)).toEqual(NO_START_RANGE)
    expect(startRangeForMode(address, true)).toEqual({
      from: '2026-05-01T00:00:00.000Z',
      to: '2026-05-03T23:59:59.999Z',
    })
  })

  it("writes the chosen days as the account zone's first and last millisecond", () => {    useZone('Europe/Berlin')

    // Europe/Moscow is three hours ahead of UTC all year, so 1 May there begins on
    // 30 April at 21:00Z. A browser-zone implementation (Berlin, one hour ahead) would instead
    // answer 30 April at 22:00Z and quietly include an extra hour of rows.
    const from = startOfDayIso(dayjs('2026-05-01'), 'Europe/Moscow')
    const to = endOfDayIso(dayjs('2026-05-01'), 'Europe/Moscow')

    expect(from).toBe('2026-04-30T21:00:00.000Z')
    expect(to).toBe('2026-05-01T20:59:59.999Z')
  })

  it('keeps both ends inclusive, so the end day is not lost', () => {
    const range = rangeFromPicker(
      [dayjs('2026-05-01'), dayjs('2026-05-03')],
      'Europe/Moscow',
    )

    expect(range.from).toBe('2026-04-30T21:00:00.000Z')
    // The last millisecond of the 3rd, not its midnight: an exclusive bound would drop
    // everything the operator watched on the day they picked as the end.
    expect(range.to).toBe('2026-05-03T20:59:59.999Z')
  })

  it('honours a DST transition as the zone states it', () => {
    // Berlin springs forward on 29 March 2026: the day begins at +01:00 and ends at +02:00, so
    // the interval is one hour shorter than a fixed offset would make it.
    const from = startOfDayIso(dayjs('2026-03-29'), 'Europe/Berlin')
    const to = endOfDayIso(dayjs('2026-03-29'), 'Europe/Berlin')

    expect(from).toBe('2026-03-28T23:00:00.000Z')
    expect(to).toBe('2026-03-29T21:59:59.999Z')
  })

  it('follows the browser when the account has no zone stored', () => {
    useZone('Asia/Tokyo')

    // `null` is the profile page's "automatic": the page prints its rows in the browser's zone,
    // so the picker's day has to be that zone's day as well.
    const from = startOfDayIso(dayjs('2026-05-01'), null)

    expect(from).toBe('2026-04-30T15:00:00.000Z')
  })

  it('round-trips a wire instant back to the calendar day the picker shows', () => {
    useZone('Europe/Berlin')

    // 1 May starts at 21:00Z in Moscow, which is 20:00Z in Berlin - still 1 May, so the picker
    // shows the day the operator picked rather than a day shifted by the browser's offset.
    const day = pickerDay('2026-04-30T21:00:00.000Z', 'Europe/Moscow')

    expect(day?.format('YYYY-MM-DD')).toBe('2026-05-01')
  })

  it('answers an empty picker for an unreadable or absent bound, never an invalid date', () => {
    expect(pickerDay(null, 'UTC')).toBeNull()
    expect(pickerDay('', 'UTC')).toBeNull()
    expect(pickerDay('not-a-date', 'UTC')).toBeNull()

    expect(rangePickerValue({ from: null, to: null }, 'UTC')).toBeNull()

    const single = rangePickerValue({ from: '2026-05-01T00:00:00.000Z', to: null }, 'UTC')
    expect(single?.[0]?.format('YYYY-MM-DD')).toBe('2026-05-01')
    expect(single?.[1]).toBeNull()
  })

  it('shows the account-zone day in the picker whatever zone the browser is in', () => {
    // The browser is a day behind UTC; the account reads in UTC. The picker must show the day
    // the account's zone (and therefore the row's printed timestamp) puts the instant on.
    useZone('America/New_York')

    expect(pickerDay('2026-05-01T00:00:00.000Z', 'UTC')?.format('YYYY-MM-DD')).toBe(
      '2026-05-01',
    )
    expect(pickerDay('2026-05-01T00:00:00.000Z', null)?.format('YYYY-MM-DD')).toBe(
      '2026-04-30',
    )
  })

  it('round-trips an address range through the picker unchanged', () => {
    useZone('Europe/Berlin')

    const original = {
      from: '2026-04-30T21:00:00.000Z',
      to: '2026-05-03T20:59:59.999Z',
    }
    const value = rangePickerValue(original, 'Europe/Moscow')

    expect(value).not.toBeNull()
    expect(rangeFromPicker(value!, 'Europe/Moscow')).toEqual(original)
  })

  it('leaves a side of the range unset when the picker has no value for it', () => {
    const range = rangeFromPicker([dayjs('2026-05-01'), null], 'UTC')

    expect(range.from).toBe('2026-05-01T00:00:00.000Z')
    expect(range.to).toBeNull()
  })

  it('prints a day in the page language and the account zone', () => {
    useZone('UTC')

    expect(formatDay('2026-04-30T21:00:00.000Z', 'en', 'Europe/Moscow')).toBe('May 1, 2026')
    // An unreadable value is answered as itself: the tag still names what the address holds.
    expect(formatDay('not-a-date', 'en', null)).toBe('not-a-date')
  })
})
