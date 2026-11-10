/**
 * The stream problem journal's table layout: the three column keys, the width each starts at,
 * the storage key of the visitor's own arrangement, and the one sort mapping.
 *
 * This module is data with a unit test (`tests/features/stream-problems/layout.test.ts`) for
 * the reason the user log's is: a width table that stops summing to the administration's total
 * is perfectly valid TypeScript, and nothing in `typecheck`, `lint` or the build would notice.
 */

/** The total every table in this administration reaches, and the columns here alone reach it. */
export const TABLE_WIDTH = 1190

/** The three columns, in the order they are drawn. */
export const COLUMN_KEYS = ['stream', 'error', 'createdAt'] as const

export type StreamProblemColumnKey = (typeof COLUMN_KEYS)[number]

/**
 * The widths the table starts at, summing to **1190**.
 *
 * The error cell is the wide one: it carries whatever the producer reported, which is the
 * longest fact in the row. The stream's name and the date are short by comparison.
 *
 * These are *starting* widths, not the answer - see `useColumnWidths`.
 */
export const DEFAULT_COLUMN_WIDTHS: Record<StreamProblemColumnKey, number> = {
  stream: 320,
  error: 570,
  createdAt: 300,
}

/** Where the widths are remembered between visits. */
export const COLUMN_WIDTHS_KEY = 'fluxy.streamProblems.columnWidths'

/** The one field this endpoint orders by. */
export type StreamProblemSortFieldName = 'createdAt'

/**
 * Every column's key mapped to the server's one sort field.
 *
 * `useServerTable` hands a sorter's `columnKey` over as `sortBy`, and this endpoint orders by
 * `createdAt` alone. Only the date column carries `sorter: true`, so the fallback never
 * reaches the wire from a header - it exists so that a hand-edited `?sortBy=` is judged
 * against the endpoint's own union rather than passed through.
 */
export const sortFieldOf = (_columnKey: string): StreamProblemSortFieldName => 'createdAt'

/** The sum of the columns' starting widths - what `scroll.x` is built from at first paint. */
export const columnWidthTotal = (widths: Record<StreamProblemColumnKey, number>): number =>
  COLUMN_KEYS.reduce((total, key) => total + widths[key], 0)
