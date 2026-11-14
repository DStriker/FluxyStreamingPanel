/**
 * The three states a ticket can be in, as the wire spells them and as the viewer sees them.
 *
 * `unread` and `read` are **viewer relative** on the server: the same row is `unread` for an
 * administrator who has not opened it and `read` for the client who wrote it, because the
 * status names the conversation from the reading side rather than a stored flag of the row.
 * `closed` is the one absolute state, and it is the one that stops replies.
 */
export type TicketStatus = 'unread' | 'read' | 'closed'
