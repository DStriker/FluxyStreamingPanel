import type { ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button, Card, Space, Tag, Tooltip } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons'
import { SERVERS_PAGE_SIZE } from '../../features/servers/api'
import type { ServerSortField } from '../../features/servers/api'
import type { Server, ServerStatus } from '../../features/servers/types'
import { useDeleteServer } from '../../features/servers/hooks/mutations'
import { useServers } from '../../features/servers/hooks/queries'
import { useProfile } from '../../features/profile/hooks/queries'
import { ResetColumnWidths, RowActions, ServerTable, TableSearch } from '../../components/table'
import type { ResizableHeaderCellProps, RowAction } from '../../components/table'
import { useColumnWidths, useRowAction, useServerTable } from '../../hooks/table'
import { hinted } from '../../utils/hinted'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'
import { tableResults } from '../../utils/tableResults'
import { dashedCell, ipCell } from '../../utils/tableCells'
import { actionsColumnWidth } from '../../utils/tableWidths'

/** The seven columns, in the order they are drawn. */
type ServerColumnKey =
  | 'name'
  | 'status'
  | 'latency'
  | 'hostname'
  | 'ip'
  | 'clientCount'
  | 'actions'

const COLUMN_KEYS: readonly ServerColumnKey[] = [
  'name',
  'status',
  'latency',
  'hostname',
  'ip',
  'clientCount',
  'actions',
]

/**
 * The width each column starts at; they sum to **1190**, the total every table here reaches.
 *
 * The name and the host name take the wide shares because they are the two things an operator
 * scans the list by, the address is next, and the actions column is exactly the two buttons a
 * row draws. Every other column is comfortably above the ninety-pixel floor.
 */
const DEFAULT_COLUMN_WIDTHS: Record<ServerColumnKey, number> = {
  name: 250,
  status: 120,
  latency: 130,
  hostname: 250,
  ip: 176,
  clientCount: 180,
  actions: actionsColumnWidth(2),
}

/** Where this table remembers the widths the visitor gave its columns. */
const COLUMN_WIDTHS_KEY = 'fluxy.servers.columnWidths'

/** The colour a state is drawn in. Two states, two labels, no ordering between them. */
const STATUS_COLOR: Record<ServerStatus, string> = {
  Active: 'green',
  Disabled: 'default',
}

interface ServersPageProps {
  /** The menu key this route carries, accepted and deliberately unread. */
  sectionKey: string
}

/**
 * The admin's servers - the addresses this installation relays through, and the machine
 * behind each one: the page behind `nav.items.serversManage`.
 *
 * Paging, search and sorting are server side, for the reason every other table's are:
 * `total`. All six content columns are sortable because the endpoint orders by each of them;
 * there is no identifier column and no selection column - a server is one row an operator
 * edits rather than a set they run together.
 *
 * The row's two actions are edit and delete. Delete asks first because it is irreversible,
 * and the **main row's delete is disabled with its reason** attached: the row is the
 * installation's own address and the server answers `409 server_immutable` for it.
 */
export default function ServersPage(_props: ServersPageProps) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const session = useSession()

  const table = useServerTable<Server, ServerSortField>({
    defaultSort: { by: 'name', order: 'asc' },
    sortFields: ['name', 'status', 'latency', 'hostname', 'ip', 'clientCount', 'createdAt'],
    pageSize: SERVERS_PAGE_SIZE,
  })

  const serversQuery = useServers(table.query)
  const { data: servers, loadError, loading } = tableResults(serversQuery)

  const profileQuery = useProfile()
  const granted = profileQuery.data?.permissions ?? []
  const canEdit = granted.includes('editServers')

  const { widths, tableWidth, atDefaults, reset, resizer: handleFor } =
    useColumnWidths<ServerColumnKey>({
      keys: COLUMN_KEYS,
      defaults: DEFAULT_COLUMN_WIDTHS,
      storageKey: COLUMN_WIDTHS_KEY,
    })

  const area = session ? areaForRole(session.role) : ''

  const removeServer = useDeleteServer()

  const { busyId, confirmDelete: askDelete } = useRowAction<Server>({
    refetch: () => void serversQuery.refetch(),
    rows: servers?.items,
    page: table.page,
    setPage: table.setPage,
  })

  const resizer = (key: ServerColumnKey): ResizableHeaderCellProps =>
    handleFor(key, t('servers.resizeColumn', { column: t(`servers.columns.${key}`) }))

  const confirmDelete = (server: Server) =>
    askDelete(server, {
      name: server.name,
      titleKey: 'servers.deleteConfirmTitle',
      bodyKey: 'servers.deleteConfirmBody',
      okKey: 'servers.deleteConfirmOk',
      run: () => removeServer.mutateAsync({ id: server.id }),
    })

  /**
   * The latency cell: seconds to a thousandth, or the word for "not measured" at `0`.
   *
   * `0` is a real state on the contract rather than a missing value, so it is drawn as a
   * sentence rather than as `0 s` - a measured zero is not a thing a probe can report.
   */
  const latencyCell = (value: number): ReactNode =>
    value === 0
      ? t('servers.notMeasured')
      : t('servers.latencyValue', { value: Number(value.toFixed(3)) })

  const columns: TableColumnsType<Server> = [
    {
      title: t('servers.columns.name'),
      dataIndex: 'name',
      key: 'name',
      width: widths.name,
      sorter: true,
      sortOrder: table.orderOf('name'),
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('name'),
      render: (name: string) => (
        <Tooltip title={name}>
          <span
            style={{
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
              maxWidth: Math.max(widths.name - 16, 40),
            }}
          >
            {name}
          </span>
        </Tooltip>
      ),
    },
    {
      title: t('servers.columns.status'),
      dataIndex: 'status',
      key: 'status',
      width: widths.status,
      sorter: true,
      sortOrder: table.orderOf('status'),
      onHeaderCell: () => resizer('status'),
      render: (value: ServerStatus) => (
        <Tag color={STATUS_COLOR[value]} style={{ marginInlineEnd: 0 }}>
          {t(`servers.statuses.${value}`)}
        </Tag>
      ),
    },
    {
      title: t('servers.columns.latency'),
      dataIndex: 'latency',
      key: 'latency',
      width: widths.latency,
      sorter: true,
      sortOrder: table.orderOf('latency'),
      align: 'center',
      onHeaderCell: () => resizer('latency'),
      render: latencyCell,
    },
    {
      title: t('servers.columns.hostname'),
      dataIndex: 'hostname',
      key: 'hostname',
      width: widths.hostname,
      sorter: true,
      sortOrder: table.orderOf('hostname'),
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('hostname'),
      render: dashedCell,
    },
    {
      title: t('servers.columns.ip'),
      dataIndex: 'ip',
      key: 'ip',
      width: widths.ip,
      sorter: true,
      sortOrder: table.orderOf('ip'),
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('ip'),
      render: ipCell,
    },
    {
      title: t('servers.columns.clientCount'),
      dataIndex: 'clientCount',
      key: 'clientCount',
      width: widths.clientCount,
      sorter: true,
      sortOrder: table.orderOf('clientCount'),
      align: 'center',
      onHeaderCell: () => resizer('clientCount'),
      render: (value: number) => (value === 0 ? t('servers.unlimited') : String(value)),
    },
    {
      title: t('servers.columns.actions'),
      key: 'actions',
      width: widths.actions,
      onHeaderCell: () => resizer('actions'),
      render: (_, server) => {
        const busy = busyId === server.id
        const refused = t('servers.noPermission')

        const actions: RowAction[] = [
          {
            key: 'edit',
            label: canEdit ? t('servers.actions.edit') : refused,
            icon: <EditOutlined />,
            disabled: !canEdit,
            onClick: () => navigate(`${area}/servers/${server.id}`),
          },
          {
            key: 'delete',
            label: !canEdit
              ? refused
              : server.isMain
                ? t('servers.deleteDisabled')
                : t('servers.actions.delete'),
            icon: <DeleteOutlined />,
            danger: true,
            disabled: !canEdit || server.isMain || busy,
            onClick: () => confirmDelete(server),
          },
        ]

        return <RowActions actions={actions} />
      },
    },
  ]

  const filtered = Boolean(table.query.search)

  /**
   * The create button, wrapped in the refusal tooltip only while it is greyed - `hinted`
   * exists to keep a *disabled* control's reason reachable, and wrapping an enabled button
   * would put the refusal in front of the one visitor who is allowed to act.
   */
  const addButton = (
    <Button
      type="primary"
      icon={<PlusOutlined />}
      disabled={!canEdit}
      onClick={() => navigate(`${area}/servers/add`)}
    >
      {t('servers.addTitle')}
    </Button>
  )

  return (
    <Card
      className="page-card"
      title={t('servers.listTitle')}
      extra={
        <Space wrap>
          <TableSearch
            value={table.searchText}
            onChange={table.setSearchText}
            onSearch={table.applySearch}
            labelKey="servers.searchLabel"
            placeholderKey="servers.searchPlaceholder"
            width={240}
          />
          <ResetColumnWidths
            label={t('servers.resetWidths')}
            disabled={atDefaults}
            onClick={() => reset()}
          />
          {canEdit ? addButton : hinted(t('servers.noPermission'), addButton)}
        </Space>
      }
    >
      <ServerTable<Server, ServerSortField>
        table={table}
        columns={columns}
        rows={servers?.items ?? []}
        total={servers?.total ?? 0}
        loading={loading}
        loadError={loadError}
        onRetry={() => void serversQuery.refetch()}
        tableWidth={tableWidth}
        emptyText={t(filtered ? 'servers.noMatches' : 'servers.empty')}
        showTotal={(total, [from, to]) => t('servers.range', { from, to, total })}
      />
    </Card>
  )
}
