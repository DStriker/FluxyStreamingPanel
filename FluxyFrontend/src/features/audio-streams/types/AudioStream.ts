import type { AudioStreamStatus } from './AudioStreamStatus'
import type { AudioStreamType } from './AudioStreamType'

/**
 * One row of the audio streams table: the eight things a line of it draws, and nothing else.
 *
 * `GET /admin/audio-streams` answers with these. Kind and state travel as the name of the
 * enum member - `OnDemand`, `Running` - the same spelling the accounts table spells its
 * own pair with, because that is what a client already matches on.
 *
 * The restreaming parameters and the source list are deliberately absent, for the reason the
 * backend's `AudioStreamListItem` gives: the table does not draw them, and a list endpoint
 * that answered with all of them would push twenty full rows over the wire to draw eight
 * columns. They arrive on `AudioStreamDetail`, once, when a form asks.
 *
 * `activeSource` is the mirror image of every other field here: nothing in the interface
 * writes it. It names the source the relaying process is using right now, which only that
 * process can know - so it is read on a row and offered nowhere.
 */
export interface AudioStream {
  /** Identifier of the stream, as text. The table shows it shortened. */
  id: string
  /** Display name, unique within its category and compared case sensitively. */
  name: string
  /** Whether the stream is active or disabled, as the name of the enum member. */
  status: AudioStreamStatus
  /** The kind of stream, which decides how it is played. */
  type: AudioStreamType
  /** Identifier of the category the stream is filed under, as text. */
  categoryId: string
  /** Display name of that category, which is what the column draws. */
  categoryName: string
  /** Address the icon is fetched from, or null when the stream has none. */
  logoUrl: string | null
  /** Source the relaying process is using right now, or null while none is relaying. */
  activeSource: string | null
}
