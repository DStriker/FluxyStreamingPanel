import { apiFetch, apiGet } from '../../utils/http'
import type { MessageResponse } from '../../types'
import type { ContentCategoryList, ContentType } from './types'

/**
 * The categories each kind of content is filed under: the list in the order the operator
 * arranged it, and the four writes a category has - create, rename, delete, and the one
 * reorder that replaces a whole type's arrangement.
 *
 * The list is a list rather than a page: a type's categories are a screenful by
 * construction, and the order they arrive in is the feature of the screen.
 */

/**
 * The four kinds of content, as the text the server parses them from.
 *
 * A union rather than `string` so that a fifth name is a compile error here instead of a
 * 400 from an endpoint the caller has already reached. The server parses case
 * insensitively; the spelling here is the enum member's own, the same convention every
 * other contract on both sides uses for `role` and `status`.
 */
export type ContentTypeParam =
  | 'videostream'
  | 'audiostream'
  | 'videoondemand'
  | 'series'

/** The query-string spelling each `ContentType` member is sent under. */
export const CONTENT_TYPE_PARAMS: Record<ContentType, ContentTypeParam> = {
  VideoStream: 'videostream',
  AudioStream: 'audiostream',
  VideoOnDemand: 'videoondemand',
  Series: 'series',
}

/**
 * Every category of one kind of content, in the order the operator arranged them in:
 * `GET /admin/content-categories?type=...`.
 *
 * A plain GET behind the admin session, so no antiforgery token - nothing is changed. The
 * answer is a list and not a page, on purpose: a type's categories are a screenful by
 * construction, the server pages nothing and the response carries no `total` - which is
 * also why there is no search, no filter and no sort here. The order the rows arrive in
 * *is* the feature of this screen, and re-sorting them in the browser would put a
 * different order under the drag handles than the one a drop would send back.
 */
export const getContentCategories = ({
  type,
  signal,
}: {
  type: ContentTypeParam
  signal?: AbortSignal
}): Promise<ContentCategoryList> =>
  apiGet<ContentCategoryList>(
    `/admin/content-categories?${new URLSearchParams({ type }).toString()}`,
    signal,
  )

/**
 * What the add-category form sends: the name and the kind it belongs to, and nothing else.
 *
 * There is deliberately no position here. A new category joins the end of its type's list,
 * and where "the end" is (the highest position plus one) is the server's fact to read - a
 * caller naming a position would be one stale screen away from overwriting a row an
 * operator has just moved.
 */
export interface NewContentCategoryValues {
  name: string
  type: ContentTypeParam
}

/**
 * Creates a category: `POST /admin/content-categories`, answered with
 * `201 content_category_created` and a `Location` naming it.
 *
 * A write, so the antiforgery token is minted at call time and never cached. There is no
 * captcha on this endpoint on purpose: the caller already holds an `AdminOnly` session,
 * which is a stronger gate than a score from reCAPTCHA (the backend's recorded departure
 * for the whole admin area).
 *
 * A name another category of the same type holds is `409 content_category_already_exists`
 * with the field named in `errors`, so the form shows it under the input rather than as a
 * toast. The same name over a *different* type is a different row and is accepted - two
 * sections of two different screens may each be called "Sports".
 */
export const createContentCategory = ({
  values,
  csrfToken,
}: {
  values: NewContentCategoryValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/admin/content-categories', {
    method: 'POST',
    body: { name: values.name, type: values.type },
    csrfToken,
  })

/**
 * Renames a category: `PATCH /admin/content-categories/{id}`, answered with
 * `200 content_category_updated`.
 *
 * The name is the one field a category can be given that it does not already have: its
 * type never moves and its position is not written one row at a time - the list is
 * re-arranged wholesale through `reorderContentCategories`. An absent name is a no-op the
 * server answers with `content_category_updated`, the convention every other PATCH here
 * follows.
 */
export const updateContentCategory = ({
  id,
  name,
  csrfToken,
}: {
  id: string
  name?: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/content-categories/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body: name === undefined ? {} : { name },
    csrfToken,
  })

/**
 * Deletes a category: `DELETE /admin/content-categories/{id}`, answered with
 * `200 content_category_deleted`.
 *
 * The only action on this page that asks first, because it is the only one that cannot be
 * undone. There is no "in use" refusal on the server yet - no table of content items
 * exists to be in one - so the confirmation dialog is the whole of the guard, which is
 * also why the dialog names the category rather than warning about members.
 */
export const deleteContentCategory = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/content-categories/${encodeURIComponent(id)}`, {
    method: 'DELETE',
    csrfToken,
  })

/**
 * Replaces the order of one type's categories with the list sent:
 * `PUT /admin/content-categories/order`, answered with
 * `200 content_categories_reordered`.
 *
 * The identifiers travel as the **full ordered list** rather than as one moved row with
 * its new position: one write of positions 1..N over the named rows is atomic where a
 * moved row plus shifting the neighbours is several, and the caller has just dragged on a
 * screen that shows the entire list, so it already knows all of it. The server refuses a
 * subset, a duplicate or a foreign identifier rather than guessing what the unnamed rows
 * should now be at.
 */
export const reorderContentCategories = ({
  type,
  orderedIds,
  csrfToken,
}: {
  type: ContentTypeParam
  orderedIds: string[]
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/admin/content-categories/order', {
    method: 'PUT',
    body: { contentType: type, orderedIds },
    csrfToken,
  })
