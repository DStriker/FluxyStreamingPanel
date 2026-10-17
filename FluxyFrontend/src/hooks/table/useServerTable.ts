import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import type { TablePaginationConfig, TableProps } from 'antd'
import { enumParam } from '../../lib/tableParams'

/** The two orders the server understands, spelled the way a query parameter spells them. */
type SortOrder = 'asc' | 'desc'

/** The two orders as a set `enumParam` can judge an address by. */
const ORDERS: readonly SortOrder[] = ['asc', 'desc']

interface ServerTableOptions<SortField extends string> {
  /**
   * The order the server is asked for until the visitor picks a column, and the order a
   * sorted header falls back to when it is clicked through to "no order".
   */
  defaultSort: { by: SortField; order: SortOrder }
  /**
   * Every field the endpoint can order by - not only the ones a header offers.
   *
   * The table's state travels in the address now, so this is what an incoming `?sortBy=` is
   * judged against: one of these is honoured, anything else falls back to the default rather
   * than reaching an endpoint that would answer `validation_failed`. The endpoint's own
   * union is the list, which is why a field no header offers (`createdAt`, `sortOrder`)
   * still belongs here - an address may legitimately name it.
   */
  sortFields: readonly SortField[]
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
  /** How many rows one page holds when the address does not say. */
  pageSize?: number
  /** The sizes the pager offers, in the order it shows them. The server's ceiling is 100. */
  pageSizeOptions?: number[]
}

/**
 * The five parameters every server-paged table in this application is read with, and the
 * controls that write them: the pager, the search box, and the sortable headers.
 *
 * They live in the **address**, not in this hook's state, and that is the whole of the
 * decision: `page`, `pageSize`, `search`, `sortBy` and `sortOrder` are written once and read
 * back every render, so a reload keeps the view, the address can be sent to somebody else,
 * and there is no second copy for either to drift away from. The page's own filters travel
 * the same way (`withFilter` in `src/lib/tableParams.ts`), which is what keeps a filter
 * change **one** navigation - the filter and the dropped page are one address, so one
 * request follows rather than a filter and then a page that starts over.
 *
 * Every write goes through `write` below: a copy of the current parameters with the one
 * thing that changed set or removed, written with `replace`. A value that says only what its
 * absence already says (page 1, the default order, an empty search) is removed rather than
 * written, so `/admin/users` and `/admin/users?page=1` are one address with one meaning.
 *
 * What it deliberately does **not** own is everything that differs per table: the page-level
 * filters themselves, bulk selection and the busy row ids, the automatic re-read, the column
 * widths, and the wording of `showTotal` - a hook cannot hold a translation key for a page
 * it does not know the name of. The pager object it returns is the part every table states
 * identically; `total` and `showTotal` are spread in beside it at the call site, where the
 * rows and the locale keys are.
 *
 * `page` and `setPage` are returned for one case that is not a control: a delete that empties
 * the page the visitor is standing on has to step back a page, and only the page's own write
 * handler knows when that happened.
 */
export function useServerTable<Row, SortField extends string>({
  defaultSort,
  sortFields,
  sortFieldOf,
  pageSize: initialPageSize = 20,
  pageSizeOptions = [10, 20, 50, 100],
}: ServerTableOptions<SortField>) {
  const [searchParams, setSearchParams] = useSearchParams()

  // Read back from the address every render. A value that is absent, unreadable or outside
  // what the table offers falls back to the default rather than reaching the endpoint, so a
  // stale or hand-edited link draws the unfiltered table instead of a `validation_failed`.
  const rawPage = Number(searchParams.get('page'))
  const page = Number.isInteger(rawPage) && rawPage > 0 ? rawPage : 1

  const rawPageSize = Number(searchParams.get('pageSize'))
  const pageSize = pageSizeOptions.includes(rawPageSize) ? rawPageSize : initialPageSize

  const search = (searchParams.get('search') ?? '').trim()

  const sortBy = enumParam(searchParams, 'sortBy', sortFields) ?? defaultSort.by
  const sortOrder = enumParam(searchParams, 'sortOrder', ORDERS) ?? defaultSort.order

  // What is typed and what the server has been asked are two different things: one request
  // per keystroke would be a request per character of a user agent, and the answer to the
  // first nine would arrive after the tenth and put a stale page on screen. The typed value
  // is the hook's only own state, and it is seeded from the applied one so a reload draws a
  // box that shows the search the table is filtered by.
  const [searchText, setSearchText] = useState(search)

  /**
   * Writes the five: a copy of the current address with one change, replaced rather than
   * pushed - changing what is being looked at is not a step worth navigating back through.
   */
  const write = (mutate: (next: URLSearchParams) => void) => {
    const next = new URLSearchParams(searchParams)

    mutate(next)
    setSearchParams(next, { replace: true })
  }

  /** Writes a parameter only when it differs from what its absence means. */
  const put = (params: URLSearchParams, key: string, value: string, fallback: string) => {
    if (value === fallback) params.delete(key)
    else params.set(key, value)
  }

  /**
   * Applies what is in the search box, from the field itself rather than from a button, so
   * the empty box empties the list the moment it becomes empty. Applied from the page's
   * `onChange`/`onSearch`, and the comparison is against the applied value rather than the
   * typed one, so typing into a box whose text was already sent asks for nothing new.
   */
  const applySearch = (value: string) => {
    const next = value.trim()

    if (next === search) return

    write((params) => {
      put(params, 'search', next, '')
      params.delete('page')
    })
  }

  /** The one non-control writer of `page`: the step back off a page a delete emptied. */
  const setPage = (next: number) => {
    write((params) => put(params, 'page', String(next), '1'))
  }

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
      write((params) => {
        put(params, 'pageSize', String(nextPageSize), String(initialPageSize))
        params.delete('page')
      })
      return
    }

    if (extra.action === 'paginate') {
      write((params) => put(params, 'page', String(pagination.current ?? 1), '1'))
      return
    }

    write((params) => {
      params.delete('page')

      const single = Array.isArray(sorter) ? sorter[0] : sorter

      if (!single?.order) {
        // The header was clicked through its sorted state to none, which is the visitor asking
        // for no order in particular. The default is then what the server is being sent, and
        // the header has to say so - a table that quietly applies its default while no arrow
        // is showing would tell them the column is unsorted when it is not.
        put(params, 'sortBy', defaultSort.by, defaultSort.by)
        put(params, 'sortOrder', defaultSort.order, defaultSort.order)
        return
      }

      const columnKey = String(single.columnKey)
      const field = sortFieldOf ? sortFieldOf(columnKey) : (columnKey as SortField)

      put(params, 'sortBy', field, defaultSort.by)
      put(params, 'sortOrder', single.order === 'ascend' ? 'asc' : 'desc', defaultSort.order)
    })
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
