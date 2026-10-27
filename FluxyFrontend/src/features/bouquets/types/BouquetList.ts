import type { Bouquet } from './Bouquet'

/**
 * Everything the bouquets endpoint answers: the whole list, lowest position first.
 *
 * Deliberately a list and not a page - there is no `total`, no `page` and no `pageSize`,
 * because there is no pager. The bouquets are a screenful of packages by construction.
 */
export interface BouquetList {
  items: Bouquet[]
}
