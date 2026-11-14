import type { TicketReply } from './TicketReply'
import type { TicketStatus } from './TicketStatus'

/**
 * One ticket and its whole conversation - `TicketDetailResponse` on the wire.
 *
 * `updatedAt` is the row's last write of any kind while `lastReplyAt` is the newest message,
 * and the two are both here because the detail page prints the thread and the list prints
 * the conversation's pulse.
 */
export interface TicketDetail {
  /** Identifier of the ticket, as text. */
  id: string
  /** Subject the ticket was opened with. */
  subject: string
  /** State of the ticket from this viewer's side. */
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
