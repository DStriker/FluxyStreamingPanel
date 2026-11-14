import type { Ticket } from './Ticket'

/**
 * One page of the account's own tickets - `TicketPageResponse` on the wire.
 *
 * `total` counts every ticket matching the search and the status filter before the page is
 * cut, because that is what the pager draws itself from: a count taken over the rows on
 * screen would offer four pages of five when the filter admitted one.
 */
export interface TicketList {
  /** Tickets on this page, in the order asked for. */
  items: Ticket[]
  /** How many tickets match, in total. */
  total: number
  /** The page these tickets are, one based. */
  page: number
  /** How many tickets one page holds. */
  pageSize: number
}
