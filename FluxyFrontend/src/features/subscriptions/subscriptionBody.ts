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
  /** Expiry as `yyyy-MM-dd`, or null for "never expires". */
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
 */
export const toCreateBody = (values: SubscriptionValues) => ({
  userId: values.userId,
  playlistId: values.playlistId,
  expireAt: values.expireAt,
  tariffPlanId: values.tariffPlanId,
  maxConnections: values.maxConnections,
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
 */
export const toUpdateBody = (values: SubscriptionValues) => ({
  playlistId: values.playlistId,
  expireAt: values.expireAt,
  tariffPlanId: values.tariffPlanId,
  clearTariffPlan: values.tariffPlanId === null,
  maxConnections: values.maxConnections,
  clearMaxConnections: values.maxConnections === null,
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
