import type { TariffPlanDurationUnit } from './TariffPlanDurationUnit'

/**
 * One row of the tariff plans table: every setting the plan carries, plus the counts of its
 * two relations.
 *
 * The identifiers of the relations arrive only on the detail - a page of plans draws how many
 * groups may buy each one and how many bouquets it delivers, never the sets themselves. The
 * row's fields are the ones the table draws, so a column added to the server without a field
 * here is a column this screen cannot show.
 */
export interface TariffPlan {
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
  /** How many user groups may be sold the plan. */
  userGroupCount: number
  /** How many bouquets the plan delivers. */
  bouquetCount: number
}
