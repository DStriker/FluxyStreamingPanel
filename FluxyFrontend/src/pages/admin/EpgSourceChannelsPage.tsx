import { useNavigate, useParams } from 'react-router-dom'
import { Button, Card, Space } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { ArrowLeftOutlined } from '@ant-design/icons'
import { EPG_SOURCE_PAGE_SIZE } from '../../features/epg/api'
import type { EpgSourceChannelSortField } from '../../features/epg/api'
import type { EpgSourceChannel } from '../../features/epg/types'
import { useEpgSourceChannels, useEpgSourceDetail } from '../../features/epg/hooks/queries'
import { ResetColumnWidths, ServerTable, TableSearch } from '../../components/table'
import type { ResizableHeaderCellProps } from '../../components/table'
import { useColumnWidths, useServerTable } from '../../hooks/table'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'
import { tableResults } from '../../utils/tableResults'
import { textCell } from '../../utils/tableCells'

/** The two columns, in the order they are drawn. */
type ChannelColumnKey = 'streamName' | 'channelKey'

const COLUMN_KEYS: readonly ChannelColumnKey[] = ['streamName', 'channelKey']

/**
 * The widths the two columns start at. There is no actions column here and nothing to
 * select: this table reads, and the widest share of the room goes to the two facts it
 * exists to state.
 */
const DEFAULT_COLUMN_WIDTHS: Record<ChannelColumnKey, number> = {
  streamName: 700,
  channelKey: 490,
}

/** Where this table remembers the widths the visitor gave its columns. */
const COLUMN_WIDTHS_KEY = 'fluxy.epgSourceChannels.columnWidths'

/** A channel row as the table wants it: `rowKey` is `id`, and the stream's id is the row's. */
type ChannelRow = EpgSourceChannel & { id: string }

interface EpgSourceChannelsPageProps {
  /** The menu key this route carries, accepted and deliberately unread. */
  sectionKey: string
}

/**
 * The streams that read their guide from one source - read-only, reached only from a row of
 * the sources table.
 *
 * The rows are derived rather than stored: a stream is a channel of a source when it names
 * that source and a key, so this is a projection of the streams table and not a second list
 * that could disagree with it. That is also why the page has no write of any kind - the
 * place to change which guide a stream reads is the stream's own form, and a second editor
 * here would be a second place to keep in step.
 *
 * The heading names the source, because a page reached from one row must say which row it
 * is about; the back button returns to the table it was opened from.
 */
export default function EpgSourceChannelsPage(_props: EpgSourceChannelsPageProps) {
  const { id } = useParams()
  const navigate = useNavigate()
  const { t } = useTranslation()
  const session = useSession()

  const table = useServerTable<ChannelRow, EpgSourceChannelSortField>({
    defaultSort: { by: 'streamName', order: 'asc' },
    sortFields: ['streamName', 'channelKey'],
    pageSize: EPG_SOURCE_PAGE_SIZE,
  })

  const sourceQuery = useEpgSourceDetail(id)
  const source = sourceQuery.data ?? null

  const channelsQuery = useEpgSourceChannels(id, table.query)
  const { data: channels, loadError, loading } = tableResults(channelsQuery)

  const { widths, tableWidth, atDefaults, reset, resizer: handleFor } =
    useColumnWidths<ChannelColumnKey>({
      keys: COLUMN_KEYS,
      defaults: DEFAULT_COLUMN_WIDTHS,
      storageKey: COLUMN_WIDTHS_KEY,
    })

  const area = session ? areaForRole(session.role) : ''
  const listPath = `${area}/epg-sources`

  const resizer = (key: ChannelColumnKey): ResizableHeaderCellProps =>
    handleFor(
      key,
      t('epgSourceChannels.resizeColumn', { column: t(`epgSourceChannels.columns.${key}`) }),
    )

  /**
   * `ServerTable` keys a row by `id`, and a channel row's identifier is the stream's. The
   * mapping happens here rather than in the contract, because the server names the field for
   * what it is (`streamId`) and only this table needs it to be a row key.
   */
  const rows: ChannelRow[] = (channels?.items ?? []).map((channel) => ({
    ...channel,
    id: channel.streamId,
  }))

  const columns: TableColumnsType<ChannelRow> = [
    {
      title: t('epgSourceChannels.columns.streamName'),
      dataIndex: 'streamName',
      key: 'streamName',
      width: widths.streamName,
      sorter: true,
      sortOrder: table.orderOf('streamName'),
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('streamName'),
      render: textCell,
    },
    {
      title: t('epgSourceChannels.columns.channelKey'),
      dataIndex: 'epgChannelKey',
      key: 'channelKey',
      width: widths.channelKey,
      sorter: true,
      sortOrder: table.orderOf('channelKey'),
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('channelKey'),
      render: textCell,
    },
  ]

  const filtered = Boolean(table.query.search)

  return (
    <Card
      className="page-card"
      title={
        source
          ? t('epgSourceChannels.title', { name: source.name })
          : t('epgSourceChannels.titlePlain')
      }
      extra={
        <Space wrap>
          <TableSearch
            value={table.searchText}
            onChange={table.setSearchText}
            onSearch={table.applySearch}
            labelKey="epgSourceChannels.searchLabel"
            placeholderKey="epgSourceChannels.searchPlaceholder"
            width={200}
          />
          <ResetColumnWidths
            label={t('epgSourceChannels.resetWidths')}
            disabled={atDefaults}
            onClick={() => reset()}
          />
          <Button icon={<ArrowLeftOutlined />} onClick={() => navigate(listPath)}>
            {t('epgSourceChannels.back')}
          </Button>
        </Space>
      }
    >
      <ServerTable<ChannelRow, EpgSourceChannelSortField>
        table={table}
        columns={columns}
        rows={rows}
        total={channels?.total ?? 0}
        loading={loading}
        loadError={loadError}
        onRetry={() => void channelsQuery.refetch()}
        tableWidth={tableWidth}
        emptyText={t(filtered ? 'epgSourceChannels.noMatches' : 'epgSourceChannels.empty')}
        showTotal={(total, [from, to]) => t('epgSourceChannels.range', { from, to, total })}
      />
    </Card>
  )
}
