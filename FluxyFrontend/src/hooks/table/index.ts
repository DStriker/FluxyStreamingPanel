/**
 * The table hooks' entry point.
 *
 * One specifier — `../hooks/table` — for the four hooks the five server-paged tables run on:
 * `useServerTable` keeps their state in the address, `useColumnWidths` owns the drag,
 * `useRowAction` runs one row's write and re-reads, and `useBulkAction` does the same for a
 * selection. Six consumers outside the folder — the five pages and `ServerTable`'s type
 * import — named seventeen of these files before; now they name one.
 *
 * The direction is one-way: nothing inside imports through this file. The kit's barrel and
 * this one do reference each other, but only through `import type` edges — `ServerTable` asks
 * this file for `useServerTable`'s return type, and `useColumnWidths` asks the kit for
 * `ResizableHeaderCellProps`. Both are erased under `isolatedModules`, so the source-level
 * circle is not a runtime one and carries none of the stale-binding risk that extracted
 * `apiUrl`. That is also why the two barrels can stay one-way at runtime while still being
 * each other's consumers on paper.
 */
export { useBulkAction } from './useBulkAction'
export { useColumnWidths } from './useColumnWidths'
export { useRowAction } from './useRowAction'
export { useServerTable } from './useServerTable'
export type { ColumnWidthsHandle } from './useColumnWidths'
export type { RowDeleteQuestion } from './useRowAction'
