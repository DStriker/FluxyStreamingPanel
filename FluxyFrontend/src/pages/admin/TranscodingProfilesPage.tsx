import type { ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button, Card, Space, Tooltip, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons'
import { TRANSCODING_PROFILES_PAGE_SIZE } from '../../features/transcoding-profiles/api'
import type { TranscodingProfileSortField } from '../../features/transcoding-profiles/api'
import {
  AUDIO_CODECS,
  PRESETS,
  VIDEO_CODECS,
  VIDEO_PROFILES,
} from '../../features/transcoding-profiles/ffmpegOptions'
import type { FfmpegOption } from '../../features/transcoding-profiles/ffmpegOptions'
import type { TranscodingProfile } from '../../features/transcoding-profiles/types'
import { useDeleteTranscodingProfile } from '../../features/transcoding-profiles/hooks/mutations'
import { useTranscodingProfiles } from '../../features/transcoding-profiles/hooks/queries'
import { useProfile } from '../../features/profile/hooks/queries'
import { ResetColumnWidths, RowActions, ServerTable, TableSearch } from '../../components/table'
import type { ResizableHeaderCellProps, RowAction } from '../../components/table'
import { useColumnWidths, useRowAction, useServerTable } from '../../hooks/table'
import { hinted } from '../../utils/hinted'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'
import { tableResults } from '../../utils/tableResults'
import { idCell } from '../../utils/tableCells'
import { actionsColumnWidth } from '../../utils/tableWidths'

/** The four columns, in the order they are drawn. */
type ProfileColumnKey = 'id' | 'name' | 'options' | 'actions'

const COLUMN_KEYS: readonly ProfileColumnKey[] = ['id', 'name', 'options', 'actions']

/**
 * The width each column starts at. The three fixed ones plus the actions column leave the name
 * and the options the rest of the **1190** total every table here reaches; the name is what an
 * operator scans the list by, and the options cell is a wrapping set of short `label: value`
 * pairs that needs the room.
 */
const DEFAULT_COLUMN_WIDTHS: Record<ProfileColumnKey, number> = {
  id: 110,
  name: 260,
  options: 720,
  // Exactly the two buttons plus the cell's padding, and the extra sixteen the tariff plans
  // table also carries: two icon buttons fit the exact width, and the room is what keeps the
  // pair off the row's edge - and keeps the word "Действия" on one line - when a longer
  // locale is in play.
  actions: actionsColumnWidth(2) + 16,
}

/** Where this table remembers the widths the visitor gave its columns. */
const COLUMN_WIDTHS_KEY = 'fluxy.transcodingProfiles.columnWidths'

/**
 * One option as the cell draws it: the label, a colon and the value.
 *
 * A value the row does not carry is not drawn at all - no dash, no empty tag - because "the
 * profile sends no flag for this" is the ordinary state of most options and a screen full of
 * dashes would bury the two or three the profile does set.
 */
const option = (label: string, value: string): ReactNode => (
  <Typography.Text type="secondary" key={label}>
    {label}: {value}
  </Typography.Text>
)

/** A URL or another long value: truncated inside the cell, the whole value on hover. */
const longOption = (label: string, value: string): ReactNode => (
  <Typography.Text type="secondary" key={label}>
    {label}:{' '}
    <Tooltip title={value}>
      <span
        style={{
          display: 'inline-block',
          maxWidth: 160,
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          whiteSpace: 'nowrap',
          verticalAlign: 'bottom',
        }}
      >
        {value}
      </span>
    </Tooltip>
  </Typography.Text>
)

/**
 * The label of one option, from the ffmpeg list, in the page's language.
 *
 * The lists hold locale keys rather than literals, so the label is one lookup; a value the list
 * does not know is shown as it is, which is the only honest answer for a row written before the
 * list grew.
 */
const labelOf = (
  options: readonly FfmpegOption[],
  value: string,
  t: (key: string) => string,
): string => {
  const found = options.find((entry) => entry.value === value)

  return found ? t(found.labelKey) : value
}

interface TranscodingProfilesPageProps {
  /** The menu key this route carries, accepted and deliberately unread. */
  sectionKey: string
}

/**
 * The admin's transcoding profiles - the named sets of ffmpeg options a stream, a video or a
 * series is converted with: the page behind `nav.items.transcodingProfilesManage`.
 *
 * Paging, search and sorting are server side, for the reason every other table's are: `total`.
 * The only sortable column is the name, because every other field is an optional ffmpeg option
 * whose order a reader does not compare down a table.
 *
 * The row's two actions are edit and delete. Delete asks first because it is the one
 * irreversible move, and it is refused with `transcoding_profile_in_use` while anything still
 * points at the profile.
 */
export default function TranscodingProfilesPage(_props: TranscodingProfilesPageProps) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const session = useSession()

  const table = useServerTable<TranscodingProfile, TranscodingProfileSortField>({
    defaultSort: { by: 'name', order: 'asc' },
    sortFields: ['name'],
    pageSize: TRANSCODING_PROFILES_PAGE_SIZE,
  })

  const profilesQuery = useTranscodingProfiles(table.query)
  const { data: profiles, loadError, loading } = tableResults(profilesQuery)

  const profileQuery = useProfile()
  const granted = profileQuery.data?.permissions ?? []
  const canEdit = granted.includes('editTranscodingProfiles')

  const { widths, tableWidth, atDefaults, reset, resizer: handleFor } =
    useColumnWidths<ProfileColumnKey>({
      keys: COLUMN_KEYS,
      defaults: DEFAULT_COLUMN_WIDTHS,
      storageKey: COLUMN_WIDTHS_KEY,
    })

  const area = session ? areaForRole(session.role) : ''

  const removeProfile = useDeleteTranscodingProfile()

  const { busyId, confirmDelete: askDelete } = useRowAction<TranscodingProfile>({
    refetch: () => void profilesQuery.refetch(),
    rows: profiles?.items,
    page: table.page,
    setPage: table.setPage,
  })

  const resizer = (key: ProfileColumnKey): ResizableHeaderCellProps =>
    handleFor(
      key,
      t('transcodingProfiles.resizeColumn', { column: t(`transcodingProfiles.columns.${key}`) }),
    )

  const confirmDelete = (profile: TranscodingProfile) =>
    askDelete(profile, {
      name: profile.name,
      titleKey: 'transcodingProfiles.deleteConfirmTitle',
      bodyKey: 'transcodingProfiles.deleteConfirmBody',
      okKey: 'transcodingProfiles.deleteConfirmOk',
      run: () => removeProfile.mutateAsync({ id: profile.id }),
    })

  /**
   * The options cell: every option the profile names, as `label: value` pairs that wrap.
   *
   * Only the options a row carries are drawn, in a fixed reading order - the one ffmpeg's own
   * command line would use. No tags: a profile is a set of values, and a screen of coloured
   * chips would read as a set of categories.
   */
  const optionsCell = (profile: TranscodingProfile): ReactNode => {
    const parts: ReactNode[] = [
      option(t('transcodingProfiles.options.videoCodec'), labelOf(VIDEO_CODECS, profile.videoCodec, t)),
      option(t('transcodingProfiles.options.audioCodec'), labelOf(AUDIO_CODECS, profile.audioCodec, t)),
      option(t('transcodingProfiles.options.preset'), labelOf(PRESETS, profile.preset, t)),
    ]

    // The three required options are already in `parts`; the rest are appended only when set.
    if (profile.videoProfile !== null) {
      parts.push(
        option(
          t('transcodingProfiles.options.videoProfile'),
          labelOf(VIDEO_PROFILES, profile.videoProfile, t),
        ),
      )
    }

    const count = (key: string, value: number | null, unit?: string) => {
      if (value !== null) {
        parts.push(
          option(
            t(`transcodingProfiles.options.${key}`),
            unit ? `${value} ${unit}` : String(value),
          ),
        )
      }
    }

    count('videoBitrateAvg', profile.videoBitrateAvg, t('transcodingProfiles.units.kbps'))
    count('audioBitrateAvg', profile.audioBitrateAvg, t('transcodingProfiles.units.kbps'))
    count('minBitrateTolerance', profile.minBitrateTolerance, t('transcodingProfiles.units.kbps'))
    count('maxBitrateTolerance', profile.maxBitrateTolerance, t('transcodingProfiles.units.kbps'))
    count('bufferSize', profile.bufferSize, t('transcodingProfiles.units.kbps'))
    count('crf', profile.crf)
    count('width', profile.width, t('transcodingProfiles.units.px'))
    count('height', profile.height, t('transcodingProfiles.units.px'))

    if (profile.aspectRatio !== null) {
      parts.push(option(t('transcodingProfiles.options.aspectRatio'), profile.aspectRatio))
    }

    count('framerate', profile.framerate, t('transcodingProfiles.units.fps'))
    count('audioSampleRate', profile.audioSampleRate, t('transcodingProfiles.units.hz'))
    count('audioChannels', profile.audioChannels)
    count('threads', profile.threads)

    if (profile.logoUrl !== null) {
      parts.push(longOption(t('transcodingProfiles.options.logoUrl'), profile.logoUrl))
    }

    if (
      profile.redactionX !== null &&
      profile.redactionY !== null &&
      profile.redactionWidth !== null &&
      profile.redactionHeight !== null
    ) {
      parts.push(
        option(
          t('transcodingProfiles.options.redaction'),
          `${profile.redactionX}, ${profile.redactionY}, ${profile.redactionWidth}, ${profile.redactionHeight}`,
        ),
      )
    }

    return (
      <Space size={[16, 4]} wrap>
        {parts}
      </Space>
    )
  }

  const columns: TableColumnsType<TranscodingProfile> = [
    {
      title: t('transcodingProfiles.columns.id'),
      dataIndex: 'id',
      key: 'id',
      width: widths.id,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('id'),
      render: idCell,
    },
    {
      title: t('transcodingProfiles.columns.name'),
      dataIndex: 'name',
      key: 'name',
      width: widths.name,
      sorter: true,
      sortOrder: table.orderOf('name'),
      onHeaderCell: () => resizer('name'),
      ellipsis: { showTitle: false },
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
      title: t('transcodingProfiles.columns.options'),
      key: 'options',
      width: widths.options,
      onHeaderCell: () => resizer('options'),
      render: (_, profile) => optionsCell(profile),
    },
    {
      title: t('transcodingProfiles.columns.actions'),
      key: 'actions',
      width: widths.actions,
      onHeaderCell: () => resizer('actions'),
      render: (_, profile) => {
        const busy = busyId === profile.id

        const actions: RowAction[] = [
          {
            key: 'edit',
            label: canEdit ? t('transcodingProfiles.actions.edit') : t('transcodingProfiles.noPermission'),
            icon: <EditOutlined />,
            disabled: !canEdit,
            onClick: () => navigate(`${area}/transcoding-profiles/${profile.id}`),
          },
          {
            key: 'delete',
            label: canEdit ? t('transcodingProfiles.actions.delete') : t('transcodingProfiles.noPermission'),
            icon: <DeleteOutlined />,
            danger: true,
            disabled: !canEdit || busy,
            onClick: () => confirmDelete(profile),
          },
        ]

        return <RowActions actions={actions} />
      },
    },
  ]

  const filtered = Boolean(table.query.search)

  /**
   * The create button, and the refusal tooltip that belongs to it - only while it is greyed.
   *
   * `hinted` exists to keep a *disabled* control's reason reachable, so wrapping an enabled
   * button in it puts the refusal in front of the one visitor who is allowed to act: hover
   * over "Добавить профиль" read "your group does not allow this" on a group that holds
   * `editTranscodingProfiles`. The wrapper is therefore applied to the refused state alone -
   * the shape the bouquets and blacklist tables state as `hintedButton`.
   */
  const addButton = (
    <Button
      type="primary"
      icon={<PlusOutlined />}
      disabled={!canEdit}
      onClick={() => navigate(`${area}/transcoding-profiles/add`)}
    >
      {t('transcodingProfiles.addTitle')}
    </Button>
  )

  return (
    <Card
      className="page-card"
      title={t('transcodingProfiles.listTitle')}
      extra={
        <Space wrap>
          <TableSearch
            value={table.searchText}
            onChange={table.setSearchText}
            onSearch={table.applySearch}
            labelKey="transcodingProfiles.searchLabel"
            placeholderKey="transcodingProfiles.searchPlaceholder"
            width={220}
          />
          <ResetColumnWidths
            label={t('transcodingProfiles.resetWidths')}
            disabled={atDefaults}
            onClick={() => reset()}
          />
          {canEdit ? addButton : hinted(t('transcodingProfiles.noPermission'), addButton)}
        </Space>
      }
    >
      <ServerTable<TranscodingProfile, TranscodingProfileSortField>
        table={table}
        columns={columns}
        rows={profiles?.items ?? []}
        total={profiles?.total ?? 0}
        loading={loading}
        loadError={loadError}
        onRetry={() => void profilesQuery.refetch()}
        tableWidth={tableWidth}
        emptyText={t(filtered ? 'transcodingProfiles.noMatches' : 'transcodingProfiles.empty')}
        showTotal={(total, [from, to]) => t('transcodingProfiles.range', { from, to, total })}
      />
    </Card>
  )
}
