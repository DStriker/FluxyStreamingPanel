import dayjs from 'dayjs'
import utc from 'dayjs/plugin/utc'
import { describe, expect, it, vi } from 'vitest'
import {
  addPlanDuration,
  projectedExpiry,
} from '../../../src/features/subscriptions/prolongPreview'

dayjs.extend(utc)

/**
 * The prolongation preview, asserted as a function - and asserted *in a non-UTC viewer zone*.
 *
 * The preview has to agree with `SubscriptionRules.ProlongedExpiry`, which adds to a
 * `DateTimeOffset` whose fields are the stored UTC instant. dayjs in its default mode adds on the
 * *viewer's* calendar, so the two disagree at every month end and across every DST transition -
 * the dialog promising a date the server does not store. These tests assert **UTC instants** and
 * `.utc()` formatting (`toISOString`), which no zone can move.
 *
 * The month-end and DST cases each do two things: they assert the right instant, and they assert
 * that the *local-mode* computation - the implementation this replaced - gives a **different** one.
 * The second half is what makes them regression tests rather than restatements: a revert to
 * `dayjs(...)` would satisfy neither.
 *
 * **Why every case pins its own zone rather than relying on the default.** These divergences only
 * exist under a *particular* viewer zone - UTC+5 is what makes 23:00Z on 30 January read as the
 * 31st, and New York is what puts a DST transition between two calendar days - so a test that
 * inherited whatever zone the machine happens to run in would prove nothing on a UTC CI box and
 * would change meaning on a developer's laptop. Each case therefore states the zone its assertion
 * depends on. The assertions themselves stay in UTC instants and `.utc()` formatting, so they hold
 * regardless of which zone was pinned; the zone only decides whether the *comparison against local
 * mode* has anything to disagree about.
 *
 * Writing the zone is enough because Node honours a runtime `TZ` assignment immediately, in both
 * directions. What it does **not** do is restore the system zone when the variable is *deleted*:
 * `vi.unstubAllEnvs()` removes a variable that started undefined, which leaves the last explicitly
 * set zone in effect rather than the machine's own. That is why no case relies on getting the
 * default back - each names the zone it needs - and why this file's final zone does not matter:
 * Vitest runs every test file in its own isolated environment. Restoring through `process` directly
 * would also mean reaching for a global `tsconfig.json` does not type for `tests/`.
 */

/** Pins the viewer's zone for the rest of this test; see the note above on per-case zones. */
const useZone = (zone: string) => vi.stubEnv('TZ', zone)

const row = (
  overrides: {
    expireAt?: string | null
    tariffPlanDurationValue?: number | null
    tariffPlanDurationUnit?: 'hours' | 'days' | 'months' | 'years' | null
  } = {},
) => ({
  expireAt: '2027-01-31T00:00:00Z' as string | null,
  tariffPlanDurationValue: 1 as number | null,
  tariffPlanDurationUnit: 'months' as 'hours' | 'days' | 'months' | 'years' | null,
  ...overrides,
})

const NOW = dayjs.utc('2026-06-15T12:00:00Z')

/** The instant a value names, whatever mode it carries. */
const instant = (value: ReturnType<typeof projectedExpiry>) =>
  value === null ? null : value.toISOString()

describe('projectedExpiry', () => {
  it('extends a live subscription from its own expiry, not from now', () => {
    // Twenty days left plus a month leaves the remainder in place. Extending from "now" would
    // silently take those twenty days away.
    useZone('Asia/Yekaterinburg')

    const result = projectedExpiry(row({ expireAt: '2026-07-05T00:00:00Z' }), NOW)

    expect(instant(result)).toBe('2026-08-05T00:00:00.000Z')
  })

  it('restarts an expired subscription from now', () => {
    // `max(now, expiry)`: a lapsed row has nothing left to extend, so the period is added to now.
    useZone('Asia/Yekaterinburg')

    const result = projectedExpiry(row({ expireAt: '2026-01-01T00:00:00Z' }), NOW)

    expect(instant(result)).toBe('2026-07-15T12:00:00.000Z')
  })

  it('reads a missing expiry as now', () => {
    useZone('Asia/Yekaterinburg')

    const result = projectedExpiry(row({ expireAt: null }), NOW)

    expect(instant(result)).toBe('2026-07-15T12:00:00.000Z')
  })

  it('treats an expiry exactly equal to now as lapsed', () => {
    // The server's floor is `existing > now ? existing : now`, so the boundary belongs to now.
    useZone('Asia/Yekaterinburg')

    const result = projectedExpiry(
      row({
        expireAt: '2026-06-15T12:00:00Z',
        tariffPlanDurationValue: 1,
        tariffPlanDurationUnit: 'days',
      }),
      NOW,
    )

    expect(instant(result)).toBe('2026-06-16T12:00:00.000Z')
  })

  it('answers nothing when there is no plan to measure by', () => {
    // The state the row's prolong button is disabled in, so the dialog is never drawn for it.
    expect(
      projectedExpiry(row({ tariffPlanDurationValue: null, tariffPlanDurationUnit: null }), NOW),
    ).toBeNull()
    expect(projectedExpiry(row({ tariffPlanDurationValue: null }), NOW)).toBeNull()
    expect(projectedExpiry(row({ tariffPlanDurationUnit: null }), NOW)).toBeNull()
  })
})

describe('month-end clamping happens on the UTC calendar, not the viewer’s', () => {
  it('adds one month to 2027-01-30T23:00Z from UTC+5 the way AddMonths does', () => {
    // The reviewer's case, one hour shifted for this zone: 23:00Z on 30 January is already
    // 31 January locally, so a *local* "+1 month" clamps from the 31st and lands a day early.
    // The server sees 30 January on the UTC calendar and answers 28 February - confirmed against
    // `DateTimeOffset.AddMonths` directly.
    useZone('Asia/Yekaterinburg')
    const stored = '2027-01-30T23:00:00Z'

    // What the old, local-mode implementation produced.
    const localMode = dayjs(stored).add(1, 'month').toISOString()
    expect(localMode).toBe('2027-02-27T23:00:00.000Z')

    // What the preview produces, and what the server actually stores.
    const preview = projectedExpiry(row({ expireAt: stored }), NOW)
    expect(instant(preview)).toBe('2027-02-28T23:00:00.000Z')

    // The regression in one line: the two modes disagree, so this cannot pass on a revert.
    expect(instant(preview)).not.toBe(localMode)
  })

  it('clamps 31 January to 28 February in a non-leap year', () => {
    useZone('Asia/Yekaterinburg')

    const preview = projectedExpiry(
      row({ expireAt: '2027-01-31T00:00:00Z', tariffPlanDurationUnit: 'months' }),
      NOW,
    )

    expect(preview?.utc().format('YYYY-MM-DD')).toBe('2027-02-28')
  })

  it('clamps 29 February to 28 February across a year', () => {
    useZone('Asia/Yekaterinburg')

    const preview = projectedExpiry(
      row({ expireAt: '2028-02-29T00:00:00Z', tariffPlanDurationUnit: 'years' }),
      dayjs.utc('2026-06-15T00:00:00Z'),
    )

    expect(preview?.utc().format('YYYY-MM-DD')).toBe('2029-02-28')
  })
})

describe('the duration is exact across the viewer’s DST transition', () => {
  it('adds ten days as ten days, not as ten times the local day length', () => {
    // 5 March 12:00Z is 07:00 EST; ten *local* days later is 07:00 EDT, which is 11:00Z - an hour
    // early, because a local day gained an hour in between. The server adds ten real days.
    useZone('America/New_York')
    const stored = '2027-03-05T12:00:00Z'

    const localMode = dayjs(stored).add(10, 'day').toISOString()
    expect(localMode).toBe('2027-03-15T11:00:00.000Z')

    const preview = projectedExpiry(
      row({ expireAt: stored, tariffPlanDurationValue: 10, tariffPlanDurationUnit: 'days' }),
      NOW,
    )
    expect(instant(preview)).toBe('2027-03-15T12:00:00.000Z')
    expect(instant(preview)).not.toBe(localMode)
  })

  it('adds 36 hours across the same transition exactly', () => {
    useZone('America/New_York')

    const preview = projectedExpiry(
      row({
        expireAt: '2027-03-13T12:00:00Z',
        tariffPlanDurationValue: 36,
        tariffPlanDurationUnit: 'hours',
      }),
      NOW,
    )

    expect(instant(preview)).toBe('2027-03-15T00:00:00.000Z')
  })
})

describe('the answer does not depend on the viewer’s zone at all', () => {
  it('projects the same instant from a zone west of UTC and one east of it', () => {
    // The strongest statement of the fix: the account reading the page may be anywhere, and the
    // date the dialog promises has to be the date the server stores in every one of them.
    const cases = [
      row({ expireAt: '2027-01-30T23:00:00Z' }),
      row({ expireAt: '2027-01-31T00:00:00Z' }),
      row({
        expireAt: '2027-03-05T12:00:00Z',
        tariffPlanDurationValue: 10,
        tariffPlanDurationUnit: 'days',
      }),
      row({ expireAt: '2028-02-29T00:00:00Z', tariffPlanDurationUnit: 'years' }),
    ]

    useZone('America/Los_Angeles')
    const west = cases.map((entry) => instant(projectedExpiry(entry, NOW)))

    useZone('Asia/Yekaterinburg')
    const east = cases.map((entry) => instant(projectedExpiry(entry, NOW)))

    expect(west).toEqual(east)
  })
})

describe('addPlanDuration', () => {
  it('leaves the moment alone for a unit outside the four', () => {
    // Unreachable from a stored row (the plan service refuses such a unit), but a throw here would
    // take the page down over a dialog, so the fallthrough is deliberate.
    const start = dayjs.utc('2026-06-15T12:00:00Z')
    expect(addPlanDuration(start, 5, 'fortnights' as 'days').toISOString()).toBe(
      start.toISOString(),
    )
  })
})
