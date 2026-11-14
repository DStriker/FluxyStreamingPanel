import {
  closeAdminTicket,
  createAdminTicketReply,
  createTicket,
  createTicketReply,
  deleteAdminTicket,
  markAdminTicketRead,
  markAdminTicketUnread,
  markTicketRead,
  reopenAdminTicket,
} from '../api'
import { useWrite } from '../../../hooks/useWrite'

/**
 * The tickets feature's writes, one hook per endpoint.
 *
 * Built on `useWrite` (`src/hooks/useWrite.ts`), so `mutateAsync` takes the endpoint's own
 * arguments without the antiforgery token. Nothing here reports, refetches or navigates -
 * that stays with the page, which owns the query that has to be re-read and the toast that
 * says what happened.
 *
 * The seven the task names are the feature's own: opening a ticket, answering it, the two
 * read marks, the two transitions and the delete. Five of them address the administrator's
 * endpoints (`/admin/tickets/...`); `useCreateTicket` and the two `Ticket`-prefixed hooks
 * below are the account-own side, and are named apart because the two families are different
 * writes behind different gates even where they share a suffix.
 */

/** Opens a ticket of the caller's own: `POST /auth/tickets`. */
export const useCreateTicket = () => useWrite(createTicket)

/** Answers a ticket as an administrator: `POST /admin/tickets/{id}/replies`. */
export const useReply = () => useWrite(createAdminTicketReply)

/** Marks a ticket read for this administrator: `POST /admin/tickets/{id}/read`. */
export const useMarkRead = () => useWrite(markAdminTicketRead)

/** Marks a ticket unread for this administrator: `POST /admin/tickets/{id}/unread`. */
export const useMarkUnread = () => useWrite(markAdminTicketUnread)

/** Closes a ticket: `POST /admin/tickets/{id}/close`. */
export const useClose = () => useWrite(closeAdminTicket)

/** Reopens a closed ticket: `POST /admin/tickets/{id}/reopen`. */
export const useReopen = () => useWrite(reopenAdminTicket)

/** Deletes a ticket and its conversation: `DELETE /admin/tickets/{id}`. */
export const useDeleteTicket = () => useWrite(deleteAdminTicket)

/** Answers one of the caller's own tickets: `POST /auth/tickets/{id}/replies`. */
export const useTicketReply = () => useWrite(createTicketReply)

/** Marks one of the caller's own tickets read: `POST /auth/tickets/{id}/read`. */
export const useTicketMarkRead = () => useWrite(markTicketRead)
