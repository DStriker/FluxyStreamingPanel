import { App, Button, Card, DatePicker, Select, Space, Tag, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import type { Dayjs } from 'dayjs'
import { useTranslation } from 'react-i18next'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { ClearOutlined } from '@ant-design/icons'
import type { CreditLogSortField } from '../../features/credit-logs/api'
import { CREDIT_LOGS_PAGE_SIZE } from '../../features/credit-logs/api'
import type { CreditLog } from '../../features/credit-logs/types'
import { creditLogActorName, creditLogTone } from '../../features/credit-logs/creditLogActor'
import {
  COLUMN_KEYS,
  COLUMN_WIDTHS_KEY,
  DEFAULT_COLUMN_WIDTHS,
  sortFieldOf,
} from '../../features/credit-logs/layout'
import type { CreditLogColumnKey } from '../../features/credit-logs/layout'
import {
  DATE_FROM_KEY,
  DATE_TO_KEY,
  NO_DATE_RANGE,
  dateRangeParams,
  rangeFromPicker,
  rangePickerValue,
} from '../../features/credit-logs/dateRange'
import { useCreditLogs } from '../../features/credit-logs/hooks/queries'
import { useClearCreditLogs } from '../../features/credit-logs/hooks/mutations'
import { useAllUsers } from '../../features/users/hooks/queries'
import { useProfile } from '../../features/profile/hooks/queries'
import { ResetColumnWidths, ServerTable, TableSearch } from '../../components/table'
import type { ResizableHeaderCellProps } from '../../components/table'
import { useColumnWidths, useServerTable } from '../../hooks/table'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'
import { formatDay, formatTimestamp } from '../../utils/dateFormat'
import { hinted } from '../../utils/hinted'
import { messageForError, successTextFor } from '../../utils/http'
import { withFilter } from '../../utils/tableParams'
import { tableResults } from '../../utils/tableResults'
import { textCell } from '../../utils/tableCells'

/** One option of the reseller picker, and one filter tag's label. */
interface Option {
  value: string
  label: string
}

/**
 * The reseller credit log: the audit trail of how each reseller's balance got to where it is.
 *
 * **The balance itself is not this page's.** It lives on the account form and in the header
 * chip; this is the record of the changes, and the two never share a fetch - a table an
 * operator reads to find what moved would hold a second copy of the balance if it also drew
 * one, and the two could disagree.
 *
 * Every parameter lives in the address: the shared five `useServerTable` reads, the reseller
 * filter and the two date bounds. The reseller cell is a link into that reseller's edit form -
 * the same target the accounts table's row action opens - because the toolbar picker already
 * narrows the list; the useful action there is editing the balance, not filtering again.
 *
 * The date range is the one control too wide for the card head, so it is a row of its own in
 * the card body above the tag row and the table - the placement `ConnectionLogsPage` sets.
 *
 * **Clear-all is never disabled by data state.** The button's words are a promise about the
 * log, not about the rows the filters admit: greying it out because the filtered slice is
 * empty would be lying about a log that still holds rows. Only the permission gates it, and
 * the confirmation is the guard.
 */
export default function CreditLogsPage(_props: { sectionKey: string }) {
  const { t, i18n } = useTranslation()
  const { message, modal } = App.useApp()
  const navigate = useNavigate()
  const session = useSession()
  const [searchParams, setSearchParams] = useSearchParams()

  const table = useServerTable<CreditLog, CreditLogSortField>({
    defaultSort: { by: 'createdAt', order: 'desc' },
    // Both are columns of the ledger row. The reseller name is joined for the page, so an
    // order on it would order a page the server had already cut - the reason the table offers
    // two sortable headers and no more.
    sortFields: ['createdAt', 'amount'],
    sortFieldOf: (columnKey) => sortFieldOf(columnKey),
    pageSize: CREDIT_LOGS_PAGE_SIZE,
  })

  const resellerId = searchParams.get('resellerId') || null
  const dateRange = dateRangeParams(searchParams, DATE_FROM_KEY, DATE_TO_KEY)

  const logsQuery = useCreditLogs({
    ...table.query,
    resellerId,
    dateFrom: dateRange.from,
    dateTo: dateRange.to,
  })
  const { data: logs, loadError, loading } = tableResults(logsQuery)

  const profileQuery = useProfile()
  const granted = profileQuery.data?.permissions ?? []
  const canClear = granted.includes('editResellers')
  const timeZone = profileQuery.data?.timeZone ?? null
  const locale = i18n.resolvedLanguage ?? i18n.language
  const area = session ? areaForRole(session.role) : ''

  /**
   * The reseller picker's options, in a query of its own: the account list needs the per-role
   * `view*` grants while this table needs `viewResellers`, and a picker that failed would offer
   * nothing to pick rather than fail the list beside it.
   */
  const usersQuery = useAllUsers()
  const userOptions: Option[] = (usersQuery.data?.items ?? []).map((user) => ({
    value: user.id,
    label: user.username,
  }))

  const { widths, tableWidth, atDefaults, reset, resizer: handleFor } =
    useColumnWidths<CreditLogColumnKey>({
      keys: COLUMN_KEYS,
      defaults: DEFAULT_COLUMN_WIDTHS,
      storageKey: COLUMN_WIDTHS_KEY,
    })

  const clearLogs = useClearCreditLogs()

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
   * Empties the whole log, after asking once.
   *
   * The command names nothing - no reseller, no bound - and the dialog says exactly that,
   * because the button's own words are a promise about the log and not about the rows on
   * screen. Deliberately not disabled on an empty or filtered view.
   */
  const confirmClear = () => {
    modal.confirm({
      title: t('creditLogs.clearConfirmTitle'),
      content: t('creditLogs.clearConfirmBody'),
      okText: t('creditLogs.clearConfirmOk'),
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

  const resizer = (key: CreditLogColumnKey): ResizableHeaderCellProps =>
    handleFor(key, t('creditLogs.resizeColumn', { column: t(`creditLogs.columns.${key}`) }))

  /**
   * The reseller cell: the login name as a link into **that reseller's edit form**, the same
   * target the accounts table's row action opens (`${area}/users/${id}`).
   *
   * It navigates in the current tab rather than opening a second one, and it no longer filters
   * this table: the toolbar's reseller picker is already how the list is narrowed, so repeating
   * that here would add nothing. The balance is edited on the account form, which is the action
   * the click is for.
   */
  const resellerCell = (log: CreditLog) => (
    <Typography.Link onClick={() => navigate(`${area}/users/${log.resellerId}`)}>
      {log.resellerUsername}
    </Typography.Link>
  )

  const columns: TableColumnsType<CreditLog> = [
    {
      title: t('creditLogs.columns.reseller'),
      dataIndex: 'resellerUsername',
      key: 'reseller',
      width: widths.reseller,
      onHeaderCell: () => resizer('reseller'),
      render: (_, log) => resellerCell(log),
    },
    {
      // The amount is a signed delta, and the two colours mean "money in" and "money out or
      // nothing came in" - zero is red rather than neutral, because it is still an entry that
      // moved nothing. The header is keyed `credits` while the request says `amount` - the one
      // key-to-field mapping the layout module documents.
      title: t('creditLogs.columns.credits'),
      dataIndex: 'amount',
      key: 'credits',
      width: widths.credits,
      sorter: true,
      sortOrder: table.orderOf('amount'),
      onHeaderCell: () => resizer('credits'),
      render: (value: number) => (
        <Typography.Text type={creditLogTone(value)}>{value}</Typography.Text>
      ),
    },
    {
      title: t('creditLogs.columns.issuedBy'),
      dataIndex: 'issuedByUsername',
      key: 'issuedBy',
      width: widths.issuedBy,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('issuedBy'),
      render: (_, log) => textCell(creditLogActorName(log)),
    },
    {
      // The stored sentence, whole: it is the auditable record and the text the search box
      // matches. No row is ever cut without its tooltip, and the column is the wide one.
      title: t('creditLogs.columns.description'),
      dataIndex: 'description',
      key: 'description',
      width: widths.description,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('description'),
      render: textCell,
    },
    {
      title: t('creditLogs.columns.createdAt'),
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: widths.createdAt,
      sorter: true,
      sortOrder: table.orderOf('createdAt'),
      onHeaderCell: () => resizer('createdAt'),
      render: (value: string) => formatTimestamp(value, locale, timeZone),
    },
  ]

  const resellerFilterLabel =
    userOptions.find((option) => option.value === resellerId)?.label ?? resellerId

  /**
   * Every active filter as a closable tag, in a fixed order: the range first, then the
   * reseller. The search is not a tag - its own box is where it is read and cleared.
   */
  const activeFilters: { key: string; label: string; clear?: () => void }[] = []

  if (dateRange.from || dateRange.to) {
    const from = formatDay(dateRange.from, locale, timeZone)
    const to = formatDay(dateRange.to, locale, timeZone)

    const label = from && to
      ? t('creditLogs.filters.dateRange', { from, to })
      : from
        ? t('creditLogs.filters.dateRangeFrom', { from })
        : t('creditLogs.filters.dateRangeTo', { to })

    activeFilters.push({
      key: 'dateRange',
      label,
      clear: () => chooseRange(null),
    })
  }

  if (resellerId) {
    activeFilters.push({
      key: 'resellerId',
      label: t('creditLogs.filters.reseller', { value: resellerFilterLabel }),
    })
  }

  const filtered = Boolean(table.query.search) || activeFilters.length > 0

  return (
    <Card
      className="page-card"
      title={t('creditLogs.title')}
      styles={{ title: { whiteSpace: 'nowrap' } }}
      extra={
        <Space wrap>
          <TableSearch
            value={table.searchText}
            onChange={table.setSearchText}
            onSearch={table.applySearch}
            labelKey="creditLogs.searchLabel"
            placeholderKey="creditLogs.searchPlaceholder"
            width={190}
          />
          <Select
            allowClear
            showSearch
            aria-label={t('creditLogs.filterAnyReseller')}
            style={{ width: 150 }}
            value={resellerId ?? undefined}
            options={userOptions}
            placeholder={t('creditLogs.filterAnyReseller')}
            optionFilterProp="label"
            onChange={(value) => choose('resellerId', value)}
          />
          <ResetColumnWidths
            label={t('creditLogs.resetWidths')}
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
              {t('creditLogs.clearAll')}
            </Button>
          ) : (
            hinted(
              t('creditLogs.noPermission'),
              <Button danger icon={<ClearOutlined />} disabled>
                {t('creditLogs.clearAll')}
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
          aria-label={t('creditLogs.filterDateRange')}
          style={{ width: 250 }}
          value={rangePickerValue(dateRange, timeZone)}
          placeholder={[t('creditLogs.filterDateFrom'), t('creditLogs.filterDateTo')]}
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

      <ServerTable<CreditLog, CreditLogSortField>
        table={table}
        columns={columns}
        rows={logs?.items ?? []}
        total={logs?.total ?? 0}
        loading={loading}
        loadError={loadError}
        onRetry={() => void logsQuery.refetch()}
        tableWidth={tableWidth}
        emptyText={t(filtered ? 'creditLogs.noMatches' : 'creditLogs.empty')}
        showTotal={(total, [from, to]) => t('creditLogs.range', { from, to, total })}
      />
    </Card>
  )
}
