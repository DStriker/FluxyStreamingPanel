import { useEffect, useRef, useState } from 'react'
import { App, Badge, Button, Card, Select, Space, Tag, theme } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate, useSearchParams } from 'react-router-dom'
import {
  MessageOutlined,
  PauseCircleOutlined,
  PlusOutlined,
  ReloadOutlined,
} from '@ant-design/icons'
import type { TicketSortField } from '../../features/tickets/api'
import { TICKETS_PAGE_SIZE } from '../../features/tickets/api'
import type { Ticket, TicketStatus } from '../../features/tickets/types'
import {
  isTicketUnread,
  TICKET_STATUSES,
  ticketStatusLabelKey,
  ticketTagColor,
} from '../../features/tickets/ticketStatus'
import { canCreateTicket } from '../../features/tickets/ticketRules'
import { createdTicketPath } from '../../features/tickets/ticketAddress'
import { useTickets } from '../../features/tickets/hooks/queries'
import { useCreateTicket } from '../../features/tickets/hooks/mutations'
import CreateTicketModal from '../../features/tickets/components/CreateTicketModal'
import { useProfile } from '../../features/profile/hooks/queries'
import { ResetColumnWidths, RowActions, ServerTable, TableSearch } from '../../components/table'
import type { ResizableHeaderCellProps } from '../../components/table'
import { useColumnWidths, useServerTable } from '../../hooks/table'
import { useSession } from '../../utils/sessionContext'
import { formatTimestamp } from '../../utils/dateFormat'
import { menuItemPath } from '../../utils/navigation'
import { messageForError, successTextFor } from '../../utils/http'
import { hinted } from '../../utils/hinted'
import { internalPath } from '../../utils/url'
import { enumParam, withFilter } from '../../utils/tableParams'
import { tableResults } from '../../utils/tableResults'
import { actionsColumnWidth } from '../../utils/tableWidths'

/** The five columns, in the order they are drawn. */
type TicketColumnKey = 'subject' | 'status' | 'createdAt' | 'lastReplyAt' | 'actions'

const COLUMN_KEYS: readonly TicketColumnKey[] = [
  'subject',
  'status',
  'createdAt',
  'lastReplyAt',
  'actions',
]

/**
 * One button in the actions column, and the width that column needs.
 *
 * `actionsColumnWidth(1)` is the width of the button cluster alone (56), but this column's
 * header is the word "Actions"/«Действия» and the account theme sets headers at `600 14px`:
 * the Russian heading measures 62px and wraps to three lines in a 56px cell, which is what a
 * real screenshot of this page showed. The 45 pixels are the connection log's own correction
 * for the same shape - `actionsColumnWidth(1) + 45` = 101, wide enough for both headings -
 * and they come out of the subject, which is the column with room to give.
 */
const ACTIONS_WIDTH = actionsColumnWidth(1) + 45

/** The total every table in this application reaches. */
const TABLE_WIDTH = 1190

/**
 * The three content columns that keep a width of their own; the subject takes whatever is
 * left of the 1190 the table has always occupied, and the actions column is measured from the
 * one button it holds. Written as a subtraction rather than as five numbers so a width that
 * changes keeps the total, instead of quietly moving the actions column off the right edge -
 * the failure this whole frame exists to prevent.
 */
const FIXED_COLUMN_WIDTHS = {
  status: 150,
  createdAt: 210,
  lastReplyAt: 210,
}

/**
 * The widths this table starts at: five columns, summing to **1190**. The subject is the wide
 * one because it is the row's one line of prose, and the two dates are the width this
 * application gives a `formatTimestamp` column.
 *
 * These are *starting* widths, not the answer - the visitor may drag them, and the reset
 * button gives these back.
 */
const DEFAULT_COLUMN_WIDTHS: Record<TicketColumnKey, number> = {
  ...FIXED_COLUMN_WIDTHS,
  subject:
    TABLE_WIDTH -
    Object.values(FIXED_COLUMN_WIDTHS).reduce((sum, width) => sum + width, 0) -
    ACTIONS_WIDTH,
  actions: ACTIONS_WIDTH,
}

/**
 * Where this table remembers the widths the visitor gave its columns.
 *
 * A key of its own rather than the administrator's `fluxy.tickets.columnWidths`: the two
 * tables share four column keys but not their arrangement, and one key would let the admin
 * list's subject width arrive in the account list (and the other way round) - which is the
 * exact drift `useColumnWidths` takes a key per table to prevent.
 */
const COLUMN_WIDTHS_KEY = 'fluxy.tickets.accountColumnWidths'

/** How often the table re-reads itself while the toggle is on, in milliseconds. */
const AUTO_REFRESH_MS = 30_000

/**
 * The account's own support tickets - the same page for a client and for a reseller.
 *
 * **The list is the conversation as this account sees it.** There is no user column, because
 * every row belongs to the reader, and the only row action is "open": a ticket is closed by
 * support, not by the visitor who asked. The one write this page offers is the one that
 * starts a conversation.
 *
 * The default order is the newest answer first, so a reply from support moves the ticket to
 * the top rather than hiding it on a later page, and an unread row is drawn with the dot and
 * a bold subject exactly as it is on the administrator's list.
 */
export default function TicketsPage(_props: { sectionKey: string }) {
  const { t, i18n } = useTranslation()
  const { message } = App.useApp()
  const navigate = useNavigate()
  const session = useSession()
  const [searchParams, setSearchParams] = useSearchParams()
  const { token } = theme.useToken()

  const profileQuery = useProfile()
  const timeZone = profileQuery.data?.timeZone ?? null
  const locale = i18n.resolvedLanguage ?? i18n.language

  /**
   * This list's own address, resolved from the menu item rather than written here - the
   * precedent `StreamProblemsPage` sets. A detail row appends its id to it, so a route renamed
   * in `NAVIGATION` reaches every link built this way and a copied string does not. `null`
   * only under the probes, which render no session provider: the link is then inert.
   */
  const listPath = session ? menuItemPath(session.role, 'tickets') : null
  const ticketPath = (id: string): string => `${listPath ?? ''}/${id}`

  const table = useServerTable<Ticket, TicketSortField>({
    defaultSort: { by: 'lastReplyAt', order: 'desc' },
    // No `username`: the self list has no user column and the endpoint does not order by one.
    sortFields: ['subject', 'createdAt', 'lastReplyAt'],
    pageSize: TICKETS_PAGE_SIZE,
  })

  const statusFilter = enumParam<TicketStatus>(searchParams, 'status', TICKET_STATUSES)

  const ticketsQuery = useTickets({ ...table.query, status: statusFilter })
  const { data: tickets, loadError, loading } = tableResults(ticketsQuery)

  /**
   * Whether the list re-reads itself. **On by default**, as on the administrator's queue: a
   * support conversation is answered from the other side, so the list a visitor is looking at
   * is expected to show the answer without a manual reload. Pausing is one press.
   */
  const [autoRefresh, setAutoRefresh] = useState(true)

  /**
   * The timer that re-reads the table while the toggle is on.
   *
   * The ref rather than the query in the dependency array is the point: `refetch` is a new
   * function whenever the answer changes, so naming it here would restart the interval after
   * every re-read - the shape `UserLogsPage` sets.
   */
  const refreshRef = useRef<() => void>(() => {})

  useEffect(() => {
    refreshRef.current = () => {
      void ticketsQuery.refetch()
    }
  })

  useEffect(() => {
    if (!autoRefresh) return

    const timer = setInterval(() => refreshRef.current(), AUTO_REFRESH_MS)

    return () => clearInterval(timer)
  }, [autoRefresh])

  const { widths, tableWidth, atDefaults, reset, resizer: handleFor } =
    useColumnWidths<TicketColumnKey>({
      keys: COLUMN_KEYS,
      defaults: DEFAULT_COLUMN_WIDTHS,
      storageKey: COLUMN_WIDTHS_KEY,
    })

  const createTicket = useCreateTicket()
  const [creating, setCreating] = useState(false)

  const canCreate = session ? canCreateTicket(session.role) : false

  /** Writes the status filter, dropping the page in the same navigation. */
  const chooseStatus = (value: TicketStatus | undefined) =>
    setSearchParams(withFilter(searchParams, 'status', value), { replace: true })

  const resizer = (key: TicketColumnKey): ResizableHeaderCellProps =>
    handleFor(key, t('tickets.resizeColumn', { column: t(`tickets.columns.${key}`) }))

  const formatMoment = (value: string): string => formatTimestamp(value, locale, timeZone)

  const statusLabelOf: Record<TicketStatus, string> = {
    unread: t('tickets.statuses.unread'),
    read: t('tickets.statuses.read'),
    closed: t('tickets.statuses.closed'),
  }

  const statusOptions = TICKET_STATUSES.map((status) => ({
    value: status,
    label: statusLabelOf[status],
  }))

  /**
   * Opens the ticket and goes to it.
   *
   * The 201's `Location` is what the contract puts the new row's path in, and the body's
   * `redirect` is honoured first, because that is the transport-level convention the rest of
   * this API uses for "go here next". **The header is the API's own path**, though - it reads
   * `/auth/tickets/{id}`, and that is not a route this router has; navigating to it lands on
   * the 404 page, which a runtime check of the create flow found. So `createdTicketPath` takes
   * the id out of the header and rebuilds the address under this area's list path. When neither
   * names an id, the list is re-read and the visitor stays on it - the new ticket is first by
   * the default order, which is the honest answer when the server named nowhere to go.
   */
  const submitCreate = async (subject: string) => {
    try {
      const result = await createTicket.mutateAsync({ values: { subject } })

      message.success(successTextFor(result.data))
      setCreating(false)

      const created =
        internalPath(result.data.redirect) ?? createdTicketPath(listPath, result.location)
      if (created) navigate(created)
      else void ticketsQuery.refetch()
    } catch (error) {
      message.error(messageForError(error))
    }
  }

  const columns: TableColumnsType<Ticket> = [
    {
      // The subject is the way into the conversation, drawn as a button rather than a link:
      // the row navigates inside this application, and an `<a>` with no href announces a link
      // that goes nowhere.
      title: t('tickets.columns.subject'),
      dataIndex: 'subject',
      key: 'subject',
      width: widths.subject,
      sorter: true,
      sortOrder: table.orderOf('subject'),
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('subject'),
      render: (_, row) => {
        const unread = isTicketUnread(row.status)

        return (
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: token.marginXXS,
              minWidth: 0,
            }}
          >
            {unread && (
              <Badge
                dot
                color={token.colorPrimary}
                // A dot with a custom colour is antd's status-badge shape: an inline-block
                // dot rather than the absolutely-positioned corner marker, which is what a
                // row needs - the dot stands beside the subject instead of over it.
                style={{ flex: '0 0 auto' }}
              />
            )}
            <Button
              type="link"
              onClick={() => navigate(ticketPath(row.id))}
              style={{
                padding: 0,
                height: 'auto',
                display: 'block',
                flex: 1,
                minWidth: 0,
                overflow: 'hidden',
                textOverflow: 'ellipsis',
                whiteSpace: 'nowrap',
                textAlign: 'start',
                fontWeight: unread ? token.fontWeightStrong : undefined,
              }}
            >
              {row.subject}
            </Button>
          </div>
        )
      },
    },
    {
      // The status is viewer relative: `unread` here means "support has answered and you have
      // not read it", which is precisely what the dot above the subject says too.
      title: t('tickets.columns.status'),
      dataIndex: 'status',
      key: 'status',
      width: widths.status,
      onHeaderCell: () => resizer('status'),
      render: (value: TicketStatus) => (
        <Tag color={ticketTagColor(value)} style={{ marginInlineEnd: 0 }}>
          {t(ticketStatusLabelKey(value))}
        </Tag>
      ),
    },
    {
      title: t('tickets.columns.createdAt'),
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: widths.createdAt,
      sorter: true,
      sortOrder: table.orderOf('createdAt'),
      onHeaderCell: () => resizer('createdAt'),
      render: formatMoment,
    },
    {
      title: t('tickets.columns.lastReplyAt'),
      dataIndex: 'lastReplyAt',
      key: 'lastReplyAt',
      width: widths.lastReplyAt,
      sorter: true,
      sortOrder: table.orderOf('lastReplyAt'),
      onHeaderCell: () => resizer('lastReplyAt'),
      render: formatMoment,
    },
    {
      // One action: open the conversation. A client cannot close, reopen or delete a ticket -
      // those are the support side's decisions - so the cluster is deliberately the smallest
      // one the frame allows.
      title: t('tickets.columns.actions'),
      key: 'actions',
      width: widths.actions,
      onHeaderCell: () => resizer('actions'),
      render: (_, row) => (
        <RowActions
          actions={[
            {
              key: 'view',
              label: t('tickets.actions.view'),
              icon: <MessageOutlined />,
              onClick: () => navigate(ticketPath(row.id)),
            },
          ]}
        />
      ),
    },
  ]

  const filtered = Boolean(table.query.search) || statusFilter !== null

  /**
   * What the auto-refresh button says while it is live or paused - the accessible name and
   * the tooltip of an icon-only control, in the toolbar's own order after the pickers.
   */
  const refreshTip = `${t(autoRefresh ? 'tickets.pauseRefresh' : 'tickets.resumeRefresh')} (${t('tickets.autoRefreshHint', { seconds: AUTO_REFRESH_MS / 1000 })})`

  return (
    <Card
      className="page-card"
      title={t('tickets.listTitle')}
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
            labelKey="tickets.searchLabel"
            placeholderKey="tickets.searchPlaceholder"
            width={200}
          />
          <Select
            allowClear
            aria-label={t('tickets.columns.status')}
            style={{ width: 130 }}
            value={statusFilter ?? undefined}
            options={statusOptions}
            placeholder={t('tickets.filterAnyStatus')}
            onChange={chooseStatus}
          />
          <ResetColumnWidths
            label={t('tickets.resetWidths')}
            disabled={atDefaults}
            onClick={() => reset()}
          />
          {/* Absent rather than disabled for the administrator, who answers tickets instead
              of opening them: this page is only routed under the two account-area roles, so
              `canCreate` is a second statement of the same rule rather than a hidden gate. */}
          {canCreate && (
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={() => setCreating(true)}
            >
              {t('tickets.createAction')}
            </Button>
          )}
        </Space>
      }
    >
      <ServerTable<Ticket, TicketSortField>
        table={table}
        columns={columns}
        rows={tickets?.items ?? []}
        total={tickets?.total ?? 0}
        loading={loading}
        loadError={loadError}
        onRetry={() => void ticketsQuery.refetch()}
        tableWidth={tableWidth}
        emptyText={t(filtered ? 'tickets.noMatches' : 'tickets.empty')}
        showTotal={(total, [from, to]) => t('tickets.range', { from, to, total })}
      />

      {/* Mounted only while it is open, so neither the form nor a half-typed subject outlives
          a close - the precedent `SendEventModal` sets. */}
      {creating && (
        <CreateTicketModal
          submitting={createTicket.isPending}
          onCancel={() => setCreating(false)}
          onSubmit={(subject) => void submitCreate(subject)}
        />
      )}
    </Card>
  )
}
