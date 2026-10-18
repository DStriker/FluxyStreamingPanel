import { apiFetch, apiGet } from '../../lib/http'
import type {
  BulkOperationResponse,
  MessageResponse,
  Role,
  UserPermission,
  UserStatus,
} from '../../types'
import type { UserGroupDetail, UserGroupList } from './types'

/**
 * The group table: reading a page and a group, creating, changing and deleting one, and the
 * bulk request a selection runs over them.
 *
 * Nothing here is captcha-gated, like the rest of the admin area: the session and a
 * `viewUserGroups`/`editUserGroups` grant are the gate.
 */

/**
 * Which column of the group table a caller asked to order by: `GET /admin/user-groups`.
 *
 * Five exist because the server can order by five - `name` (the default), `role`, `status`,
 * `permissionsCount` and `createdAt`. A value no server can sort on would be a header that
 * flips its arrow and changes nothing, so this list is the endpoint's idea of what is
 * sortable rather than the page's. The identifier is deliberately absent on both sides: a
 * random uuid is an order nobody asked for.
 */
export type UserGroupSortField = 'name' | 'role' | 'status' | 'permissionsCount' | 'createdAt'

/** Which way a page of groups runs. `asc` by default - the list starts at A. */
export type UserGroupSortOrder = 'asc' | 'desc'

/**
 * The page size a page uses when it wants **every** group rather than a page of them: the
 * server's own ceiling (`UserGroupListLimits.MaxPageSize`).
 *
 * One constant because there is one answer - both the accounts table's group filter and the
 * account form's group picker ask for the same thing, and two copies of `100` in two files
 * is one of them eventually not being 100. Groups are few by construction: an installation
 * has three it cannot operate without plus whatever an operator adds, and the whole point of
 * a group is that it is a *kind* of account rather than a per-account record. So a page this
 * size is the entire list in every realistic case, and the alternative - a search box inside
 * a picker - would be asking the visitor to guess at a name they came here not to have to
 * know.
 *
 * It is a page size rather than a promise: past this many groups the filter and the picker
 * show what they were given, and both would need a search box before they would show less
 * truthfully.
 */
export const ALL_GROUPS_PAGE_SIZE = 100

/**
 * One page of groups, ordered and filtered by the server: `GET /admin/user-groups`.
 *
 * A plain GET behind the admin session, so no antiforgery token - nothing is changed. The
 * query string is built with `URLSearchParams` for the reason the accounts page builds its
 * own: a `+`, an `&` or a space in what somebody typed looking for a group name would
 * otherwise change the meaning of the request instead of being part of the term.
 *
 * `role` and `status` are `null` for "no filter" rather than an empty string - a filter the
 * page has cleared has no business appearing in the query at all - and when present they are
 * the name of the enum member (`Admin`, `Blocked`), which the server parses case
 * insensitively but which is spelled here the way every other contract spells it. A filter
 * naming no member is refused rather than ignored, so a typo answers `400` instead of
 * quietly returning every group, which reads exactly like a filter that matched everything.
 *
 * The group picker asks for this same endpoint with `pageSize: ALL_GROUPS_PAGE_SIZE` - the
 * server's ceiling (`UserGroupListLimits.MaxPageSize`) - rather than a second unpaged route
 * existing for it. The count a picker would ignore is the same number the table needs, and
 * one route is one thing to keep correct.
 */
export const getUserGroups = ({
  page,
  pageSize,
  search,
  role,
  status,
  sortBy,
  sortOrder,
  signal,
}: {
  page: number
  pageSize: number
  search?: string
  role?: Role | null
  status?: UserStatus | null
  sortBy?: UserGroupSortField
  sortOrder?: UserGroupSortOrder
  signal?: AbortSignal
}): Promise<UserGroupList> => {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })

  if (search) query.set('search', search)
  if (role) query.set('role', role)
  if (status) query.set('status', status)
  query.set('sortBy', sortBy ?? 'name')
  query.set('sortOrder', sortOrder ?? 'asc')

  return apiGet<UserGroupList>(`/admin/user-groups?${query.toString()}`, signal)
}

/**
 * One group in full, for the edit form: `GET /admin/user-groups/{id}`.
 *
 * A plain GET like the list. The identifier travels in the path rather than the body, and a
 * group that does not exist is `404 user_group_not_found`, so the endpoint is no probe for
 * anything else.
 *
 * The permissions come back as individual keys rather than as a bitmask number: a client
 * that had to decode `15` would be one that has to know which bit the system set next,
 * which is the thing a permission catalog exists to keep stable.
 */
export const getUserGroup = (id: string, signal?: AbortSignal): Promise<UserGroupDetail> =>
  apiGet<UserGroupDetail>(`/admin/user-groups/${encodeURIComponent(id)}`, signal)

/**
 * What the create form sends to make a group in one request.
 *
 * All four values are required rather than defaulted: a group with no level would be one
 * whose members hold nothing anybody could answer for, and a group with no state would be
 * neither registered nor blocked. The form always knows all four, so neither is left for the
 * server to invent - a default is a decision about somebody else's group.
 *
 * `permissions` is the whole set, because a set of which half the caller never speaks is a
 * set nobody can read. A grant the role does not own is dropped by the server rather than
 * refused; a name that does not exist at all is refused with the list that does.
 */
export interface NewUserGroupValues {
  name: string
  role: Role
  status: UserStatus
  permissions: UserPermission[]
}

/** Creates a group: `POST /admin/user-groups`, answered with `201 user_group_created` and a `Location` naming it. */
export const createUserGroup = ({
  values,
  csrfToken,
}: {
  values: NewUserGroupValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/admin/user-groups', {
    method: 'POST',
    body: {
      name: values.name,
      role: values.role,
      status: values.status,
      permissions: values.permissions,
    },
    csrfToken,
  })

/**
 * What the edit form may change: every property is optional and an absent one means "leave
 * it alone", which is what stops a request from clearing a value simply because it forgot
 * to include it.
 *
 * `permissions` is the one field where absence is not enough: an **empty** list clears every
 * grant and a **missing** one changes none, and that difference is the only way a body can
 * say either. The form therefore always sends the set it holds - `[]` for a group that
 * grants nothing - rather than omitting it when nothing is ticked.
 *
 * A base group accepts `name` and nothing else; anything more is `409 user_group_immutable`.
 * The form disables those controls rather than discovering this afterwards, and the refusal
 * is what an endpoint that forgot to disable them gets.
 */
export interface UserGroupPatch {
  name?: string
  role?: Role
  status?: UserStatus
  /** The whole set the group will grant. `[]` clears; omit to leave untouched. */
  permissions?: UserPermission[]
}

/** Changes a group: `PATCH /admin/user-groups/{id}`, answered with `200 user_group_updated`. */
export const updateUserGroup = ({
  id,
  patch,
  csrfToken,
}: {
  id: string
  patch: UserGroupPatch
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/user-groups/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body: patch,
    csrfToken,
  })

/**
 * Deletes a group: `DELETE /admin/user-groups/{id}`, answered with `200 user_group_deleted`.
 *
 * The only action on this page that asks first, because it is the only one that cannot be
 * undone - and because the refusal it is most likely to meet (`409 user_group_in_use`) is
 * also the one worth reading *before* the click. Accounts still belonging to the group are
 * refused by the server before any write; the form additionally shows the member count on
 * the detail so the answer is not a surprise.
 *
 * A base group is refused with `409 user_group_immutable` however it is reached, which is
 * why its row draws no delete button at all.
 */
export const deleteUserGroup = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/user-groups/${encodeURIComponent(id)}`, {
    method: 'DELETE',
    csrfToken,
  })

/**
 * The three operations one request can run over a set of groups, as the text the server
 * parses them from.
 *
 * Deliberately not the accounts' union: a bulk edit of names, levels or permissions does not
 * exist (a base group refuses all of them anyway), so a name from the accounts list would be
 * a compile-time lie here. Two enums make "this action was sent to the wrong endpoint" a
 * compile error rather than a runtime surprise.
 */
export type BulkUserGroupAction = 'block' | 'unblock' | 'delete'

/**
 * Runs one operation over a set of groups: `POST /admin/user-groups/bulk`.
 *
 * Block and unblock are named rather than spelled as a `status` patch for the same reason the
 * row buttons name them: what the operator means is "block these", and the service decides
 * that this is `PATCH { status: 'Blocked' }`. One place decides that, not three.
 *
 * The two refusals that protect the installation are unchanged by any of this and are not
 * restated here - a base group answers `user_group_immutable` per entry and a group still
 * holding members answers `user_group_in_use`, both from the single-group methods the server
 * calls. So a run over a selection containing two of the foundation rows blocks the other
 * three and reports the two by name, rather than changing nothing at all.
 */
export const bulkUserGroups = ({
  ids,
  action,
  csrfToken,
}: {
  ids: string[]
  action: BulkUserGroupAction
  csrfToken: string | null
}): Promise<BulkOperationResponse> =>
  apiFetch('/admin/user-groups/bulk', {
    method: 'POST',
    body: { action, ids },
    csrfToken,
  })
