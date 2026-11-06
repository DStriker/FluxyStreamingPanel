/**
 * The credit log's table layout: the five column keys, the width each starts at, the storage
 * key of the visitor's own arrangement, and the one place a column key and the server's sort
 * field differ.
 *
 * This module is data with a unit test (`tests/features/credit-logs/layout.test.ts`) for the
 * reason the connection log's is: a width table that stops summing to the administration's
 * total is perfectly valid TypeScript, and nothing in `typecheck`, `lint` or the build would
 * notice. `probe:render` can assert the rendered widths; the sum as an invariant belongs to a
 * test that fails on the number rather than on a regexp.
 */

/** The total every table in this administration reaches, and the columns here alone reach it. */
export const TABLE_WIDTH = 1190

/** The five columns, in the order they are drawn. */
export const COLUMN_KEYS = ['reseller', 'credits', 'issuedBy', 'description', 'createdAt'] as const

export type CreditLogColumnKey = (typeof COLUMN_KEYS)[number]

/**
 * The widths the table starts at, summing to **1190**.
 *
 * The description is the wide one because it is the row's own sentence and the one column the
 * search box matches - a record read line by line. The two names are equal at 170: the
 * reseller and the issuer are the same kind of fact (a login name), and a difference between
 * them would make one look more important than the other. The amount is 110 - a signed
 * integer and a wallet glyph, nothing longer - and the date 200, the width every table here
 * gives a `formatTimestamp` column.
 *
 * These are *starting* widths, not the answer - see `useColumnWidths`.
 */
export const DEFAULT_COLUMN_WIDTHS: Record<CreditLogColumnKey, number> = {
  reseller: 170,
  credits: 110,
  issuedBy: 170,
  description: 540,
  createdAt: 200,
}

/** Where the widths are remembered between visits. */
export const COLUMN_WIDTHS_KEY = 'fluxy.creditLogs.columnWidths'

/**
 * The one column whose key is not its server sort field.
 *
 * `useServerTable` hands a sorter's `columnKey` over as `sortBy`, and this endpoint orders by
 * `createdAt` and `amount` - so the "Credits" header must be keyed `credits` (the word the
 * table draws and the visitor sorts by) while the request says `amount`. A column key that
 * named the field directly would have to call its header "Amount"; a mapping table of more
 * than one entry would be a second place for the two lists to drift.
 *
 * The default order is `createdAt desc`, which is the endpoint's own default too.
 */
export const sortFieldOf = (columnKey: string): 'createdAt' | 'amount' =>
  columnKey === 'credits' ? 'amount' : 'createdAt'

/** The sum of the columns' starting widths - what `scroll.x` is built from at first paint. */
export const columnWidthTotal = (widths: Record<CreditLogColumnKey, number>): number =>
  COLUMN_KEYS.reduce((total, key) => total + widths[key], 0)
