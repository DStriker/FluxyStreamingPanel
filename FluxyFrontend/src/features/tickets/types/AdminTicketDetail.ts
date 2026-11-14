import type { TicketReply } from './TicketReply'
import type { TicketStatus } from './TicketStatus'

/**
 * One ticket in full as the administrator sees it - `AdminTicketDetailResponse` on the wire.
 *
 * The account's own detail plus the two facts that make a row somebody else's: who opened it.
 */
export interface AdminTicketDetail {
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
  /** When the ticket row itself was last changed. */
  updatedAt: string
  /** When the newest message was written. */
  lastReplyAt: string
  /** The conversation, oldest first. */
  replies: TicketReply[]
}
