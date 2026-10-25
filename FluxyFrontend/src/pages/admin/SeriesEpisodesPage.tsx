import { useMemo, useState } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { Alert, App, Button, Card, Dropdown, Modal, Select, Space, Tag } from 'antd'
import type { MenuProps, TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import {
  ArrowLeftOutlined,
  DeleteOutlined,
  EditOutlined,
  EyeInvisibleOutlined,
  EyeOutlined,
  FolderAddOutlined,
  MoreOutlined,
  PlusOutlined,
  SwapOutlined,
} from '@ant-design/icons'
import { EPISODES_PAGE_SIZE } from '../../features/series/api'
import type { BulkEpisodeAction, EpisodeSortField } from '../../features/series/api'
import type { Episode, EpisodeStatus, EpisodeType } from '../../features/series/types'
import {
  useBulkEpisodes,
  useDeleteEpisode,
  useRequestEpisodeTranscode,
  useSetEpisodeStatus,
} from '../../features/series/hooks/mutations'
import {
  useEpisodeTranscodeFormats,
  useEpisodes,
  useSeriesDetail,
} from '../../features/series/hooks/queries'
import { useProfile } from '../../features/profile/hooks/queries'
import { useBulkAction, useColumnWidths, useRowAction, useServerTable } from '../../hooks/table'
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

/** The eight columns, in the order they are drawn. */
type EpisodeColumnKey =
  | 'id'
  | 'season'
  | 'episode'
  | 'name'
  | 'status'
  | 'releaseDate'
  | 'rating'
  | 'actions'

const COLUMN_KEYS: readonly EpisodeColumnKey[] = [
  'id',
  'season',
  'episode',
  'name',
  'status',
  'releaseDate',
  'rating',
  'actions',
]

const FIXED_COLUMN_WIDTHS = {
  id: 80,
  season: 120,
  episode: 90,
  status: 140,
  releaseDate: 110,
  rating: 110,
}

const ACTIONS_WIDTH = actionsColumnWidth(4)
const TABLE_WIDTH = 1190

const DEFAULT_COLUMN_WIDTHS: Record<EpisodeColumnKey, number> = {
  ...FIXED_COLUMN_WIDTHS,
  name:
    TABLE_WIDTH -
    Object.values(FIXED_COLUMN_WIDTHS).reduce((sum, width) => sum + width, 0) -
    ACTIONS_WIDTH,
  actions: ACTIONS_WIDTH,
}

const COLUMN_WIDTHS_KEY = 'fluxy.seriesEpisodes.columnWidths'

const STATUSES: EpisodeStatus[] = ['Active', 'Inactive']
const TYPES: EpisodeType[] = ['Relay', 'Redirect']

interface SeriesEpisodesPageProps {
  /** The menu key this route carries, accepted and deliberately unread. */
  sectionKey: string
}

/**
 * The episodes of one series: the table behind a row of the series catalogue, at an address
 * the sidebar deliberately does not offer.
 *
 * The filters live in the address, so a reload or a shared link keeps the view; the season
 * filter is the one the add-episode form carries back from here, which is what makes "add an
 * episode to the season I am looking at" one click rather than a second decision.
 */
export default function SeriesEpisodesPage(_props: SeriesEpisodesPageProps) {
  const { t } = useTranslation()
  const { modal, message } = App.useApp()
  const navigate = useNavigate()
  const session = useSession()
  const { id } = useParams()
  const seriesId = id ?? ''

  const table = useServerTable<Episode, EpisodeSortField>({
    defaultSort: { by: 'season', order: 'asc' },
    sortFields: ['season', 'episode', 'name', 'status', 'type', 'releaseDate', 'rating'],
    pageSize: EPISODES_PAGE_SIZE,
  })

  const [searchParams, setSearchParams] = useSearchParams()
  const rawSeason = searchParams.get('seasonNumber') ?? ''
  const parsedSeason = Number.parseInt(rawSeason, 10)
  const seasonFilter =
    Number.isInteger(parsedSeason) && parsedSeason > 0 ? parsedSeason : null
  const statusFilter = enumParam(searchParams, 'status', STATUSES)
  const typeFilter = enumParam(searchParams, 'type', TYPES)

  const seriesQuery = useSeriesDetail(seriesId || undefined)
  const episodesQuery = useEpisodes(seriesId || undefined, {
    ...table.query,
    seasonNumber: seasonFilter,
    status: statusFilter,
    type: typeFilter,
  })
  const { data: episodes, loadError, loading } = tableResults(episodesQuery)

  const formatsQuery = useEpisodeTranscodeFormats()
  const formats = formatsQuery.data
  const transcodingAvailable = formats?.available ?? false

  const { widths, tableWidth, atDefaults, reset, resizer: handleFor } =
    useColumnWidths<EpisodeColumnKey>({
      keys: COLUMN_KEYS,
      defaults: DEFAULT_COLUMN_WIDTHS,
      storageKey: COLUMN_WIDTHS_KEY,
    })

  const area = session ? areaForRole(session.role) : ''
  const backPath = `${area}/series`

  const profileQuery = useProfile()
  const granted = profileQuery.data?.permissions ?? []
  const canEdit = granted.includes('editSeries')

  const { selectedIds, setSelectedIds, bulkBusy, applyBulk } = useBulkAction({
    rows: episodes?.items,
    refetch: () => {
      void episodesQuery.refetch()
      void seriesQuery.refetch()
    },
    partialTitleKey: 'seriesEpisodes.bulk.partialTitle',
  })

  const [bulkTypeOpen, setBulkTypeOpen] = useState(false)
  const [bulkType, setBulkType] = useState<EpisodeType>('Relay')
  const [transcodeTarget, setTranscodeTarget] = useState<Episode | null>(null)

  const removeEpisode = useDeleteEpisode()
  const setStatus = useSetEpisodeStatus()
  const bulk = useBulkEpisodes()
  const requestTranscode = useRequestEpisodeTranscode()

  const { busyId, runAction, confirmDelete: askDelete } = useRowAction<Episode>({
    refetch: () => {
      void episodesQuery.refetch()
      void seriesQuery.refetch()
    },
    rows: episodes?.items,
    page: table.page,
    setPage: table.setPage,
  })

  const chooseSeason = (value: string | undefined) =>
    setSearchParams(withFilter(searchParams, 'seasonNumber', value), { replace: true })

  const chooseStatus = (value: EpisodeStatus | undefined) =>
    setSearchParams(withFilter(searchParams, 'status', value), { replace: true })

  const chooseType = (value: EpisodeType | undefined) =>
    setSearchParams(withFilter(searchParams, 'type', value), { replace: true })

  const resizer = (key: EpisodeColumnKey): ResizableHeaderCellProps =>
    handleFor(key, t('seriesEpisodes.resizeColumn', { column: t(`seriesEpisodes.columns.${key}`) }))

  const runBulk = (
    payload: {
      action: BulkEpisodeAction
      status?: EpisodeStatus
      type?: EpisodeType
    },
    onSettled?: () => void,
  ) =>
    applyBulk(
      () =>
        bulk.mutateAsync({
          seriesId,
          payload: { ...payload, ids: selectedIds.map(String) },
        }),
      onSettled,
    )

  const confirmBulkDelete = () => {
    modal.confirm({
      title: t('seriesEpisodes.bulk.deleteTitle', { count: selectedIds.length }),
      content: t('seriesEpisodes.bulk.deleteBody'),
      okText: t('seriesEpisodes.deleteConfirmOk'),
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
        label: t('seriesEpisodes.bulk.publish'),
      },
      {
        key: 'hide',
        icon: <EyeInvisibleOutlined />,
        label: t('seriesEpisodes.bulk.hide'),
      },
      { type: 'divider' },
      {
        key: 'change-type',
        icon: <SwapOutlined />,
        label: t('seriesEpisodes.bulk.type'),
      },
      { type: 'divider' },
      {
        key: 'delete',
        icon: <DeleteOutlined />,
        danger: true,
        label: t('seriesEpisodes.bulk.delete'),
      },
    ],
    [t],
  )

  const bulkEnabled = canEdit && selectedIds.length > 0 && !bulkBusy

  const confirmDelete = (episode: Episode) =>
    askDelete(episode, {
      name: episode.name ?? `S${episode.seasonNumber}E${episode.episodeNumber}`,
      titleKey: 'seriesEpisodes.deleteConfirmTitle',
      bodyKey: 'seriesEpisodes.deleteConfirmBody',
      okKey: 'seriesEpisodes.deleteConfirmOk',
      run: () =>
        removeEpisode.mutateAsync({ seriesId, episodeId: episode.id }),
    })

  const askTranscode = async (episode: Episode, format: string) => {
    try {
      const answer = await requestTranscode.mutateAsync({
        seriesId,
        episodeId: episode.id,
        format,
      })
      message.success(successTextFor(answer))
      setTranscodeTarget(null)
    } catch (error) {
      message.error(messageForError(error))
    }
  }

  const transcodeHint = !canEdit
    ? t('seriesEpisodes.noPermission')
    : transcodingAvailable
      ? t('seriesEpisodes.actions.transcode')
      : t('seriesEpisodes.actions.transcodeUnavailable')

  const columns: TableColumnsType<Episode> = [
    {
      title: t('seriesEpisodes.columns.id'),
      dataIndex: 'id',
      key: 'id',
      width: widths.id,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('id'),
      render: idCell,
    },
    {
      title: t('seriesEpisodes.columns.season'),
      dataIndex: 'seasonNumber',
      key: 'season',
      width: widths.season,
      sorter: true,
      sortOrder: table.orderOf('season'),
      align: 'center',
      onHeaderCell: () => resizer('season'),
    },
    {
      title: t('seriesEpisodes.columns.episode'),
      dataIndex: 'episodeNumber',
      key: 'episode',
      width: widths.episode,
      sorter: true,
      sortOrder: table.orderOf('episode'),
      align: 'center',
      onHeaderCell: () => resizer('episode'),
    },
    {
      title: t('seriesEpisodes.columns.name'),
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
      title: t('seriesEpisodes.columns.status'),
      dataIndex: 'status',
      key: 'status',
      width: widths.status,
      sorter: true,
      sortOrder: table.orderOf('status'),
      onHeaderCell: () => resizer('status'),
      render: (value: EpisodeStatus, episode: Episode) => (
        <Space direction="vertical" size={0}>
          <Tag color={value === 'Active' ? 'green' : 'default'} style={{ marginInlineEnd: 0 }}>
            {t(`series.statuses.${value}`)}
          </Tag>
          <span style={{ fontSize: 12, opacity: 0.65 }}>
            {t(`seriesEpisodes.types.${episode.type}`)}
          </span>
        </Space>
      ),
    },
    {
      title: t('seriesEpisodes.columns.releaseDate'),
      dataIndex: 'releaseDate',
      key: 'releaseDate',
      width: widths.releaseDate,
      sorter: true,
      sortOrder: table.orderOf('releaseDate'),
      onHeaderCell: () => resizer('releaseDate'),
      render: dashedCell,
    },
    {
      title: t('seriesEpisodes.columns.rating'),
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
      title: t('seriesEpisodes.columns.actions'),
      key: 'actions',
      width: widths.actions,
      onHeaderCell: () => resizer('actions'),
      render: (_, episode) => {
        const busy = busyId === episode.id

        const actions: RowAction[] = [
          {
            key: 'transcode',
            label: transcodeHint,
            icon: <SwapOutlined />,
            disabled: !canEdit || !transcodingAvailable,
            onClick: () => setTranscodeTarget(episode),
          },
          episode.status === 'Active'
            ? {
                key: 'hide',
                label: t('seriesEpisodes.actions.hide'),
                icon: <EyeInvisibleOutlined />,
                loading: busy,
                disabled: busy || !canEdit,
                onClick: () =>
                  void runAction(
                    () =>
                      setStatus.mutateAsync({
                        seriesId,
                        episodeId: episode.id,
                        status: 'Inactive',
                      }),
                    episode,
                  ),
              }
            : {
                key: 'publish',
                label: t('seriesEpisodes.actions.publish'),
                icon: <EyeOutlined />,
                loading: busy,
                disabled: busy || !canEdit,
                onClick: () =>
                  void runAction(
                    () =>
                      setStatus.mutateAsync({
                        seriesId,
                        episodeId: episode.id,
                        status: 'Active',
                      }),
                    episode,
                  ),
              },
          {
            key: 'edit',
            label: t('seriesEpisodes.actions.edit'),
            icon: <EditOutlined />,
            disabled: !canEdit,
            onClick: () => navigate(`${area}/series/${seriesId}/episodes/${episode.id}`),
          },
          {
            key: 'delete',
            label: t('seriesEpisodes.actions.delete'),
            icon: <DeleteOutlined />,
            danger: true,
            disabled: busy || !canEdit,
            onClick: () => confirmDelete(episode),
          },
        ]

        return <RowActions actions={actions} />
      },
    },
  ]

  const seasonOptions = useMemo(() => {
    const count = seriesQuery.data?.seasonsCount ?? 0

    return Array.from({ length: count }, (_, index) => {
      const season = index + 1

      return { value: String(season), label: t('seriesEpisodes.seasonOption', { season }) }
    })
  }, [seriesQuery.data?.seasonsCount, t])

  const statusOptions = useMemo(
    () => STATUSES.map((status) => ({ value: status, label: t(`series.statuses.${status}`) })),
    [t],
  )

  const typeOptions = useMemo(
    () => TYPES.map((type) => ({ value: type, label: t(`seriesEpisodes.types.${type}`) })),
    [t],
  )

  const filtered = Boolean(
    table.query.search || seasonFilter || statusFilter || typeFilter,
  )

  const addEpisodeSeason = seasonFilter ?? (seriesQuery.data?.seasonsCount || 1)

  return (
    <Card
      className="page-card"
      title={
        <Space direction="vertical" size={0}>
          <span>{t('seriesEpisodes.listTitle')}</span>
          {seriesQuery.data?.name && (
            <span style={{ fontSize: 12, fontWeight: 400, opacity: 0.65 }}>
              {seriesQuery.data.name}
            </span>
          )}
        </Space>
      }
      extra={
        <Space wrap>
          {hinted(
            t('seriesEpisodes.backToSeries'),
            <Button
              icon={<ArrowLeftOutlined />}
              aria-label={t('seriesEpisodes.backToSeries')}
              onClick={() => navigate(backPath)}
            />,
          )}
          {hinted(
            canEdit ? t('seriesEpisodes.bulk.menu') : t('seriesEpisodes.noPermission'),
            <Dropdown menu={{ items: bulkItems, onClick: onBulkMenu }} disabled={!bulkEnabled}>
              <Button icon={<MoreOutlined />} loading={bulkBusy}>
                {selectedIds.length > 0
                  ? t('seriesEpisodes.bulk.menuWithCount', { count: selectedIds.length })
                  : t('seriesEpisodes.bulk.menu')}
              </Button>
            </Dropdown>,
          )}
          <TableSearch
            value={table.searchText}
            onChange={table.setSearchText}
            onSearch={table.applySearch}
            labelKey="seriesEpisodes.searchLabel"
            placeholderKey="seriesEpisodes.searchPlaceholder"
            width={180}
          />
          <Select
            allowClear
            aria-label={t('seriesEpisodes.columns.season')}
            style={{ width: 140 }}
            value={seasonFilter === null ? undefined : String(seasonFilter)}
            options={seasonOptions}
            placeholder={t('seriesEpisodes.filterAnySeason')}
            onChange={chooseSeason}
          />
          <Select
            allowClear
            aria-label={t('seriesEpisodes.columns.status')}
            style={{ width: 125 }}
            value={statusFilter ?? undefined}
            options={statusOptions}
            placeholder={t('seriesEpisodes.filterAnyStatus')}
            onChange={chooseStatus}
          />
          <Select
            allowClear
            aria-label={t('seriesEpisodes.columns.type')}
            style={{ width: 140 }}
            value={typeFilter ?? undefined}
            options={typeOptions}
            placeholder={t('seriesEpisodes.filterAnyType')}
            onChange={chooseType}
          />
          <ResetColumnWidths
            label={t('seriesEpisodes.resetWidths')}
            disabled={atDefaults}
            onClick={() => reset()}
          />
          {hinted(
            t('seriesEpisodes.addSeason'),
            <Button
              icon={<FolderAddOutlined />}
              aria-label={t('seriesEpisodes.addSeason')}
              disabled={!canEdit}
              onClick={() => navigate(`${area}/series/${seriesId}/seasons/add`)}
            />,
          )}
          <Button
            type="primary"
            icon={<PlusOutlined />}
            disabled={!canEdit}
            onClick={() =>
              navigate(
                `${area}/series/${seriesId}/episodes/add?season=${addEpisodeSeason}`,
              )
            }
          >
            {t('seriesEpisodes.addEpisode')}
          </Button>
        </Space>
      }
    >
      <ServerTable<Episode, EpisodeSortField>
        table={table}
        columns={columns}
        rows={episodes?.items ?? []}
        total={episodes?.total ?? 0}
        loading={loading}
        loadError={loadError}
        onRetry={() => episodesQuery.refetch()}
        tableWidth={tableWidth}
        emptyText={t(filtered ? 'seriesEpisodes.noMatches' : 'seriesEpisodes.empty')}
        showTotal={(total, [from, to]) => t('seriesEpisodes.range', { from, to, total })}
        selection={{ selectedRowKeys: selectedIds, onChange: setSelectedIds }}
      />

      <Modal
        title={t('seriesEpisodes.transcode.title', {
          name: transcodeTarget?.name ?? '',
        })}
        open={transcodeTarget !== null}
        footer={null}
        onCancel={() => setTranscodeTarget(null)}
      >
        {!transcodingAvailable && (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 16 }}
            message={t('seriesEpisodes.transcode.unavailable')}
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

      <Modal
        title={t('seriesEpisodes.bulk.typeTitle')}
        open={bulkTypeOpen}
        okText={t('seriesEpisodes.bulk.apply')}
        okButtonProps={{ loading: bulkBusy }}
        cancelText={t('actions.cancel')}
        onOk={() =>
          runBulk({ action: 'change-type', type: bulkType }, () => setBulkTypeOpen(false))
        }
        onCancel={() => setBulkTypeOpen(false)}
      >
        <Select
          aria-label={t('seriesEpisodes.columns.type')}
          style={{ width: '100%' }}
          value={bulkType}
          options={typeOptions}
          onChange={setBulkType}
        />
      </Modal>
    </Card>
  )
}
