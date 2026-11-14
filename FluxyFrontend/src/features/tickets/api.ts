import { apiCreate, apiFetch, apiGet } from '../../utils/http'
import type { MessageResponse } from '../../types'
import type {
  AdminTicketDetail,
  AdminTicketList,
  TicketDetail,
  TicketList,
  TicketStatus,
} from './types'

/**
 * The support tickets: the account's own queue and the administrator's, with the writes each
 * side may make.
 *
 * Two families of paths, one per area, because the two are two different reads of one
 * resource: `/auth/tickets` is always the caller's own conversation, `/admin/tickets` is every
 * account's. Neither family takes an owner: the server derives the account from the session,
 * so a ticket id can never name somebody else's thread from the self side.
 *
 * Every read here is a plain GET, so no antiforgery token travels with it - the token's job
 * is to prove a write was aimed at this site. Every write mints its token at call time
 * through the caller (see `hooks/mutations.ts`, which builds on `useWrite`), and none of them
 * carries a captcha: the session is the stronger gate, the departure every admin area call
 * already records.
 */

/** The page size the pager offers, which is also the server's default. */
export const TICKETS_PAGE_SIZE = 20

/** The fields the administrator's list can be ordered by, as the query-string spells them. */
export type AdminTicketSortField = 'subject' | 'username' | 'createdAt' | 'lastReplyAt'

/** The fields the account's own list can be ordered by. `username` is deliberately absent. */
export type TicketSortField = 'subject' | 'createdAt' | 'lastReplyAt'

export type TicketSortOrder = 'asc' | 'desc'

/**
 * The filters both lists share beyond the five table parameters.
 *
 * `status` is viewer relative: `unread` means "not read by me", which is a different row on
 * the two sides of one conversation. `search` matches the subject only - a message body is
 * not a column of the list and a search that reached into it would answer with rows whose
 * subject has nothing to do with the term.
 */
export interface TicketFilters {
  status?: TicketStatus | null
}

/** Everything one page of the administrator's list carries. */
export interface AdminTicketQuery extends TicketFilters {
  page: number
  pageSize: number
  search?: string
  sortBy: AdminTicketSortField
  sortOrder: TicketSortOrder
}

/** Everything one page of the account's own list carries. */
export interface TicketQuery extends TicketFilters {
  page: number
  pageSize: number
  search?: string
  sortBy: TicketSortField
  sortOrder: TicketSortOrder
}

/**
 * One page of every account's tickets: `GET /admin/tickets`.
 *
 * Built with `URLSearchParams` for the reason every list query here is: a search term with
 * `+` or `&` in it is searched for rather than reinterpreted, and an absent filter is left
 * off the request rather than sent as the string `"null"`.
 */
export const getAdminTickets = ({
  page,
  pageSize,
  search,
  status,
  sortBy,
  sortOrder,
  signal,
}: AdminTicketQuery & { signal?: AbortSignal }): Promise<AdminTicketList> => {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
    sortBy,
    sortOrder,
  })

  if (search) query.set('search', search)
  if (status) query.set('status', status)

  return apiGet<AdminTicketList>(`/admin/tickets?${query.toString()}`, signal)
}

/** One ticket in full, with its conversation: `GET /admin/tickets/{id}`. */
export const getAdminTicket = (id: string, signal?: AbortSignal): Promise<AdminTicketDetail> =>
  apiGet<AdminTicketDetail>(`/admin/tickets/${encodeURIComponent(id)}`, signal)

/** One page of the caller's own tickets: `GET /auth/tickets`. */
export const getTickets = ({
  page,
  pageSize,
  search,
  status,
  sortBy,
  sortOrder,
  signal,
}: TicketQuery & { signal?: AbortSignal }): Promise<TicketList> => {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
    sortBy,
    sortOrder,
  })

  if (search) query.set('search', search)
  if (status) query.set('status', status)

  return apiGet<TicketList>(`/auth/tickets?${query.toString()}`, signal)
}

/** One of the caller's own tickets in full: `GET /auth/tickets/{id}`. */
export const getTicket = (id: string, signal?: AbortSignal): Promise<TicketDetail> =>
  apiGet<TicketDetail>(`/auth/tickets/${encodeURIComponent(id)}`, signal)

/** What the create dialog sends: the subject and nothing else. */
export interface NewTicketValues {
  subject: string
}

/**
 * Opens a ticket: `POST /auth/tickets`, answered with `201 ticket_created` and a `Location`
 * naming the new row.
 *
 * It goes through `apiCreate` rather than `apiFetch` because the new ticket's address is the
 * header, not the body: a page that has just opened a ticket should land in its conversation,
 * and the contract sends no identifier in the body to build that address from. The second
 * element is the header verbatim; the page passes it through `internalPath` before navigating,
 * which is the rule every server-named destination follows here.
 */
export const createTicket = ({
  values,
  csrfToken,
}: {
  values: NewTicketValues
  csrfToken: string | null
}): Promise<{ data: MessageResponse; location: string | null }> =>
  apiCreate('/auth/tickets', {
    body: { subject: values.subject },
    csrfToken,
  })

/** What both reply endpoints send: the message itself. */
export interface NewTicketReplyValues {
  message: string
}

/** Answers a ticket as its owner: `POST /auth/tickets/{id}/replies`. */
export const createTicketReply = ({
  id,
  values,
  csrfToken,
}: {
  id: string
  values: NewTicketReplyValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/auth/tickets/${encodeURIComponent(id)}/replies`, {
    method: 'POST',
    body: { message: values.message },
    csrfToken,
  })

/** Marks one of the caller's own tickets read: `POST /auth/tickets/{id}/read`. */
export const markTicketRead = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/auth/tickets/${encodeURIComponent(id)}/read`, {
    method: 'POST',
    csrfToken,
  })

/**
 * One of the administrator's addressed actions on a ticket.
 *
 * The four share a path shape and differ only in the segment, which is why the helper is
 * private: a caller names the action it wants and the helper is the one place the method,
 * the path and the token are written together.
 */
const transitionAdminTicket = (
  id: string,
  action: 'read' | 'unread' | 'close' | 'reopen',
  csrfToken: string | null,
): Promise<MessageResponse> =>
  apiFetch(`/admin/tickets/${encodeURIComponent(id)}/${action}`, {
    method: 'POST',
    csrfToken,
  })

/** Marks a ticket read for this administrator: `200 ticket_marked_read`. */
export const markAdminTicketRead = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> => transitionAdminTicket(id, 'read', csrfToken)

/** Marks a ticket unread for this administrator: `200 ticket_marked_unread`. */
export const markAdminTicketUnread = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> => transitionAdminTicket(id, 'unread', csrfToken)

/** Closes a ticket: `200 ticket_closed`; `409 invalid_status` when it already is. */
export const closeAdminTicket = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> => transitionAdminTicket(id, 'close', csrfToken)

/** Reopens a closed ticket: `200 ticket_reopened`; `409 invalid_status` when it is not closed. */
export const reopenAdminTicket = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> => transitionAdminTicket(id, 'reopen', csrfToken)

/** Answers a ticket as an administrator: `POST /admin/tickets/{id}/replies`. */
export const createAdminTicketReply = ({
  id,
  values,
  csrfToken,
}: {
  id: string
  values: NewTicketReplyValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/tickets/${encodeURIComponent(id)}/replies`, {
    method: 'POST',
    body: { message: values.message },
    csrfToken,
  })

/** Deletes a ticket and its conversation: `DELETE /admin/tickets/{id}`, `200 ticket_deleted`. */
export const deleteAdminTicket = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/tickets/${encodeURIComponent(id)}`, {
    method: 'DELETE',
    csrfToken,
  })
