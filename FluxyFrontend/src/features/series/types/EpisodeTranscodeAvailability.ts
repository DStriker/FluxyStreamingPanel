/**
 * Whether this installation can transcode episodes, and the formats the menu offers.
 *
 * `available` is a fact about the server rather than about one episode, so the table asks it
 * once and draws the action disabled with a reason.
 */
export interface EpisodeTranscodeAvailability {
  /** Whether a transcoder is configured. */
  available: boolean
  /** Format names, in the order the menu draws them. */
  formats: string[]
}
