/**
 * The API request failure log's table layout: the five column keys, the width each starts at,
 * the storage key of the visitor's own arrangement, and the one place a column key and the
 * server's sort field differ.
 *
 * This module is data with a unit test (`tests/features/api-request-failures/layout.test.ts`)
 * for the reason the user log's is: a width table that stops summing to the administration's
 * total is perfectly valid TypeScript, and nothing in `typecheck`, `lint` or the build would
 * notice. `probe:render` can assert the rendered widths; the sum as an invariant belongs to a
 * test that fails on the number rather than on a regexp.
 */

/** The total every table in this administration reaches, and the columns here alone reach it. */
export const TABLE_WIDTH = 1190

/** The five columns, in the order they are drawn. */
export const COLUMN_KEYS = ['subscription', 'content', 'reason', 'network', 'createdAt'] as const

export type ApiRequestFailureColumnKey = (typeof COLUMN_KEYS)[number]

/**
 * The widths the table starts at, summing to **1190**.
 *
 * The content cell is the wide one: it carries a primary name and a muted identifier under
 * it, and a content name is the longest fact in the row. The network cell holds a flag, an
 * address and a provider on two lines, so it takes the second-widest slot. The subscription
 * data is an opaque access parameter; the reason is a short tag; the date is 200, the width
 * this table gives a `formatTimestamp` column.
 *
 * These are *starting* widths, not the answer - see `useColumnWidths`.
 */
export const DEFAULT_COLUMN_WIDTHS: Record<ApiRequestFailureColumnKey, number> = {
  subscription: 220,
  content: 320,
  reason: 200,
  network: 250,
  createdAt: 200,
}

/** Where the widths are remembered between visits. */
export const COLUMN_WIDTHS_KEY = 'fluxy.apiRequestFailures.columnWidths'

/** The two fields this endpoint orders by. */
export type ApiRequestFailureSortFieldName = 'createdAt' | 'reason'

/**
 * The one column whose key is not a server sort field, and the two that are.
 *
 * `useServerTable` hands a sorter's `columnKey` over as `sortBy`, and this endpoint orders by
 * `createdAt` and `reason` only. The reason column is therefore keyed `reason` - the field's
 * own name - and every other key answers `createdAt`, which is the endpoint's default too.
 * Only columns carrying `sorter: true` ever produce an event, so the fallback never reaches
 * the wire from a header.
 *
 * The default order is `createdAt desc`, which is the endpoint's own default too.
 */
export const sortFieldOf = (columnKey: string): ApiRequestFailureSortFieldName =>
  columnKey === 'reason' ? 'reason' : 'createdAt'

/** The sum of the columns' starting widths - what `scroll.x` is built from at first paint. */
export const columnWidthTotal = (widths: Record<ApiRequestFailureColumnKey, number>): number =>
  COLUMN_KEYS.reduce((total, key) => total + widths[key], 0)
