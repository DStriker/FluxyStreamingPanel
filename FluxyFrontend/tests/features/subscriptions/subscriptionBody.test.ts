import { describe, expect, it } from 'vitest'
import {
  expireAtPickerProps,
  toCreateBody,
  toUpdateBody,
} from '../../../src/features/subscriptions/subscriptionBody'
import type { SubscriptionValues } from '../../../src/features/subscriptions/subscriptionBody'

/**
 * The two body mappings, asserted as functions.
 *
 * They are the half of the form a render cannot reach - every `probe:render` lands before a submit
 * - and the one place where "the operator cleared the plan" becomes a request. A body that named a
 * null `tariffPlanId` without the companion flag would be a *keep*, which is exactly the bug this
 * file exists to stand against: the form would report "saved" and the old plan would still be
 * attached.
 */

const values = (overrides: Partial<SubscriptionValues> = {}): SubscriptionValues => ({
  userId: 'user-1',
  playlistId: '0123456789abcdef0123456789abcdef',
  expireAt: '2027-01-31',
  tariffPlanId: 'plan-1',
  maxConnections: 3,
  isRestreamer: true,
  ipWhitelist: ['192.168.1.0/24'],
  userAgentWhitelist: ['Mozilla/5.0'],
  isIspLock: false,
  isCountryLock: false,
  isTrial: false,
  outputFormats: ['mpegTs', 'hls'],
  magMac: 'AA:BB:CC:DD:EE:FF',
  isStbLock: false,
  enigma2Mac: null,
  bouquetIds: ['bouquet-1'],
  ...overrides,
})

describe('toCreateBody', () => {
  it('sends every field the create endpoint takes, including the owner', () => {
    const body = toCreateBody(values())

    expect(body.userId).toBe('user-1')
    expect(body.tariffPlanId).toBe('plan-1')
    expect(body.playlistId).toBe('0123456789abcdef0123456789abcdef')
    expect(body.expireAt).toBe('2027-01-31')
    expect(body.maxConnections).toBe(3)
    expect(body.isRestreamer).toBe(true)
    expect(body.ipWhitelist).toEqual(['192.168.1.0/24'])
    expect(body.userAgentWhitelist).toEqual(['Mozilla/5.0'])
    expect(body.outputFormats).toEqual(['mpegTs', 'hls'])
    expect(body.magMac).toBe('AA:BB:CC:DD:EE:FF')
    expect(body.bouquetIds).toEqual(['bouquet-1'])
  })

  it('never sends a clear flag, because a create has nothing to clear', () => {
    const body = toCreateBody(values({ tariffPlanId: null, maxConnections: null }))

    expect(body).not.toHaveProperty('clearTariffPlan')
    expect(body).not.toHaveProperty('clearMaxConnections')
  })
})

describe('toUpdateBody', () => {
  it('derives clearTariffPlan from an empty plan select', () => {
    // The scenario the specification states as "clearing the plan nulls the reference and
    // re-activates the fields". A null identifier alone would be read as "keep".
    const body = toUpdateBody(values({ tariffPlanId: null }))

    expect(body.tariffPlanId).toBeNull()
    expect(body.clearTariffPlan).toBe(true)
  })

  it('treats an undefined plan id as a cleared plan, which is what antd actually stores', () => {
    // The runtime case the type cannot express: a cleared antd `Select` calls
    // `onChange(undefined)` and rc-field-form keeps the key, so the form store holds
    // `tariffPlanId: undefined` - never `null`. A `=== null` comparison answered false for it and
    // the body said `clearTariffPlan: false`, so the plan stayed attached while the form reported
    // "saved". The cast is the point of the test: it feeds the module the value antd produces.
    const body = toUpdateBody(
      values({ tariffPlanId: undefined as unknown as null }),
    )

    expect(body.tariffPlanId).toBeNull()
    expect(body.clearTariffPlan).toBe(true)
  })

  it('treats an undefined ceiling as unlimited, which is what antd actually stores', () => {
    const body = toUpdateBody(
      values({ tariffPlanId: null, maxConnections: undefined as unknown as null }),
    )

    expect(body.maxConnections).toBeNull()
    expect(body.clearMaxConnections).toBe(true)
  })

  it('does not clear the plan when one is attached', () => {
    const body = toUpdateBody(values({ tariffPlanId: 'plan-1' }))

    expect(body.tariffPlanId).toBe('plan-1')
    expect(body.clearTariffPlan).toBe(false)
  })

  it('derives clearMaxConnections from an empty ceiling', () => {
    // Without a plan the ceiling is the row's own, so an empty value is the operator asking for
    // "unlimited".
    const body = toUpdateBody(values({ tariffPlanId: null, maxConnections: null }))

    expect(body.maxConnections).toBeNull()
    expect(body.clearMaxConnections).toBe(true)
  })

  it('does not clear the ceiling when one is set', () => {
    const body = toUpdateBody(values({ maxConnections: 5 }))

    expect(body.maxConnections).toBe(5)
    expect(body.clearMaxConnections).toBe(false)
  })

  it('does not clear the ceiling while a plan owns it', () => {
    // A plan-attached save drops the dependent fields, so its null ceiling is "not sent" rather
    // than "unlimited". Sending the clear while a plan is attached would be a request about a
    // field the plan already decides.
    const body = toUpdateBody(values({ tariffPlanId: 'plan-1', maxConnections: null }))

    expect(body.clearMaxConnections).toBe(false)
  })

  it('keeps the two flags independent of each other', () => {
    // Clearing the plan does not clear the ceiling and the other way round: they are two fields
    // with two decisions, and one flag driven off the other would collapse them.
    const planOnly = toUpdateBody(values({ tariffPlanId: null, maxConnections: 7 }))
    expect(planOnly.clearTariffPlan).toBe(true)
    expect(planOnly.clearMaxConnections).toBe(false)

    const ceilingOnly = toUpdateBody(values({ tariffPlanId: null, maxConnections: null }))
    expect(ceilingOnly.clearTariffPlan).toBe(true)
    expect(ceilingOnly.clearMaxConnections).toBe(true)
  })

  it('sends an empty text field as empty rather than null, which is how a text field clears', () => {
    // The API's rule: an absent text property keeps, an empty string clears. A null playlist id
    // would be "keep", so a form that cleared the field must send the empty string.
    const cleared = toUpdateBody(values({ playlistId: '' }))
    expect(cleared.playlistId).toBe('')

    const kept = toUpdateBody(values({ playlistId: 'abc' }))
    expect(kept.playlistId).toBe('abc')
  })

  it('sends an empty list as empty, which is how a list clears', () => {
    const body = toUpdateBody(values({ ipWhitelist: [], userAgentWhitelist: [], bouquetIds: [] }))

    expect(body.ipWhitelist).toEqual([])
    expect(body.userAgentWhitelist).toEqual([])
    expect(body.bouquetIds).toEqual([])
  })

  it('carries the dependent fields the request stated, so a plan-less save stores them', () => {
    const body = toUpdateBody(
      values({ isRestreamer: true, isTrial: true, isCountryLock: true, outputFormats: ['hls'] }),
    )

    expect(body.isRestreamer).toBe(true)
    expect(body.isTrial).toBe(true)
    expect(body.isCountryLock).toBe(true)
    expect(body.outputFormats).toEqual(['hls'])
  })

  it('sends the empty string to clear an expiry, because null means "keep" on PATCH', () => {
    // The sentinel contract, pinned: an absent or null expiry keeps the stored one and only `''`
    // clears it. A "never expires" tick that sent null would look like a successful save that
    // changed nothing - the third time this form would have shipped that mistake.
    const cleared = toUpdateBody(values({ expireAt: '' }))

    expect(cleared.expireAt).toBe('')
    expect(cleared.expireAt).not.toBeNull()

    // An empty picker is *not* a statement, so it answers null and the row keeps its expiry.
    const kept = toUpdateBody(values({ expireAt: null }))
    expect(kept.expireAt).toBeNull()
  })

  it('folds the empty-string clear into "no expiry" on a create, which has nothing to keep', () => {
    expect(toCreateBody(values({ expireAt: '' })).expireAt).toBeNull()
    expect(toCreateBody(values({ expireAt: null })).expireAt).toBeNull()
  })

  it('does not send the owner, which never changes after the subscription is issued', () => {
    const body = toUpdateBody(values())

    expect(body).not.toHaveProperty('userId')
  })
})

describe('expireAtPickerProps', () => {
  it('converts the stored ISO string to a Dayjs the picker can validate', () => {
    // The regression this test exists for: handing `DatePicker` a raw string made a populated edit
    // form throw `getUDayjs(...).isValid is not a function`. The value must be a Dayjs object, and
    // it must be valid.
    const props = expireAtPickerProps('2027-03-04T15:30:00Z')

    expect(props.value).not.toBeNull()
    expect(typeof props.value?.isValid).toBe('function')
    expect(props.value?.isValid()).toBe(true)
    expect(props.value?.toISOString()).toBe('2027-03-04T15:30:00.000Z')
  })

  it('answers null for an expiry the form has not set', () => {
    expect(expireAtPickerProps(null).value).toBeNull()
    expect(expireAtPickerProps(undefined).value).toBeNull()
    expect(expireAtPickerProps('').value).toBeNull()
  })
})
