import dayjs from 'dayjs'
import type { SubscriptionOutputFormat } from './types'

/**
 * Every setting a subscription carries - the body of both writes.
 *
 * A value shape rather than a contract of one endpoint, because the form and both mappings below
 * speak it: the create and the update differ in which fields they send, not in what the operator
 * typed.
 */
export interface SubscriptionValues {
  /** Owner, only sent on create; the server ignores it on an update. */
  userId: string | null
  /** The playlist service's identifier, or null. */
  playlistId: string | null
  /**
   * Expiry as an ISO 8601 date-time, `''` to clear it, or null for "not stated".
   *
   * The three states are the contract's, not this form's invention: on a change an absent or null
   * value keeps the stored expiry, and only the empty string clears it. A create treats `''` and
   * null the same way (no expiry).
   */
  expireAt: string | null
  /** The tariff plan it is sold under, or null. */
  tariffPlanId: string | null
  /** Connection ceiling, or null for unlimited. */
  maxConnections: number | null
  /** Whether the subscriber may restream. */
  isRestreamer: boolean
  /** Addresses it may be watched from. Empty means no restriction. */
  ipWhitelist: string[]
  /** User agents it may be watched with. Empty means no restriction. */
  userAgentWhitelist: string[]
  /** Whether it is pinned to one provider. */
  isIspLock: boolean
  /** Whether it is pinned to one country. */
  isCountryLock: boolean
  /** Whether it is a trial. */
  isTrial: boolean
  /** The formats it is delivered in. At least one. */
  outputFormats: SubscriptionOutputFormat[]
  /** MAG device's MAC, or null. */
  magMac: string | null
  /** Whether MAG identifiers are checked strictly. */
  isStbLock: boolean
  /** Enigma2 device's MAC, or null. */
  enigma2Mac: string | null
  /** The bouquets it carries while no plan is attached. */
  bouquetIds: string[]
}

/**
 * The body a create sends: every field, with the two "clear" flags left out.
 *
 * A create has nothing to clear, so the two flags would be a statement about a row that does not
 * exist yet. The owner travels because a create is the one request that chooses it.
 *
 * The two nullable values are sent as an explicit `null` rather than omitted: an absent key and a
 * null key mean the same thing to this endpoint, and writing the null states "no plan" and "no
 * ceiling" rather than leaving a reader to infer it from what is missing.
 */
export const toCreateBody = (values: SubscriptionValues) => ({
  userId: values.userId,
  playlistId: values.playlistId,
  // A create has no stored expiry to keep, so the empty-string clear collapses into "none" here.
  expireAt: values.expireAt || null,
  tariffPlanId: values.tariffPlanId ?? null,
  maxConnections: values.maxConnections ?? null,
  isRestreamer: values.isRestreamer,
  ipWhitelist: values.ipWhitelist,
  userAgentWhitelist: values.userAgentWhitelist,
  isIspLock: values.isIspLock,
  isCountryLock: values.isCountryLock,
  isTrial: values.isTrial,
  outputFormats: values.outputFormats,
  magMac: values.magMac,
  isStbLock: values.isStbLock,
  enigma2Mac: values.enigma2Mac,
  bouquetIds: values.bouquetIds,
})

/**
 * The body an update sends.
 *
 * The whole form travels rather than a diff, for the reason every other form here does: one screen
 * holds one subscription, so what the operator sees and what the row becomes are the same
 * statement.
 *
 * **The two clear flags are derived from the values, never stored on the form.** A nullable field
 * on this API distinguishes "leave it alone" from "set it to nothing" by a companion flag
 * (`clearTariffPlan`, `clearMaxConnections`), and the transport reads an absent identifier as
 * "keep". So an empty plan select has to say *both* things at once: the identifier null **and** the
 * flag true. Reading the flag off the form would need a second piece of state kept in step with the
 * first, and the day they disagreed an operator would clear the plan, save successfully, and find
 * it still attached - which is exactly what this derivation exists to prevent.
 *
 * The comparison is `== null` rather than `=== null`, and the identifier is written out as
 * `?? null`, because **the form store can hold `undefined` here**: a cleared antd `Select` calls
 * `onChange(undefined)` and rc-field-form keeps the key, so the runtime value is a state the
 * declared `string | null` cannot express. `=== null` would answer false for it and send
 * `clearTariffPlan: false` with the identifier dropped by JSON - a "save" that keeps the old plan.
 * The boundary in `SubscriptionFormPage.currentValues` also coerces it, and this stays tolerant so
 * the pure module is correct for a value its own type says cannot arrive.
 */
export const toUpdateBody = (values: SubscriptionValues) => ({
  playlistId: values.playlistId,
  // Passed through unchanged on purpose: `''` is the clear and null is "keep", and folding either
  // into the other is exactly the bug this form has already shipped three times.
  expireAt: values.expireAt,
  tariffPlanId: values.tariffPlanId ?? null,
  clearTariffPlan: values.tariffPlanId == null,
  maxConnections: values.maxConnections ?? null,
  // The ceiling is plan-owned: a null while a plan is attached is the form *not sending* the
  // field, not the operator asking for "unlimited". Only a plan-less save can clear it.
  clearMaxConnections: values.tariffPlanId == null && values.maxConnections == null,
  isRestreamer: values.isRestreamer,
  ipWhitelist: values.ipWhitelist,
  userAgentWhitelist: values.userAgentWhitelist,
  isIspLock: values.isIspLock,
  isCountryLock: values.isCountryLock,
  isTrial: values.isTrial,
  outputFormats: values.outputFormats,
  magMac: values.magMac,
  isStbLock: values.isStbLock,
  enigma2Mac: values.enigma2Mac,
  bouquetIds: values.bouquetIds,
})

/**
 * The props antd's `DatePicker` needs, derived from the wire string the form stores.
 *
 * `DatePicker` requires a `Dayjs` object and calls `.isValid()` on whatever it is handed, so an
 * ISO string passed straight through throws `getUDayjs(...).isValid is not a function` - which is
 * exactly what a populated edit form did. Converting at the field boundary (the pattern
 * `VideoOnDemandFormPage` uses for its release date) keeps the store on the contract's string shape
 * while the control gets the type it can actually use.
 *
 * Extracted rather than written inline so a unit test can pin the conversion: every render of the
 * page in `probe:render` lands before the row arrives, so a probe can never see this path.
 */
export const expireAtPickerProps = (value?: string | null): { value: dayjs.Dayjs | null } => ({
  value: value ? dayjs(value) : null,
})
