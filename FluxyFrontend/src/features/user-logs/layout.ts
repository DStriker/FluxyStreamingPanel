/**
 * The user log's table layout: the five column keys, the width each starts at, the storage
 * key of the visitor's own arrangement, and the one place a column key and the server's sort
 * field differ.
 *
 * This module is data with a unit test (`tests/features/user-logs/layout.test.ts`) for the
 * reason the credit log's is: a width table that stops summing to the administration's total
 * is perfectly valid TypeScript, and nothing in `typecheck`, `lint` or the build would
 * notice. `probe:render` can assert the rendered widths; the sum as an invariant belongs to a
 * test that fails on the number rather than on a regexp.
 */

/** The total every table in this administration reaches, and the columns here alone reach it. */
export const TABLE_WIDTH = 1190

/** The five columns, in the order they are drawn. */
export const COLUMN_KEYS = ['username', 'recordType', 'description', 'network', 'createdAt'] as const

export type UserLogColumnKey = (typeof COLUMN_KEYS)[number]

/**
 * The widths the table starts at, summing to **1190**.
 *
 * The description is the wide one because it is the row's own sentence and the one column
 * the search box matches. The network cell has to hold a flag, an address and a provider on
 * two lines, so it takes the second-widest slot. The username and the record type are short
 * values; the date is 190, the width this table gives a `formatTimestamp` column.
 *
 * These are *starting* widths, not the answer - see `useColumnWidths`.
 */
export const DEFAULT_COLUMN_WIDTHS: Record<UserLogColumnKey, number> = {
  username: 210,
  recordType: 170,
  description: 400,
  network: 220,
  createdAt: 190,
}

/** Where the widths are remembered between visits. */
export const COLUMN_WIDTHS_KEY = 'fluxy.userLogs.columnWidths'

/** The two fields this endpoint orders by. */
export type UserLogSortFieldName = 'createdAt' | 'recordType'

/**
 * The one column whose key is not a server sort field, and the two that are.
 *
 * `useServerTable` hands a sorter's `columnKey` over as `sortBy`, and this endpoint orders by
 * `createdAt` and `recordType` only. The record-type column is therefore keyed
 * `recordType` - the field's own name - and every other key answers `createdAt`, which is
 * the endpoint's default too. Only columns carrying `sorter: true` ever produce an event, so
 * the fallback never reaches the wire from a header.
 *
 * The default order is `createdAt desc`, which is the endpoint's own default too.
 */
export const sortFieldOf = (columnKey: string): UserLogSortFieldName =>
  columnKey === 'recordType' ? 'recordType' : 'createdAt'

/** The sum of the columns' starting widths - what `scroll.x` is built from at first paint. */
export const columnWidthTotal = (widths: Record<UserLogColumnKey, number>): number =>
  COLUMN_KEYS.reduce((total, key) => total + widths[key], 0)
