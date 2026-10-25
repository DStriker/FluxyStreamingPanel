import { useMemo, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { App, Button, Card, Dropdown, Image, Modal, Select, Space, Tag } from 'antd'
import type { MenuProps, TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import {
  DeleteOutlined,
  EditOutlined,
  EyeInvisibleOutlined,
  EyeOutlined,
  FolderAddOutlined,
  MoreOutlined,
  PlusCircleOutlined,
  PlusOutlined,
  SwapOutlined,
  UnorderedListOutlined,
} from '@ant-design/icons'
import { CONTENT_TYPE_PARAMS } from '../../features/content-categories/api'
import { useContentCategories } from '../../features/content-categories/hooks/queries'
import { SERIES_PAGE_SIZE } from '../../features/series/api'
import type { BulkSeriesAction, SeriesSortField } from '../../features/series/api'
import type { Series, SeriesStatus } from '../../features/series/types'
import {
  useBulkSeries,
  useDeleteSeries,
  useSetSeriesStatus,
} from '../../features/series/hooks/mutations'
import { useSeries } from '../../features/series/hooks/queries'
import { useProfile } from '../../features/profile/hooks/queries'
import { useBulkAction, useColumnWidths, useRowAction, useServerTable } from '../../hooks/table'
import { ResetColumnWidths, RowActions, ServerTable, TableSearch } from '../../components/table'
import type { ResizableHeaderCellProps, RowAction } from '../../components/table'
import { hinted } from '../../utils/hinted'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'
import { tableResults } from '../../utils/tableResults'
import { idCell, textCell } from '../../utils/tableCells'
import { enumParam, withFilter } from '../../utils/tableParams'
import { actionsColumnWidth } from '../../utils/tableWidths'

/** The eight columns, in the order they are drawn. */
type SeriesColumnKey =
  | 'id'
  | 'poster'
  | 'name'
  | 'category'
  | 'status'
  | 'seasons'
  | 'episodes'
  | 'actions'

const COLUMN_KEYS: readonly SeriesColumnKey[] = [
  'id',
  'poster',
  'name',
  'category',
  'status',
  'seasons',
  'episodes',
  'actions',
]

/**
 * The width each column starts at. The six fixed columns and the actions column leave the
 * name the rest of the **1190** the page's columns occupy; the selection column `ServerTable`
 * adds is the 40 that makes **1230** - the same total every other table here reaches.
 */
const FIXED_COLUMN_WIDTHS = {
  id: 80,
  poster: 70,
  category: 150,
  status: 140,
  seasons: 100,
  episodes: 100,
}

const ACTIONS_WIDTH = actionsColumnWidth(6)
const TABLE_WIDTH = 1190

const DEFAULT_COLUMN_WIDTHS: Record<SeriesColumnKey, number> = {
  ...FIXED_COLUMN_WIDTHS,
  name:
    TABLE_WIDTH -
    Object.values(FIXED_COLUMN_WIDTHS).reduce((sum, width) => sum + width, 0) -
    ACTIONS_WIDTH,
  actions: ACTIONS_WIDTH,
}

/** Where this table remembers the widths the visitor gave its columns. */
const COLUMN_WIDTHS_KEY = 'fluxy.series.columnWidths'

/** The two states, in the order the backend declares them. */
const STATUSES: SeriesStatus[] = ['Active', 'Inactive']

interface SeriesPageProps {
  /** The menu key this route carries, accepted and deliberately unread. */
  sectionKey: string
}

/**
 * The admin's series catalogue: the page behind `nav.items.seriesManage`.
 *
 * Paging, search, filtering and sorting are all server side, for the reason every other
 * table's are: `total`. The filters live in the address, so a reload or a shared link keeps
 * the view. The three actions that are not edits lead to the episodes and the two forms that
 * add one, all of them at addresses the sidebar deliberately does not offer.
 */
export default function SeriesPage(_props: SeriesPageProps) {
  const { t } = useTranslation()
  const { modal } = App.useApp()
  const navigate = useNavigate()
  const session = useSession()

  const table = useServerTable<Series, SeriesSortField>({
    defaultSort: { by: 'name', order: 'asc' },
    sortFields: ['name', 'category', 'status', 'seasons', 'episodes'],
    pageSize: SERIES_PAGE_SIZE,
  })

  const [searchParams, setSearchParams] = useSearchParams()
  const categoryFilter = searchParams.get('categoryId') || null
  const statusFilter = enumParam(searchParams, 'status', STATUSES)

  const seriesQuery = useSeries({
    ...table.query,
    categoryId: categoryFilter,
    status: statusFilter,
  })
  const { data: series, loadError, loading } = tableResults(seriesQuery)

  const categoriesQuery = useContentCategories(CONTENT_TYPE_PARAMS.Series)
  const categories = categoriesQuery.data?.items ?? []

  const { widths, tableWidth, atDefaults, reset, resizer: handleFor } =
    useColumnWidths<SeriesColumnKey>({
      keys: COLUMN_KEYS,
      defaults: DEFAULT_COLUMN_WIDTHS,
      storageKey: COLUMN_WIDTHS_KEY,
    })

  const area = session ? areaForRole(session.role) : ''

  const profileQuery = useProfile()
  const granted = profileQuery.data?.permissions ?? []
  const canEdit = granted.includes('editSeries')

  const { selectedIds, setSelectedIds, bulkBusy, applyBulk } = useBulkAction({
    rows: series?.items,
    refetch: () => void seriesQuery.refetch(),
    partialTitleKey: 'series.bulk.partialTitle',
  })

  const [bulkCategoryOpen, setBulkCategoryOpen] = useState(false)
  const [bulkCategoryId, setBulkCategoryId] = useState<string | null>(null)

  const removeSeries = useDeleteSeries()
  const setStatus = useSetSeriesStatus()
  const bulk = useBulkSeries()

  const { busyId, runAction, confirmDelete: askDelete } = useRowAction<Series>({
    refetch: () => void seriesQuery.refetch(),
    rows: series?.items,
    page: table.page,
    setPage: table.setPage,
  })

  const chooseCategory = (value: string | undefined) =>
    setSearchParams(withFilter(searchParams, 'categoryId', value), { replace: true })

  const chooseStatus = (value: SeriesStatus | undefined) =>
    setSearchParams(withFilter(searchParams, 'status', value), { replace: true })

  const resizer = (key: SeriesColumnKey): ResizableHeaderCellProps =>
    handleFor(key, t('series.resizeColumn', { column: t(`series.columns.${key}`) }))

  const runBulk = (
    payload: {
      action: BulkSeriesAction
      categoryId?: string
      status?: SeriesStatus
    },
    onSettled?: () => void,
  ) =>
    applyBulk(
      () => bulk.mutateAsync({ payload: { ...payload, ids: selectedIds.map(String) } }),
      onSettled,
    )

  const confirmBulkDelete = () => {
    modal.confirm({
      title: t('series.bulk.deleteTitle', { count: selectedIds.length }),
      content: t('series.bulk.deleteBody'),
      okText: t('series.deleteConfirmOk'),
      okButtonProps: { danger: true },
      cancelText: t('actions.cancel'),
      onOk: () => runBulk({ action: 'delete' }),
    })
  }

  const onBulkMenu: MenuProps['onClick'] = ({ key }) => {
    switch (key) {
      case 'publish':
        runBulk({ action: 'change-status', status: 'Active' })
        break
      case 'hide':
        runBulk({ action: 'change-status', status: 'Inactive' })
        break
      case 'move-category':
        setBulkCategoryOpen(true)
        break
      case 'delete':
        confirmBulkDelete()
        break
    }
  }

  const bulkItems = useMemo<MenuProps['items']>(
    () => [
      {
        key: 'publish',
        icon: <EyeOutlined />,
        label: t('series.bulk.publish'),
      },
      {
        key: 'hide',
        icon: <EyeInvisibleOutlined />,
        label: t('series.bulk.hide'),
      },
      { type: 'divider' },
      {
        key: 'move-category',
        icon: <SwapOutlined />,
        label: t('series.bulk.move'),
      },
      { type: 'divider' },
      {
        key: 'delete',
        icon: <DeleteOutlined />,
        danger: true,
        label: t('series.bulk.delete'),
      },
    ],
    [t],
  )

  const bulkEnabled = canEdit && selectedIds.length > 0 && !bulkBusy

  const confirmDelete = (item: Series) =>
    askDelete(item, {
      name: item.name,
      titleKey: 'series.deleteConfirmTitle',
      bodyKey: 'series.deleteConfirmBody',
      okKey: 'series.deleteConfirmOk',
      run: () => removeSeries.mutateAsync({ id: item.id }),
    })

  const columns: TableColumnsType<Series> = [
    {
      title: t('series.columns.id'),
      dataIndex: 'id',
      key: 'id',
      width: widths.id,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('id'),
      render: idCell,
    },
    {
      title: t('series.columns.poster'),
      dataIndex: 'posterUrl',
      key: 'poster',
      width: widths.poster,
      align: 'center',
      onHeaderCell: () => resizer('poster'),
      render: (value: string | null) =>
        value ? (
          <Image
            src={value}
            alt=""
            width={40}
            height={56}
            preview={false}
            style={{ objectFit: 'cover', borderRadius: 4 }}
          />
        ) : (
          <span>—</span>
        ),
    },
    {
      title: t('series.columns.name'),
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
      title: t('series.columns.category'),
      dataIndex: 'categoryName',
      key: 'category',
      width: widths.category,
      sorter: true,
      sortOrder: table.orderOf('category'),
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('category'),
      render: textCell,
    },
    {
      title: t('series.columns.status'),
      dataIndex: 'status',
      key: 'status',
      width: widths.status,
      sorter: true,
      sortOrder: table.orderOf('status'),
      onHeaderCell: () => resizer('status'),
      render: (value: SeriesStatus) => (
        <Tag color={value === 'Active' ? 'green' : 'default'} style={{ marginInlineEnd: 0 }}>
          {t(`series.statuses.${value}`)}
        </Tag>
      ),
    },
    {
      title: t('series.columns.seasons'),
      dataIndex: 'seasonsCount',
      key: 'seasons',
      width: widths.seasons,
      sorter: true,
      sortOrder: table.orderOf('seasons'),
      align: 'center',
      onHeaderCell: () => resizer('seasons'),
    },
    {
      title: t('series.columns.episodes'),
      dataIndex: 'episodesCount',
      key: 'episodes',
      width: widths.episodes,
      sorter: true,
      sortOrder: table.orderOf('episodes'),
      align: 'center',
      onHeaderCell: () => resizer('episodes'),
    },
    {
      title: t('series.columns.actions'),
      key: 'actions',
      width: widths.actions,
      onHeaderCell: () => resizer('actions'),
      render: (_, item) => {
        const busy = busyId === item.id

        const actions: RowAction[] = [
          item.status === 'Active'
            ? {
                key: 'hide',
                label: t('series.actions.hide'),
                icon: <EyeInvisibleOutlined />,
                loading: busy,
                disabled: busy || !canEdit,
                onClick: () =>
                  void runAction(
                    () => setStatus.mutateAsync({ id: item.id, status: 'Inactive' }),
                    item,
                  ),
              }
            : {
                key: 'publish',
                label: t('series.actions.publish'),
                icon: <EyeOutlined />,
                loading: busy,
                disabled: busy || !canEdit,
                onClick: () =>
                  void runAction(
                    () => setStatus.mutateAsync({ id: item.id, status: 'Active' }),
                    item,
                  ),
              },
          {
            key: 'episodes',
            label: t('series.actions.episodes'),
            icon: <UnorderedListOutlined />,
            onClick: () => navigate(`${area}/series/${item.id}/episodes`),
          },
          {
            key: 'add-episode',
            label: t('series.actions.addEpisode'),
            icon: <PlusCircleOutlined />,
            disabled: !canEdit,
            onClick: () =>
              navigate(
                `${area}/series/${item.id}/episodes/add?season=${item.seasonsCount || 1}`,
              ),
          },
          {
            key: 'add-season',
            label: t('series.actions.addSeason'),
            icon: <FolderAddOutlined />,
            disabled: !canEdit,
            onClick: () => navigate(`${area}/series/${item.id}/seasons/add`),
          },
          {
            key: 'edit',
            label: t('series.actions.edit'),
            icon: <EditOutlined />,
            disabled: !canEdit,
            onClick: () => navigate(`${area}/series/${item.id}`),
          },
          {
            key: 'delete',
            label: t('series.actions.delete'),
            icon: <DeleteOutlined />,
            danger: true,
            disabled: busy || !canEdit,
            onClick: () => confirmDelete(item),
          },
        ]

        return <RowActions actions={actions} />
      },
    },
  ]

  const categoryOptions = useMemo(
    () => categories.map((category) => ({ value: category.id, label: category.name })),
    [categories],
  )

  const statusOptions = useMemo(
    () => STATUSES.map((status) => ({ value: status, label: t(`series.statuses.${status}`) })),
    [t],
  )

  const filtered = Boolean(table.query.search || categoryFilter || statusFilter)

  return (
    <Card
      className="page-card"
      title={t('series.listTitle')}
      extra={
        <Space wrap>
          {hinted(
            canEdit ? t('series.bulk.menu') : t('series.noPermission'),
            <Dropdown menu={{ items: bulkItems, onClick: onBulkMenu }} disabled={!bulkEnabled}>
              <Button icon={<MoreOutlined />} loading={bulkBusy}>
                {selectedIds.length > 0
                  ? t('series.bulk.menuWithCount', { count: selectedIds.length })
                  : t('series.bulk.menu')}
              </Button>
            </Dropdown>,
          )}
          <TableSearch
            value={table.searchText}
            onChange={table.setSearchText}
            onSearch={table.applySearch}
            labelKey="series.searchLabel"
            placeholderKey="series.searchPlaceholder"
            width={180}
          />
          <Select
            allowClear
            aria-label={t('series.columns.category')}
            style={{ width: 160 }}
            value={categoryFilter ?? undefined}
            options={categoryOptions}
            placeholder={t('series.filterAnyCategory')}
            onChange={chooseCategory}
          />
          <Select
            allowClear
            aria-label={t('series.columns.status')}
            style={{ width: 125 }}
            value={statusFilter ?? undefined}
            options={statusOptions}
            placeholder={t('series.filterAnyStatus')}
            onChange={chooseStatus}
          />
          <ResetColumnWidths
            label={t('series.resetWidths')}
            disabled={atDefaults}
            onClick={() => reset()}
          />
          <Button
            type="primary"
            icon={<PlusOutlined />}
            disabled={!canEdit}
            onClick={() => navigate(`${area}/series/add`)}
          >
            {t('series.addTitle')}
          </Button>
        </Space>
      }
    >
      <ServerTable<Series, SeriesSortField>
        table={table}
        columns={columns}
        rows={series?.items ?? []}
        total={series?.total ?? 0}
        loading={loading}
        loadError={loadError}
        onRetry={() => seriesQuery.refetch()}
        tableWidth={tableWidth}
        emptyText={t(filtered ? 'series.noMatches' : 'series.empty')}
        showTotal={(total, [from, to]) => t('series.range', { from, to, total })}
        selection={{ selectedRowKeys: selectedIds, onChange: setSelectedIds }}
      />

      <Modal
        title={t('series.bulk.moveTitle')}
        open={bulkCategoryOpen}
        okText={t('series.bulk.apply')}
        okButtonProps={{ disabled: !bulkCategoryId, loading: bulkBusy }}
        cancelText={t('actions.cancel')}
        onOk={() =>
          runBulk(
            { action: 'move-category', categoryId: bulkCategoryId ?? undefined },
            () => setBulkCategoryOpen(false),
          )
        }
        onCancel={() => setBulkCategoryOpen(false)}
      >
        <Select
          showSearch
          allowClear
          aria-label={t('series.bulk.movePlaceholder')}
          style={{ width: '100%' }}
          value={bulkCategoryId ?? undefined}
          options={categoryOptions}
          placeholder={t('series.bulk.movePlaceholder')}
          onChange={(value) => setBulkCategoryId(value ?? null)}
        />
      </Modal>
    </Card>
  )
}
