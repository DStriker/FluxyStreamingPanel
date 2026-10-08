import { useEffect, useState } from 'react'
import { Alert, Button, Card, Input, Space, Table, Tag, Typography } from 'antd'
import type { TableColumnsType, TableProps } from 'antd'
import { useTranslation } from 'react-i18next'
import { getSessionHistory, getProfile } from '../lib/api'
import type { SessionSortField, SessionSortOrder } from '../lib/api'
import { messageForError } from '../lib/http'
import { countryName } from '../lib/countryName'
import { formatTimestamp } from '../lib/dateFormat'
import { describeUserAgent } from '../lib/userAgent'
import ResizableHeaderCell from '../components/ResizableHeaderCell'
import type { ResizableHeaderCellProps } from '../components/ResizableHeaderCell'
import TruncatedCell from '../components/TruncatedCell'
import { useColumnWidths } from '../hooks/useColumnWidths'
import type { SessionHistoryResponse, SessionVisit } from '../types'

/**
 * How many visits one page holds. The server's own default, kept in step by hand because the
 * two are one contract written twice - and because a page size the pager draws from has to be
 * known before the first answer arrives, or the pager would show one row per page until it did.
 */
const PAGE_SIZE = 20

/** The sizes the visitor may pick, in the same order the pager shows them. */
const PAGE_SIZE_OPTIONS = [10, 20, 50, 100]

/** The five columns, in the order they are drawn, named by their own keys. */
type SessionColumnKey = 'when' | 'ip' | 'country' | 'network' | 'agent'

const COLUMN_KEYS: readonly SessionColumnKey[] = ['when', 'ip', 'country', 'network', 'agent']

/**
 * The width each column starts at, and the width it goes back to when asked to reset.
 *
 * Every column has a width of its own rather than letting the browser work it out, which is the
 * whole answer to "what happens when a provider name is very long": a column without one grows
 * to fit its widest cell, so sixty characters of registry name would widen the table and push
 * every column after it sideways. The widths are fixed, the two unbounded columns truncate, and
 * the table scrolls inside its own card instead of dragging the page with it.
 *
 * These are *starting* widths, not the answer - the visitor may move them, and the sum of
 * whatever they are is what the table asks the card to be able to scroll (`tableWidth` below).
 * What the visitor may never do is remove one, which is what the floor inside
 * `useColumnWidths` clamps to.
 *
 * The country column used to be 110 for a code; it is 150 because the cell now carries a name.
 */
const DEFAULT_COLUMN_WIDTHS: Record<SessionColumnKey, number> = {
  when: 300,
  ip: 170,
  country: 150,
  network: 240,
  agent: 280,
}

/**
 * Where the widths are remembered between visits.
 *
 * Only here. Resizing is a preference about how one person reads one table, and it is worth
 * surviving a reload precisely because the alternative is re-dragging five columns every time
 * the page is opened. Nothing else about the table is stored: a stored page or filter would
 * be a claim about what is on the server, which is not true just because the browser says so.
 */
const COLUMN_WIDTHS_KEY = 'fluxy.sessions.columnWidths'

/**
 * The account's visit history: one read-only row per token the account ever held.
 *
 * Read only, and that is the whole shape of the page. There is no form here, no action column
 * and no selection - a history is a record of what happened, so the only things this page can
 * do are read it, turn its pages, narrow it, order it and arrange it. Anything else would
 * imply the visitor could edit a fact the server keeps precisely so that it cannot be edited.
 *
 * One card holds all of it. The heading, the search and the table are one thing to look at -
 * a second card carrying a sentence about the first would be a second box around a single
 * subject, and the sentence is the kind of copy a page only needs while it has no way to say
 * the same thing by working. The search replaces it: it states what the rows are by accepting
 * what you are looking for.
 *
 * Search and sort are sent to the server rather than applied to the rows on screen, and that is
 * not a preference. The pager shows `total`, and a filter applied here would count what it is
 * about to hide - the visitor would be offered four pages of five rows when the filter admitted
 * one. The same applies to the ordering: sorting one page would look correct until they turned
 * to the second. Both are therefore parameters of the request, and both reset the pager,
 * because a filtered list starts at its first page and page 4 of a new order is not where
 * anybody asked to be.
 *
 * The rows are tokens rather than sessions, so a browser that refreshed from a different network
 * appears twice. That is the point: "a sign-in I do not recognise" and "a rotation I do not
 * recognise" are the same alarm, and hiding the second behind an aggregation of the first would
 * hide the moment the network actually changed.
 *
 * Dates are rendered in the account's own time zone rather than the browser's, because the
 * profile page says they are - "Dates and times are shown in this zone" is a promise the whole
 * account area has to keep, and a history page that answered in the browser's zone would be
 * the one page where the setting visibly did nothing. The zone is read once on mount: it
 * changes only from the profile page, which is a navigation away, so re-reading it per page
 * would buy nothing. The *language* of the dates follows the page rather than the browser for
 * the same reason the country column does: this application has a language switcher, and a
 * date printed in one language beside a country name printed in another is the switcher
 * visibly failing halfway down a row.
 */
export default function SessionsPage({ sectionKey }: { sectionKey: string }) {
  const { t, i18n } = useTranslation()

  const [history, setHistory] = useState<SessionHistoryResponse | null>(null)
  const [timeZone, setTimeZone] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(PAGE_SIZE)
  // What is typed and what the server has been asked are two different things: one request per
  // keystroke would be a request per character of a user agent, and the answer to the first
  // nine would arrive after the tenth and put a stale page on screen.
  const [searchText, setSearchText] = useState('')
  const [search, setSearch] = useState('')
  const [sortBy, setSortBy] = useState<SessionSortField>('visitedAt')
  const [sortOrder, setSortOrder] = useState<SessionSortOrder>('desc')
  // Bumped by the retry button rather than by the page changing, so a failed load can be asked
  // for again without pretending the visitor navigated somewhere else.
  const [attempt, setAttempt] = useState(0)

  /**
   * The five widths: their state, their memory between visits, and the drag that changes
   * them. All of that lives in `useColumnWidths`, which the admin's user table calls too -
   * the handle, the listeners and the clamp are the part of this page that reads as if it
   * were simple and is not, and two copies of those rules would be two places for one of
   * them to be quietly wrong where nothing in `typecheck`, `lint` or the build can tell.
   */
  const { widths, tableWidth, atDefaults, reset, resizer: handleFor } =
    useColumnWidths<SessionColumnKey>({
      keys: COLUMN_KEYS,
      defaults: DEFAULT_COLUMN_WIDTHS,
      storageKey: COLUMN_WIDTHS_KEY,
    })

  /**
   * `resolvedLanguage` rather than `language`: the two differ whenever the requested language
   * has no bundle, and then every string falls back to English while `language` still reports
   * the language nobody is being shown. A page reading `Germany` next to `Германия` would be
   * exactly that disagreement, made visible in one cell.
   */
  const locale = i18n.resolvedLanguage ?? i18n.language

  useEffect(() => {
    // The zone is read for the dates below, not for this page's own state: a failure here leaves
    // the browser's zone in place and the list still renders, which is the right degradation -
    // the history matters and the exact zone it is printed in does not.
    let cancelled = false

    getProfile()
      .then((profile) => {
        if (!cancelled) setTimeZone(profile.timeZone)
      })
      .catch(() => {})

    return () => {
      cancelled = true
    }
  }, [])

  useEffect(() => {
    // A generation of one per effect run rather than one shared counter: an answer from the
    // page the visitor just left must not overwrite the page they moved to, and an effect that
    // is torn down mid-flight must not write into a table that has already drawn its next row.
    let cancelled = false

    setLoading(true)

    getSessionHistory({ page, pageSize, search, sortBy, sortOrder })
      .then((answer) => {
        if (cancelled) return
        setHistory(answer)
        setLoadError(null)
      })
      .catch((error) => {
        if (cancelled) return
        setHistory(null)
        setLoadError(messageForError(error))
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [page, pageSize, search, sortBy, sortOrder, attempt])

  /**
   * Applies what is in the search box, from the field itself rather than from a button, so the
   * empty box empties the list the moment it becomes empty.
   */
  const applySearch = (value: string) => {
    const next = value.trim()
    const current = search.trim()

    if (next === current) return

    setSearch(next)
    setPage(1)
  }

  /**
   * Reads the pager and the sortable headers together, because antd reports both through one
   * handler and the two behave differently: a new page keeps the order, a new order starts the
   * pages over.
   */
  const handleTableChange: TableProps<SessionVisit>['onChange'] = (
    pagination,
    _filters,
    sorter,
    extra,
  ) => {
    const nextPageSize = pagination.pageSize ?? PAGE_SIZE

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
      // for no order in particular. The default is then what the server is being sent, and the
      // header has to say so - a table that quietly applies its default order while no arrow is
      // showing is telling them the column is unsorted when it is not.
      setSortBy('visitedAt')
      setSortOrder('desc')
      return
    }

    setSortBy(single.columnKey === 'ip' ? 'ip' : 'visitedAt')
    setSortOrder(single.order === 'ascend' ? 'asc' : 'desc')
  }

  /**
   * A date as this account's own zone and the page's own language show it, falling back to the
   * browser's zone when the stored one is absent or is one this ICU build refuses to format in.
   */
  const formatMoment = (value: string): string => formatTimestamp(value, locale, timeZone)

  /** A value the server could not determine, drawn as a dash rather than as an empty cell. */
  const orDash = (value: string | number | null | undefined): string =>
    value === null || value === undefined || value === '' ? '—' : String(value)

  /**
   * Which order a column is in, or nothing when it is not the one the page is sorted by.
   *
   * The arrows are controlled rather than left to antd: the server always applies an order, so
   * a header that showed no arrow while rows arrived newest-first would be describing a table
   * that does not exist. Only the two stored columns can be sorted at all - see the note on
   * `SessionSortField`.
   */
  const orderOf = (key: SessionSortField) =>
    sortBy === key ? (sortOrder === 'asc' ? 'ascend' : 'descend') : null

  /**
   * One column's resize handle, with this page's name for the column.
   *
   * The hook owns the drag, the clamp and the stored widths, but only the page knows that a
   * column is called "When": the handle's accessible name is built here and handed over as an
   * argument, because a translation key is not something a generic hook can hold.
   */
  const resizer = (key: SessionColumnKey): ResizableHeaderCellProps =>
    handleFor(key, t('sessions.resizeColumn', { column: t(`sessions.columns.${key}`) }))

  const columns: TableColumnsType<SessionVisit> = [
    {
      title: t('sessions.columns.when'),
      key: 'when',
      width: widths.when,
      sorter: true,
      sortOrder: orderOf('visitedAt'),
      onHeaderCell: () => resizer('when'),
      render: (_, visit) => (
        <Space size={8} wrap>
          <span>{formatMoment(visit.visitedAt)}</span>
          {visit.isCurrent && (
            <Tag color="processing" style={{ marginInlineEnd: 0 }}>
              {t('sessions.current')}
            </Tag>
          )}
        </Space>
      ),
    },
    {
      title: t('sessions.columns.ip'),
      dataIndex: 'ip',
      key: 'ip',
      width: widths.ip,
      sorter: true,
      sortOrder: orderOf('ip'),
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('ip'),
      render: (value: string | null) => (
        <Typography.Text code>
          <TruncatedCell text={orDash(value)} full={value} />
        </Typography.Text>
      ),
    },
    {
      // A code is what GeoIP produces and what the allow lists are written against, but nobody
      // reads `DE` as a country - they read Germany, or `Германия` on a Russian page. The name
      // is looked up where the names live (`countryName`, which is the platform's own table),
      // and the column is 150 rather than the 110 a two-letter code needed because a name does
      // not fit in that. The tooltip is the name rather than the code: the cell may still be
      // cut for `South Georgia and the South Sandwich Islands`, and the code is not what this
      // column is showing anyone.
      title: t('sessions.columns.country'),
      dataIndex: 'countryCode',
      key: 'country',
      width: widths.country,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('country'),
      render: (value: string | null) => {
        const name = countryName(value, locale)
        return name === null ? '—' : <TruncatedCell text={name} full={name} />
      },
    },
    {
      // The one column whose content the server does not bound. An organisation name is a
      // registry's idea of a company and can run to sixty characters, so this is where an
      // unbounded column would have widened the whole table - the width above and the
      // truncation below are what keep the answer to that in this cell and nowhere else.
      title: t('sessions.columns.network'),
      key: 'network',
      width: widths.network,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('network'),
      render: (_, visit) => {
        const { organization, autonomousSystemNumber } = visit

        if (organization === null && autonomousSystemNumber === null) return '—'

        const label =
          autonomousSystemNumber === null
            ? (organization as string)
            : organization === null
              ? `AS${autonomousSystemNumber}`
              : `${organization} · AS${autonomousSystemNumber}`

        return <TruncatedCell text={label} full={label} />
      },
    },
    {
      // The summary replaces the raw string rather than joining it, and the tooltip carries the
      // raw string instead: twenty rows of `Mozilla/5.0 (Windows NT 10.0; Win64; x64) ...` is
      // a wall nobody scans, while `Chrome 140 (Windows 10, x64)` is one line per row - and
      // "is this the client I own" is answered by the whole thing, on hover, as before. A
      // client the parser has no name for keeps its string untouched; see `describeUserAgent`.
      title: t('sessions.columns.agent'),
      dataIndex: 'userAgent',
      key: 'agent',
      width: widths.agent,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('agent'),
      render: (value: string | null) => {
        if (!value) return '—'
        return <TruncatedCell text={describeUserAgent(value) ?? value} full={value} />
      },
    },
  ]

  return (
    <Card
      title={t(sectionKey)}
      extra={
        <Space wrap>
          <Input.Search
            allowClear
            aria-label={t('sessions.searchLabel')}
            placeholder={t('sessions.searchPlaceholder')}
            style={{ width: 320 }}
            value={searchText}
            onChange={(event) => {
              setSearchText(event.target.value)

              // An emptied box empties the list straight away. Waiting for Enter would mean the
              // visitor clears the field, sees the old filter still applied, and has to press
              // something they just finished typing into.
              if (event.target.value === '') applySearch('')
            }}
            onSearch={applySearch}
          />
          {/* Disabled rather than hidden when the widths are already the defaults: a control
              that appears and disappears reads as a broken one, and there is nothing to undo
              until somebody has actually dragged something. */}
          <Button
            disabled={atDefaults}
            onClick={() => reset()}
          >
            {t('sessions.resetWidths')}
          </Button>
        </Space>
      }
    >
      {loadError ? (
        <Alert
          type="error"
          showIcon
          message={loadError}
          action={
            <Button size="small" onClick={() => setAttempt((value) => value + 1)} loading={loading}>
              {t('actions.retry')}
            </Button>
          }
        />
      ) : (
        <Table<SessionVisit>
          rowKey="id"
          columns={columns}
          dataSource={history?.items ?? []}
          loading={loading}
          // `ResizableHeaderCell` rather than the default cell: the handles are a sibling of the
          // header's own content, and the only way to put them there without nesting a focusable
          // span inside antd's sortable `<button>` is to own the cell. It is a module-level
          // function on purpose - a component type created inside this one would be a new type
          // on every repaint, and React would then unmount the handle mid-drag, dropping the
          // pointer with it.
          components={{ header: { cell: ResizableHeaderCell } }}
          scroll={{ x: tableWidth }}
          onChange={handleTableChange}
          locale={{ emptyText: t(search ? 'sessions.noMatches' : 'sessions.empty') }}
          pagination={{
            current: page,
            pageSize,
            total: history?.total ?? 0,
            showSizeChanger: true,
            pageSizeOptions: PAGE_SIZE_OPTIONS,
            showTotal: (total, range) =>
              t('sessions.range', { from: range[0], to: range[1], total }),
          }}
        />
      )}
    </Card>
  )
}
