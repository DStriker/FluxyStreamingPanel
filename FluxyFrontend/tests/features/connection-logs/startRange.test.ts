import dayjs from 'dayjs'
import timezone from 'dayjs/plugin/timezone'
import utc from 'dayjs/plugin/utc'
import { describe, expect, it, vi } from 'vitest'
import {
  NO_START_RANGE,
  STARTED_FROM_KEY,
  STARTED_TO_KEY,
  startRangeForMode,
  startRangeParams,
} from '../../../src/features/connection-logs/startRange'
import { rangeFromPicker } from '../../../src/utils/dateRange'

dayjs.extend(utc)
dayjs.extend(timezone)

/**
 * The connection log's own half of the start-time range: its two parameter names and the
 * mode gate. The arithmetic - calendar days in, account-zone instants out - is shared with
 * the credit log and pinned in `tests/utils/dateRange.test.ts`; what stays here is the part
 * no other table has, because no other table has a live view.
 *
 * The zone stubs are still here for the one round-trip case that proves the re-exported
 * helper is the shared one rather than a second copy.
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
    expect(startRangeParams(new URLSearchParams('startedFrom=&startedTo='))).toEqual({
      from: null,
      to: null,
    })
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

  it('still converts a picked range through the shared account-zone arithmetic', () => {
    // The re-export is the shared module, not a copy: the same zone arithmetic the credit log
    // now uses answers here too, with both ends inclusive.
    useZone('Europe/Berlin')

    const range = rangeFromPicker([dayjs('2026-05-01'), dayjs('2026-05-03')], 'Europe/Moscow')

    expect(range.from).toBe('2026-04-30T21:00:00.000Z')
    expect(range.to).toBe('2026-05-03T20:59:59.999Z')
  })
})
