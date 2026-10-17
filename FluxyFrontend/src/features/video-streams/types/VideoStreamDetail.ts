import type { VideoStream } from './VideoStream'

/**
 * One stream in full, for the edit form: everything the operator can decide about it,
 * plus the one fact the relaying process reports back.
 *
 * `GET /admin/video-streams/{id}` answers with this. It is the list row plus the payload
 * the row deliberately left out, and the two lists arrive **in the order they are relayed
 * or played** - which is the order the form draws its fields in and the order a save sends
 * them back in. The server replaces both lists wholesale rather than patching them, so
 * what the form shows is exactly what the next save writes.
 *
 * The payload the chosen kind of stream does not use is empty here (`sources` for a series
 * stream, `files` for everything but the file playlist), which is what lets a form read a
 * stream and write it back without carrying a value the row never held.
 *
 * `status` and `activeSource` come along for the same reason they are on the row - the
 * form shows them as facts - but neither is ever sent back: `status` belongs to the start
 * and stop actions, and `activeSource` belongs to no writer at all.
 */
export interface VideoStreamDetail extends VideoStream {
  /** Enigma2 service reference as text, or null. */
  enigma2Sid: string | null
  /** How far behind live the relay runs, in minutes. Zero means no deliberate delay. */
  delayMinutes: number
  /** Series the stream plays, as text, or null. Used by `OnlineSeries` and nothing else. */
  seriesId: string | null
  /** On-demand item the stream plays, as text, or null. Used by `VodPlaylist`. */
  vodId: string | null
  /** Programme guide the schedule is read from, as text, or null. */
  epgSourceId: string | null
  /** Key the channel is filed under inside that guide, or null. */
  epgChannelKey: string | null
  /** How many days of archive are kept. Zero for a stream that keeps none. */
  archiveDays: number
  /** User agent the relayed requests carry, or null. */
  restreamUserAgent: string | null
  /** Proxy the relay goes out through, or null. */
  restreamProxy: string | null
  /** Cookies the relayed requests carry, or null. */
  restreamCookies: string | null
  /** Extra headers the relayed requests carry, as typed, or null. */
  restreamHeaders: string | null
  /** Sources to relay from, in order, the first as the primary and the rest as fallback. */
  sources: string[]
  /** Files to play, in order. Empty for every kind but `FilePlaylist`. */
  files: string[]
}
