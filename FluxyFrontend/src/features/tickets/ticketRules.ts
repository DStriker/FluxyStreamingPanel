import type { Role } from '../../types'
import type { TicketStatus } from './types'

/**
 * The two questions about a ticket that both lists and both details ask, as pure rules.
 *
 * They are functions rather than conditions written at each call site because each one is
 * read from more than one place and a screen that disagreed with the server about either
 * would offer a control whose request is refused: support is the client's and the reseller's
 * door, not the administrator's, and a closed ticket accepts no replies.
 */

/**
 * Whether this role may open a ticket of its own.
 *
 * Administrators answer tickets; they do not have their own queue, so the "Create ticket"
 * button is absent from the admin area rather than disabled. Clients and resellers both own
 * the account area, and the same page serves both.
 */
export const canCreateTicket = (role: Role): boolean => role !== 'Admin'

/**
 * Whether a ticket in this status still accepts messages.
 *
 * Only `closed` refuses. `unread` and `read` describe a conversation in progress from the
 * viewer's side, and either may be answered by either participant - the server decides who
 * is allowed to answer, the status only decides whether the conversation is over.
 */
export const canReply = (status: TicketStatus): boolean => status !== 'closed'
