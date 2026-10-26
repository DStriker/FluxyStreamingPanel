import { useNavigate } from 'react-router-dom'
import { Button, Card, Space } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import {
  DeleteOutlined,
  EditOutlined,
  PlusOutlined,
  ReloadOutlined,
  UnorderedListOutlined,
} from '@ant-design/icons'
import { EPG_SOURCE_PAGE_SIZE } from '../../features/epg/api'
import type { EpgSourceSortField } from '../../features/epg/api'
import type { EpgSource } from '../../features/epg/types'
import {
  useDeleteEpgSource,
} from '../../features/epg/hooks/mutations'
import { useEpgSources } from '../../features/epg/hooks/queries'
import { useProfile } from '../../features/profile/hooks/queries'
import { ResetColumnWidths, RowActions, ServerTable, TableSearch } from '../../components/table'
import type { ResizableHeaderCellProps, RowAction } from '../../components/table'
import { useColumnWidths, useRowAction, useServerTable } from '../../hooks/table'
import { formatTimestamp } from '../../utils/dateFormat'
import { hinted } from '../../utils/hinted'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'
import { tableResults } from '../../utils/tableResults'
import { idCell, textCell } from '../../utils/tableCells'
import { actionsColumnWidth } from '../../utils/tableWidths'

/** The six columns, in the order they are drawn. */
type EpgColumnKey = 'id' | 'name' | 'url' | 'daysKeep' | 'lastEpgUpdatedAt' | 'actions'

const COLUMN_KEYS: readonly EpgColumnKey[] = [
  'id',
  'name',
  'url',
  'daysKeep',
  'lastEpgUpdatedAt',
  'actions',
]

/**
 * The width each column starts at. The six sum to **1190** - the same total every other
 * table here reaches - and the name takes the room the rest does not need, because it is the
 * one column nothing truncates a second time. The actions column is exactly as wide as the
 * three buttons a row draws.
 */
const FIXED_COLUMN_WIDTHS = {
  id: 100,
  url: 380,
  daysKeep: 140,
  lastEpgUpdatedAt: 200,
}

const ACTIONS_WIDTH = actionsColumnWidth(3)
const TABLE_WIDTH = 1190

const DEFAULT_COLUMN_WIDTHS: Record<EpgColumnKey, number> = {
  ...FIXED_COLUMN_WIDTHS,
  name:
    TABLE_WIDTH -
    Object.values(FIXED_COLUMN_WIDTHS).reduce((sum, width) => sum + width, 0) -
    ACTIONS_WIDTH,
  actions: ACTIONS_WIDTH,
}

/** Where this table remembers the widths the visitor gave its columns. */
const COLUMN_WIDTHS_KEY = 'fluxy.epgSources.columnWidths'

interface EpgSourcesPageProps {
  /** The menu key this route carries, accepted and deliberately unread. */
  sectionKey: string
}

/**
 * The admin's programme guide sources: the page behind `nav.items.epgManage`.
 *
 * Paging, search and sorting are server side, for the reason every other table's are:
 * `total`. **There is no selection column and no bulk menu** - the owner asked for neither,
 * and a source is one row an operator edits rather than a set they run together. The
 * toolbar's "Reload EPG" button is a deliberate stub: the updater does not exist yet, so the
 * button is drawn disabled with the reason attached rather than wired to an endpoint that
 * would do nothing.
 *
 * The row whose purpose is not a write is **Channels**, and it sits among the row actions
 * rather than in the toolbar for the same reason the groups table's "show accounts" does:
 * which source's channels to look at is a decision about *this row*.
 */
export default function EpgSourcesPage(_props: EpgSourcesPageProps) {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const session = useSession()

  const table = useServerTable<EpgSource, EpgSourceSortField>({
    defaultSort: { by: 'name', order: 'asc' },
    sortFields: ['name', 'url', 'daysKeep', 'lastEpgUpdatedAt'],
    pageSize: EPG_SOURCE_PAGE_SIZE,
  })

  const sourcesQuery = useEpgSources(table.query)
  const { data: sources, loadError, loading } = tableResults(sourcesQuery)

  const profileQuery = useProfile()
  const granted = profileQuery.data?.permissions ?? []
  const canEdit = granted.includes('editEpgSources')

  const locale = i18n.resolvedLanguage ?? i18n.language
  const timeZone = profileQuery.data?.timeZone ?? null

  const { widths, tableWidth, atDefaults, reset, resizer: handleFor } =
    useColumnWidths<EpgColumnKey>({
      keys: COLUMN_KEYS,
      defaults: DEFAULT_COLUMN_WIDTHS,
      storageKey: COLUMN_WIDTHS_KEY,
    })

  const area = session ? areaForRole(session.role) : ''

  const removeSource = useDeleteEpgSource()

  const { busyId, confirmDelete: askDelete } = useRowAction<EpgSource>({
    refetch: () => void sourcesQuery.refetch(),
    rows: sources?.items,
    page: table.page,
    setPage: table.setPage,
  })

  const resizer = (key: EpgColumnKey): ResizableHeaderCellProps =>
    handleFor(key, t('epgSources.resizeColumn', { column: t(`epgSources.columns.${key}`) }))

  const confirmDelete = (source: EpgSource) =>
    askDelete(source, {
      name: source.name,
      titleKey: 'epgSources.deleteConfirmTitle',
      bodyKey: 'epgSources.deleteConfirmBody',
      okKey: 'epgSources.deleteConfirmOk',
      run: () => removeSource.mutateAsync({ id: source.id }),
    })

  const columns: TableColumnsType<EpgSource> = [
    {
      title: t('epgSources.columns.id'),
      dataIndex: 'id',
      key: 'id',
      width: widths.id,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('id'),
      render: idCell,
    },
    {
      title: t('epgSources.columns.name'),
      dataIndex: 'name',
      key: 'name',
      width: widths.name,
      sorter: true,
      sortOrder: table.orderOf('name'),
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('name'),
      render: textCell,
    },
    {
      title: t('epgSources.columns.url'),
      dataIndex: 'url',
      key: 'url',
      width: widths.url,
      sorter: true,
      sortOrder: table.orderOf('url'),
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('url'),
      render: textCell,
    },
    {
      title: t('epgSources.columns.daysKeep'),
      dataIndex: 'daysKeep',
      key: 'daysKeep',
      width: widths.daysKeep,
      sorter: true,
      sortOrder: table.orderOf('daysKeep'),
      align: 'center',
      onHeaderCell: () => resizer('daysKeep'),
    },
    {
      title: t('epgSources.columns.lastEpgUpdatedAt'),
      dataIndex: 'lastEpgUpdatedAt',
      key: 'lastEpgUpdatedAt',
      width: widths.lastEpgUpdatedAt,
      sorter: true,
      sortOrder: table.orderOf('lastEpgUpdatedAt'),
      onHeaderCell: () => resizer('lastEpgUpdatedAt'),
      render: (value: string | null) => formatTimestamp(value, locale, timeZone),
    },
    {
      title: t('epgSources.columns.actions'),
      key: 'actions',
      width: widths.actions,
      onHeaderCell: () => resizer('actions'),
      render: (_, source) => {
        const busy = busyId === source.id

        const actions: RowAction[] = [
          {
            key: 'channels',
            label: t('epgSources.actions.channels'),
            icon: <UnorderedListOutlined />,
            onClick: () => navigate(`${area}/epg-sources/${source.id}/channels`),
          },
          {
            key: 'edit',
            label: canEdit ? t('epgSources.actions.edit') : t('epgSources.noPermission'),
            icon: <EditOutlined />,
            disabled: !canEdit,
            onClick: () => navigate(`${area}/epg-sources/${source.id}`),
          },
          {
            key: 'delete',
            label: canEdit ? t('epgSources.actions.delete') : t('epgSources.noPermission'),
            icon: <DeleteOutlined />,
            danger: true,
            disabled: !canEdit || busy,
            onClick: () => confirmDelete(source),
          },
        ]

        return <RowActions actions={actions} />
      },
    },
  ]

  const filtered = Boolean(table.query.search)

  return (
    <Card
      className="page-card"
      title={t('epgSources.listTitle')}
      extra={
        <Space wrap>
          {hinted(
            t('epgSources.reloadUnavailable'),
            <Button icon={<ReloadOutlined />} disabled>
              {t('epgSources.reload')}
            </Button>,
          )}
          <TableSearch
            value={table.searchText}
            onChange={table.setSearchText}
            onSearch={table.applySearch}
            labelKey="epgSources.searchLabel"
            placeholderKey="epgSources.searchPlaceholder"
            width={200}
          />
          <ResetColumnWidths
            label={t('epgSources.resetWidths')}
            disabled={atDefaults}
            onClick={() => reset()}
          />
          <Button
            type="primary"
            icon={<PlusOutlined />}
            disabled={!canEdit}
            onClick={() => navigate(`${area}/epg-sources/add`)}
          >
            {t('epgSources.addTitle')}
          </Button>
        </Space>
      }
    >
      <ServerTable<EpgSource, EpgSourceSortField>
        table={table}
        columns={columns}
        rows={sources?.items ?? []}
        total={sources?.total ?? 0}
        loading={loading}
        loadError={loadError}
        onRetry={() => void sourcesQuery.refetch()}
        tableWidth={tableWidth}
        emptyText={t(filtered ? 'epgSources.noMatches' : 'epgSources.empty')}
        showTotal={(total, [from, to]) => t('epgSources.range', { from, to, total })}
      />
    </Card>
  )
}
