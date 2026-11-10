import { useEffect, useRef, useState } from 'react'
import { Alert, App, Button, Card, DatePicker, Select, Space, Tag } from 'antd'
import type { TableColumnsType } from 'antd'
import type { Dayjs } from 'dayjs'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { ClearOutlined, PauseCircleOutlined, ReloadOutlined } from '@ant-design/icons'
import { STREAM_PROBLEMS_PAGE_SIZE } from '../../features/stream-problems/api'
import type { StreamProblemSortField } from '../../features/stream-problems/api'
import type { StreamProblemRow } from '../../features/stream-problems/types'
import {
  COLUMN_KEYS,
  COLUMN_WIDTHS_KEY,
  DEFAULT_COLUMN_WIDTHS,
  sortFieldOf,
} from '../../features/stream-problems/layout'
import type { StreamProblemColumnKey } from '../../features/stream-problems/layout'
import {
  DATE_FROM_KEY,
  DATE_TO_KEY,
  NO_DATE_RANGE,
  dateRangeParams,
  rangeFromPicker,
  rangePickerValue,
} from '../../features/stream-problems/dateRange'
import { useStreamProblems } from '../../features/stream-problems/hooks/queries'
import { useClearStreamProblems } from '../../features/stream-problems/hooks/mutations'
import { useProfile } from '../../features/profile/hooks/queries'
import { useVideoStreams } from '../../features/video-streams/hooks/queries'
import { ResetColumnWidths, ServerTable, TableSearch } from '../../components/table'
import type { ResizableHeaderCellProps } from '../../components/table'
import { useColumnWidths, useServerTable } from '../../hooks/table'
import { formatDay, formatTimestamp } from '../../utils/dateFormat'
import { hinted } from '../../utils/hinted'
import { messageForError, successTextFor } from '../../utils/http'
import { withFilter } from '../../utils/tableParams'
import { tableResults } from '../../utils/tableResults'
import { textCell } from '../../utils/tableCells'

/** How often the table re-reads itself while the toggle is on, in milliseconds. */
const AUTO_REFRESH_MS = 30_000

/**
 * The stream problem journal: every problem the stream producer reported, which stream it was
 * about, what it said and when.
 *
 * **The journal is evidence, not a table of resources.** Nothing on this screen writes a row:
 * a problem is recorded by the future producer through the backend's own service, so there is
 * no create or edit control anywhere - the one write is the total erase. That is also why the
 * stream cell draws the stream's name alone: the opaque `streamId` is the row's foreign key,
 * not a fact the visitor reads.
 *
 * Every parameter lives in the address: the shared five `useServerTable` reads, the `streamId`
 * filter and the two date bounds. The range is the one control too wide for the card head, so
 * it is a row of its own in the card body above the tag row and the table - the placement
 * `ConnectionLogsPage` sets.
 *
 * **Read and clear are different grants.** `viewStreams` opens the list; `editStreams` opens
 * the wipe, because destroying the streams' own record is a change to what they surface holds.
 *
 * **Clear-all is never disabled by data state.** The button's words are a promise about the
 * journal, not about the rows the filters admit: greying it out because the filtered slice is
 * empty would be lying about a journal that still holds rows. Only the permission gates it, and
 * the confirmation is the guard.
 */
export default function StreamProblemsPage(_props: { sectionKey: string }) {
  const { t, i18n } = useTranslation()
  const { message, modal } = App.useApp()
  const [searchParams, setSearchParams] = useSearchParams()

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
    profileQuery.data !== undefined && !(profileQuery.data.permissions ?? []).includes('viewStreams')
  const canClear = granted.includes('editStreams')

  const table = useServerTable<StreamProblemRow, StreamProblemSortField>({
    defaultSort: { by: 'createdAt', order: 'desc' },
    // The only column the endpoint orders by. Nothing joined or resolved is sortable: an order
    // on such a value would order the page on screen rather than the list.
    sortFields: ['createdAt'],
    sortFieldOf: (columnKey) => sortFieldOf(columnKey),
    pageSize: STREAM_PROBLEMS_PAGE_SIZE,
  })

  /** The stream filter, written by the toolbar's picker. A blank value is no filter. */
  const streamId = searchParams.get('streamId') || null

  /** The two date bounds the address carries, both inclusive on the server. */
  const dateRange = dateRangeParams(searchParams, DATE_FROM_KEY, DATE_TO_KEY)

  const journalQuery = useStreamProblems(
    {
      ...table.query,
      streamId,
      dateFrom: dateRange.from,
      dateTo: dateRange.to,
    },
    { enabled: !refused },
  )
  const { data: journal, loadError, loading } = tableResults(journalQuery)

  /**
   * The stream picker's options, read at the page level across features - the connection log's
   * precedent. A failure here offers an empty picker rather than failing the table.
   */
  const streamsQuery = useVideoStreams({
    page: 1,
    pageSize: 1000,
    search: '',
    sortBy: 'name',
    sortOrder: 'asc',
  })
  const streamOptions = (streamsQuery.data?.items ?? []).map((stream) => ({
    value: stream.id,
    label: stream.name,
  }))

  const { widths, tableWidth, atDefaults, reset, resizer: handleFor } =
    useColumnWidths<StreamProblemColumnKey>({
      keys: COLUMN_KEYS,
      defaults: DEFAULT_COLUMN_WIDTHS,
      storageKey: COLUMN_WIDTHS_KEY,
    })

  const clearJournal = useClearStreamProblems()

  /**
   * Whether the table re-reads itself. **On by default**, on the owner's call: problems arrive
   * continuously while streams run, so this page is a live feed rather than an audit trail read
   * once. Pausing is one press, and the button's accessible name says which action it performs.
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
   * calls would re-read the half-written range in between. The conversion to the account's
   * zone lives in the shared `dateRange` helper, so the picker's day and the printed day agree.
   */
  const chooseRange = (value: [Dayjs | null, Dayjs | null] | null) => {
    const range = value ? rangeFromPicker(value, timeZone) : NO_DATE_RANGE
    const withFrom = withFilter(searchParams, DATE_FROM_KEY, range.from)

    setSearchParams(withFilter(withFrom, DATE_TO_KEY, range.to), { replace: true })
  }

  /**
   * Empties the whole journal, after asking once.
   *
   * The command names nothing - no stream, no date, no search - and the dialog says exactly
   * that, because the button's own words are a promise about the journal and not about the rows
   * on screen. Deliberately not disabled on an empty or filtered view.
   */
  const confirmClear = () => {
    modal.confirm({
      title: t('streamProblems.clearConfirmTitle'),
      content: t('streamProblems.clearConfirmBody'),
      okText: t('streamProblems.clearConfirmOk'),
      okButtonProps: { danger: true },
      cancelText: t('actions.cancel'),
      onOk: async () => {
        try {
          const result = await clearJournal.mutateAsync({})

          message.success(successTextFor(result))
          void journalQuery.refetch()
        } catch (error) {
          message.error(messageForError(error))
        }
      },
    })
  }

  const resizer = (key: StreamProblemColumnKey): ResizableHeaderCellProps =>
    handleFor(
      key,
      t('streamProblems.resizeColumn', {
        column: t(`streamProblems.columns.${key}`),
      }),
    )

  const columns: TableColumnsType<StreamProblemRow> = [
    {
      // The stream's name as it reads now, resolved by the server's join. The opaque `streamId`
      // is not drawn: it identifies the foreign key, not the stream the visitor came to see.
      // The native `title` is antd's own ellipsis tooltip, which this cell needs no wrapper for.
      title: t('streamProblems.columns.stream'),
      dataIndex: 'streamName',
      key: 'stream',
      width: widths.stream,
      ellipsis: { showTitle: true },
      onHeaderCell: () => resizer('stream'),
    },
    {
      title: t('streamProblems.columns.error'),
      dataIndex: 'errorMessage',
      key: 'error',
      width: widths.error,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('error'),
      render: (value: string) => textCell(value),
    },
    {
      title: t('streamProblems.columns.createdAt'),
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: widths.createdAt,
      sorter: true,
      sortOrder: table.orderOf('createdAt'),
      onHeaderCell: () => resizer('createdAt'),
      render: (value: string) => formatTimestamp(value, locale, timeZone),
    },
  ]

  const streamFilterLabel =
    streamOptions.find((option) => option.value === streamId)?.label ?? streamId

  /**
   * Every active filter as a closable tag, in a fixed order rather than the order they were
   * applied. The range leads, as it does on the connection and user logs. The search is not a
   * tag - its own box is where it is read and cleared.
   */
  const activeFilters: { key: string; label: string; clear?: () => void }[] = []

  if (dateRange.from || dateRange.to) {
    const from = formatDay(dateRange.from, locale, timeZone)
    const to = formatDay(dateRange.to, locale, timeZone)

    const label = from && to
      ? t('streamProblems.filters.dateRange', { from, to })
      : from
        ? t('streamProblems.filters.dateRangeFrom', { from })
        : t('streamProblems.filters.dateRangeTo', { to })

    activeFilters.push({
      key: 'dateRange',
      label,
      clear: () => chooseRange(null),
    })
  }

  if (streamId) {
    activeFilters.push({
      key: 'streamId',
      label: t('streamProblems.filters.stream', { value: streamFilterLabel }),
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
  const refreshTip = `${t(autoRefresh ? 'streamProblems.pauseRefresh' : 'streamProblems.resumeRefresh')} (${t('streamProblems.autoRefreshHint', { seconds: AUTO_REFRESH_MS / 1000 })})`

  /**
   * The refusal, drawn **in place of the whole toolbar, the filters and the table**. The card
   * and its heading stay, because the address still names the page even when the grant does
   * not - and an empty table under a toast would describe a journal that exists.
   */
  if (refused) {
    return (
      <Card
        className="page-card"
        title={t('streamProblems.title')}
        styles={{ title: { whiteSpace: 'nowrap' } }}
      >
        <Alert type="warning" showIcon message={t('streamProblems.noPermission')} />
      </Card>
    )
  }

  return (
    <Card
      className="page-card"
      title={t('streamProblems.title')}
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
            labelKey="streamProblems.searchLabel"
            placeholderKey="streamProblems.searchPlaceholder"
            width={190}
          />
          <Select
            allowClear
            showSearch
            aria-label={t('streamProblems.filterAnyStream')}
            style={{ width: 220 }}
            value={streamId ?? undefined}
            options={streamOptions}
            placeholder={t('streamProblems.filterAnyStream')}
            optionFilterProp="label"
            onChange={(value) => choose('streamId', value)}
          />
          <ResetColumnWidths
            label={t('streamProblems.resetWidths')}
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
              {t('streamProblems.clearAll')}
            </Button>
          ) : (
            hinted(
              t('streamProblems.noPermission'),
              <Button danger icon={<ClearOutlined />} disabled>
                {t('streamProblems.clearAll')}
              </Button>,
            )
          )}
        </Space>
      }
    >
      {/* The date range is a row of its own, under the card head and above the tag row and the
          table: it is the one filter whose control is too wide to share the toolbar. The value
          is derived from the address rather than held in state, so an arriving link shows the
          range it carries; the conversion to the account's zone lives in `dateRange.ts`. */}
      <div style={{ marginBottom: 12 }}>
        <DatePicker.RangePicker
          allowClear
          aria-label={t('streamProblems.filterDateRange')}
          style={{ width: 250 }}
          value={rangePickerValue(dateRange, timeZone)}
          placeholder={[
            t('streamProblems.filterDateFrom'),
            t('streamProblems.filterDateTo'),
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

      <ServerTable<StreamProblemRow, StreamProblemSortField>
        table={table}
        columns={columns}
        rows={journal?.items ?? []}
        total={journal?.total ?? 0}
        loading={loading}
        loadError={loadError}
        onRetry={() => void journalQuery.refetch()}
        tableWidth={tableWidth}
        emptyText={t(filtered ? 'streamProblems.noMatches' : 'streamProblems.empty')}
        showTotal={(total, [from, to]) => t('streamProblems.range', { from, to, total })}
      />
    </Card>
  )
}
