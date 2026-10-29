import type { SubscriptionDerived } from './SubscriptionDerived'
import type { SubscriptionOutputFormat } from './SubscriptionOutputFormat'
import type { SubscriptionStatus } from './SubscriptionStatus'

/**
 * One subscription in full, as the edit form reads it.
 *
 * The stored values and the plan-derived ones are both present, and that is deliberate: the form
 * has to *show* what the plan decides while it is attached, and it also has to know what the row
 * carries underneath so clearing the plan restores something real rather than a guess.
 */
export interface SubscriptionDetail {
  /** Identifier of the subscription, as text. */
  id: string
  /** Identifier of the owner, as text. */
  userId: string
  /** Login name of the owner. */
  username: string
  /** Identifier of the issuer, as text, or null. */
  resellerId: string | null
  /** Login name of the issuer, or null. */
  creatorUsername: string | null
  /** The playlist service's identifier, or null. */
  playlistId: string | null
  /** When it lapses, or null for "never expires". */
  expireAt: string | null
  /** Identifier of the tariff plan, as text, or null. */
  tariffPlanId: string | null
  /** Name of the tariff plan, or null. */
  tariffPlanName: string | null
  /** The computed state. */
  status: SubscriptionStatus
  /** Stored connection ceiling, or null for unlimited. */
  maxConnections: number | null
  /** Stored restreaming flag. */
  isRestreamer: boolean
  /** Stored addresses, exact or CIDR. Empty means no restriction. */
  ipWhitelist: string[]
  /** Stored user agents. Empty means no restriction. */
  userAgentWhitelist: string[]
  /** Stored provider lock flag. */
  isIspLock: boolean
  /** Stored country lock flag. */
  isCountryLock: boolean
  /** Stored trial flag. */
  isTrial: boolean
  /** Stored formats. */
  outputFormats: SubscriptionOutputFormat[]
  /** Stored MAG MAC, or null. */
  magMac: string | null
  /** Stored STB lock flag. */
  isStbLock: boolean
  /** Stored Enigma2 MAC, or null. */
  enigma2Mac: string | null
  /** The subscription's own bouquets, dormant while a plan is attached. */
  bouquetIds: string[]
  /** When the subscription was issued. */
  createdAt: string
  /** The plan-derived values while a plan is attached, or null. */
  derived: SubscriptionDerived | null
}
