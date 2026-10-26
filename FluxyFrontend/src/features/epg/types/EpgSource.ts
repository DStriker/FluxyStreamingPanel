/**
 * One programme guide source, as `GET /admin/epg-sources` answers with it.
 *
 * The raw guide and everything parsed out of it are deliberately absent: both belong to the
 * updater, and neither is anything a screen draws. `lastEpgUpdatedAt` is present because the
 * table does draw it - "when was this guide refreshed last" - and it is null until the
 * updater has run once.
 */
export interface EpgSource {
  /** Identifier of the source, as text. The table shows it shortened. */
  id: string
  /** Display name, unique across the installation and compared case sensitively. */
  name: string
  /** Absolute http(s) address the guide is fetched from. */
  url: string
  /** How many days of programmes are kept for this source. */
  daysKeep: number
  /** When the guide was last refreshed, as an ISO timestamp, or null when it never has been. */
  lastEpgUpdatedAt: string | null
}
