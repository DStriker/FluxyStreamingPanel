import type { TicketStatus } from './types'

/**
 * The ticket status as the screen draws it: a tag colour, a label key, and the one predicate
 * the list rows and the detail page read.
 *
 * The wire has three values and the screen has three words. `unread` is the *active* one -
 * it is the state an administrator has not looked at yet, which is exactly the "open" state
 * the spec names - so it takes the processing colour, while `read` and `closed` are both
 * quiet: one is a conversation already seen, the other a conversation finished. They share
 * the neutral colour and are told apart by their labels, which is why the label is a key of
 * its own rather than a colour's description.
 *
 * Nothing here is stored: `unread`/`read` are viewer relative on the server, so the same row
 * can arrive with a different `status` for two viewers. The functions therefore take the
 * status they are given and never compare it with a guessed viewer.
 */

/** The colours antd's `Tag` accepts here: the one active state, and the neutral one. */
export type TicketTagColor = 'processing' | 'default'

/**
 * The three wire values, in the order the filter lists them.
 *
 * The status picker's membership and `enumParam`'s whitelist both read this list, so a
 * hand-edited `?status=` outside it draws the unfiltered table rather than reaching the
 * endpoint as a `validation_failed`.
 */
export const TICKET_STATUSES: readonly TicketStatus[] = ['unread', 'read', 'closed']

/** The tag colour of a status: the unread conversation is the only one that stands out. */
export const ticketTagColor = (status: TicketStatus): TicketTagColor =>
  status === 'unread' ? 'processing' : 'default'

/**
 * The locale key of a status label.
 *
 * `Tickets.statuses.<wire value>` rather than a hand-kept map, so a status added to the wire
 * union is a compile error in the locale files instead of a tag with no text.
 */
export const ticketStatusLabelKey = (status: TicketStatus): string => `tickets.statuses.${status}`

/**
 * Whether this status means "not read yet for this viewer".
 *
 * The list draws the dot and the bold subject from it, and the detail page fires its one
 * mark-read write from it. `closed` is never unread: a closed ticket may still hold a message
 * nobody has read, but the conversation is over and a dot on it would ask for an action that
 * cannot be taken.
 */
export const isTicketUnread = (status: TicketStatus): boolean => status === 'unread'
