import type { BouquetMember } from './BouquetMember'

/**
 * The two lists one tab of the bouquet content manager draws.
 *
 * Both arrive in one answer, because they are two halves of one sentence - an item deleted
 * between two requests would be missing from both, and one added in between would appear
 * in both at once. Neither is paged: the manager moves items between two complete sets.
 *
 * `members` is in the order the bouquet holds, which is the operator's order and the one
 * the next save writes back. `available` is everything of that kind the bouquet does not
 * hold, ordered by name.
 */
export interface BouquetContentLists {
  /** What the bouquet holds of this kind, in the order it holds it. */
  members: BouquetMember[]
  /** Everything of this kind the bouquet does not hold, ordered by name. */
  available: BouquetMember[]
}
