import dayjs from 'dayjs'
import { describe, expect, it } from 'vitest'
import {
  addPlanDuration,
  projectedExpiry,
} from '../../../src/features/subscriptions/prolongPreview'

/**
 * The prolongation preview, asserted as a function.
 *
 * This is the half of the dialog no render can reach: `probe:render` lands before a click, and the
 * date is computed in the confirm handler. It is also the half that has to agree with the server,
 * so every case below is written from `SubscriptionRules.ProlongedExpiry` rather than from what
 * the code happened to do - the formula is `max(now, the stored expiry) + the plan's duration`,
 * and the calendar units have to behave like .NET's `AddMonths`/`AddYears`.
 */

const row = (overrides: {
  expireAt?: string | null
  tariffPlanDurationValue?: number | null
  tariffPlanDurationUnit?: 'hours' | 'days' | 'months' | 'years' | null
} = {}) => ({
  expireAt: '2027-01-31T00:00:00Z' as string | null,
  tariffPlanDurationValue: 1 as number | null,
  tariffPlanDurationUnit: 'months' as 'hours' | 'days' | 'months' | 'years' | null,
  ...overrides,
})

const NOW = dayjs('2026-06-15T12:00:00Z')

describe('projectedExpiry', () => {
  it('extends a live subscription from its own expiry, not from now', () => {
    // The rule the prolongation is for: twenty days left plus a month leaves the remainder in
    // place. Extending from "now" would silently take those twenty days away.
    const result = projectedExpiry(row({ expireAt: '2026-07-05T00:00:00Z' }), NOW)

    expect(result?.toISOString()).toBe('2026-08-05T00:00:00.000Z')
  })

  it('restarts an expired subscription from now', () => {
    // `max(now, expiry)`: a lapsed row has nothing left to extend, so the period is added to now -
    // which is what "revives it" means.
    const result = projectedExpiry(row({ expireAt: '2026-01-01T00:00:00Z' }), NOW)

    expect(result?.toISOString()).toBe('2026-07-15T12:00:00.000Z')
  })

  it('reads a missing expiry as now', () => {
    // A row that never lapsed but carries no expiry gets a full period rather than none.
    const result = projectedExpiry(row({ expireAt: null }), NOW)

    expect(result?.toISOString()).toBe('2026-07-15T12:00:00.000Z')
  })

  it('adds calendar months the way AddMonths does, clamping the day', () => {
    // 31 January + 1 month is 28 February in a non-leap year, not 3 March. A projection built on
    // "a month is 30 days" would answer 2 March and disagree with the server.
    const result = projectedExpiry(
      row({ expireAt: '2027-01-31T00:00:00Z', tariffPlanDurationUnit: 'months' }),
      dayjs('2026-06-15T00:00:00Z'),
    )

    expect(result?.format('YYYY-MM-DD')).toBe('2027-02-28')
  })

  it('adds calendar years with the same clamping', () => {
    // 29 February 2028 + 1 year is 28 February 2029.
    const leap = projectedExpiry(
      row({ expireAt: '2028-02-29T00:00:00Z', tariffPlanDurationUnit: 'years' }),
      dayjs('2026-06-15T00:00:00Z'),
    )

    expect(leap?.format('YYYY-MM-DD')).toBe('2029-02-28')
  })

  it('adds hours and days as exact arithmetic', () => {
    const hours = projectedExpiry(
      row({ expireAt: '2026-07-01T00:00:00Z', tariffPlanDurationValue: 36, tariffPlanDurationUnit: 'hours' }),
      NOW,
    )
    expect(hours?.toISOString()).toBe('2026-07-02T12:00:00.000Z')

    const days = projectedExpiry(
      row({ expireAt: '2026-07-01T00:00:00Z', tariffPlanDurationValue: 10, tariffPlanDurationUnit: 'days' }),
      NOW,
    )
    expect(days?.toISOString()).toBe('2026-07-11T00:00:00.000Z')
  })

  it('answers nothing when there is no plan to measure by', () => {
    // The state the row's prolong button is disabled in, so the dialog is never drawn for it.
    expect(
      projectedExpiry(row({ tariffPlanDurationValue: null, tariffPlanDurationUnit: null }), NOW),
    ).toBeNull()
    expect(projectedExpiry(row({ tariffPlanDurationValue: null }), NOW)).toBeNull()
    expect(projectedExpiry(row({ tariffPlanDurationUnit: null }), NOW)).toBeNull()
  })

  it('treats an expiry exactly equal to now as lapsed', () => {
    // `max` in the server is `existing > now ? existing : now`, so the boundary is inclusive on
    // now's side; the preview has to pick the same branch or it would add the period to a date
    // that is a second away from being the same instant.
    const result = projectedExpiry(
      row({ expireAt: NOW.toISOString(), tariffPlanDurationValue: 1, tariffPlanDurationUnit: 'days' }),
      NOW,
    )

    expect(result?.toISOString()).toBe('2026-06-16T12:00:00.000Z')
  })
})

describe('addPlanDuration', () => {
  it('leaves the moment alone for a unit outside the four', () => {
    // Unreachable from a stored row (the plan service refuses such a unit), but a throw here would
    // take the page down over a dialog, so the fallthrough is deliberate.
    const start = dayjs('2026-06-15T12:00:00Z')
    expect(
      addPlanDuration(start, 5, 'fortnights' as 'days').toISOString(),
    ).toBe(start.toISOString())
  })
})
