import { useEffect, useState } from 'react'
import { Alert, Button, Card, Input, Space, Table, Tag, Tooltip, Typography } from 'antd'
import type { TableColumnsType, TableProps } from 'antd'
import { useTranslation } from 'react-i18next'
import { getSessionHistory, getProfile } from '../lib/api'
import type { SessionSortField, SessionSortOrder } from '../lib/api'
import { messageForError } from '../lib/http'
import type { SessionHistoryResponse, SessionVisit } from '../types'

/**
 * How many visits one page holds. The server's own default, kept in step by hand because the
 * two are one contract written twice - and because a page size the pager draws from has to be
 * known before the first answer arrives, or the pager would show one row per page until it did.
 */
const PAGE_SIZE = 20

/** The sizes the visitor may pick, in the same order the pager shows them. */
const PAGE_SIZE_OPTIONS = [10, 20, 50, 100]

/**
 * The sum of the column widths, and so the width the table needs before it may start scrolling.
 *
 * Every column has a width of its own rather than letting the browser work it out, which is the
 * whole answer to "what happens when a provider name is very long": a column without one grows
 * to fit its widest cell, so sixty characters of registry name would widen the table and push
 * every column after it sideways. The widths are fixed here, the two unbounded columns
 * truncate, and the table scrolls inside its own card instead of dragging the page with it.
 */
const TABLE_WIDTH = 1100

/**
 * A cell whose whole value does not fit, with the rest of it on hover.
 *
 * Three columns need this: a user agent is a sentence, a provider name is whatever the registry
 * called the company, and an IPv6 address is thirty-nine characters. None of them can be
 * allowed to widen its column, and none of them may be silently cut off either - the point of
 * the page is to read what is there.
 *
 * `full` is a separate value rather than always the text, because the network column shows one
 * line while the cell may hold both a name and an AS number. It is deliberately not antd's own
 * cell tooltip: that one takes the value it was given and an ellipsised cell has been cut
 * before the browser ever builds a `title`.
 */
function TruncatedCell({ text, full }: { text: string; full?: string | null }) {
  if (!full) return <>{text}</>

  return (
    <Tooltip
      title={full}
      styles={{ container: { maxWidth: 560, whiteSpace: 'normal', wordBreak: 'break-word' } }}
    >
      <span>{text}</span>
    </Tooltip>
  )
}

/**
 * The account's visit history: one read-only row per token the account ever held.
 *
 * Read only, and that is the whole shape of the page. There is no form here, no action column
 * and no selection - a history is a record of what happened, so the only things this page can
 * do are read it, turn its pages, narrow it and order it. Anything else would imply the visitor
 * could edit a fact the server keeps precisely so that it cannot be edited.
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
 * account area has to keep, and a history page that answered in the browser's zone would be the
 * one page where the setting visibly did nothing. The zone is read once on mount: it changes
 * only from the profile page, which is a navigation away, so re-reading it per page would buy
 * nothing.
 */
export default function SessionsPage({ sectionKey }: { sectionKey: string }) {
  const { t } = useTranslation()

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
   * A date as this account's own zone shows it, falling back to the browser's when the zone is
   * absent or is one the runtime refuses to format in.
   *
   * The `try` is not decoration. A zone stored by an older build, or a zone this ICU build does
   * not carry, makes `Intl.DateTimeFormat` throw before it returns a formatter - and an
   * exception thrown while rendering a row would take the whole page down over one timestamp.
   */
  const formatMoment = (value: string): string => {
    const date = new Date(value)

    if (Number.isNaN(date.getTime())) return '—'

    try {
      return new Intl.DateTimeFormat(undefined, {
        dateStyle: 'medium',
        timeStyle: 'medium',
        ...(timeZone ? { timeZone } : {}),
      }).format(date)
    } catch {
      return new Intl.DateTimeFormat(undefined, {
        dateStyle: 'medium',
        timeStyle: 'medium',
      }).format(date)
    }
  }

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

  const columns: TableColumnsType<SessionVisit> = [
    {
      title: t('sessions.columns.when'),
      key: 'when',
      width: 300,
      sorter: true,
      sortOrder: orderOf('visitedAt'),
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
      width: 170,
      sorter: true,
      sortOrder: orderOf('ip'),
      ellipsis: { showTitle: false },
      render: (value: string | null) => (
        <Typography.Text code>
          <TruncatedCell text={orDash(value)} full={value} />
        </Typography.Text>
      ),
    },
    {
      title: t('sessions.columns.country'),
      dataIndex: 'countryCode',
      key: 'country',
      width: 110,
      render: (value: string | null) => orDash(value),
    },
    {
      // The one column whose content the server does not bound. An organisation name is a
      // registry's idea of a company and can run to sixty characters, so this is where an
      // unbounded column would have widened the whole table - the width above and the
      // truncation below are what keep the answer to that in this cell and nowhere else.
      title: t('sessions.columns.network'),
      key: 'network',
      width: 240,
      ellipsis: { showTitle: false },
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
      title: t('sessions.columns.agent'),
      dataIndex: 'userAgent',
      key: 'agent',
      width: 280,
      ellipsis: { showTitle: false },
      render: (value: string | null) => <TruncatedCell text={orDash(value)} full={value} />,
    },
  ]

  return (
    <Card
      title={t(sectionKey)}
      extra={
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
          scroll={{ x: TABLE_WIDTH }}
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
