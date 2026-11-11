import type { MagEventSortField } from './api'

/**
 * The MAG event journal's table layout: the five column keys, the width each starts at, the
 * storage key of the visitor's own arrangement, and the one sort mapping.
 *
 * This module is data with a unit test (`tests/features/mag-events/layout.test.ts`) for the
 * reason the stream problem log's is: a width table that stops summing to the administration's
 * total is perfectly valid TypeScript, and nothing in `typecheck`, `lint` or the build would
 * notice.
 */

/** The total every table in this administration reaches, and the columns here alone reach it. */
export const TABLE_WIDTH = 1190

/** The five columns, in the order they are drawn: MAC, event, message, status, date. */
export const COLUMN_KEYS = ['mac', 'event', 'message', 'status', 'date'] as const

export type MagEventColumnKey = (typeof COLUMN_KEYS)[number]

/**
 * The widths the table starts at, summing to **1190**.
 *
 * The message cell is the wide one: it carries the operation's payload - a message the device
 * showed or a stream identifier - which is the longest fact in the row. The device address,
 * the operation's name, the two-state tag and the timestamp are short by comparison.
 *
 * These are *starting* widths, not the answer - see `useColumnWidths`.
 */
export const DEFAULT_COLUMN_WIDTHS: Record<MagEventColumnKey, number> = {
  mac: 180,
  event: 170,
  message: 520,
  status: 120,
  date: 200,
}

/** Where the widths are remembered between visits. */
export const COLUMN_WIDTHS_KEY = 'fluxy.magEvents.columnWidths'

/**
 * Every column's key mapped to the server's one sort field.
 *
 * `useServerTable` hands a sorter's `columnKey` over as `sortBy`, and this endpoint orders by
 * `createdAt` alone. Only the date column carries `sorter: true`, so the fallback never
 * reaches the wire from a header - it exists so that a hand-edited `?sortBy=` is judged
 * against the endpoint's own union rather than passed through.
 */
export const sortFieldOf = (_columnKey: string): MagEventSortField => 'createdAt'

/** The sum of the columns' starting widths - what `scroll.x` is built from at first paint. */
export const columnWidthTotal = (widths: Record<MagEventColumnKey, number>): number =>
  COLUMN_KEYS.reduce((total, key) => total + widths[key], 0)
