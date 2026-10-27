import { apiFetch, apiGet } from '../../utils/http'
import type { MessageResponse } from '../../types'
import type { BouquetContentLists, BouquetList } from './types'

/**
 * The bouquets - the named packages of content - and the content each one holds.
 *
 * Two shapes live here, and the difference between them is the whole domain. The package
 * itself is a name and a position among the packages, which is what the table's drag
 * re-arranges. What a package holds is four lists, one per kind of content, each with its
 * own order, which is what the content manager edits.
 */

/**
 * The four kinds of content, as the text the server parses them from.
 *
 * A union rather than `string` so that a fifth name is a compile error here instead of a
 * 400 from an endpoint the caller has already reached. The server parses case
 * insensitively; the spelling here is the general one the categories feature also uses,
 * which is the enum member's own in lower case.
 *
 * Deliberately repeated rather than imported from `features/content-categories/api.ts`:
 * a feature imports neither another feature nor a page, and the five words are cheaper to
 * repeat than the rule is to break.
 */
export type ContentTypeParam = 'videostream' | 'audiostream' | 'videoondemand' | 'series'

/** Every kind, in the order the four tabs are drawn in. */
export const CONTENT_TYPE_TABS: readonly ContentTypeParam[] = [
  'videostream',
  'audiostream',
  'videoondemand',
  'series',
]

/**
 * Every bouquet with the counts of what it holds: `GET /admin/bouquets`.
 *
 * A plain GET behind the admin session, so no antiforgery token - nothing is changed. The
 * answer is a list and not a page: the bouquets are a screenful of packages by
 * construction, and the order the rows arrive in *is* the feature of the screen.
 *
 * The same call feeds the bouquet picker on the four content forms, which is why the rows
 * carry their counts rather than a second endpoint carrying them for the table alone.
 */
export const getBouquets = ({ signal }: { signal?: AbortSignal } = {}): Promise<BouquetList> =>
  apiGet<BouquetList>('/admin/bouquets', signal)

/**
 * Creates a bouquet: `POST /admin/bouquets`, answered with `201 bouquet_created` and a
 * `Location` naming it.
 *
 * There is deliberately no position in the body: a new bouquet joins the end of the list,
 * and where "the end" is (the highest position plus one) is the server's fact to read.
 *
 * A name another bouquet holds *in any spelling* is `409 bouquet_already_exists` with the
 * field named in `errors`, so the dialog shows it under the input rather than as a toast.
 */
export const createBouquet = ({
  name,
  csrfToken,
}: {
  name: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/admin/bouquets', { method: 'POST', body: { name }, csrfToken })

/**
 * Renames a bouquet: `PATCH /admin/bouquets/{id}`, answered with `200 bouquet_updated`.
 *
 * The name is the one field a bouquet can be given that it does not already have - its
 * position is not written one row at a time, the list is re-arranged wholesale through
 * `reorderBouquets`.
 */
export const renameBouquet = ({
  id,
  name,
  csrfToken,
}: {
  id: string
  name: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/bouquets/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body: { name },
    csrfToken,
  })

/**
 * Deletes a bouquet: `DELETE /admin/bouquets/{id}`, answered with `200 bouquet_deleted`.
 *
 * Refused with `409 bouquet_not_empty` while anything is inside it, naming how many items
 * stand in the way. That refusal is the server's rule rather than the UI's advice: the
 * button on the row is disabled for the same reason, so a stale screen is answered rather
 * than obeyed.
 */
export const deleteBouquet = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/bouquets/${encodeURIComponent(id)}`, { method: 'DELETE', csrfToken })

/**
 * Replaces the order of the bouquets with the list sent: `PUT /admin/bouquets/order`,
 * answered with `200 bouquets_reordered`.
 *
 * The identifiers travel as the **full ordered list** rather than as one moved row with
 * its new position: one write of positions 1..N over the named rows is atomic where a
 * moved row plus shifting its neighbours is several. The server refuses a subset, a
 * duplicate or a foreign identifier rather than guessing what the unnamed rows should now
 * be at.
 */
export const reorderBouquets = ({
  orderedIds,
  csrfToken,
}: {
  orderedIds: string[]
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/admin/bouquets/order', { method: 'PUT', body: { orderedIds }, csrfToken })

/**
 * The two lists one tab of the content manager draws:
 * `GET /admin/bouquets/{id}/content?contentType=...`.
 *
 * Both lists in one answer, because they are two halves of one sentence - an item deleted
 * between two requests would be missing from both, and one added in between would appear
 * in both at once. Neither list is paged: the manager moves items between two complete
 * sets, and a pager over either half would make "select all" mean "the page I am looking
 * at".
 */
export const getBouquetContent = ({
  id,
  type,
  signal,
}: {
  id: string
  type: ContentTypeParam
  signal?: AbortSignal
}): Promise<BouquetContentLists> =>
  apiGet<BouquetContentLists>(
    `/admin/bouquets/${encodeURIComponent(id)}/content?${new URLSearchParams({ contentType: type }).toString()}`,
    signal,
  )

/**
 * Replaces what the bouquet holds of one kind, in the order sent:
 * `PUT /admin/bouquets/{id}/content`, answered with `200 bouquet_content_set`.
 *
 * **The order in the body is the order the bouquet holds.** Positions are written 1..N in
 * exactly that sequence, so what the operator arranged on screen is what the next read
 * answers - which is the whole promise of the drag handles and of the A-Z button.
 *
 * An empty list is a statement rather than an omission: it empties the bouquet of that
 * kind. The body always carries the full list, never a diff.
 */
export const setBouquetContent = ({
  id,
  type,
  contentIds,
  csrfToken,
}: {
  id: string
  type: ContentTypeParam
  contentIds: string[]
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/bouquets/${encodeURIComponent(id)}/content`, {
    method: 'PUT',
    body: { contentType: type, contentIds },
    csrfToken,
  })
