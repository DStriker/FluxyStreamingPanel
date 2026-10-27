import type { AudioStream } from './AudioStream'

/**
 * One audio stream in full, for the edit form: everything the operator can decide about it,
 * plus the one fact the relaying process reports back.
 *
 * `GET /admin/audio-streams/{id}` answers with this. It is the list row plus the payload
 * the row deliberately left out, and the sources arrive **in the order they are relayed** -
 * which is the order the form draws its fields in and the order a save sends them back in.
 * The server replaces the list wholesale rather than patching it, so what the form shows is
 * exactly what the next save writes.
 *
 * `status` and `activeSource` come along for the same reason they are on the row - the form
 * shows them as facts - and only one of them travels back: the form's own status picker
 * sends the state the operator chose, while `activeSource` belongs to the relaying process
 * and no request in this interface writes it.
 */
export interface AudioStreamDetail extends AudioStream {
  /** Enigma2 service reference as text, or null. */
  enigma2Sid: string | null
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
  /**
   * Bouquets the stream belongs to, in the bouquets' own order. A **set**: it fills the
   * picker on the form and says nothing about the stream's position inside one.
   */
  bouquetIds: string[]
}
