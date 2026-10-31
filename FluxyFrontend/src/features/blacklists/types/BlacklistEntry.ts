import type { BlacklistType } from './BlacklistType'

/**
 * One blacklist entry, as `GET /admin/blacklist-entries` answers with it.
 *
 * The counter and the added date are present because the table draws them; neither is
 * writable through this API - the enforcement increments the counter, and the added date is
 * stamped once when the row is inserted.
 */
export interface BlacklistEntry {
  /** Identifier of the entry, as text. The table shows it shortened. */
  id: string
  /** Which of the three lists the entry belongs to. */
  type: BlacklistType
  /** The value that is refused: an address for the IP list, the literal text otherwise. */
  value: string
  /** How many attempts the entry has blocked. Starts at zero and is read-only here. */
  blockedAttempts: number
  /** Free-text note an operator left beside the entry, or null when there is none. */
  note: string | null
  /** When the entry was added, as an ISO timestamp. */
  createdAt: string
}
