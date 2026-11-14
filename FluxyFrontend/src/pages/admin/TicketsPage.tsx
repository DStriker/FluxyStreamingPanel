import { useEffect, useRef, useState } from 'react'
import { Badge, Button, Card, Select, Space, Tag, Typography, theme } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate, useSearchParams } from 'react-router-dom'
import {
  CheckOutlined,
  DeleteOutlined,
  EyeInvisibleOutlined,
  FolderOpenOutlined,
  MessageOutlined,
  PauseCircleOutlined,
  ReloadOutlined,
} from '@ant-design/icons'
import type { AdminTicketSortField } from '../../features/tickets/api'
import { TICKETS_PAGE_SIZE } from '../../features/tickets/api'
import type { AdminTicket, TicketStatus } from '../../features/tickets/types'
import {
  isTicketUnread,
  TICKET_STATUSES,
  ticketStatusLabelKey,
  ticketTagColor,
} from '../../features/tickets/ticketStatus'
import { useAdminTickets } from '../../features/tickets/hooks/queries'
import {
  useClose,
  useDeleteTicket,
  useMarkUnread,
  useReopen,
} from '../../features/tickets/hooks/mutations'
import { useProfile } from '../../features/profile/hooks/queries'
import { ResetColumnWidths, RowActions, ServerTable, TableSearch } from '../../components/table'
import type { ResizableHeaderCellProps, RowAction } from '../../components/table'
import { useColumnWidths, useRowAction, useServerTable } from '../../hooks/table'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'
import { formatTimestamp } from '../../utils/dateFormat'
import { hinted } from '../../utils/hinted'
import { menuItemPath } from '../../utils/navigation'
import { enumParam, withFilter } from '../../utils/tableParams'
import { tableResults } from '../../utils/tableResults'
import { actionsColumnWidth } from '../../utils/tableWidths'

/** The six columns, in the order they are drawn. */
type TicketColumnKey = 'subject' | 'user' | 'status' | 'createdAt' | 'lastReplyAt' | 'actions'

const COLUMN_KEYS: readonly TicketColumnKey[] = [
  'subject',
  'user',
  'status',
  'createdAt',
  'lastReplyAt',
  'actions',
]

/** The actions column is exactly as wide as the four buttons a row draws. */
const ACTIONS_WIDTH = actionsColumnWidth(4)

/**
 * The widths this table starts at, summing to **1190** - the total every administrator table
 * here reaches. The subject is the wide column because it is the row's one line of prose; the
 * two dates are the width this application gives a `formatTimestamp` column, and the user and
 * the status are short facts.
 *
 * These are *starting* widths, not the answer - the visitor may drag them, and the reset
 * button gives these back.
 */
const DEFAULT_COLUMN_WIDTHS: Record<TicketColumnKey, number> = {
  subject: 340,
  user: 200,
  status: 130,
  createdAt: 190,
  lastReplyAt: 190,
  actions: ACTIONS_WIDTH,
}

/** Where this table remembers the widths the visitor gave its columns. */
const COLUMN_WIDTHS_KEY = 'fluxy.tickets.columnWidths'

/** How often the table re-reads itself while the toggle is on, in milliseconds. */
const AUTO_REFRESH_MS = 30_000

/**
 * Every account's support tickets - the administrator's queue.
 *
 * **The ticket is a conversation, so the list is a queue rather than a catalogue.** The
 * default order is the newest answer first, and an unread ticket is drawn with the dot and a
 * bold subject: the two facts that decide which row to open next. Closing and reopening are
 * one state-dependent button rather than two, because a row is in exactly one of the two
 * states the pair describes.
 *
 * Every parameter lives in the address, through `useServerTable` and one filter write - the
 * search box narrows subjects, the status picker narrows the viewer-relative state, and a
 * change to either drops the page in the same navigation.
 *
 * The read is the full list of every account's tickets; the administrator may open any row,
 * answer it, close it or delete it. The account's own queue is a different page against a
 * different endpoint (`pages/account/TicketsPage`), and the two share their words, their
 * status colours and their column widths, not their data.
 */
export default function TicketsPage(_props: { sectionKey: string }) {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const session = useSession()
  const [searchParams, setSearchParams] = useSearchParams()
  const { token } = theme.useToken()

  const profileQuery = useProfile()
  const timeZone = profileQuery.data?.timeZone ?? null
  const locale = i18n.resolvedLanguage ?? i18n.language
  const area = session ? areaForRole(session.role) : ''

  /**
   * This list's own address, resolved from the menu item rather than written here.
   *
   * A detail row appends its id to it, which is the one way to reach a page with no menu item
   * of its own without repeating the segment: a route renamed in `NAVIGATION` reaches every
   * link built this way, and a copied string does not. `null` only under the probes, which
   * render no session provider - the link is then inert rather than wrong.
   */
  const listPath = session ? menuItemPath(session.role, 'tickets') : null
  const ticketPath = (id: string): string => `${listPath ?? ''}/${id}`

  const table = useServerTable<AdminTicket, AdminTicketSortField>({
    defaultSort: { by: 'lastReplyAt', order: 'desc' },
    // The endpoint's own sortable fields. `status` is not one of them: the wire status is
    // computed per viewer, so ordering by it would order a value no column of the row holds.
    sortFields: ['subject', 'username', 'createdAt', 'lastReplyAt'],
    pageSize: TICKETS_PAGE_SIZE,
  })

  const statusFilter = enumParam<TicketStatus>(searchParams, 'status', TICKET_STATUSES)

  const ticketsQuery = useAdminTickets({ ...table.query, status: statusFilter })
  const { data: tickets, loadError, loading } = tableResults(ticketsQuery)

  /**
   * Whether the queue re-reads itself. **On by default**: a support queue is a live
   * conversation - an answer written while the visitor is on the page is the whole point of
   * the screen - and pausing it is one press.
   */
  const [autoRefresh, setAutoRefresh] = useState(true)

  /**
   * The timer that re-reads the table while the toggle is on.
   *
   * The ref rather than the query in the dependency array is the point: `refetch` is a new
   * function whenever the answer changes, so naming it here would restart the interval after
   * every re-read. This is the precedent `UserLogsPage`/`StreamProblemsPage` set.
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

  const markUnread = useMarkUnread()
  const closeTicket = useClose()
  const reopenTicket = useReopen()
  const removeTicket = useDeleteTicket()

  const { busyId, runAction, confirmDelete: askDelete } = useRowAction<AdminTicket>({
    refetch: () => void ticketsQuery.refetch(),
    rows: tickets?.items,
    page: table.page,
    setPage: table.setPage,
  })

  /** Writes the status filter, dropping the page in the same navigation. */
  const chooseStatus = (value: TicketStatus | undefined) =>
    setSearchParams(withFilter(searchParams, 'status', value), { replace: true })

  const resizer = (key: TicketColumnKey): ResizableHeaderCellProps =>
    handleFor(key, t('tickets.resizeColumn', { column: t(`tickets.columns.${key}`) }))

  const formatMoment = (value: string): string => formatTimestamp(value, locale, timeZone)

  /**
   * The status filter's words, one literal `t()` per wire value so `probe:i18n` sees them and
   * a fourth status is a compile error in both locale files.
   */
  const statusLabelOf: Record<TicketStatus, string> = {
    unread: t('tickets.statuses.unread'),
    read: t('tickets.statuses.read'),
    closed: t('tickets.statuses.closed'),
  }

  const statusOptions = TICKET_STATUSES.map((status) => ({
    value: status,
    label: statusLabelOf[status],
  }))

  /** Asks before deleting: it takes the conversation with it and cannot be undone. */
  const confirmDelete = (row: AdminTicket) =>
    askDelete(row, {
      name: row.subject,
      titleKey: 'tickets.deleteConfirmTitle',
      bodyKey: 'tickets.deleteConfirmBody',
      okKey: 'tickets.deleteConfirmOk',
      run: () => removeTicket.mutateAsync({ id: row.id }),
    })

  const columns: TableColumnsType<AdminTicket> = [
    {
      // The subject is the way into the conversation, and it is drawn as a button rather
      // than a link: the row navigates inside this application, and an `<a>` without an href
      // is a control a screen reader announces as a link that goes nowhere.
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
      // Who the conversation belongs to, as the way into that account - the same target the
      // accounts table's own edit action opens.
      title: t('tickets.columns.user'),
      dataIndex: 'username',
      key: 'username',
      width: widths.user,
      sorter: true,
      sortOrder: table.orderOf('username'),
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('user'),
      render: (_, row) => (
        <Typography.Link onClick={() => navigate(`${area}/users/${row.userId}`)}>
          {row.username}
        </Typography.Link>
      ),
    },
    {
      // A tag rather than coloured text: three states read as three labels down a column,
      // and the colour is a description, not a rank. The status is viewer relative, so
      // `unread` here means "this administrator has not read it".
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
      // The four actions as icons with tooltips: opening the conversation, marking it
      // unread again, the one state-dependent transition, and the one that cannot be undone.
      title: t('tickets.columns.actions'),
      key: 'actions',
      width: widths.actions,
      onHeaderCell: () => resizer('actions'),
      render: (_, row) => {
        const busy = busyId === row.id
        const closed = row.status === 'closed'

        const actions: RowAction[] = [
          {
            key: 'view',
            label: t('tickets.actions.view'),
            icon: <MessageOutlined />,
            onClick: () => navigate(ticketPath(row.id)),
          },
          {
            key: 'unread',
            label: t('tickets.actions.markUnread'),
            icon: <EyeInvisibleOutlined />,
            loading: busy,
            // A ticket that already is unread has nothing to mark: the button is disabled
            // rather than hidden, so the row's cluster keeps its rhythm.
            disabled: busy || row.status === 'unread',
            onClick: () => runAction(() => markUnread.mutateAsync({ id: row.id }), row),
          },
          closed
            ? {
                key: 'reopen',
                label: t('tickets.actions.reopen'),
                icon: <FolderOpenOutlined />,
                loading: busy,
                disabled: busy,
                onClick: () => runAction(() => reopenTicket.mutateAsync({ id: row.id }), row),
              }
            : {
                key: 'close',
                label: t('tickets.actions.close'),
                icon: <CheckOutlined />,
                loading: busy,
                disabled: busy,
                onClick: () => runAction(() => closeTicket.mutateAsync({ id: row.id }), row),
              },
          {
            key: 'delete',
            label: t('tickets.actions.delete'),
            icon: <DeleteOutlined />,
            danger: true,
            loading: busy,
            disabled: busy,
            onClick: () => confirmDelete(row),
          },
        ]

        return <RowActions actions={actions} />
      },
    },
  ]

  const filtered = Boolean(table.query.search) || statusFilter !== null

  /**
   * What the auto-refresh button says while it is live or paused.
   *
   * It is the button's whole accessible name and its tooltip, because the control is
   * icon-only: the heading shares its line with a search box, the status picker, the width
   * reset and (on the account's list) the create command, and a worded label would push the
   * table sideways for a control that is one glyph wide.
   */
  const refreshTip = `${t(autoRefresh ? 'tickets.pauseRefresh' : 'tickets.resumeRefresh')} (${t('tickets.autoRefreshHint', { seconds: AUTO_REFRESH_MS / 1000 })})`

  return (
    <Card
      className="page-card"
      title={t('tickets.listTitle')}
      // The heading is the one thing in the head that must never yield: `Space wrap` stacks
      // the toolbar onto a second row instead, which is taller but readable.
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
        </Space>
      }
    >
      <ServerTable<AdminTicket, AdminTicketSortField>
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
    </Card>
  )
}
