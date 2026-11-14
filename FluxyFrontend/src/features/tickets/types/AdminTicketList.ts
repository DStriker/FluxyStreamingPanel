import type { AdminTicket } from './AdminTicket'

/**
 * One page of every account's tickets - `AdminTicketPageResponse` on the wire.
 *
 * `total` counts the whole filtered set rather than the rows of this page, for the reason
 * the account's own list states: the pager draws itself from the server's count.
 */
export interface AdminTicketList {
  /** Tickets on this page, in the order asked for. */
  items: AdminTicket[]
  /** How many tickets match, in total. */
  total: number
  /** The page these tickets are, one based. */
  page: number
  /** How many tickets one page holds. */
  pageSize: number
}
