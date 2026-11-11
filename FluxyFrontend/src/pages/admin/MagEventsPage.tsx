import { useEffect, useRef, useState } from 'react'
import { Alert, App, Button, Card, DatePicker, Select, Space, Tag, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import type { Dayjs } from 'dayjs'
import { useTranslation } from 'react-i18next'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { ClearOutlined, PauseCircleOutlined, ReloadOutlined } from '@ant-design/icons'
import { MAG_EVENTS_PAGE_SIZE } from '../../features/mag-events/api'
import type { MagEventSortField } from '../../features/mag-events/api'
import type { MagEventRow, MagEventStatus, MagEventType } from '../../features/mag-events/types'
import { MAG_EVENT_TYPES } from '../../features/mag-events/events'
import {
  COLUMN_KEYS,
  COLUMN_WIDTHS_KEY,
  DEFAULT_COLUMN_WIDTHS,
  sortFieldOf,
} from '../../features/mag-events/layout'
import type { MagEventColumnKey } from '../../features/mag-events/layout'
import {
  DATE_FROM_KEY,
  DATE_TO_KEY,
  NO_DATE_RANGE,
  dateRangeParams,
  rangeFromPicker,
  rangePickerValue,
} from '../../features/mag-events/dateRange'
import { useMagEvents } from '../../features/mag-events/hooks/queries'
import { useClearMagEvents } from '../../features/mag-events/hooks/mutations'
import { useProfile } from '../../features/profile/hooks/queries'
import { ResetColumnWidths, ServerTable, TableSearch } from '../../components/table'
import type { ResizableHeaderCellProps } from '../../components/table'
import { useColumnWidths, useServerTable } from '../../hooks/table'
import { formatDay, formatTimestamp } from '../../utils/dateFormat'
import { hinted } from '../../utils/hinted'
import { messageForError, successTextFor } from '../../utils/http'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'
import { enumParam, withFilter } from '../../utils/tableParams'
import { tableResults } from '../../utils/tableResults'
import { textCell } from '../../utils/tableCells'

/** How often the table re-reads itself while the toggle is on, in milliseconds. */
const AUTO_REFRESH_MS = 30_000

/** The dash a cell draws for a value the server could not determine (U+2014). */
const DASH = '\u2014'

/**
 * The MAG event journal: every event sent to a subscription's device, which operation it was,
 * what it carried, whether the device confirmed it, and when.
 *
 * **The journal is evidence, not a table of resources.** Nothing here writes a row by hand: a
 * row is written by sending an event from the subscriptions table, and the only write this
 * screen owns is the total erase. That is also why the row's MAC is a link back to the
 * subscription it belongs to - the address is a fact about that row, and the operator who
 * wants to send another event is one click from where that happens.
 *
 * Every parameter lives in the address: the shared five `useServerTable` reads, the operation
 * filter, the subscription a drill-down names and the two date bounds. The `?subscriptionId=`
 * is what makes a drill-down a link rather than a click that appears to do nothing, and the
 * search is deliberately not one of the tags - its own box is where it is read and cleared.
 *
 * **Read and change are different grants.** `viewSubscriptions` opens the list and
 * `editSubscriptions` opens the wipe, because destroying the record of what was sent is a
 * change to the same surface.
 *
 * The date range is the one control too wide for the card head, so it is a row of its own in
 * the card body above the tag row and the table - the placement `ConnectionLogsPage` sets.
 *
 * **Clear-all is never disabled by data state.** The button's words are a promise about the
 * journal, not about the rows the filters admit: greying it out because the filtered slice is
 * empty would be lying about a journal that still holds rows. Only the permission gates it,
 * and the confirmation is the guard.
 */
export default function MagEventsPage(_props: { sectionKey: string }) {
  const { t, i18n } = useTranslation()
  const { message, modal } = App.useApp()
  const [searchParams, setSearchParams] = useSearchParams()
  const navigate = useNavigate()
  const session = useSession()

  const profileQuery = useProfile()
  const granted = profileQuery.data?.permissions ?? []
  const timeZone = profileQuery.data?.timeZone ?? null
  const locale = i18n.resolvedLanguage ?? i18n.language

  /**
   * The refusal is answered **only once the profile has answered**: an unanswered profile is
   * "not known yet", which must draw the ordinary shell in its loading form rather than an
   * accusation. The journal query is disabled while refused, so the screen does not spend a
   * request on a list the server would answer 403.
   */
  const refused =
    profileQuery.data !== undefined &&
    !(profileQuery.data.permissions ?? []).includes('viewSubscriptions')
  const canClear = granted.includes('editSubscriptions')

  const area = session ? areaForRole(session.role) : ''

  const table = useServerTable<MagEventRow, MagEventSortField>({
    defaultSort: { by: 'createdAt', order: 'desc' },
    // The only column the endpoint orders by. Nothing joined or resolved is sortable: an order
    // on such a value would order the page on screen rather than the list.
    sortFields: ['createdAt'],
    sortFieldOf: (columnKey) => sortFieldOf(columnKey),
    pageSize: MAG_EVENTS_PAGE_SIZE,
  })

  /**
   * The operation filter, judged by the server's own vocabulary: a hand-edited `?event=` that
   * names no member is dropped rather than sent, because the endpoint refuses an unknown value
   * with a 400 under that parameter's own name.
   */
  const eventFilter = enumParam<MagEventType>(searchParams, 'event', MAG_EVENT_TYPES)

  /** The subscription a drill-down named. A blank value is no filter. */
  const subscriptionId = searchParams.get('subscriptionId') || null

  /** The two date bounds the address carries, both inclusive on the server. */
  const dateRange = dateRangeParams(searchParams, DATE_FROM_KEY, DATE_TO_KEY)

  const journalQuery = useMagEvents(
    {
      ...table.query,
      event: eventFilter,
      subscriptionId,
      dateFrom: dateRange.from,
      dateTo: dateRange.to,
    },
    { enabled: !refused },
  )
  const { data: journal, loadError, loading } = tableResults(journalQuery)

  /**
   * The six operations' words, one literal `t()` per wire value.
   *
   * Written as a `Record` over the union rather than as a hand-kept array beside
   * `MAG_EVENT_TYPES`: the picker's membership and the label's spelling then come from one
   * list, and an operation added to the vocabulary is a compile error here rather than an
   * option that quietly has no text. The keys are literal so `probe:i18n` can see all six.
   */
  const eventLabelOf: Record<MagEventType, string> = {
    playChannel: t('magEvents.events.playChannel'),
    closePortal: t('magEvents.events.closePortal'),
    reloadPortal: t('magEvents.events.reloadPortal'),
    rebootDevice: t('magEvents.events.rebootDevice'),
    sendMessage: t('magEvents.events.sendMessage'),
    resetStbLock: t('magEvents.events.resetStbLock'),
  }

  /** The two confirmation states, and the tone of each tag. */
  const statusLabelOf: Record<MagEventStatus, string> = {
    confirmed: t('magEvents.statuses.confirmed'),
    notConfirmed: t('magEvents.statuses.notConfirmed'),
  }

  const statusTone: Record<MagEventStatus, string> = {
    confirmed: 'success',
    notConfirmed: 'warning',
  }

  const { widths, tableWidth, atDefaults, reset, resizer: handleFor } =
    useColumnWidths<MagEventColumnKey>({
      keys: COLUMN_KEYS,
      defaults: DEFAULT_COLUMN_WIDTHS,
      storageKey: COLUMN_WIDTHS_KEY,
    })

  const clearJournal = useClearMagEvents()

  /**
   * Whether the table re-reads itself. **On by default**: a sent event is confirmed by the
   * device a moment later, so this page is a live feed while somebody is watching one arrive.
   * Pausing is one press, and the button's accessible name says which action it performs.
   */
  const [autoRefresh, setAutoRefresh] = useState(true)

  /**
   * The timer that re-reads the table while the toggle is on.
   *
   * The ref rather than the query in the dependency array is the point: `refetch` is a new
   * function whenever the answer changes, so naming it here would restart the interval after
   * every re-read. Toggling the switch changes no query key and remounts nothing.
   */
  const refreshRef = useRef<() => void>(() => {})

  useEffect(() => {
    refreshRef.current = () => {
      if (refused) return
      void journalQuery.refetch()
    }
  })

  useEffect(() => {
    if (!autoRefresh || refused) return

    const timer = setInterval(() => refreshRef.current(), AUTO_REFRESH_MS)

    return () => clearInterval(timer)
  }, [autoRefresh, refused])

  /** Writes one filter, dropping `page` in the same write. */
  const choose = (key: string, value: string | null | undefined) =>
    setSearchParams(withFilter(searchParams, key, value), { replace: true })

  /**
   * Writes the whole date range in one navigation.
   *
   * The two bounds are one control over two parameters, so they are one write: two `choose`
   * calls would re-read the half-written range in between. The conversion to the account's zone
   * lives in the shared `dateRange` helper, so the picker's day and the printed day agree.
   */
  const chooseRange = (value: [Dayjs | null, Dayjs | null] | null) => {
    const range = value ? rangeFromPicker(value, timeZone) : NO_DATE_RANGE
    const withFrom = withFilter(searchParams, DATE_FROM_KEY, range.from)

    setSearchParams(withFilter(withFrom, DATE_TO_KEY, range.to), { replace: true })
  }

  /**
   * Empties the whole journal, after asking once.
   *
   * The command names nothing - no operation, no subscription, no date, no search - and the
   * dialog says exactly that, because the button's own words are a promise about the journal
   * and not about the rows on screen. Deliberately not disabled on an empty or filtered view.
   */
  const confirmClear = () => {
    modal.confirm({
      title: t('magEvents.clearConfirmTitle'),
      content: t('magEvents.clearConfirmBody'),
      okText: t('magEvents.clearConfirmOk'),
      okButtonProps: { danger: true },
      cancelText: t('actions.cancel'),
      onOk: async () => {
        try {
          const result = await clearJournal.mutateAsync()

          message.success(successTextFor(result))
          void journalQuery.refetch()
        } catch (error) {
          message.error(messageForError(error))
        }
      },
    })
  }

  const resizer = (key: MagEventColumnKey): ResizableHeaderCellProps =>
    handleFor(key, t('magEvents.resizeColumn', { column: t(`magEvents.columns.${key}`) }))

  /**
   * The device address as a link back to the subscription it belongs to.
   *
   * **Not permission-gated**: the whole page is behind `viewSubscriptions` already, and the
   * subscription form's address is the same area's, reached by `areaForRole` rather than a
   * copied segment. A row whose subscription has no device draws a dash - unknown is not a
   * link to anywhere.
   */
  const macCell = (row: MagEventRow) => {
    const value = row.magMac

    if (value === null) return <Typography.Text type="secondary">{DASH}</Typography.Text>

    return (
      <Typography.Link
        onClick={() => navigate(`${area}/subscriptions/${row.subscriptionId}`)}
      >
        <Typography.Text code>{value}</Typography.Text>
      </Typography.Link>
    )
  }

  const columns: TableColumnsType<MagEventRow> = [
    {
      title: t('magEvents.columns.mac'),
      dataIndex: 'magMac',
      key: 'mac',
      width: widths.mac,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('mac'),
      render: (_, row) => macCell(row),
    },
    {
      title: t('magEvents.columns.event'),
      dataIndex: 'eventType',
      key: 'event',
      width: widths.event,
      onHeaderCell: () => resizer('event'),
      render: (value: MagEventType) => eventLabelOf[value],
    },
    {
      title: t('magEvents.columns.message'),
      dataIndex: 'message',
      key: 'message',
      width: widths.message,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('message'),
      render: (value: string | null) => textCell(value ?? DASH),
    },
    {
      title: t('magEvents.columns.status'),
      dataIndex: 'status',
      key: 'status',
      width: widths.status,
      onHeaderCell: () => resizer('status'),
      render: (value: MagEventStatus) => (
        <Tag color={statusTone[value]} style={{ marginInlineEnd: 0 }}>
          {statusLabelOf[value]}
        </Tag>
      ),
    },
    {
      // Both a column of the row and the endpoint's only sort field: the timestamp is stored,
      // so ordering by it orders the list rather than the page on screen.
      title: t('magEvents.columns.date'),
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: widths.date,
      sorter: true,
      sortOrder: table.orderOf('createdAt'),
      onHeaderCell: () => resizer('date'),
      render: (value: string) => formatTimestamp(value, locale, timeZone),
    },
  ]

  /**
   * Every active filter as a closable tag, in a fixed order rather than the order they were
   * applied: the range first, as it does on every log, then the operation, then the
   * subscription a drill-down named. The search is not a tag - its own box is where it is read
   * and cleared. The picker contributes **one** tag naming everything chosen in it, because one
   * picker is one choice.
   */
  const activeFilters: { key: string; label: string; clear?: () => void }[] = []

  if (dateRange.from || dateRange.to) {
    const from = formatDay(dateRange.from, locale, timeZone)
    const to = formatDay(dateRange.to, locale, timeZone)

    const label = from && to
      ? t('magEvents.filters.dateRange', { from, to })
      : from
        ? t('magEvents.filters.dateRangeFrom', { from })
        : t('magEvents.filters.dateRangeTo', { to })

    activeFilters.push({
      key: 'dateRange',
      label,
      clear: () => chooseRange(null),
    })
  }

  if (eventFilter) {
    activeFilters.push({
      key: 'event',
      label: t('magEvents.filters.event', { value: eventLabelOf[eventFilter] }),
    })
  }

  if (subscriptionId) {
    activeFilters.push({
      key: 'subscriptionId',
      label: t('magEvents.filters.subscription', { value: `${subscriptionId.slice(0, 8)}…` }),
    })
  }

  const filtered = Boolean(table.query.search) || activeFilters.length > 0

  /**
   * What the auto-refresh button says while it is live or paused.
   *
   * It is the button's whole accessible name and its tooltip, because the control is icon-only:
   * the heading shares its line with a search box, a picker and the clear command, and a worded
   * label would push the table sideways for a control that is one glyph wide.
   */
  const refreshTip = `${t(autoRefresh ? 'magEvents.pauseRefresh' : 'magEvents.resumeRefresh')} (${t('magEvents.autoRefreshHint', { seconds: AUTO_REFRESH_MS / 1000 })})`

  /**
   * The refusal, drawn **in place of the whole toolbar, the filters and the table**. The card
   * and its heading stay, because the address still names the page even when the grant does
   * not - and an empty table under a toast would describe a journal that exists.
   */
  if (refused) {
    return (
      <Card
        className="page-card"
        title={t('magEvents.title')}
        styles={{ title: { whiteSpace: 'nowrap' } }}
      >
        <Alert type="warning" showIcon message={t('magEvents.noPermission')} />
      </Card>
    )
  }

  return (
    <Card
      className="page-card"
      title={t('magEvents.title')}
      styles={{ title: { whiteSpace: 'nowrap' } }}
      extra={
        <Space wrap>
          {hinted(
            refreshTip,
            <Button
              icon={autoRefresh ? <PauseCircleOutlined /> : <ReloadOutlined />}
              aria-label={refreshTip}
              onClick={() => setAutoRefresh((current) => !current)}
            />,
          )}
          <TableSearch
            value={table.searchText}
            onChange={table.setSearchText}
            onSearch={table.applySearch}
            labelKey="magEvents.searchLabel"
            placeholderKey="magEvents.searchPlaceholder"
            width={190}
          />
          <Select
            allowClear
            aria-label={t('magEvents.filterAnyEvent')}
            style={{ width: 170 }}
            value={eventFilter ?? undefined}
            options={MAG_EVENT_TYPES.map((event) => ({
              value: event,
              label: eventLabelOf[event],
            }))}
            placeholder={t('magEvents.filterAnyEvent')}
            onChange={(value: MagEventType | undefined) => choose('event', value)}
          />
          <ResetColumnWidths
            label={t('magEvents.resetWidths')}
            disabled={atDefaults}
            onClick={() => reset()}
          />
          {canClear ? (
            <Button
              danger
              icon={<ClearOutlined />}
              loading={clearJournal.isPending}
              onClick={confirmClear}
            >
              {t('magEvents.clearAll')}
            </Button>
          ) : (
            hinted(
              t('magEvents.noPermission'),
              <Button danger icon={<ClearOutlined />} disabled>
                {t('magEvents.clearAll')}
              </Button>,
            )
          )}
        </Space>
      }
    >
      {/* The date range is a row of its own, under the card head and above the tag row and the
          table: it is the one filter whose control is too wide to share the toolbar. The value
          is derived from the address rather than held in state, so an arriving link shows the
          range it carries. */}
      <div style={{ marginBottom: 12 }}>
        <DatePicker.RangePicker
          allowClear
          aria-label={t('magEvents.filterDateRange')}
          style={{ width: 250 }}
          value={rangePickerValue(dateRange, timeZone)}
          placeholder={[
            t('magEvents.filterDateFrom'),
            t('magEvents.filterDateTo'),
          ]}
          onChange={(value) => chooseRange(value as [Dayjs | null, Dayjs | null] | null)}
        />
      </div>

      {activeFilters.length > 0 && (
        <Space wrap size={4} style={{ marginBottom: 12 }}>
          {activeFilters.map((filter) => (
            <Tag
              key={filter.key}
              closable
              onClose={(event) => {
                event.preventDefault()

                if (filter.clear) {
                  filter.clear()
                  return
                }

                choose(filter.key, null)
              }}
            >
              {filter.label}
            </Tag>
          ))}
        </Space>
      )}

      <ServerTable<MagEventRow, MagEventSortField>
        table={table}
        columns={columns}
        rows={journal?.items ?? []}
        total={journal?.total ?? 0}
        loading={loading}
        loadError={loadError}
        onRetry={() => void journalQuery.refetch()}
        tableWidth={tableWidth}
        emptyText={t(filtered ? 'magEvents.noMatches' : 'magEvents.empty')}
        showTotal={(total, [from, to]) => t('magEvents.range', { from, to, total })}
      />
    </Card>
  )
}
