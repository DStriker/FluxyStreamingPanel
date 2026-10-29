import type { SubscriptionOutputFormat } from './SubscriptionOutputFormat'

/**
 * What the attached tariff plan decides for one subscription, as the edit form draws it disabled.
 *
 * A separate block from the stored fields because the two are different facts: the stored value is
 * what the row carries underneath, and this is what the plan decides while it is attached. The
 * form shows this one while a plan is selected and the stored one once it is cleared.
 */
export interface SubscriptionDerived {
  /** Connection ceiling the plan allows, or null for unlimited. */
  maxConnections: number | null
  /** Whether the plan allows restreaming. */
  isRestreamer: boolean
  /** The formats the plan delivers. */
  outputFormats: SubscriptionOutputFormat[]
  /** Whether the plan's MAG identifiers are checked strictly. */
  isStbLock: boolean
  /** Whether the plan pins the subscription to one country. */
  isCountryLock: boolean
  /** Whether the plan is a trial. */
  isTrial: boolean
  /** Whether the plan delivers a playlist. */
  supportsPlaylist: boolean
  /** Whether the plan delivers to a MAG device. */
  supportsMag: boolean
  /** Whether the plan delivers to an Enigma2 device. */
  supportsEnigma2: boolean
  /** The bouquets the plan delivers, as text. */
  bouquetIds: string[]
}
