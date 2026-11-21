import type { MagContainer } from './MagContainer'
import type { MagTheme } from './MagTheme'

/**
 * `GET /admin/mag-settings` - the MAG portal's one-row configuration.
 *
 * Both model lists are canonical spellings from the STB catalogue in the operator's order, and
 * the PATCH requires the same eleven fields back - so this type does double duty as the write
 * body's shape; `MagSettingsValues` is an explicit alias rather than a second interface, because
 * there is no asymmetry here to justify one.
 */
export interface MagSettings {
  /** Whether every category is shown. */
  showAllCategories: boolean
  /** Whether the strict security checks are enforced. */
  strictSecurity: boolean
  /** Whether channel logos are drawn. */
  showChannelLogos: boolean
  /** Whether preview logos are drawn. */
  showPreviewLogos: boolean
  /** Container the portal delivers by default. */
  defaultContainer: MagContainer
  /** Theme the portal is shown with by default, in the owner's spelling. */
  defaultTheme: MagTheme
  /** Longest recording the portal allows, in minutes. */
  recordingMaxLengthMinutes: number
  /** How many local recordings a device may keep; `0` allows none. */
  maxLocalRecordings: number
  /** How many simultaneous viewers one device may have. */
  playbackLimit: number
  /** Set-top-box models allowed to connect, in catalogue order. */
  allowedStbTypes: string[]
  /** Set-top-box models allowed to record. */
  allowedRecordingStbTypes: string[]
}
