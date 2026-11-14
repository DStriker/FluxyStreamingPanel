import type { TicketStatus } from './TicketStatus'

/**
 * One row of the administrator's ticket list - `AdminTicketItemResponse` on the wire.
 *
 * The admin list is every account's conversation, so each row names whose it is. `status` is
 * viewer relative here too: `unread` means this administrator has not read the newest part,
 * which is what the row's dot and the default order are about.
 */
export interface AdminTicket {
  /** Identifier of the ticket, as text. */
  id: string
  /** Identifier of the account that opened the ticket. */
  userId: string
  /** Login name of that account. */
  username: string
  /** Subject the ticket was opened with. */
  subject: string
  /** State of the ticket from the administrator's side. */
  status: TicketStatus
  /** When the ticket was opened. */
  createdAt: string
  /** When the newest message was written. */
  lastReplyAt: string
}
