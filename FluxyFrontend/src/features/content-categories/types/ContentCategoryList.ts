import type { ContentCategory } from './ContentCategory'

/**
 * `GET /admin/content-categories?type=...` - every category of one kind of content, in the
 * order the operator arranged them in.
 *
 * Deliberately a list and not a page: there is no `total`, no `page` and no `pageSize`,
 * because there is no pager. A type's categories are a screenful by construction - they are
 * the section headings of a content grid, not a per-row record - and a pager over a list
 * that never outgrows one page would promise a second page that has no reason to exist.
 */
export interface ContentCategoryList {
  /** Every category of the type, lowest position first. */
  items: ContentCategory[]
}
