import type { VideoStreamStatus } from './VideoStreamStatus'
import type { VideoStreamType } from './VideoStreamType'

/**
 * One row of the streams table: the ten things a line of it draws, and nothing else.
 *
 * `GET /admin/video-streams` answers with these. Kind and state travel as the name of the
 * enum member - `Permanent`, `Running` - the same spelling the accounts table spells its
 * own pair with, because that is what a client already matches on.
 *
 * The restreaming parameters, the payload and the archive length are deliberately absent,
 * for the reason the backend's `VideoStreamListItem` gives: the table does not draw them,
 * and a list endpoint that answered with all of them would push twenty full rows over the
 * wire to draw ten columns. They arrive on `VideoStreamDetail`, once, when a form asks.
 *
 * `hasEpg` is the one field that is not a column. Whether a stream carries a programme
 * guide is a question about two fields (`epgSourceId` and `epgChannelKey`), and the server
 * answers it once per row so the table never has to know the answer has two halves.
 *
 * `activeSource` is the mirror image of every other field here: nothing in the interface
 * writes it. It names the source the relaying process is using right now, which only that
 * process can know - so it is read on a row and offered nowhere.
 */
export interface VideoStream {
  /** Identifier of the stream, as text. The table shows it shortened. */
  id: string
  /** Display name, unique within its category and compared case sensitively. */
  name: string
  /** Whether the stream is broadcasting right now, as the name of the enum member. */
  status: VideoStreamStatus
  /** The kind of stream, which decides what it broadcasts. */
  type: VideoStreamType
  /** Identifier of the category the stream is filed under, as text. */
  categoryId: string
  /** Display name of that category, which is what the column draws. */
  categoryName: string
  /** Address the icon is fetched from, or null when the stream has none. */
  logoUrl: string | null
  /** Source the relaying process is using right now, or null while none is relaying. */
  activeSource: string | null
  /** Whether the stream carries a programme guide at all. */
  hasEpg: boolean
  /** Position among the streams of the same category, lowest first. */
  sortOrder: number
}
