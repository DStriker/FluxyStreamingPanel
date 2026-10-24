/**
 * Whether this installation can transcode, and the formats the menu offers.
 *
 * `available` is a fact about the server rather than about one video, so the table asks it
 * once and can draw the action disabled with a reason instead of offering five buttons whose
 * every press would be refused.
 */
export interface VideoOnDemandTranscodeAvailability {
  /** Whether a transcoder is configured. */
  available: boolean
  /** Format names, in the order the menu draws them. */
  formats: string[]
}
