import { useEffect, useRef, useState } from 'react'
import {
  Alert,
  App,
  Button,
  Card,
  DatePicker,
  Select,
  Space,
  Tag,
  Typography,
} from 'antd'
import type { TableColumnsType } from 'antd'
import type { Dayjs } from 'dayjs'
import { useTranslation } from 'react-i18next'
import { useNavigate, useSearchParams } from 'react-router-dom'
import {
  ClearOutlined,
  PauseCircleOutlined,
  ReloadOutlined,
} from '@ant-design/icons'
import type { UserLogSortField } from '../../features/user-logs/api'
import { USER_LOGS_PAGE_SIZE } from '../../features/user-logs/api'
import type { UserLogRecordType, UserLogRow } from '../../features/user-logs/types'
import { USER_LOG_RECORD_TYPES } from '../../features/user-logs/recordTypes'
import { recordTone } from '../../features/user-logs/recordTone'
import {
  COLUMN_KEYS,
  COLUMN_WIDTHS_KEY,
  DEFAULT_COLUMN_WIDTHS,
  sortFieldOf,
} from '../../features/user-logs/layout'
import type { UserLogColumnKey } from '../../features/user-logs/layout'
import {
  DATE_FROM_KEY,
  DATE_TO_KEY,
  NO_DATE_RANGE,
  dateRangeParams,
  rangeFromPicker,
  rangePickerValue,
} from '../../features/user-logs/dateRange'
import { networkFilterParams } from '../../features/user-logs/networkFilters'
import { useUserLogs } from '../../features/user-logs/hooks/queries'
import { useClearUserLogs } from '../../features/user-logs/hooks/mutations'
import { useAllUsers } from '../../features/users/hooks/queries'
import { useAllUserGroups } from '../../features/user-groups/hooks/queries'
import { useProfile } from '../../features/profile/hooks/queries'
import ConnectionNetworkCell from '../../features/connection-logs/components/ConnectionNetworkCell'
import { ResetColumnWidths, ServerTable, TableSearch } from '../../components/table'
import type { ResizableHeaderCellProps } from '../../components/table'
import { useColumnWidths, useServerTable } from '../../hooks/table'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'
import { formatDay, formatTimestamp } from '../../utils/dateFormat'
import { hinted } from '../../utils/hinted'
import { messageForError, successTextFor } from '../../utils/http'
import { listParam, withFilter, withFilterList } from '../../utils/tableParams'
import { tableResults } from '../../utils/tableResults'
import { textCell } from '../../utils/tableCells'

/** How often the table re-reads itself while the toggle is on, in milliseconds. */
const AUTO_REFRESH_MS = 30_000

/** A uuid, checked case-insensitively because the server parses one either way. */
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

/** The dash a cell draws for a value the server could not determine (U+2014). */
const DASH = '\u2014'

/** One option of the three pickers, and one filter tag's label. */
interface Option {
  value: string
  label: string
}

/**
 * The user audit log: every attempt to register, sign in, reset a password, change an account
 * or sign out, with its outcome and the network it came from.
 *
 * **The log is evidence, not a table of accounts.** A row may name no account at all (a
 * sign-in for a login name nobody holds, or one whose account has since been deleted), and in
 * that case `accountId` is null while `username` still carries the identifier the visitor
 * typed. That is why the user cell links only when there is an account to open and draws plain
 * text when there is not.
 *
 * Every parameter lives in the address: the shared five `useServerTable` reads, the three
 * repeated list filters, the three network filters and the two date bounds. The repeated
 * filters are the endpoint's own shape (`?userIds=a&userIds=b`), so the page writes them
 * through `withFilterList` and reads them back through `listParam` - a bad element is dropped
 * rather than sent, because the endpoint refuses one with a 400 under that parameter's name.
 *
 * **The network cell is this page's click-through, exactly as on the connection log.** Each of
 * the three facts in it writes its own filter, and the write drops `page` in the same
 * navigation so a click from page four lands on page one of the narrower list. The keys are
 * the endpoint's own (`ip`, `country`, `isp`), which is what lets the same shared cell write
 * either screen's address.
 *
 * **`viewUserLogs` gates both the read and the wipe.** There is no `editUserLogs`: an operator
 * never writes a row, and the only write here is the total erase - so the grant that admits
 * the read admits the clear, and that is the whole of the permission model on this screen.
 *
 * The date range is the long control that cannot share the card head, so it has a row of its own
 * in the card body above the tag row and the table - the placement `ConnectionLogsPage` sets.
 * The three list pickers are bounded selects and stay in the toolbar beside the search box.
 *
 * **Clear-all is never disabled by data state.** The button's words are a promise about the
 * log, not about the rows the filters admit: greying it out because the filtered slice is
 * empty would be lying about a log that still holds rows. Only the permission gates it, and
 * the confirmation is the guard.
 */
export default function UserLogsPage(_props: { sectionKey: string }) {
  const { t, i18n } = useTranslation()
  const { message, modal } = App.useApp()
  const navigate = useNavigate()
  const session = useSession()
  const [searchParams, setSearchParams] = useSearchParams()

  const profileQuery = useProfile()
  const granted = profileQuery.data?.permissions ?? []
  const timeZone = profileQuery.data?.timeZone ?? null
  const locale = i18n.resolvedLanguage ?? i18n.language
  const area = session ? areaForRole(session.role) : ''

  /**
   * The refusal is answered **only once the profile has answered**: an unanswered profile is
   * "not known yet", which must draw the ordinary shell in its loading form rather than an
   * accusation. The logs query is disabled while refused, so the screen does not spend a
   * request on a list the server would answer 403.
   */
  const refused =
    profileQuery.data !== undefined && !(profileQuery.data.permissions ?? []).includes('viewUserLogs')
  const canClear = granted.includes('viewUserLogs')

  const table = useServerTable<UserLogRow, UserLogSortField>({
    defaultSort: { by: 'createdAt', order: 'desc' },
    // Both are columns of the log row. The username is resolved for the page (the joined
    // account's current name, else the identifier recovered from the description), so an order
    // on it would order a page the server had already cut - which is why the table offers two
    // sortable headers and no more.
    sortFields: ['createdAt', 'recordType'],
    sortFieldOf: (columnKey) => sortFieldOf(columnKey),
    pageSize: USER_LOGS_PAGE_SIZE,
  })

  // The three repeated filters, each element judged by what the server will accept: a uuid for
  // the two account pickers, the record-type whitelist for the third.
  const userIds = listParam(searchParams, 'userIds', (value) => UUID.test(value))
  const groupIds = listParam(searchParams, 'groupIds', (value) => UUID.test(value))
  const recordTypes = listParam<UserLogRecordType>(
    searchParams,
    'recordTypes',
    USER_LOG_RECORD_TYPES,
  )
  const dateRange = dateRangeParams(searchParams, DATE_FROM_KEY, DATE_TO_KEY)

  /**
   * The three network filters, written by clicking a fact in the shared network cell.
   *
   * They are read through the feature's own helper, as the connection log reads its
   * counterparts: a value the cell wrote is the row's own stored value, and the endpoint
   * compares it exactly. A blank value is no filter, which is why the tag row and the request
   * both treat it as absent.
   */
  const { ip, country, isp } = networkFilterParams(searchParams)

  const logsQuery = useUserLogs(
    {
      ...table.query,
      userIds,
      groupIds,
      recordTypes,
      ip,
      country,
      isp,
      dateFrom: dateRange.from,
      dateTo: dateRange.to,
    },
    { enabled: !refused },
  )
  const { data: logs, loadError, loading } = tableResults(logsQuery)

  /**
   * The two pickers' option lists, each in a query of its own so a failure of one offers an
   * empty picker rather than failing the table beside it. The record-type list needs no query:
   * its options are the wire vocabulary and its words are the page's own.
   */
  const usersQuery = useAllUsers()
  const userOptions: Option[] = (usersQuery.data?.items ?? []).map((user) => ({
    value: user.id,
    label: user.username,
  }))

  const groupsQuery = useAllUserGroups()
  const groupOptions: Option[] = (groupsQuery.data?.items ?? []).map((group) => ({
    value: group.id,
    label: group.name,
  }))

  /**
   * The ten record types' words, one literal `t()` per wire value.
   *
   * Written as a `Record` over the union rather than as a hand-kept array beside
   * `USER_LOG_RECORD_TYPES`: the picker's membership and the label's spelling then come from
   * one list, and a type added to the vocabulary is a compile error here rather than an option
   * that quietly has no text. The keys are literal so `probe:i18n` can see all ten.
   */
  const typeLabelOf: Record<UserLogRecordType, string> = {
    registration_submit: t('userLogs.types.registration_submit'),
    registration_confirm: t('userLogs.types.registration_confirm'),
    login: t('userLogs.types.login'),
    password_reset_request: t('userLogs.types.password_reset_request'),
    password_reset_confirm: t('userLogs.types.password_reset_confirm'),
    username_change: t('userLogs.types.username_change'),
    email_change: t('userLogs.types.email_change'),
    password_change: t('userLogs.types.password_change'),
    login_guard_change: t('userLogs.types.login_guard_change'),
    logout: t('userLogs.types.logout'),
  }

  /** The ten record types, each with the words this screen speaks for it. */
  const typeOptions: Option[] = USER_LOG_RECORD_TYPES.map((type) => ({
    value: type,
    label: typeLabelOf[type],
  }))

  const { widths, tableWidth, atDefaults, reset, resizer: handleFor } =
    useColumnWidths<UserLogColumnKey>({
      keys: COLUMN_KEYS,
      defaults: DEFAULT_COLUMN_WIDTHS,
      storageKey: COLUMN_WIDTHS_KEY,
    })

  const clearLogs = useClearUserLogs()

  /**
   * Whether the table re-reads itself. **Off by default**, on the owner's call: this is a
   * record to read, not a live feed, and a page that moves under the cursor is worse on an
   * audit trail than a stale row a press away. Pausing and resuming is one press.
   */
  const [autoRefresh, setAutoRefresh] = useState(false)

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
      void logsQuery.refetch()
    }
  })

  useEffect(() => {
    if (!autoRefresh) return

    const timer = setInterval(() => refreshRef.current(), AUTO_REFRESH_MS)

    return () => clearInterval(timer)
  }, [autoRefresh])

  /** Writes one filter, dropping `page` in the same write. */
  const choose = (key: string, value: string | null | undefined) =>
    setSearchParams(withFilter(searchParams, key, value), { replace: true })

  /** Writes one repeated filter: the whole selection in one navigation. */
  const chooseList = (key: string, values: string[]) =>
    setSearchParams(withFilterList(searchParams, key, values), { replace: true })

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
   * The command names nothing - no account, no group, no type, no bound - and the dialog says
   * exactly that, because the button's own words are a promise about the log and not about the
   * rows on screen. Deliberately not disabled on an empty or filtered view.
   */
  const confirmClear = () => {
    modal.confirm({
      title: t('userLogs.clearConfirmTitle'),
      content: t('userLogs.clearConfirmBody'),
      okText: t('userLogs.clearConfirmOk'),
      okButtonProps: { danger: true },
      cancelText: t('actions.cancel'),
      onOk: async () => {
        try {
          const result = await clearLogs.mutateAsync({})

          message.success(successTextFor(result))
          void logsQuery.refetch()
        } catch (error) {
          message.error(messageForError(error))
        }
      },
    })
  }

  const resizer = (key: UserLogColumnKey): ResizableHeaderCellProps =>
    handleFor(key, t('userLogs.resizeColumn', { column: t(`userLogs.columns.${key}`) }))

  /**
   * The user cell: the account's login name as a link into **that account's edit form**, or
   * plain text when there is no account to open.
   *
   * A null `accountId` is a refused attempt against an unknown account, or one whose account
   * has since been deleted - `username` still carries the typed identifier, and a link built
   * from a null id would open the account form at `/users/null`. The whole point of the column
   * is "who was this about", and the answer survives the account.
   */
  const userCell = (row: UserLogRow) => {
    if (row.accountId) {
      return (
        <Typography.Link onClick={() => navigate(`${area}/users/${row.accountId}`)}>
          {row.username ?? DASH}
        </Typography.Link>
      )
    }

    return <Typography.Text>{row.username ?? DASH}</Typography.Text>
  }

  /** The record-type cell: a tag coloured by the attempt's outcome. */
  const typeCell = (row: UserLogRow) => (
    <Tag color={recordTone(row)} style={{ marginInlineEnd: 0 }}>
      {typeLabelOf[row.recordType]}
    </Tag>
  )

  const columns: TableColumnsType<UserLogRow> = [
    {
      title: t('userLogs.columns.username'),
      dataIndex: 'username',
      key: 'username',
      width: widths.username,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('username'),
      render: (_, row) => userCell(row),
    },
    {
      // Both a column of the row and the endpoint's second sort field: the record type is
      // stored, so ordering by it orders the list rather than the page on screen.
      title: t('userLogs.columns.recordType'),
      dataIndex: 'recordType',
      key: 'recordType',
      width: widths.recordType,
      sorter: true,
      sortOrder: table.orderOf('recordType'),
      onHeaderCell: () => resizer('recordType'),
      render: (_, row) => typeCell(row),
    },
    {
      // The stored sentence, whole: it is the auditable record and the text the search box
      // matches. No row is ever cut without its tooltip, and the column is the wide one.
      title: t('userLogs.columns.description'),
      dataIndex: 'description',
      key: 'description',
      width: widths.description,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('description'),
      render: textCell,
    },
    {
      // The shared network cell, now clickable here too: this endpoint takes the same
      // `ip`/`country`/`isp` parameters as the connection log, so each fact writes its own
      // filter through `choose` - the address, the provider and the flag alike. The cell
      // decides which key a fact belongs to; the page only writes what it hands over.
      title: t('userLogs.columns.network'),
      key: 'network',
      width: widths.network,
      onHeaderCell: () => resizer('network'),
      render: (_, row) => <ConnectionNetworkCell log={row} locale={locale} onFilter={choose} />,
    },
    {
      title: t('userLogs.columns.createdAt'),
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: widths.createdAt,
      sorter: true,
      sortOrder: table.orderOf('createdAt'),
      onHeaderCell: () => resizer('createdAt'),
      render: (value: string) => formatTimestamp(value, locale, timeZone),
    },
  ]

  const userLabels = userIds.map(
    (id) => userOptions.find((option) => option.value === id)?.label ?? id,
  )
  const groupLabels = groupIds.map(
    (id) => groupOptions.find((option) => option.value === id)?.label ?? id,
  )
  const typeTagLabels = recordTypes.map((type) => typeLabelOf[type])

  /**
   * Every active filter as a closable tag, in a fixed order: the range first, then the three
   * network filters, then the three list filters. That order copies the connection log's, where
   * a clicked network fact tags immediately after the range: the network tags are written by a
   * click on the table, so they read next to the range and ahead of the pickers whose values
   * the visitor chose. The search is not a tag - its own box is where it is read and cleared.
   * Each list picker contributes **one** tag naming everything chosen in it, because one
   * picker is one choice; the range is one control over two parameters, so its tag clears both
   * sides in one write.
   */
  const activeFilters: { key: string; label: string; clear?: () => void }[] = []

  if (dateRange.from || dateRange.to) {
    const from = formatDay(dateRange.from, locale, timeZone)
    const to = formatDay(dateRange.to, locale, timeZone)

    const label = from && to
      ? t('userLogs.filters.dateRange', { from, to })
      : from
        ? t('userLogs.filters.dateRangeFrom', { from })
        : t('userLogs.filters.dateRangeTo', { to })

    activeFilters.push({
      key: 'dateRange',
      label,
      clear: () => chooseRange(null),
    })
  }

  if (country) {
    activeFilters.push({
      key: 'country',
      label: t('userLogs.filters.country', { value: country }),
    })
  }
  if (ip) {
    activeFilters.push({ key: 'ip', label: t('userLogs.filters.ip', { value: ip }) })
  }
  if (isp) {
    activeFilters.push({
      key: 'isp',
      label: t('userLogs.filters.isp', { value: isp }),
    })
  }

  if (userIds.length > 0) {
    activeFilters.push({
      key: 'userIds',
      label: t('userLogs.filters.user', { value: userLabels.join(', ') }),
    })
  }
  if (groupIds.length > 0) {
    activeFilters.push({
      key: 'groupIds',
      label: t('userLogs.filters.group', { value: groupLabels.join(', ') }),
    })
  }
  if (recordTypes.length > 0) {
    activeFilters.push({
      key: 'recordTypes',
      label: t('userLogs.filters.recordType', { value: typeTagLabels.join(', ') }),
    })
  }

  const filtered = Boolean(table.query.search) || activeFilters.length > 0

  /**
   * What the auto-refresh button says while it is live or paused.
   *
   * It is the button's whole accessible name and its tooltip, because the control is
   * icon-only: the heading shares its line with a search box, three pickers and the clear
   * command, and a worded label would push the table sideways for a control that is one glyph
   * wide.
   */
  const refreshTip = `${t(autoRefresh ? 'userLogs.pauseRefresh' : 'userLogs.resumeRefresh')} (${t('userLogs.autoRefreshHint', { seconds: AUTO_REFRESH_MS / 1000 })})`

  /**
   * The refusal, drawn **in place of the whole toolbar, the filters and the table**. The card
   * and its heading stay, because the address still names the page even when the grant does
   * not - and an empty table under a toast would describe a log that exists.
   */
  if (refused) {
    return (
      <Card
        className="page-card"
        title={t('userLogs.title')}
        styles={{ title: { whiteSpace: 'nowrap' } }}
      >
        <Alert type="warning" showIcon message={t('userLogs.noPermission')} />
      </Card>
    )
  }

  return (
    <Card
      className="page-card user-logs-card"
      title={t('userLogs.title')}
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
            labelKey="userLogs.searchLabel"
            placeholderKey="userLogs.searchPlaceholder"
            width={170}
          />
          <Select
            allowClear
            showSearch
            mode="multiple"
            aria-label={t('userLogs.filterAnyUser')}
            style={{ width: 190 }}
            value={userIds}
            options={userOptions}
            placeholder={t('userLogs.filterAnyUser')}
            optionFilterProp="label"
            onChange={(value: string[]) => chooseList('userIds', value)}
          />
          <Select
            allowClear
            showSearch
            mode="multiple"
            aria-label={t('userLogs.filterAnyGroup')}
            style={{ width: 180 }}
            value={groupIds}
            options={groupOptions}
            placeholder={t('userLogs.filterAnyGroup')}
            optionFilterProp="label"
            onChange={(value: string[]) => chooseList('groupIds', value)}
          />
          <Select
            allowClear
            showSearch
            mode="multiple"
            aria-label={t('userLogs.filterAnyType')}
            style={{ width: 190 }}
            value={recordTypes}
            options={typeOptions}
            placeholder={t('userLogs.filterAnyType')}
            optionFilterProp="label"
            onChange={(value: string[]) => chooseList('recordTypes', value)}
          />
          <ResetColumnWidths
            label={t('userLogs.resetWidths')}
            disabled={atDefaults}
            onClick={() => reset()}
          />
          {canClear ? (
            <Button
              danger
              icon={<ClearOutlined />}
              loading={clearLogs.isPending}
              onClick={confirmClear}
            >
              {t('userLogs.clearAll')}
            </Button>
          ) : (
            hinted(
              t('userLogs.noPermission'),
              <Button danger icon={<ClearOutlined />} disabled>
                {t('userLogs.clearAll')}
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
          aria-label={t('userLogs.filterDateRange')}
          style={{ width: 250 }}
          value={rangePickerValue(dateRange, timeZone)}
          placeholder={[t('userLogs.filterDateFrom'), t('userLogs.filterDateTo')]}
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

      <ServerTable<UserLogRow, UserLogSortField>
        table={table}
        columns={columns}
        rows={logs?.items ?? []}
        total={logs?.total ?? 0}
        loading={loading}
        loadError={loadError}
        onRetry={() => void logsQuery.refetch()}
        tableWidth={tableWidth}
        emptyText={t(filtered ? 'userLogs.noMatches' : 'userLogs.empty')}
        showTotal={(total, [from, to]) => t('userLogs.range', { from, to, total })}
      />
    </Card>
  )
}
