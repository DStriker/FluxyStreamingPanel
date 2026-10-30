import { Table } from 'antd'
import type { TableColumnsType, TablePaginationConfig, TableProps } from 'antd'
import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import type { Key } from 'react'
import type { useServerTable } from '../../hooks/table'
import { MIN_COLUMN_WIDTH, distributeWidths } from '../../utils/tableWidths'
import LoadErrorAlert from './LoadErrorAlert'
import ResizableHeaderCell from './ResizableHeaderCell'

/**
 * The checkbox column antd prepends to a table whose rows can be selected.
 *
 * It is not one of the page's own columns, so it is not part of `useColumnWidths`' sum, its
 * reset or its storage - the columns a page owns keep their own widths, their own reset and
 * their own remembered arrangement while the table as a whole still fills the width it has
 * always filled. It *is* part of `scroll.x`, which is why the arithmetic below adds it back
 * rather than folding it into the hook's sum: a sum that included it would make the stored
 * widths and the reset disagree with the columns they are supposed to describe.
 *
 * 40 is the width antd's selection column asks for, and every table here pays for it out of
 * its content columns rather than by growing - the pages' default widths are written down
 * already accounting for it, which is why they sum to 40 less than the 1230 the whole table
 * occupies.
 */
const SELECTION_WIDTH = 40

/**
 * The layout effect that measures the box before the first paint - and the plain effect on
 * the server, where a layout effect does nothing but warn.
 *
 * The measure has to happen before the first paint, or the table would be drawn at its
 * full width for one frame and then squeezed into the box, which is a visible jump on
 * every page load and on every toggle of the sidebar.
 */
const useMeasuredLayoutEffect = typeof window === 'undefined' ? useEffect : useLayoutEffect

/**
 * The selection half of a table, exactly as `useBulkAction` hands it over: the keys the
 * current page has marked, and the write-back that antd's header checkbox and the rows'
 * own checkboxes call.
 */
export interface ServerTableSelection {
  selectedRowKeys: Key[]
  onChange: (keys: Key[]) => void
}

interface ServerTableProps<Row extends object, SortField extends string> {
  /** `useServerTable`'s own return: the five parameters, the pager and the sort handler. */
  table: ReturnType<typeof useServerTable<Row, SortField>>
  /** The columns, with their widths, sorters and `onHeaderCell` handles already in place. */
  columns: TableColumnsType<Row>
  /** The page of rows - the page hands `items ?? []`, so the table never sees `undefined`. */
  rows: Row[]
  /** What the pager counts: the endpoint's `total`, not the length of `rows`. */
  total: number
  /** Whether a read is in flight - `tableResults`' answer, so a page turn keeps the rows visible. */
  loading: boolean
  /** The sentence for a failed read, or `null` - also `tableResults`'. */
  loadError: string | null
  /** How a failed read is retried, which is the page's own `refetch`. */
  onRetry: () => void
  /** The sum of the page's own widths, from `useColumnWidths`: the width the table scrolls to. */
  tableWidth: number
  /** What is drawn when there are no rows; the page decides whether that is "empty" or "no matches". */
  emptyText: string
  /**
   * This table's own `showTotal` wording, written out at the call site where the rows and
   * the locale keys are - the same division `useServerTable` draws for the pager it hands
   * back.
   */
  showTotal: NonNullable<TablePaginationConfig['showTotal']>
  /** The selection, for the tables with checkboxes. Its column is added to `scroll.x` here. */
  selection?: ServerTableSelection
  /**
   * The expandable rows configuration, passed through untouched.
   *
   * It exists for exactly one table: the subscriptions page draws its service subrows as
   * **forced-open** expanded rows - every row expanded, no chevron and no interaction - which is
   * the one way to render a strip of subrows below a row without injecting `<tr>`s into antd's
   * own body. Every other table leaves this undefined and draws no expand column at all.
   */
  expandable?: TableProps<Row>['expandable']
  /**
   * A class for each row, or nothing.
   *
   * One table needs it: the subscriptions list draws forced-open strips under every row, and a
   * selected row has to carry its strips with it. antd paints an expanded row's own `<tr>` with a
   * fixed background rather than inheriting the selection style, so the page marks the row and the
   * stylesheet follows the mark.
   */
  rowClassName?: (row: Row) => string
}

/**
 * The body of every server-paged table in this application.
 *
 * It owns the four things the five tables would otherwise state five times, and each is a
 * rule rather than a convenience:
 *
 * - the failed-read branch, which draws `LoadErrorAlert` **in place of** the table - a read
 *   that failed has nothing to show and one thing to offer, and a table sitting under an
 *   error banner would be an empty table pretending to be an answer;
 * - `ResizableHeaderCell` as the header cell, which is where the resize handles live. It has
 *   to be a module-scope component type, because a type created inside a render would be a
 *   new one on every repaint and React would then unmount a handle mid-drag, dropping the
 *   pointer with it - and the whole split between the cell's keyboard access and the
 *   handle's is documented on that component, once;
 * - the scroll box: `scroll.x` is the page's own widths plus the checkbox column when there
 *   is one, so the table never widens its card and the columns a page owns keep their own
 *   widths, reset and memory;
 * - the fit: the room between the columns is **divided again** to the width of the box the
 *   table is drawn in, which is what keeps the right-hand column - the actions - on the
 *   screen whether the sidebar is open or closed. The table is measured rather than assumed,
 *   because the box is a fact about the window, the zoom and the sidebar, and none of the
 *   three is a number this application knows. The division is done here rather than left to
 *   the browser, because a browser stretches a fixed-layout table's columns in proportion
 *   and would hand the actions column the dead space its width was measured to remove;
 * - the pager: `useServerTable`'s page/pageSize/pageSizeOptions, the endpoint's `total`
 *   (which is what the pager counts, not the rows on screen), and the page's own `showTotal`
 *   wording, handed in already translated.
 *
 * What it deliberately does not own is everything that differs, on the same line
 * `useServerTable` draws: the columns and their widths, the query itself, the page's filters
 * and toolbar, and the selection's state - a component holding a translation key for a page
 * it cannot name would be the wrong way round.
 */
export default function ServerTable<Row extends object, SortField extends string>({
  table,
  columns,
  rows,
  total,
  loading,
  loadError,
  onRetry,
  tableWidth,
  emptyText,
  showTotal,
  selection,
  expandable,
  rowClassName,
}: ServerTableProps<Row, SortField>) {
  const boxRef = useRef<HTMLDivElement>(null)
  const [boxWidth, setBoxWidth] = useState<number | null>(null)

  /**
   * The width of the box the table is drawn in, measured rather than assumed.
   *
   * The box is the card's own body, and what it is depends on three things this
   * application does not control: how wide the window is, how far the browser is zoomed in,
   * and whether the sidebar is open - which is 164 pixels of difference on its own. A table
   * that assumes a width therefore overflows on somebody's screen and not on the one it was
   * written on, and the column that disappears is the one at the far right: the actions.
   *
   * The observer rather than a single measurement, because the box changes under the
   * visitor's hand - collapsing the sidebar is the same gesture as opening a page, and the
   * table has to answer both without a reload.
   */
  useMeasuredLayoutEffect(() => {
    const element = boxRef.current
    if (!element) return

    const measure = () => setBoxWidth(element.clientWidth)
    measure()

    if (typeof ResizeObserver === 'undefined') return
    const observer = new ResizeObserver(measure)
    observer.observe(element)
    return () => observer.disconnect()
  }, [])

  if (loadError) {
    return <LoadErrorAlert message={loadError} loading={loading} onRetry={onRetry} />
  }

  /**
   * The fit of the columns into the box.
   *
   * Every column has a width of its own and the table is a fixed-layout one, so the sum of
   * those widths is the width the table wants. The room between the columns is then divided
   * again - in the same proportions the visitor arranged them in, and never below the floor
   * - and `scroll.x` becomes the width they now take. The table fills the box exactly, and
   * nothing is cut off at its right edge.
   *
   * The room is divided here rather than left to the browser, and that is the whole point of
   * doing it: a browser stretches a fixed-layout table that is narrower than its box, and it
   * stretches every column in proportion - which would hand the actions column, whose width
   * is a measurement rather than a preference, the very dead space the measurement was taken
   * to remove. Here the extra room goes to the columns that can take it, and the actions
   * column stays exactly as wide as the buttons in it.
   *
   * **Not every column is squeezed.** A column that truncates - one with `ellipsis` - can
   * be narrower, because its content has a smaller form. A column that holds something
   * fixed - a logo, a tag, a row's buttons - keeps its width, because its content has none.
   * The actions column is the one this matters for: squeezing it would put the buttons
   * outside their own cell, which is the very thing the column's width was measured for.
   *
   * **And the table has a floor.** A squeezed table on a phone would be eight columns of
   * forty pixels, which is not a table anybody can read, so a squeezed column stops at the
   * width a cell still reads at (`MIN_COLUMN_WIDTH`) and the table keeps the sum
   * of those floors when the box is narrower still: it then scrolls inside the card, which
   * is what a table on a phone is supposed to do. The room a floored column does not need
   * is given to the columns that can still take it, so the table ends up at the width of the
   * box rather than a little past it.
   *
   * The fitting is a fact about the screen and not about the arrangement: the widths the
   * visitor chose stay as they are, so a wider window - or a collapsed sidebar - gives the
   * columns their room back.
   */
  const selectionWidth = selection ? SELECTION_WIDTH : 0
  const room = boxWidth === null ? null : boxWidth - selectionWidth

  // The columns that may be squeezed, and the ones that may not, with the widths of each.
  const squeezable = columns.map((column, index) => ({ column, index })).filter(
    ({ column }) => column.ellipsis !== undefined && typeof column.width === 'number',
  )
  const squeezableWidth = squeezable.reduce(
    (sum, { column }) => sum + (column.width as number),
    0,
  )
  const fixedWidth = tableWidth - squeezableWidth

  // The table's own floor: the fixed columns as they are, and the squeezable ones at the
  // width a cell still reads at.
  const minimum = fixedWidth + squeezable.length * MIN_COLUMN_WIDTH

  // The width the columns are given room for: what the box has, but never less than the
  // floor. It is *not* capped at the width the columns asked for: a box wider than the
  // table is handed out as well, and handing it out here rather than leaving it to the
  // browser is the point - a browser that stretches a fixed-layout table stretches every
  // column in proportion, and the actions column is the one column whose width is a
  // measurement rather than a preference.
  const target = room === null ? tableWidth : Math.max(room, minimum)

  // The room divided between the squeezable columns, in the proportions the visitor arranged
  // them in and never below the floor.
  const fitted = distributeWidths(
    squeezable.map(({ column }) => column.width as number),
    target - fixedWidth,
    MIN_COLUMN_WIDTH,
  )

  const fittedColumns = columns.map((column, index) => {
    const position = squeezable.findIndex((entry) => entry.index === index)
    return position === -1 ? column : { ...column, width: fitted[position] }
  })

  // The width the fitted columns actually take. It is read off the columns rather than off
  // the target, because the floor can make them wider than the target asked for: a column
  // squeezed to its last pixel stops there, and the table is then a little wider than the
  // room it was given - which is the floor doing its job, and not a rounding error to be
  // hidden from `scroll.x`.
  const fittedWidth = fittedColumns.reduce(
    (sum, column) => sum + (typeof column.width === 'number' ? column.width : 0),
    0,
  )

  return (
    <div ref={boxRef} style={{ width: '100%' }}>
      <Table<Row>
        rowKey="id"
        columns={fittedColumns}
        dataSource={rows}
        loading={loading}
        components={{ header: { cell: ResizableHeaderCell } }}
        rowSelection={
          selection
            ? {
                selectedRowKeys: selection.selectedRowKeys,
                onChange: selection.onChange,
                columnWidth: SELECTION_WIDTH,
              }
            : undefined
        }
        scroll={{ x: fittedWidth + selectionWidth }}
        onChange={table.handleTableChange}
        expandable={expandable}
        rowClassName={rowClassName ? (row: Row) => rowClassName(row) : undefined}
        locale={{ emptyText }}
        pagination={{
          ...table.pagination,
          total,
          showTotal,
        }}
      />
    </div>
  )
}
