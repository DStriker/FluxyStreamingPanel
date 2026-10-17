import type { ContentType } from './ContentType'

/**
 * One row of the content categories table: what a line of it draws.
 *
 * `GET /admin/content-categories?type=...` answers with these, already ordered by
 * `sortOrder` - the order the operator arranged them in on this very screen. A client that
 * draws the rows in order never needs to read `sortOrder`; it is on the row for the one
 * that wants to show or verify a position without re-deriving it from the index.
 *
 * `contentType` travels on every row even though the list was asked for by one kind, so an
 * item cached or carried across screens names what it is without a wrapper to remember the
 * question in.
 */
export interface ContentCategory {
  /** Identifier of the category, as text. */
  id: string
  /** Display name, unique within its content type, compared case sensitively. */
  name: string
  /** The kind of content this category sorts, as the name of the enum member. */
  contentType: ContentType
  /** Position among the categories of the same kind, lowest first. */
  sortOrder: number
}
