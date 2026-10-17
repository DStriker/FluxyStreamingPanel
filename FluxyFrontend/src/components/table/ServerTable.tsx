import { Table } from 'antd'
import type { TableColumnsType, TablePaginationConfig } from 'antd'
import type { Key } from 'react'
import type { useServerTable } from '../../hooks/useServerTable'
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
}: ServerTableProps<Row, SortField>) {
  if (loadError) {
    return <LoadErrorAlert message={loadError} loading={loading} onRetry={onRetry} />
  }

  return (
    <Table<Row>
      rowKey="id"
      columns={columns}
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
      scroll={{ x: tableWidth + (selection ? SELECTION_WIDTH : 0) }}
      onChange={table.handleTableChange}
      locale={{ emptyText }}
      pagination={{
        ...table.pagination,
        total,
        showTotal,
      }}
    />
  )
}
