import type { TariffPlanDurationUnit } from './TariffPlanDurationUnit'

/**
 * One tariff plan in full, as the edit form reads it: every setting plus the identifiers of
 * the two relations, so its pickers open on what the plan actually has.
 *
 * A separate type from `TariffPlan` rather than an extension of it, because the detail is the
 * only shape that carries `userGroupIds` and `bouquetIds` - a list row that pretended to have
 * them would be a row whose arrays are always empty, which is exactly the kind of lie a type
 * should not tell.
 */
export interface TariffPlanDetail {
  /** Identifier of the plan, as text. */
  id: string
  /** Display name, unique for the installation without regard to case. */
  name: string
  /** Whether the plan is a free trial. */
  isTrial: boolean
  /** Cost in credits. Zero is a free plan. */
  cost: number
  /** How much of `durationUnit` the plan lasts for. */
  durationValue: number
  /** The unit the validity period is counted in. */
  durationUnit: TariffPlanDurationUnit
  /** Whether the plan may be delivered as an M3U playlist. */
  supportsM3u: boolean
  /** Whether the plan may be delivered to a MAG set-top box. */
  supportsMag: boolean
  /** Whether the plan may be delivered to an Enigma2 receiver. */
  supportsEnigma2: boolean
  /** Whether the plan may be delivered as an MPEG-TS stream. */
  outputMpegTs: boolean
  /** Whether the plan may be delivered as an HLS stream. */
  outputHls: boolean
  /** Whether a MAG device's STB identifier is checked strictly. */
  strictStbDeviceId: boolean
  /** Whether a subscriber may restream the subscription. */
  allowRestream: boolean
  /** How many devices may be connected at the same time. */
  maxConnections: number
  /** Whether connections from different countries are allowed at the same time. */
  allowMultipleCountries: boolean
  /** Every group that may be sold the plan. */
  userGroupIds: string[]
  /** Every bouquet the plan delivers. */
  bouquetIds: string[]
}
