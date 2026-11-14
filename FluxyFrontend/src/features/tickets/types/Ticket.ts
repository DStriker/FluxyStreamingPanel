import type { TicketStatus } from './TicketStatus'

/**
 * One row of the account's own ticket list - `TicketItemResponse` on the wire.
 *
 * The self list carries no author: every row belongs to the account reading it. `lastReplyAt`
 * is the freshness of the conversation and the list's default order, so a support answer
 * moves the ticket to the top rather than hiding it on a later page.
 */
export interface Ticket {
  /** Identifier of the ticket, as text. */
  id: string
  /** Subject the ticket was opened with. */
  subject: string
  /** State of the ticket from this viewer's side. */
  status: TicketStatus
  /** When the ticket was opened. */
  createdAt: string
  /** When the newest message was written. */
  lastReplyAt: string
}
