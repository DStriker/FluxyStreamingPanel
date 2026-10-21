/**
 * The table kit's entry point.
 *
 * One specifier — `../components/table` — for the frame the five server-paged tables draw,
 * the row actions the categories table shares, and the icon-only width reset every table
 * with resizable columns offers: consumers outside the folder name it once instead of three
 * to five of its files, and a rename inside the folder reaches none of them. Seven files,
 * nine consumers: that ratio is why this barrel exists where features deliberately have
 * none.
 *
 * The direction is one-way: nothing inside the folder imports through this file
 * (`ServerTable` reaches `./LoadErrorAlert` and `./ResizableHeaderCell` directly), because
 * a barrel that one of its own members imported back would be a cycle — the same hazard
 * the `apiUrl` module was extracted to avoid.
 */
export { default as LoadErrorAlert } from './LoadErrorAlert'
export { default as ResetColumnWidths } from './ResetColumnWidths'
export { default as ResizableHeaderCell } from './ResizableHeaderCell'
export { default as RowActions } from './RowActions'
export { default as ServerTable } from './ServerTable'
export { default as TableSearch } from './TableSearch'
export { default as TruncatedCell } from './TruncatedCell'
export type { ResizableHeaderCellProps } from './ResizableHeaderCell'
export type { RowAction } from './RowActions'
export type { ServerTableSelection } from './ServerTable'
