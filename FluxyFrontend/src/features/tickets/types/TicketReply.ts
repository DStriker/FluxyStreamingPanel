/**
 * One message of a ticket's conversation - `TicketReplyResponse` on the wire.
 *
 * `isAdminReply` is the only thing that decides which side of the chat the bubble sits on,
 * and it is deliberately a boolean rather than an author role: the conversation has two
 * participants, so one bit is the whole of what a row needs to know.
 *
 * `readAt` is the receipt of **this** reply, not of the thread: it is null while the other
 * side has not read the message, and a timestamp once it has. The read mark under an own
 * message is drawn from it, which is why it travels on every row rather than once per ticket.
 *
 * `authorId` and `authorName` are null for a reply whose account has since been deleted -
 * the conversation keeps what was said, and the row says the author is no longer there.
 */
export interface TicketReply {
  /** Identifier of the message, as text. */
  id: string
  /** The text the author wrote. */
  message: string
  /** Whether an administrator wrote this message. Fixed for the row. */
  isAdminReply: boolean
  /** Identifier of the author, or null when the account no longer exists. */
  authorId: string | null
  /** Display name of the author, or null when the account no longer exists. */
  authorName: string | null
  /** When the other side read this message, or null while it is unread. */
  readAt: string | null
  /** When the message was written. */
  createdAt: string
}
