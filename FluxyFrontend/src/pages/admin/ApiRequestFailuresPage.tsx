import { useEffect, useRef, useState } from 'react'
import { Alert, App, Button, Card, DatePicker, Select, Space, Tag, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import type { Dayjs } from 'dayjs'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { ClearOutlined, PauseCircleOutlined, ReloadOutlined } from '@ant-design/icons'
import type { ApiRequestFailureSortField } from '../../features/api-request-failures/api'
import { API_REQUEST_FAILURES_PAGE_SIZE } from '../../features/api-request-failures/api'
import type { ApiRequestFailureReason, ApiRequestFailureRow } from '../../features/api-request-failures/types'
import { API_REQUEST_FAILURE_REASONS } from '../../features/api-request-failures/reasons'
import { reasonTone } from '../../features/api-request-failures/reasonTone'
import {
  COLUMN_KEYS,
  COLUMN_WIDTHS_KEY,
  DEFAULT_COLUMN_WIDTHS,
  sortFieldOf,
} from '../../features/api-request-failures/layout'
import type { ApiRequestFailureColumnKey } from '../../features/api-request-failures/layout'
import {
  DATE_FROM_KEY,
  DATE_TO_KEY,
  NO_DATE_RANGE,
  dateRangeParams,
  rangeFromPicker,
  rangePickerValue,
} from '../../features/api-request-failures/dateRange'
import { useApiRequestFailures } from '../../features/api-request-failures/hooks/queries'
import { useClearApiRequestFailures } from '../../features/api-request-failures/hooks/mutations'
import { useProfile } from '../../features/profile/hooks/queries'
import ConnectionNetworkCell from '../../features/connection-logs/components/ConnectionNetworkCell'
import { networkFilterParams } from '../../features/user-logs/networkFilters'
import { ResetColumnWidths, ServerTable, TableSearch } from '../../components/table'
import type { ResizableHeaderCellProps } from '../../components/table'
import { useColumnWidths, useServerTable } from '../../hooks/table'
import { formatDay, formatTimestamp } from '../../utils/dateFormat'
import { hinted } from '../../utils/hinted'
import { messageForError, successTextFor } from '../../utils/http'
import { listParam, withFilter, withFilterList } from '../../utils/tableParams'
import { tableResults } from '../../utils/tableResults'
import { textCell } from '../../utils/tableCells'

/** How often the table re-reads itself while the toggle is on, in milliseconds. */
const AUTO_REFRESH_MS = 30_000

/** The dash a cell draws for a value the server could not determine (U+2014). */
const DASH = '\u2014'

/** One option of the reason picker, and one filter tag's label. */
interface Option {
  value: string
  label: string
}

/**
 * The API request failure log: every public-API request that was refused, why, for which
 * subscription data and which content, and from where.
 *
 * **The log is evidence, not a table of resources.** A row may name no subscription data and
 * no content at all - a refusal for an unknown identifier or a blocked account names neither
 * - so both cells draw a dash rather than an empty gap, and the content group is null as a
 * group, which is why one dash covers its three fields.
 *
 * Every parameter lives in the address: the shared five `useServerTable` reads, the repeated
 * `reasons` filter, the three network filters and the two date bounds. The repeated filter is
 * the endpoint's own shape (`?reasons=a&reasons=b`), so the page writes it through
 * `withFilterList` and reads it back through `listParam` - a bad element is dropped rather
 * than sent, because the endpoint refuses one with a 400 under that parameter's name.
 *
 * **The network cell is this page's click-through, exactly as on the connection and user
 * logs.** Each of the three facts in it writes its own filter, and the write drops `page` in
 * the same navigation so a click from page four lands on page one of the narrower list. The
 * keys are the endpoint's own (`ip`, `country`, `isp`), which is what lets the same shared
 * cell write this screen's address.
 *
 * **`viewApiRequestFailures` gates both the read and the wipe.** There is no
 * `editApiRequestFailures`: an operator never writes a row - this capability has no
 * production write path at all - and the only write here is the total erase, so the grant
 * that admits the read admits the clear. That is the whole of the permission model on this
 * screen.
 *
 * The date range is the one control too wide for the card head, so it is a row of its own in
 * the card body above the tag row and the table - the placement `ConnectionLogsPage` sets.
 *
 * **Clear-all is never disabled by data state.** The button's words are a promise about the
 * log, not about the rows the filters admit: greying it out because the filtered slice is
 * empty would be lying about a log that still holds rows. Only the permission gates it, and
 * the confirmation is the guard.
 */
export default function ApiRequestFailuresPage(_props: { sectionKey: string }) {
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
   * accusation. The log query is disabled while refused, so the screen does not spend a
   * request on a list the server would answer 403.
   */
  const refused =
    profileQuery.data !== undefined &&
    !(profileQuery.data.permissions ?? []).includes('viewApiRequestFailures')
  const canClear = granted.includes('viewApiRequestFailures')

  const table = useServerTable<ApiRequestFailureRow, ApiRequestFailureSortField>({
    defaultSort: { by: 'createdAt', order: 'desc' },
    // Both are columns of the log row. Nothing joined or resolved is sortable: an order on
    // such a value would order the page on screen rather than the list.
    sortFields: ['createdAt', 'reason'],
    sortFieldOf: (columnKey) => sortFieldOf(columnKey),
    pageSize: API_REQUEST_FAILURES_PAGE_SIZE,
  })

  /** The repeated reason filter, each element judged by the server's own vocabulary. */
  const reasons = listParam<ApiRequestFailureReason>(
    searchParams,
    'reasons',
    API_REQUEST_FAILURE_REASONS,
  )

  /**
   * The three network filters, written by clicking a fact in the shared network cell.
   *
   * They are read through the helper the user log reads its counterparts with: a value the
   * cell wrote is the row's own stored value, and the endpoint compares it exactly. A blank
   * value is no filter, which is why the tag row and the request both treat it as absent.
   */
  const { ip, country, isp } = networkFilterParams(searchParams)

  /** The two date bounds the address carries, both inclusive on the server. */
  const dateRange = dateRangeParams(searchParams, DATE_FROM_KEY, DATE_TO_KEY)

  const logQuery = useApiRequestFailures(
    {
      ...table.query,
      reasons,
      ip,
      country,
      isp,
      dateFrom: dateRange.from,
      dateTo: dateRange.to,
    },
    { enabled: !refused },
  )
  const { data: log, loadError, loading } = tableResults(logQuery)

  /**
   * The six reasons' words, one literal `t()` per wire value.
   *
   * Written as a `Record` over the union rather than as a hand-kept array beside
   * `API_REQUEST_FAILURE_REASONS`: the picker's membership and the label's spelling then come
   * from one list, and a reason added to the vocabulary is a compile error here rather than an
   * option that quietly has no text. The keys are literal so `probe:i18n` can see all six.
   */
  const reasonLabelOf: Record<ApiRequestFailureReason, string> = {
    unknown_id: t('apiRequestFailures.reasons.unknown_id'),
    geo_ip_violation: t('apiRequestFailures.reasons.geo_ip_violation'),
    auth_failed: t('apiRequestFailures.reasons.auth_failed'),
    too_many_connections: t('apiRequestFailures.reasons.too_many_connections'),
    subscription_expired: t('apiRequestFailures.reasons.subscription_expired'),
    user_blocked: t('apiRequestFailures.reasons.user_blocked'),
  }

  /** The six reasons, each with the words this screen speaks for it. */
  const reasonOptions: Option[] = API_REQUEST_FAILURE_REASONS.map((reason) => ({
    value: reason,
    label: reasonLabelOf[reason],
  }))

  const { widths, tableWidth, atDefaults, reset, resizer: handleFor } =
    useColumnWidths<ApiRequestFailureColumnKey>({
      keys: COLUMN_KEYS,
      defaults: DEFAULT_COLUMN_WIDTHS,
      storageKey: COLUMN_WIDTHS_KEY,
    })

  const clearLog = useClearApiRequestFailures()

  /**
   * Whether the table re-reads itself. **On by default**, on the owner's call: refusals arrive
   * continuously, so this page is a live feed rather than an audit trail read once. Pausing is
   * one press, and the button's accessible name says which action it performs.
   */
  const [autoRefresh, setAutoRefresh] = useState(true)

  /**
   * The timer that re-reads the table while the toggle is on.
   *
   * The ref rather than the query in the dependency array is the point: `refetch` is a new
   * function whenever the answer changes, so naming it here would restart the interval after
   * every re-read.
   */
  const refreshRef = useRef<() => void>(() => {})

  useEffect(() => {
    refreshRef.current = () => {
      if (refused) return
      void logQuery.refetch()
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

  /** Writes the whole repeated filter: the selection in one navigation. */
  const chooseReasons = (values: string[]) =>
    setSearchParams(withFilterList(searchParams, 'reasons', values), { replace: true })

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
   * Empties the whole log, after asking once.
   *
   * The command names nothing - no reason, no network, no search - and the dialog says exactly
   * that, because the button's own words are a promise about the log and not about the rows on
   * screen. Deliberately not disabled on an empty or filtered view.
   */
  const confirmClear = () => {
    modal.confirm({
      title: t('apiRequestFailures.clearConfirmTitle'),
      content: t('apiRequestFailures.clearConfirmBody'),
      okText: t('apiRequestFailures.clearConfirmOk'),
      okButtonProps: { danger: true },
      cancelText: t('actions.cancel'),
      onOk: async () => {
        try {
          const result = await clearLog.mutateAsync({})

          message.success(successTextFor(result))
          void logQuery.refetch()
        } catch (error) {
          message.error(messageForError(error))
        }
      },
    })
  }

  const resizer = (key: ApiRequestFailureColumnKey): ResizableHeaderCellProps =>
    handleFor(
      key,
      t('apiRequestFailures.resizeColumn', {
        column: t(`apiRequestFailures.columns.${key}`),
      }),
    )

  /** The content cell: the stored name, with its opaque identifier muted underneath. */
  const contentCell = (row: ApiRequestFailureRow) => {
    if (!row.contentName && !row.contentId) {
      return <Typography.Text type="secondary">{DASH}</Typography.Text>
    }

    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 2, minWidth: 0 }}>
        <Typography.Text ellipsis={{ tooltip: row.contentName ?? undefined }}>
          {row.contentName ?? DASH}
        </Typography.Text>
        {row.contentId && (
          <Typography.Text type="secondary" ellipsis={{ tooltip: row.contentId }}>
            {row.contentId}
          </Typography.Text>
        )}
      </div>
    )
  }

  const columns: TableColumnsType<ApiRequestFailureRow> = [
    {
      title: t('apiRequestFailures.columns.subscription'),
      dataIndex: 'subscriptionData',
      key: 'subscription',
      width: widths.subscription,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('subscription'),
      render: (value: string | null) => textCell(value ?? DASH),
    },
    {
      title: t('apiRequestFailures.columns.content'),
      key: 'content',
      width: widths.content,
      onHeaderCell: () => resizer('content'),
      render: (_, row) => contentCell(row),
    },
    {
      // Both a column of the row and the endpoint's second sort field: the reason is stored,
      // so ordering by it orders the list rather than the page on screen.
      title: t('apiRequestFailures.columns.reason'),
      dataIndex: 'reason',
      key: 'reason',
      width: widths.reason,
      sorter: true,
      sortOrder: table.orderOf('reason'),
      onHeaderCell: () => resizer('reason'),
      render: (reason: ApiRequestFailureReason) => (
        <Tag color={reasonTone(reason)} style={{ marginInlineEnd: 0 }}>
          {reasonLabelOf[reason]}
        </Tag>
      ),
    },
    {
      // The shared network cell, clickable here too: this endpoint takes the same
      // `ip`/`country`/`isp` parameters as the connection and user logs, so each fact writes
      // its own filter through `choose` - the address, the provider and the flag alike.
      title: t('apiRequestFailures.columns.network'),
      key: 'network',
      width: widths.network,
      onHeaderCell: () => resizer('network'),
      render: (_, row) => <ConnectionNetworkCell log={row} locale={locale} onFilter={choose} />,
    },
    {
      title: t('apiRequestFailures.columns.createdAt'),
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: widths.createdAt,
      sorter: true,
      sortOrder: table.orderOf('createdAt'),
      onHeaderCell: () => resizer('createdAt'),
      render: (value: string) => formatTimestamp(value, locale, timeZone),
    },
  ]

  const reasonTagLabels = reasons.map((reason) => reasonLabelOf[reason])

  /**
   * Every active filter as a closable tag, in a fixed order: the date range first, then the
   * three network filters, then the reason filter. That order copies the connection and user
   * logs, where the range leads and a clicked network fact tags immediately after it: the
   * network tags are written by a click on the table, so they read ahead of the picker whose
   * values the visitor chose. The search is not a tag - its own box is where it is read and
   * cleared. The picker contributes **one** tag naming everything chosen in it, because one
   * picker is one choice.
   */
  const activeFilters: { key: string; label: string; clear?: () => void }[] = []

  if (dateRange.from || dateRange.to) {
    const from = formatDay(dateRange.from, locale, timeZone)
    const to = formatDay(dateRange.to, locale, timeZone)

    const label = from && to
      ? t('apiRequestFailures.filters.dateRange', { from, to })
      : from
        ? t('apiRequestFailures.filters.dateRangeFrom', { from })
        : t('apiRequestFailures.filters.dateRangeTo', { to })

    activeFilters.push({
      key: 'dateRange',
      label,
      clear: () => chooseRange(null),
    })
  }

  if (country) {
    activeFilters.push({
      key: 'country',
      label: t('apiRequestFailures.filters.country', { value: country }),
    })
  }
  if (ip) {
    activeFilters.push({ key: 'ip', label: t('apiRequestFailures.filters.ip', { value: ip }) })
  }
  if (isp) {
    activeFilters.push({
      key: 'isp',
      label: t('apiRequestFailures.filters.isp', { value: isp }),
    })
  }
  if (reasons.length > 0) {
    activeFilters.push({
      key: 'reasons',
      label: t('apiRequestFailures.filters.reason', { value: reasonTagLabels.join(', ') }),
    })
  }

  const filtered = Boolean(table.query.search) || activeFilters.length > 0

  /**
   * What the auto-refresh button says while it is live or paused.
   *
   * It is the button's whole accessible name and its tooltip, because the control is
   * icon-only: the heading shares its line with a search box, a picker and the clear command,
   * and a worded label would push the table sideways for a control that is one glyph wide.
   */
  const refreshTip = `${t(autoRefresh ? 'apiRequestFailures.pauseRefresh' : 'apiRequestFailures.resumeRefresh')} (${t('apiRequestFailures.autoRefreshHint', { seconds: AUTO_REFRESH_MS / 1000 })})`

  /**
   * The refusal, drawn **in place of the whole toolbar, the filters and the table**. The card
   * and its heading stay, because the address still names the page even when the grant does
   * not - and an empty table under a toast would describe a log that exists.
   */
  if (refused) {
    return (
      <Card
        className="page-card"
        title={t('apiRequestFailures.title')}
        styles={{ title: { whiteSpace: 'nowrap' } }}
      >
        <Alert type="warning" showIcon message={t('apiRequestFailures.noPermission')} />
      </Card>
    )
  }

  return (
    <Card
      className="page-card"
      title={t('apiRequestFailures.title')}
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
            labelKey="apiRequestFailures.searchLabel"
            placeholderKey="apiRequestFailures.searchPlaceholder"
            width={170}
          />
          <Select
            allowClear
            showSearch
            mode="multiple"
            aria-label={t('apiRequestFailures.filterAnyReason')}
            style={{ width: 220 }}
            value={reasons}
            options={reasonOptions}
            placeholder={t('apiRequestFailures.filterAnyReason')}
            optionFilterProp="label"
            onChange={(value: string[]) => chooseReasons(value)}
          />
          <ResetColumnWidths
            label={t('apiRequestFailures.resetWidths')}
            disabled={atDefaults}
            onClick={() => reset()}
          />
          {canClear ? (
            <Button
              danger
              icon={<ClearOutlined />}
              loading={clearLog.isPending}
              onClick={confirmClear}
            >
              {t('apiRequestFailures.clearAll')}
            </Button>
          ) : (
            hinted(
              t('apiRequestFailures.noPermission'),
              <Button danger icon={<ClearOutlined />} disabled>
                {t('apiRequestFailures.clearAll')}
              </Button>,
            )
          )}
        </Space>
      }
    >
      {/* The date range is a row of its own, under the card head and above the tag row and the
          table: it is the one filter whose control is too wide to share the toolbar. The value
          is derived from the address rather than held in state, so an arriving link shows the
          range it carries; the conversion to the account's zone lives in the shared
          `dateRange` helper. */}
      <div style={{ marginBottom: 12 }}>
        <DatePicker.RangePicker
          allowClear
          aria-label={t('apiRequestFailures.filterDateRange')}
          style={{ width: 250 }}
          value={rangePickerValue(dateRange, timeZone)}
          placeholder={[
            t('apiRequestFailures.filterDateFrom'),
            t('apiRequestFailures.filterDateTo'),
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

      <ServerTable<ApiRequestFailureRow, ApiRequestFailureSortField>
        table={table}
        columns={columns}
        rows={log?.items ?? []}
        total={log?.total ?? 0}
        loading={loading}
        loadError={loadError}
        onRetry={() => void logQuery.refetch()}
        tableWidth={tableWidth}
        emptyText={t(filtered ? 'apiRequestFailures.noMatches' : 'apiRequestFailures.empty')}
        showTotal={(total, [from, to]) =>
          t('apiRequestFailures.range', { from, to, total })
        }
      />
    </Card>
  )
}
