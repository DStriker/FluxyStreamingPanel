import { actionsColumnWidth } from '../../utils/tableWidths'

/**
 * The two modes of the connection log, and **everything about them that differs by numbers**.
 *
 * This module exists because the two modes are served by one component (see
 * `ConnectionLogsPage`), and the numbers they disagree about - the column set, the widths and
 * the storage key - have to move together or not at all. When they lived inside the page,
 * changing one of the three was a change nobody could check from outside: `typecheck`, `lint`
 * and the build all accept a width table that no longer sums to the table's own total, and a
 * probe could only ever assert what happened to be written down. Here they are data, and
 * `tests/features/connection-logs/layout.test.ts` pins the invariants the owner asked for.
 *
 * The two keys are **deliberately different strings**. A width stored for a six-column table
 * is a claim about that table; reading it into the other mode is how the live page's reset
 * once broke the activity table (its `endedAt` arrived as the live mode's `0`, was clamped to
 * the floor, and the table no longer summed to 1190).
 */

/** The total every table in this administration reaches, selection column excluded. */
export const TABLE_WIDTH = 1190

/** The columns either mode can draw, in the order they are drawn. */
export type ConnectionLogColumnKey =
  | 'subscription'
  | 'user'
  | 'stream'
  | 'network'
  | 'agent'
  | 'startedAt'
  | 'endedAt'
  | 'actions'

/**
 * The seven columns of the live view: there is no end to show, because none has happened.
 *
 * The actions cell is the kill stub - drawn disabled, with its reason - and no end column.
 */
export const LIVE_COLUMN_KEYS: readonly ConnectionLogColumnKey[] = [
  'subscription',
  'user',
  'stream',
  'network',
  'agent',
  'startedAt',
  'actions',
]

/**
 * The seven of the activity view: the end time replaces the actions cell rather than joining it.
 *
 * **A finished session has nothing to act on, so the activity table draws no actions column at
 * all rather than an empty one.** An actions column that is empty on every row is a heading over
 * nothing, and it costs the pixels the two last columns are read through.
 */
export const ACTIVITY_COLUMN_KEYS: readonly ConnectionLogColumnKey[] = [
  'subscription',
  'user',
  'stream',
  'network',
  'agent',
  'startedAt',
  'endedAt',
]

/**
 * The widths the live table starts at, summing to **1190**.
 *
 * `endedAt` is present at 0 only because one map type serves both modes; the live view never
 * draws that column, `LIVE_COLUMN_KEYS` is what decides which widths are summed, stored and
 * reset, and the unit test pins the drawn seven to 1190.
 *
 * The owner's round-3 corrections, as numbers:
 *
 * - **the log's `id` column is gone**, replaced by two: `subscription` (the identifier, a link to
 *   its edit form) and `user` (the owner's name, a link to the account form). The room the old
 *   combined cell had, 300, is split between them, because each now holds one fact that
 *   truncates rather than two facts sharing a line;
 * - **the actions column is 45 wider** (`actionsColumnWidth(1)` = 56, so 101) and the `stream`
 *   column 45 narrower (344 to 299), so the kill stub's own measure is not borrowed from the
 *   title beside it;
 * - **`agent` is new and one line per row**, because a user agent is what tells an operator which
 *   client is behind a connection - the same summary the visit history prints, and the way to
 *   narrow the log to that exact client;
 * - **the address starts at 160 and the agent at 170 on the owner's round-4 correction.** The
 *   address column was 120 and the agent 210; the owner moved 40 px from the agent to the address,
 *   so the total is unchanged and the address an operator reads first is no longer the narrowest
 *   text in the row. The agent still has more room than the address, because its summary is a
 *   sentence while an address is at most 45 characters.
 */
export const LIVE_COLUMN_WIDTHS: Record<ConnectionLogColumnKey, number> = {
  subscription: 130,
  user: 130,
  stream: 299,
  network: 160,
  agent: 170,
  startedAt: 200,
  endedAt: 0,
  actions: actionsColumnWidth(1) + 45,
}

/**
 * The widths the activity table starts at, summing to the same **1190** - with no actions column.
 *
 * **`startedAt` and `endedAt` are equal**, on the owner's correction: they are the two ends of
 * one interval, the table is read straight across them, and a difference between the two made the
 * duration look like it belonged to a column of its own. The pair is 200 each in both modes,
 * because the same timestamp is printed in both.
 *
 * **The five content columns are the live table's, and only `stream` differs.** The activity view
 * spends 200 on its end column where the live one spends 101 on its actions cell, so the title
 * gives the 99 back - and the owner's 45 comes out of the same title here as on the live table.
 * That is the whole of the arithmetic: `130 + 130 + 200 + 160 + 170 + 200 + 200 = 1190`.
 *
 * The network and agent widths are the live table's too, including the owner's round-4 split of
 * 40 px from the agent to the address: the two screens read the same row, and a column that moved
 * between them would shift the table under a visitor who only switched the slice.
 */
export const ACTIVITY_COLUMN_WIDTHS: Record<ConnectionLogColumnKey, number> = {
  subscription: 130,
  user: 130,
  stream: 200,
  network: 160,
  agent: 170,
  startedAt: 200,
  endedAt: 200,
  actions: 0,
}

/**
 * Where each mode remembers the widths the visitor gave its columns.
 *
 * **Two keys rather than one**, and that is a bug fix rather than a preference: the two modes
 * have different column sets, and a width stored for a seven-column table is a claim about a
 * six-column one. One key shared by both let a reset pressed on the live page land in the
 * activity table as a `startedAt`/`endedAt` pair it had never been given.
 *
 * **The version suffix is the round-3 column set.** The identifiers are unchanged and the column
 * *keys* changed: a stored arrangement from before this correction describes columns that no
 * longer exist (`id`, and a `subscription` cell that held two facts), so reading it would put the
 * id column's width under the subscription link. A version bump is how the visitor's older
 * arrangement stops applying without a migration of localStorage.
 */
export const COLUMN_WIDTHS_KEYS = {
  live: 'fluxy.connectionLogs.live.columnWidths.v2',
  activity: 'fluxy.connectionLogs.activity.columnWidths.v2',
} as const

/** The sum of one mode's own columns - what `scroll.x` is built from. */
export const columnWidthTotal = (
  keys: readonly ConnectionLogColumnKey[],
  widths: Record<ConnectionLogColumnKey, number>,
): number => keys.reduce((total, key) => total + widths[key], 0)
