import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import {
  Alert,
  App,
  Button,
  Card,
  Dropdown,
  Image,
  Modal,
  Select,
  Space,
  Tag,
} from 'antd'
import type { MenuProps, TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import {
  CloudSyncOutlined,
  DeleteOutlined,
  EditOutlined,
  EyeInvisibleOutlined,
  EyeOutlined,
  MoreOutlined,
  PauseCircleOutlined,
  PlayCircleOutlined,
  PlusOutlined,
  ReloadOutlined,
  SwapOutlined,
} from '@ant-design/icons'
import { CONTENT_TYPE_PARAMS } from '../../features/content-categories/api'
import {
  VIDEO_ON_DEMAND_PAGE_SIZE,
  playVideoOnDemandUrl,
} from '../../features/video-on-demand/api'
import type {
  BulkVideoOnDemandAction,
  VideoOnDemandSortField,
} from '../../features/video-on-demand/api'
import type {
  VideoOnDemand,
  VideoOnDemandStatus,
  VideoOnDemandType,
} from '../../features/video-on-demand/types'
import { useContentCategories } from '../../features/content-categories/hooks/queries'
import { useProfile } from '../../features/profile/hooks/queries'
import {
  useBulkVideoOnDemand,
  useDeleteVideoOnDemand,
  useRequestVideoOnDemandTranscode,
  useSetVideoOnDemandStatus,
} from '../../features/video-on-demand/hooks/mutations'
import {
  useVideoOnDemand,
  useVideoOnDemandDetail,
  useVideoOnDemandTranscodeFormats,
} from '../../features/video-on-demand/hooks/queries'
import { useColumnWidths, useBulkAction, useRowAction, useServerTable } from '../../hooks/table'
import { ResetColumnWidths, RowActions, ServerTable, TableSearch } from '../../components/table'
import type { ResizableHeaderCellProps, RowAction } from '../../components/table'
import { hinted } from '../../utils/hinted'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'
import { tableResults } from '../../utils/tableResults'
import { dashedCell, idCell, textCell } from '../../utils/tableCells'
import { enumParam, withFilter } from '../../utils/tableParams'
import { actionsColumnWidth } from '../../utils/tableWidths'
import { messageForError, successTextFor } from '../../utils/http'

/** How often the table re-reads itself while the toggle below is on. */
const AUTO_REFRESH_MS = 30_000

/** The nine columns, in the order they are drawn. */
type VodColumnKey =
  | 'id'
  | 'poster'
  | 'name'
  | 'category'
  | 'status'
  | 'releaseDate'
  | 'rating'
  | 'actions'

const COLUMN_KEYS: readonly VodColumnKey[] = [
  'id',
  'poster',
  'name',
  'category',
  'status',
  'releaseDate',
  'rating',
  'actions',
]

/**
 * The width each column starts at. The eight sum to **1190**, and the selection column
 * `ServerTable` adds is the 40 that makes **1230** - the same total every other table here
 * reaches. The name takes the room the rest does not need, because it is the one column
 * nothing truncates a second time; the actions column is exactly as wide as the five
 * buttons a row draws.
 */
const FIXED_COLUMN_WIDTHS = {
  id: 100,
  poster: 70,
  category: 170,
  status: 160,
  releaseDate: 120,
  rating: 90,
}

const ACTIONS_WIDTH = actionsColumnWidth(5)
const TABLE_WIDTH = 1190

const DEFAULT_COLUMN_WIDTHS: Record<VodColumnKey, number> = {
  ...FIXED_COLUMN_WIDTHS,
  name:
    TABLE_WIDTH -
    Object.values(FIXED_COLUMN_WIDTHS).reduce((sum, width) => sum + width, 0) -
    ACTIONS_WIDTH,
  actions: ACTIONS_WIDTH,
}

/** Where this table remembers the widths the visitor gave its columns. */
const COLUMN_WIDTHS_KEY = 'fluxy.videoOnDemand.columnWidths'

/** The two states and the two kinds, in the order the backend declares them. */
const STATUSES: VideoOnDemandStatus[] = ['Active', 'Inactive']
const TYPES: VideoOnDemandType[] = ['Relay', 'Redirect']

interface VideoOnDemandPageProps {
  /** The menu key this route carries, accepted and deliberately unread. */
  sectionKey: string
}

/**
 * The admin's on-demand catalogue: the page behind `nav.items.videoOnDemandManage`.
 *
 * Paging, search, filtering and sorting are all server side, for the reason every other
 * table's are: `total`. The filters live in the address, so a reload or a shared link keeps
 * the view.
 *
 * The two actions that are not edits are the reason this page exists beside the streams one:
 * **play** opens the video through the endpoint that owns the delivery rule, and
 * **transcode** offers the five formats. No encoder is configured in this pass, so the
 * transcode action is drawn disabled with the reason - the same "tell the truth before the
 * click" rule the streams' play button follows.
 */
export default function VideoOnDemandPage(_props: VideoOnDemandPageProps) {
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const navigate = useNavigate()
  const session = useSession()

  const table = useServerTable<VideoOnDemand, VideoOnDemandSortField>({
    defaultSort: { by: 'name', order: 'asc' },
    sortFields: ['name', 'category', 'status', 'type', 'releaseDate', 'rating'],
    pageSize: VIDEO_ON_DEMAND_PAGE_SIZE,
  })

  const [searchParams, setSearchParams] = useSearchParams()
  const categoryFilter = searchParams.get('categoryId') || null
  const statusFilter = enumParam(searchParams, 'status', STATUSES)
  const typeFilter = enumParam(searchParams, 'type', TYPES)
  const [autoRefresh, setAutoRefresh] = useState(true)

  const videosQuery = useVideoOnDemand({
    ...table.query,
    categoryId: categoryFilter,
    status: statusFilter,
    type: typeFilter,
  })
  const { data: videos, loadError, loading } = tableResults(videosQuery)

  const categoriesQuery = useContentCategories(CONTENT_TYPE_PARAMS.VideoOnDemand)
  const categories = categoriesQuery.data?.items ?? []

  const formatsQuery = useVideoOnDemandTranscodeFormats()
  const formats = formatsQuery.data
  const transcodingAvailable = formats?.available ?? false

  const { widths, tableWidth, atDefaults, reset, resizer: handleFor } =
    useColumnWidths<VodColumnKey>({
      keys: COLUMN_KEYS,
      defaults: DEFAULT_COLUMN_WIDTHS,
      storageKey: COLUMN_WIDTHS_KEY,
    })

  const area = session ? areaForRole(session.role) : ''

  const profileQuery = useProfile()
  const granted = profileQuery.data?.permissions ?? []
  const canEdit = granted.includes('editVideoOnDemand')

  const { selectedIds, setSelectedIds, bulkBusy, applyBulk } = useBulkAction({
    rows: videos?.items,
    refetch: () => void videosQuery.refetch(),
    partialTitleKey: 'videoOnDemand.bulk.partialTitle',
  })

  const [bulkCategoryOpen, setBulkCategoryOpen] = useState(false)
  const [bulkCategoryId, setBulkCategoryId] = useState<string | null>(null)
  const [bulkTypeOpen, setBulkTypeOpen] = useState(false)
  const [bulkType, setBulkType] = useState<VideoOnDemandType>('Relay')

  const [playTarget, setPlayTarget] = useState<VideoOnDemand | null>(null)
  const playDetail = useVideoOnDemandDetail(playTarget?.id)

  const [transcodeTarget, setTranscodeTarget] = useState<VideoOnDemand | null>(null)
  const requestTranscode = useRequestVideoOnDemandTranscode()

  const removeVideo = useDeleteVideoOnDemand()
  const setStatus = useSetVideoOnDemandStatus()
  const bulk = useBulkVideoOnDemand()

  const { busyId, runAction, confirmDelete: askDelete } = useRowAction<VideoOnDemand>({
    refetch: () => void videosQuery.refetch(),
    rows: videos?.items,
    page: table.page,
    setPage: table.setPage,
  })

  const refreshRef = useRef<() => void>(() => {})

  useEffect(() => {
    refreshRef.current = () => {
      void videosQuery.refetch()
    }
  })

  useEffect(() => {
    if (!autoRefresh) return

    const timer = setInterval(() => refreshRef.current(), AUTO_REFRESH_MS)

    return () => clearInterval(timer)
  }, [autoRefresh])

  const chooseCategory = (value: string | undefined) =>
    setSearchParams(withFilter(searchParams, 'categoryId', value), { replace: true })

  const chooseStatus = (value: VideoOnDemandStatus | undefined) =>
    setSearchParams(withFilter(searchParams, 'status', value), { replace: true })

  const chooseType = (value: VideoOnDemandType | undefined) =>
    setSearchParams(withFilter(searchParams, 'type', value), { replace: true })

  const resizer = (key: VodColumnKey): ResizableHeaderCellProps =>
    handleFor(key, t('videoOnDemand.resizeColumn', { column: t(`videoOnDemand.columns.${key}`) }))

  const runBulk = (
    payload: { action: BulkVideoOnDemandAction; categoryId?: string; status?: VideoOnDemandStatus; type?: VideoOnDemandType },
    onSettled?: () => void,
  ) =>
    applyBulk(
      () => bulk.mutateAsync({ payload: { ...payload, ids: selectedIds.map(String) } }),
      onSettled,
    )

  const confirmBulkDelete = () => {
    modal.confirm({
      title: t('videoOnDemand.bulk.deleteTitle', { count: selectedIds.length }),
      content: t('videoOnDemand.bulk.deleteBody'),
      okText: t('videoOnDemand.deleteConfirmOk'),
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
      case 'change-type':
        setBulkTypeOpen(true)
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
        label: t('videoOnDemand.bulk.publish'),
      },
      {
        key: 'hide',
        icon: <EyeInvisibleOutlined />,
        label: t('videoOnDemand.bulk.hide'),
      },
      { type: 'divider' },
      {
        key: 'change-type',
        icon: <CloudSyncOutlined />,
        label: t('videoOnDemand.bulk.type'),
      },
      {
        key: 'move-category',
        icon: <SwapOutlined />,
        label: t('videoOnDemand.bulk.move'),
      },
      { type: 'divider' },
      {
        key: 'delete',
        icon: <DeleteOutlined />,
        danger: true,
        label: t('videoOnDemand.bulk.delete'),
      },
    ],
    [t],
  )

  const bulkEnabled = canEdit && selectedIds.length > 0 && !bulkBusy

  const confirmDelete = (video: VideoOnDemand) =>
    askDelete(video, {
      name: video.name,
      titleKey: 'videoOnDemand.deleteConfirmTitle',
      bodyKey: 'videoOnDemand.deleteConfirmBody',
      okKey: 'videoOnDemand.deleteConfirmOk',
      run: () => removeVideo.mutateAsync({ id: video.id }),
    })

  const askTranscode = async (video: VideoOnDemand, format: string) => {
    try {
      const answer = await requestTranscode.mutateAsync({ id: video.id, format })
      message.success(successTextFor(answer))
      setTranscodeTarget(null)
    } catch (error) {
      message.error(messageForError(error))
    }
  }

  const transcodeHint = !canEdit
    ? t('videoOnDemand.noPermission')
    : transcodingAvailable
      ? t('videoOnDemand.actions.transcode')
      : t('videoOnDemand.actions.transcodeUnavailable')

  const columns: TableColumnsType<VideoOnDemand> = [
    {
      title: t('videoOnDemand.columns.id'),
      dataIndex: 'id',
      key: 'id',
      width: widths.id,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('id'),
      render: idCell,
    },
    {
      title: t('videoOnDemand.columns.poster'),
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
      title: t('videoOnDemand.columns.name'),
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
      title: t('videoOnDemand.columns.category'),
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
      title: t('videoOnDemand.columns.status'),
      dataIndex: 'status',
      key: 'status',
      width: widths.status,
      sorter: true,
      sortOrder: table.orderOf('status'),
      onHeaderCell: () => resizer('status'),
      render: (value: VideoOnDemandStatus, video: VideoOnDemand) => (
        <Space direction="vertical" size={0}>
          <Tag color={value === 'Active' ? 'green' : 'default'} style={{ marginInlineEnd: 0 }}>
            {t(`videoOnDemand.statuses.${value}`)}
          </Tag>
          <span style={{ fontSize: 12, opacity: 0.65 }}>
            {t(`videoOnDemand.types.${video.type}`)}
          </span>
        </Space>
      ),
    },
    {
      title: t('videoOnDemand.columns.releaseDate'),
      dataIndex: 'releaseDate',
      key: 'releaseDate',
      width: widths.releaseDate,
      sorter: true,
      sortOrder: table.orderOf('releaseDate'),
      onHeaderCell: () => resizer('releaseDate'),
      render: dashedCell,
    },
    {
      title: t('videoOnDemand.columns.rating'),
      dataIndex: 'rating',
      key: 'rating',
      width: widths.rating,
      sorter: true,
      sortOrder: table.orderOf('rating'),
      align: 'center',
      onHeaderCell: () => resizer('rating'),
      render: (value: number | null) => (value === null ? <span>—</span> : value.toFixed(1)),
    },
    {
      title: t('videoOnDemand.columns.actions'),
      key: 'actions',
      width: widths.actions,
      onHeaderCell: () => resizer('actions'),
      render: (_, video) => {
        const busy = busyId === video.id

        const actions: RowAction[] = [
          video.status === 'Active'
            ? {
                key: 'hide',
                label: t('videoOnDemand.actions.hide'),
                icon: <EyeInvisibleOutlined />,
                loading: busy,
                disabled: busy,
                onClick: () =>
                  runAction(
                    () => setStatus.mutateAsync({ id: video.id, status: 'Inactive' }),
                    video,
                  ),
              }
            : {
                key: 'publish',
                label: t('videoOnDemand.actions.publish'),
                icon: <EyeOutlined />,
                loading: busy,
                disabled: busy,
                onClick: () =>
                  runAction(
                    () => setStatus.mutateAsync({ id: video.id, status: 'Active' }),
                    video,
                  ),
              },
          {
            key: 'play',
            label: t('videoOnDemand.actions.play'),
            icon: <PlayCircleOutlined />,
            onClick: () => setPlayTarget(video),
          },
          {
            key: 'transcode',
            label: transcodeHint,
            icon: <SwapOutlined />,
            disabled: !canEdit || !transcodingAvailable,
            onClick: () => setTranscodeTarget(video),
          },
          {
            key: 'edit',
            label: t('videoOnDemand.actions.edit'),
            icon: <EditOutlined />,
            onClick: () => navigate(`${area}/video-on-demand/${video.id}`),
          },
          {
            key: 'delete',
            label: t('videoOnDemand.actions.delete'),
            icon: <DeleteOutlined />,
            danger: true,
            disabled: busy,
            onClick: () => confirmDelete(video),
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
    () => STATUSES.map((status) => ({ value: status, label: t(`videoOnDemand.statuses.${status}`) })),
    [t],
  )

  const typeOptions = useMemo(
    () => TYPES.map((type) => ({ value: type, label: t(`videoOnDemand.types.${type}`) })),
    [t],
  )

  const filtered = Boolean(table.query.search || categoryFilter || statusFilter || typeFilter)

  const refreshTip = `${t(
    autoRefresh ? 'videoOnDemand.pauseRefresh' : 'videoOnDemand.resumeRefresh',
  )} (${t('videoOnDemand.autoRefreshHint', { seconds: AUTO_REFRESH_MS / 1000 })})`

  return (
    <Card
      className="page-card"
      title={t('videoOnDemand.listTitle')}
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
          {hinted(
            canEdit ? t('videoOnDemand.bulk.menu') : t('videoOnDemand.noPermission'),
            <Dropdown menu={{ items: bulkItems, onClick: onBulkMenu }} disabled={!bulkEnabled}>
              <Button icon={<MoreOutlined />} loading={bulkBusy}>
                {selectedIds.length > 0
                  ? t('videoOnDemand.bulk.menuWithCount', { count: selectedIds.length })
                  : t('videoOnDemand.bulk.menu')}
              </Button>
            </Dropdown>,
          )}
          <TableSearch
            value={table.searchText}
            onChange={table.setSearchText}
            onSearch={table.applySearch}
            labelKey="videoOnDemand.searchLabel"
            placeholderKey="videoOnDemand.searchPlaceholder"
            width={180}
          />
          <Select
            allowClear
            aria-label={t('videoOnDemand.columns.category')}
            style={{ width: 160 }}
            value={categoryFilter ?? undefined}
            options={categoryOptions}
            placeholder={t('videoOnDemand.filterAnyCategory')}
            onChange={chooseCategory}
          />
          <Select
            allowClear
            aria-label={t('videoOnDemand.columns.status')}
            style={{ width: 125 }}
            value={statusFilter ?? undefined}
            options={statusOptions}
            placeholder={t('videoOnDemand.filterAnyStatus')}
            onChange={chooseStatus}
          />
          <Select
            allowClear
            aria-label={t('videoOnDemand.columns.type')}
            style={{ width: 150 }}
            value={typeFilter ?? undefined}
            options={typeOptions}
            placeholder={t('videoOnDemand.filterAnyType')}
            onChange={chooseType}
          />
          <ResetColumnWidths
            label={t('videoOnDemand.resetWidths')}
            disabled={atDefaults}
            onClick={() => reset()}
          />
          <Button
            type="primary"
            icon={<PlusOutlined />}
            disabled={!canEdit}
            onClick={() => navigate(`${area}/video-on-demand/add`)}
          >
            {t('videoOnDemand.addTitle')}
          </Button>
        </Space>
      }
    >
      <ServerTable<VideoOnDemand, VideoOnDemandSortField>
        table={table}
        columns={columns}
        rows={videos?.items ?? []}
        total={videos?.total ?? 0}
        loading={loading}
        loadError={loadError}
        onRetry={() => videosQuery.refetch()}
        tableWidth={tableWidth}
        emptyText={t(filtered ? 'videoOnDemand.noMatches' : 'videoOnDemand.empty')}
        showTotal={(total, [from, to]) => t('videoOnDemand.range', { from, to, total })}
        selection={{ selectedRowKeys: selectedIds, onChange: setSelectedIds }}
      />

      {/* The player. The source is the endpoint, not the stored address: the server owns
          whether a Relay is proxied and a Redirect answers 302, so one URL covers both. */}
      <Modal
        title={playTarget?.name}
        open={playTarget !== null}
        footer={null}
        width={800}
        onCancel={() => setPlayTarget(null)}
      >
        {playTarget && (
          // The caption comes from the item's optional subtitle address, so the element has
          // no track when the row carries none. The rule below assumes a video always has
          // captions; here that is a per-row fact rather than a property of the page.
          // eslint-disable-next-line jsx-a11y/media-has-caption
          <video
            key={playTarget.id}
            controls
            autoPlay
            style={{ width: '100%', maxHeight: '70vh', background: '#000' }}
            src={playVideoOnDemandUrl(playTarget.id)}
          >
            {playDetail.data?.subtitleUrl && (
              <track
                kind="subtitles"
                src={playDetail.data.subtitleUrl}
                default
              />
            )}
          </video>
        )}
      </Modal>

      {/* The transcoding surface. The server has no encoder in this pass, so the menu is
          disabled before the click; the dialog still states the fact rather than drawing
          five buttons whose every press would be refused. */}
      <Modal
        title={t('videoOnDemand.transcode.title', { name: transcodeTarget?.name ?? '' })}
        open={transcodeTarget !== null}
        footer={null}
        onCancel={() => setTranscodeTarget(null)}
      >
        {!transcodingAvailable && (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 16 }}
            message={t('videoOnDemand.transcode.unavailable')}
          />
        )}
        <Space wrap>
          {(formats?.formats ?? []).map((format) => (
            <Button
              key={format}
              disabled={!transcodingAvailable || requestTranscode.isPending}
              onClick={() => void askTranscode(transcodeTarget!, format)}
            >
              {format.toUpperCase()}
            </Button>
          ))}
        </Space>
      </Modal>

      {/* The two bulk edits that need a value, one dialog each: the value fits one control,
          and a dialog is the only place a menu that has already closed can still ask. */}
      <Modal
        title={t('videoOnDemand.bulk.moveTitle')}
        open={bulkCategoryOpen}
        okText={t('videoOnDemand.bulk.apply')}
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
          aria-label={t('videoOnDemand.bulk.movePlaceholder')}
          style={{ width: '100%' }}
          value={bulkCategoryId ?? undefined}
          options={categoryOptions}
          placeholder={t('videoOnDemand.bulk.movePlaceholder')}
          onChange={(value) => setBulkCategoryId(value ?? null)}
        />
      </Modal>

      <Modal
        title={t('videoOnDemand.bulk.typeTitle')}
        open={bulkTypeOpen}
        okText={t('videoOnDemand.bulk.apply')}
        okButtonProps={{ loading: bulkBusy }}
        cancelText={t('actions.cancel')}
        onOk={() => runBulk({ action: 'change-type', type: bulkType }, () => setBulkTypeOpen(false))}
        onCancel={() => setBulkTypeOpen(false)}
      >
        <Select
          aria-label={t('videoOnDemand.columns.type')}
          style={{ width: '100%' }}
          value={bulkType}
          options={typeOptions}
          onChange={setBulkType}
        />
      </Modal>
    </Card>
  )
}
