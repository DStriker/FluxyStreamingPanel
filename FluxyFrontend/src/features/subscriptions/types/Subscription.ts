import type { SubscriptionStatus } from './SubscriptionStatus'
import type { TariffPlanDurationUnit } from './TariffPlanDurationUnit'

/**
 * One row of the subscriptions table: what the main row and its service subrows draw.
 *
 * The fields are the *effective* ones - a subscription with a plan shows the plan's connection
 * ceiling, trial flag and service support, because that is what it would actually deliver. The
 * three support flags are what the subrows' "add a service" line is gated on, so the page can
 * decide which buttons to draw without a second call per row.
 */
export interface Subscription {
  /** Identifier of the subscription, as text. */
  id: string
  /** Identifier of the owner, as text. */
  userId: string
  /** Login name of the owner. */
  username: string
  /** Whether the owner's effective status is Blocked. */
  ownerBlocked: boolean
  /** Login name of the issuer, or null when that account has been deleted. */
  creatorUsername: string | null
  /** Identifier of the tariff plan, as text, or null. */
  tariffPlanId: string | null
  /** Name of that plan, or null. */
  tariffPlanName: string | null
  /**
   * How many `tariffPlanDurationUnit` the attached plan lasts for, or null when there is none.
   *
   * The prolongation dialog projects the new expiry before asking, and the projection has to use
   * the server's own rule - `max(now, expiry) + the plan's duration` - so the two numbers travel
   * with the page the table already reads rather than costing a request per dialog open.
   */
  tariffPlanDurationValue: number | null
  /** The unit that length is counted in, or null when there is no plan. */
  tariffPlanDurationUnit: TariffPlanDurationUnit | null
  /** When the subscription was issued. */
  createdAt: string
  /** When it lapses, or null for "never expires". */
  expireAt: string | null
  /** Whether the subscription counts as a trial - the plan's answer when one is attached. */
  isTrial: boolean
  /** The computed state. */
  status: SubscriptionStatus
  /** Effective connection ceiling, or null for unlimited. */
  maxConnections: number | null
  /** The playlist service's identifier, or null. */
  playlistId: string | null
  /** The MAG device's MAC, or null. */
  magMac: string | null
  /** The Enigma2 device's MAC, or null. */
  enigma2Mac: string | null
  /** Whether the effective plan allows a playlist. */
  supportsPlaylist: boolean
  /** Whether the effective plan allows a MAG device. */
  supportsMag: boolean
  /** Whether the effective plan allows an Enigma2 device. */
  supportsEnigma2: boolean
}
