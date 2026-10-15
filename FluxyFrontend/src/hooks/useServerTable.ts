import { useState } from 'react'
import type { TablePaginationConfig, TableProps } from 'antd'

/** The two orders the server understands, spelled the way a query parameter spells them. */
type SortOrder = 'asc' | 'desc'

interface ServerTableOptions<SortField extends string> {
  /**
   * The order the server is asked for until the visitor picks a column, and the order a
   * sorted header falls back to when it is clicked through to "no order".
   */
  defaultSort: { by: SortField; order: SortOrder }
  /**
   * How a column key names the server's sort field, for the tables whose column keys are not
   * already the field names - the visit history keys its "When" column `when` while the
   * endpoint orders by `visitedAt`, so that page passes a mapping. Left out, the key *is*
   * the field: the other three tables key their sortable columns by the field names the
   * endpoint declares, and the cast inside is the contract that makes those two lists one -
   * only columns carrying `sorter: true` ever produce a sorter event, and every table makes
   * those columns the server's own names.
   */
  sortFieldOf?: (columnKey: string) => SortField
  /** How many rows one page holds, when the table does not follow the server's own default. */
  pageSize?: number
  /** The sizes the pager offers, in the order it shows them. The server's ceiling is 100. */
  pageSizeOptions?: number[]
}

/**
 * The five parameters every server-paged table in this application is read with, and the
 * controls that write them: the pager, the search box, and the two sortable headers.
 *
 * This exists because five tables asked the same question in the same way and the answer was
 * written out five times - `page`, `pageSize`, the typed search and the applied one, the two
 * halves of the order, and the one handler antd reports paging and sorting through. Two of
 * those pieces are the kind that cannot be copied correctly by eye: the handler has to tell a
 * page turn from a sort (a new page keeps the order, a new order starts the pages over), and
 * `applySearch` has to compare the *trimmed* value against what the server was last asked, or
 * a trailing space would be a request that changed nothing.
 *
 * What it deliberately does **not** own is everything that differs per table: the page-level
 * filters (kept in the page's state or in the address), bulk selection and the busy row ids,
 * the automatic re-read, the column widths, and the wording of `showTotal` - a hook cannot
 * hold a translation key for a page it does not know the name of. The pager object it returns
 * is the part every table states identically; `total` and `showTotal` are spread in beside it
 * at the call site, where the rows and the locale keys are.
 *
 * `page` and `setPage` are returned for one case that is not a control: a delete that empties
 * the page the visitor is standing on has to step back a page, and only the page's own write
 * handler knows when that happened.
 */
export function useServerTable<Row, SortField extends string>({
  defaultSort,
  sortFieldOf,
  pageSize: initialPageSize = 20,
  pageSizeOptions = [10, 20, 50, 100],
}: ServerTableOptions<SortField>) {
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(initialPageSize)
  // What is typed and what the server has been asked are two different things: one request
  // per keystroke would be a request per character of a user agent, and the answer to the
  // first nine would arrive after the tenth and put a stale page on screen.
  const [searchText, setSearchText] = useState('')
  const [search, setSearch] = useState('')
  const [sortBy, setSortBy] = useState<SortField>(defaultSort.by)
  const [sortOrder, setSortOrder] = useState<SortOrder>(defaultSort.order)

  /**
   * Applies what is in the search box, from the field itself rather than from a button, so
   * the empty box empties the list the moment it becomes empty. Applied from the page's
   * `onChange`/`onSearch`, and the comparison is against the applied value rather than the
   * typed one, so typing into a box whose text was already sent asks for nothing new.
   */
  const applySearch = (value: string) => {
    const next = value.trim()

    if (next === search.trim()) return

    setSearch(next)
    setPage(1)
  }

  /** Starts the pager over, for a filter that made the list a different list. */
  const resetPage = () => setPage(1)

  /**
   * Reads the pager and the sortable headers together, because antd reports both through one
   * handler and the two behave differently: a new page keeps the order, a new order starts
   * the pages over.
   */
  const handleTableChange: NonNullable<TableProps<Row>['onChange']> = (
    pagination,
    _filters,
    sorter,
    extra,
  ) => {
    const nextPageSize = pagination.pageSize ?? pageSize

    if (nextPageSize !== pageSize) {
      // A different page size is a different pager, so this starts again at the first page
      // rather than leaving the visitor on a page number that no longer means what it did.
      setPageSize(nextPageSize)
      setPage(1)
      return
    }

    if (extra.action === 'paginate') {
      setPage(pagination.current ?? 1)
      return
    }

    setPage(1)

    const single = Array.isArray(sorter) ? sorter[0] : sorter

    if (!single?.order) {
      // The header was clicked through its sorted state to none, which is the visitor asking
      // for no order in particular. The default is then what the server is being sent, and
      // the header has to say so - a table that quietly applies its default while no arrow
      // is showing would tell them the column is unsorted when it is not.
      setSortBy(defaultSort.by)
      setSortOrder(defaultSort.order)
      return
    }

    const columnKey = String(single.columnKey)
    setSortBy(sortFieldOf ? sortFieldOf(columnKey) : (columnKey as SortField))
    setSortOrder(single.order === 'ascend' ? 'asc' : 'desc')
  }

  /**
   * Which order a column is in, or nothing when it is not the one the table is sorted by.
   *
   * The arrows are controlled rather than left to antd: the server always applies an order,
   * so a header that showed no arrow while rows arrived newest-first would be describing a
   * table that does not exist.
   */
  const orderOf = (key: SortField): 'ascend' | 'descend' | null =>
    sortBy === key ? (sortOrder === 'asc' ? 'ascend' : 'descend') : null

  return {
    /** Exactly the parameters the endpoints take, for the page's own query hook. */
    query: { page, pageSize, search, sortBy, sortOrder },
    page,
    setPage,
    searchText,
    setSearchText,
    applySearch,
    resetPage,
    orderOf,
    handleTableChange,
    /** The pager fields every table states identically; `total`/`showTotal` are the page's. */
    pagination: {
      current: page,
      pageSize,
      showSizeChanger: true,
      pageSizeOptions,
    } satisfies TablePaginationConfig,
  }
}
