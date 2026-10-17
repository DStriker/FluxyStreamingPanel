import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { Button, Card, Image, Select, Space, Tag } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import {
  CaretRightOutlined,
  DeleteOutlined,
  EditOutlined,
  EyeOutlined,
  PauseCircleOutlined,
  PlusOutlined,
  ReloadOutlined,
  StopOutlined,
} from '@ant-design/icons'
import { CONTENT_TYPE_PARAMS, VIDEO_STREAMS_PAGE_SIZE } from '../lib/api'
import type { VideoStreamSortField } from '../lib/api'
import type { VideoStream, VideoStreamStatus, VideoStreamType } from '../types'
import { hinted } from '../lib/hinted'
import { areaForRole } from '../lib/session'
import { useSession } from '../lib/sessionContext'
import { tableResults } from '../lib/tableResults'
import { dashedCell, idCell, textCell } from '../lib/tableCells'
import { enumParam, withFilter } from '../lib/tableParams'
import { RowActions, ServerTable, TableSearch } from '../components/table'
import type { ResizableHeaderCellProps, RowAction } from '../components/table'
import { useContentCategories } from '../features/content-categories/queries'
import {
  useDeleteVideoStream,
  useStartVideoStream,
  useStopVideoStream,
} from '../features/video-streams/mutations'
import { useVideoStreams } from '../features/video-streams/queries'
import { useColumnWidths, useRowAction, useServerTable } from '../hooks/table'

/** How often the table re-reads itself while the toggle below is on, in milliseconds. */
const AUTO_REFRESH_MS = 30_000

/** The eight columns, in the order they are drawn, named by their own keys. */
type StreamsColumnKey =
  | 'id'
  | 'icon'
  | 'name'
  | 'category'
  | 'activeSource'
  | 'status'
  | 'epg'
  | 'actions'

const COLUMN_KEYS: readonly StreamsColumnKey[] = [
  'id',
  'icon',
  'name',
  'category',
  'activeSource',
  'status',
  'epg',
  'actions',
]

/**
 * The width each column starts at, and the width it goes back to when asked to reset.
 *
 * Summed to **1230**, the same total the accounts and the groups tables add up to, and for
 * the same reason: all three sit under one sidebar, and a visitor who can see one whole
 * should be able to see the other. The two content columns are narrow *because* their cells
 * truncate into tooltips - the identifier prints eight characters with the full GUID on
 * hover, and the active source is whatever an address happens to be - while the name keeps
 * the room, because it is the one thing on this page nothing truncates a second time.
 */
const DEFAULT_COLUMN_WIDTHS: Record<StreamsColumnKey, number> = {
  id: 100,
  icon: 70,
  name: 260,
  category: 170,
  activeSource: 220,
  status: 110,
  epg: 90,
  actions: 210,
}

/** Where this table remembers the widths the visitor gave its columns. */
const COLUMN_WIDTHS_KEY = 'fluxy.videoStreams.columnWidths'

/** The two states and the six kinds, in the order the backend declares them. */
const STATUSES: VideoStreamStatus[] = ['Stopped', 'Running']
const TYPES: VideoStreamType[] = [
  'Permanent',
  'OnDemand',
  'Redirect',
  'OnlineSeries',
  'VodPlaylist',
  'FilePlaylist',
]

/**
 * Two descriptions, not a ramp.
 *
 * `VideoStreamStatus` is not an ordered scale - a stream is broadcasting or it is not, and
 * neither is "more" of anything than the other - so the colours say what the state *is*.
 */
const STATUS_COLOR: Record<VideoStreamStatus, string> = {
  Stopped: 'default',
  Running: 'success',
}

interface VideoStreamsPageProps {
  /**
   * The menu key this route carries, accepted and deliberately unread - the same as on
   * `UserGroupsPage` and for the same reason. The card is headed by its own
   * `videoStreams.listTitle`, because the menu item here is the verb `Manage`/`Управление`
   * and a card saying only "Manage" over a table would name neither the page nor its
   * subject. The two names are `Video streams` beside `Manage`, one naming what is looked
   * at and one naming how the visitor got there.
   */
  sectionKey: string
}

/**
 * The admin's streams table - the page behind `nav.items.videoStreamsManage`.
 *
 * Shaped after `UserGroupsPage` down to the same pieces, and deliberately so: two tables on
 * one page of one application having different rules for a resize handle, a pager or a sort
 * arrow would be two places for one of those rules to be quietly wrong.
 *
 * **Paging, search, filtering and sorting are all server side**, for the reason every other
 * table's are: `total`. A category filter applied in the browser would narrow the page on
 * screen while the pager still counted everything, so every change goes back to
 * `GET /admin/video-streams` and any change that is not a page turn resets to page 1. Three
 * of the eight headers are controlled - the server can order by three of the five sort
 * fields (`type` and `sortOrder` have no column to offer a header for), and the other five
 * are never fields the endpoint orders by, so a header that flipped its arrow over rows it
 * had not re-fetched would be describing a table that does not exist.
 *
 * It is also the only table here that re-reads **on a timer**: whether a stream is running
 * is a fact about a process, and a table that only learned it when somebody clicked would
 * be drawing a minute-old answer. The timer is on by own default with a visible pause
 * beside the search, because a page that silently re-reads under a visitor's cursor is a
 * page that moves the row they were about to press - and a pause they cannot find is not a
 * pause.
 *
 * Start and stop are real actions and are the only two the row performs; restart and play
 * are drawn **disabled with the reason attached**, the same "tell the truth before the
 * click" rule the base-group buttons follow: the relaying process behind them does not
 * exist yet, and a button that answered with a 404 would say less than a greyed one.
 */
export default function VideoStreamsPage(_props: VideoStreamsPageProps) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const session = useSession()

  /**
   * The five request parameters, the pager and the three sortable headers - all of that is
   * `useServerTable`, which the other server-paged tables call too. The mapping needs no
   * help here: the sortable columns are keyed by the server's own field names, so a click
   * hands its own key over as the field. `type` and `sortOrder` are fields the endpoint
   * honours but no header can offer, and the rest of the columns never produce an event at
   * all.
   */
  const table = useServerTable<VideoStream, VideoStreamSortField>({
    defaultSort: { by: 'name', order: 'asc' },
    // Every field the endpoint orders by, not only the three headers below it: `type` and
    // `sortOrder` have no sorter on this table, but an address may legitimately name them.
    sortFields: ['name', 'category', 'status', 'type', 'sortOrder'],
    // The one page that names its own size rather than taking the shared default:
    // `VIDEO_STREAMS_PAGE_SIZE` is what the endpoint documents, and the two are 20 only
    // because two files currently agree.
    pageSize: VIDEO_STREAMS_PAGE_SIZE,
  })

  /**
   * The three filters, in the address rather than in this component's state - the decision
   * every server-paged table makes about what it is showing: the address is the single
   * source, so it is shareable and a reload keeps the filter, with no effect anywhere
   * keeping two copies in step. The two enums are judged by the values this page's own
   * controls can produce, so a hand-edited word draws the unfiltered table rather than a
   * `validation_failed` from the endpoint; the category is an identifier, and the categories
   * endpoint is the only thing that knows which ones exist.
   */
  const [searchParams, setSearchParams] = useSearchParams()
  const categoryFilter = searchParams.get('categoryId') || null
  const statusFilter = enumParam(searchParams, 'status', STATUSES)
  const typeFilter = enumParam(searchParams, 'type', TYPES)
  /** Whether the table re-reads itself. On by default, and pausing is one press. */
  const [autoRefresh, setAutoRefresh] = useState(true)

  /**
   * The page of streams, fetched through a query whose key **is** the parameters - the same
   * shape the other tables use: the key changing *is* the new request, and every successful
   * action re-reads through `refetch()` rather than by bumping an `attempt` counter.
   */
  const streamsQuery = useVideoStreams({
    ...table.query,
    categoryId: categoryFilter,
    status: statusFilter,
    type: typeFilter,
  })
  const { data: streams, loadError, loading } = tableResults(streamsQuery)

  /**
   * The categories the filter and the add button choose from, in a query of its own rather
   * than riding along with the table's.
   *
   * The list itself needs `viewStreams` and the categories need `viewStreams` too (both are
   * behind the same policy), so one shared call would be no safer - what the separate query
   * buys is that a failure of one is a failure of one: a missing category list leaves the
   * filter offering nothing to pick, which is the truth, rather than taking the whole table
   * down with it.
   */
  const categoriesQuery = useContentCategories(CONTENT_TYPE_PARAMS.VideoStream)
  const categories = categoriesQuery.data?.items ?? []

  /** The eight widths: their state, their memory between visits, and the drag that changes them. */
  const { widths, tableWidth, atDefaults, reset, resizer: handleFor } =
    useColumnWidths<StreamsColumnKey>({
      keys: COLUMN_KEYS,
      defaults: DEFAULT_COLUMN_WIDTHS,
      storageKey: COLUMN_WIDTHS_KEY,
    })

  // The area this page hangs from, for the row edit button and the way to the add form.
  // `null` only under the probes, which render no provider: `RequireAuth` publishes one for
  // every route that reaches this page, so an empty string here means the guard was
  // bypassed rather than that nobody is signed in.
  const area = session ? areaForRole(session.role) : ''

  /**
   * The timer that keeps the table honest about a fact about a *process*.
   *
   * The ref rather than the query in the dependency array is the whole point: `refetch` is
   * a new function whenever the answer changes, so naming it here would restart the interval
   * after every single re-read - which is a timer that fires once and then never again
   * whenever the rows happen to change, i.e. always. The ref is written on every render and
   * read on every tick, so the interval is created exactly once per change of the toggle and
   * always calls the newest fetch. It is torn down on unmount by the same cleanup, so a
   * visitor who leaves the page stops the timer rather than leaving a request behind.
   */
  const refreshRef = useRef<() => void>(() => {})

  useEffect(() => {
    refreshRef.current = () => {
      void streamsQuery.refetch()
    }
  })

  useEffect(() => {
    if (!autoRefresh) return

    const timer = setInterval(() => refreshRef.current(), AUTO_REFRESH_MS)

    return () => clearInterval(timer)
  }, [autoRefresh])

  /**
   * The three filters, each one address and one navigation: `withFilter` writes the filter
   * and drops `page` in the same write - a different list is a different pager, and leaving
   * the visitor on page 3 of a filter that admits one page would show them an empty table
   * while `total` above it said 1.
   *
   * `replace` because a change to what is being looked at is not a step worth a history
   * entry, so the back button takes the visitor off the page instead of back through every
   * filter they tried.
   */
  const chooseCategory = (value: string | undefined) =>
    setSearchParams(withFilter(searchParams, 'categoryId', value), { replace: true })

  const chooseStatus = (value: VideoStreamStatus | undefined) =>
    setSearchParams(withFilter(searchParams, 'status', value), { replace: true })

  const chooseType = (value: VideoStreamType | undefined) =>
    setSearchParams(withFilter(searchParams, 'type', value), { replace: true })

  /**
   * One column's resize handle, with this page's name for the column.
   *
   * The hook owns the drag, the clamp and the stored widths, but only the page knows what a
   * column is called: the handle's accessible name is built here and handed over as an
   * argument, because a translation key is not something a generic hook can hold.
   */
  const resizer = (key: StreamsColumnKey): ResizableHeaderCellProps =>
    handleFor(key, t('videoStreams.resizeColumn', { column: t(`videoStreams.columns.${key}`) }))

  /**
   * One row action, and the id whose own buttons spin while it runs - the reporting and the
   * re-read are `useRowAction`'s, which the other administrator tables call too. The writes
   * behind the actions are `mutations.ts`'s, one hook per endpoint, so none of them mints a
   * token here.
   */
  const startStream = useStartVideoStream()
  const stopStream = useStopVideoStream()
  const removeStream = useDeleteVideoStream()

  const { busyId, runAction, confirmDelete: askDelete } = useRowAction<VideoStream>({
    refetch: () => void streamsQuery.refetch(),
    rows: streams?.items,
    page: table.page,
    setPage: table.setPage,
  })

  /**
   * Asks before deleting, because this is the only action here that cannot be undone: the
   * other two move a row between states, this removes the row and its sources and its files
   * with it. No password step and no captcha - the admin session plus a fresh antiforgery
   * token are the gate, the same as every other write on this page.
   *
   * The body quotes the stream's own name, because that is what the visitor is reading when
   * they press it. What happens to the *category* is the category's dialog to make: the
   * server refuses to delete one a stream still files under, and that is a question about
   * this row's category rather than about this row.
   *
   * The question itself, the re-read and the step back off an emptied page are
   * `useRowAction`'s; what is left here is the wording and the write.
   */
  const confirmDelete = (stream: VideoStream) =>
    askDelete(stream, {
      name: stream.name,
      titleKey: 'videoStreams.deleteConfirmTitle',
      bodyKey: 'videoStreams.deleteConfirmBody',
      okKey: 'videoStreams.deleteConfirmOk',
      run: () => removeStream.mutateAsync({ id: stream.id }),
    })

  const columns: TableColumnsType<VideoStream> = [
    {
      // Eight characters of the identifier and the rest on hover, for the reason the
      // accounts table does it: a full GUID is 36 characters of hex that nobody reads, and
      // what this column is for is telling two rows apart and picking one to edit.
      title: t('videoStreams.columns.id'),
      dataIndex: 'id',
      key: 'id',
      width: widths.id,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('id'),
      render: idCell,
    },
    {
      // The icon, drawn at a fixed size because an operator's logo can be any shape they
      // like and a column sized by its widest image is a column that changes width when a
      // logo is replaced. A stream with no address draws a dash: no icon is a fact about
      // the stream, not a failure to load one.
      title: t('videoStreams.columns.icon'),
      dataIndex: 'logoUrl',
      key: 'icon',
      width: widths.icon,
      align: 'center',
      onHeaderCell: () => resizer('icon'),
      render: (value: string | null) =>
        value ? (
          <Image
            src={value}
            alt=""
            width={28}
            height={28}
            preview={false}
            style={{ objectFit: 'contain' }}
          />
        ) : (
          <span>—</span>
        ),
    },
    {
      // The name is what the whole page is read by - it is what the delete dialog quotes
      // and what the edit form is identified by - so it gets the room and the
      // truncation-with-tooltip rather than a cut with no way to see the rest.
      title: t('videoStreams.columns.name'),
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
      // The category's display name rather than its identifier, for the same reason the
      // server sorts on the name: an order or a column over an identifier nobody sees would
      // order an invisible value beside a visible one.
      title: t('videoStreams.columns.category'),
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
      // The one column whose value nothing in the interface writes. It is what the relaying
      // process reported back, so a dash is the honest answer for a stream that is stopped
      // or one whose process has not said which source it took - the same `—` the visit
      // history draws for an address its GeoIP files cannot name.
      //
      // Never sortable, and `VideoStreamSortField` records why: the column is null for every
      // stream that is not relaying, and ordering by it would lead with the streams nobody
      // is watching.
      title: t('videoStreams.columns.activeSource'),
      dataIndex: 'activeSource',
      key: 'activeSource',
      width: widths.activeSource,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('activeSource'),
      render: dashedCell,
    },
    {
      // A tag rather than coloured text, for the reason the other tables use one: two states
      // read as two labels next to each other down a column, which is what a scanning
      // administrator is looking for - and the colour is a description, not a rank
      // (`VideoStreamStatus` is not an ordered scale).
      title: t('videoStreams.columns.status'),
      dataIndex: 'status',
      key: 'status',
      width: widths.status,
      sorter: true,
      sortOrder: table.orderOf('status'),
      onHeaderCell: () => resizer('status'),
      render: (value: VideoStreamStatus) => (
        <Tag color={STATUS_COLOR[value]} style={{ marginInlineEnd: 0 }}>
          {t(`videoStreams.statuses.${value}`)}
        </Tag>
      ),
    },
    {
      // A yes-or-no about two fields, which the server has already answered as `hasEpg` so
      // this cell never has to know the answer has two halves - a half-configured pair is
      // refused on the way in, so a row here is always either fully guided or not guided at
      // all. Not sortable: it is derived rather than stored.
      title: t('videoStreams.columns.epg'),
      dataIndex: 'hasEpg',
      key: 'epg',
      width: widths.epg,
      align: 'center',
      onHeaderCell: () => resizer('epg'),
      render: (value: boolean) =>
        value ? (
          <Tag color="processing" style={{ marginInlineEnd: 0 }}>
            {t('videoStreams.epgOn')}
          </Tag>
        ) : (
          <Tag style={{ marginInlineEnd: 0 }}>{t('videoStreams.epgOff')}</Tag>
        ),
    },
    {
      // The five actions as icons with tooltips - icons carry no text of their own, so the
      // name each one gets in the locale files is the only thing that says what pressing it
      // does.
      //
      // Start and stop are the only two that do anything: they are the transition the row
      // exists to make, and starting a stream that is already running is a success rather
      // than a complaint, so a double click is not a failure. Restart and play are drawn
      // **disabled with the reason still attached** - the relaying process behind them does
      // not exist yet, and a greyed button with a sentence says that before the click,
      // where a live button answering 404 would only say something failed after it.
      title: t('videoStreams.columns.actions'),
      key: 'actions',
      width: widths.actions,
      onHeaderCell: () => resizer('actions'),
      render: (_, stream) => {
        const busy = busyId === stream.id

        const actions: RowAction[] = [
          stream.status === 'Running'
            ? {
                key: 'stop',
                label: t('videoStreams.actions.stop'),
                icon: <StopOutlined />,
                loading: busy,
                disabled: busy,
                onClick: () => runAction(() => stopStream.mutateAsync({ id: stream.id }), stream),
              }
            : {
                key: 'start',
                label: t('videoStreams.actions.start'),
                icon: <CaretRightOutlined />,
                loading: busy,
                disabled: busy,
                onClick: () => runAction(() => startStream.mutateAsync({ id: stream.id }), stream),
              },
          // The relaying process behind these two does not exist yet: a greyed button with a
          // sentence says that before the click, where a live button answering 404 would only
          // say something failed after it.
          {
            key: 'restart',
            label: t('videoStreams.actions.notImplemented'),
            icon: <ReloadOutlined />,
            disabled: true,
          },
          {
            key: 'play',
            label: t('videoStreams.actions.notImplemented'),
            icon: <EyeOutlined />,
            disabled: true,
          },
          {
            key: 'edit',
            label: t('videoStreams.actions.edit'),
            icon: <EditOutlined />,
            onClick: () => navigate(`${area}/video-streams/${stream.id}`),
          },
          {
            key: 'delete',
            label: t('videoStreams.actions.delete'),
            icon: <DeleteOutlined />,
            danger: true,
            disabled: busy,
            onClick: () => confirmDelete(stream),
          },
        ]

        return <RowActions actions={actions} />
      },
    },
  ]

  const categoryOptions = useMemo(
    () =>
      categories.map((category) => ({
        value: category.id,
        label: category.name,
      })),
    [categories],
  )

  const statusOptions = useMemo(
    () => STATUSES.map((status) => ({ value: status, label: t(`videoStreams.statuses.${status}`) })),
    [t],
  )

  const typeOptions = useMemo(
    () => TYPES.map((type) => ({ value: type, label: t(`videoStreams.types.${type}`) })),
    [t],
  )

  /** Whether anything at all is being filtered - what the empty state says the table is. */
  const filtered = Boolean(
    table.query.search || categoryFilter || statusFilter || typeFilter,
  )

  /**
   * What the auto-refresh button says while it is greyed *or* live.
   *
   * It is a tooltip rather than a label, and the button is icon-only: the row beside the
   * card title is already the widest toolbar in the application (search, three filters,
   * reset, add) and a Russian `Продолжить автообновление` in it pushed the table itself
   * sideways for a control that is one glyph wide. What the state *is* is still one hover
   * away - the reason a control is drawn as an icon rather than as a word is never that
   * the word stopped mattering, only that it does not have to sit in the row.
   */
  const refreshTip = `${t(autoRefresh ? 'videoStreams.pauseRefresh' : 'videoStreams.resumeRefresh')} (${t('videoStreams.autoRefreshHint', { seconds: AUTO_REFRESH_MS / 1000 })})`

  return (
    <Card
      title={t('videoStreams.listTitle')}
      // The title pinned to one line, for the reason the other two tables pin theirs: this
      // row shares the card header with a toolbar that is now the widest of the three.
      styles={{ title: { whiteSpace: 'nowrap' } }}
      extra={
        <Space wrap>
          {/* Icon-only, with its whole sentence in the tooltip: see `refreshTip` for why
              the label itself left the row. Drawn either way rather than appearing once it
              is paused, for the reason the reset button below is drawn while disabled - a
              control that comes and goes reads as a broken one. */}
          {hinted(
            refreshTip,
            <Button
              icon={autoRefresh ? <PauseCircleOutlined /> : <ReloadOutlined />}
              aria-label={refreshTip}
              onClick={() => setAutoRefresh((current) => !current)}
            />,
          )}
          {/* Narrower than they might be, for the reason the other two tables' three are:
              this row has to fit beside the card's title on one Full HD screen. What each
              loses in room it keeps in its aria-label, which still names the whole field. */}
          <TableSearch
            value={table.searchText}
            onChange={table.setSearchText}
            onSearch={table.applySearch}
            labelKey="videoStreams.searchLabel"
            placeholderKey="videoStreams.searchPlaceholder"
            width={180}
          />
          <Select
            allowClear
            aria-label={t('videoStreams.columns.category')}
            style={{ width: 160 }}
            value={categoryFilter ?? undefined}
            options={categoryOptions}
            placeholder={t('videoStreams.filterAnyCategory')}
            onChange={chooseCategory}
          />
          <Select
            allowClear
            aria-label={t('videoStreams.columns.status')}
            style={{ width: 125 }}
            value={statusFilter ?? undefined}
            options={statusOptions}
            placeholder={t('videoStreams.filterAnyStatus')}
            onChange={chooseStatus}
          />
          <Select
            allowClear
            aria-label={t('videoStreams.columns.type')}
            style={{ width: 170 }}
            value={typeFilter ?? undefined}
            options={typeOptions}
            placeholder={t('videoStreams.filterAnyType')}
            onChange={chooseType}
          />
          {/* Disabled rather than hidden when the widths are already the defaults: a control
              that appears and disappears reads as a broken one, and there is nothing to undo
              until somebody has actually dragged something. */}
          <Button disabled={atDefaults} onClick={() => reset()}>
            {t('videoStreams.resetWidths')}
          </Button>
          {/* The same address the "add" route opens, offered here because this is the screen
              from which adding one is a decision. Its label comes from the form's own title
              rather than from `nav.items.videoStreamsManage`, which is the *list's* menu
              item and reads only "Manage". */}
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => navigate(`${area}/video-streams/add`)}
          >
            {t('videoStreams.addTitle')}
          </Button>
        </Space>
      }
    >
      <ServerTable<VideoStream, VideoStreamSortField>
        table={table}
        columns={columns}
        rows={streams?.items ?? []}
        total={streams?.total ?? 0}
        loading={loading}
        loadError={loadError}
        onRetry={() => streamsQuery.refetch()}
        tableWidth={tableWidth}
        emptyText={t(filtered ? 'videoStreams.noMatches' : 'videoStreams.empty')}
        showTotal={(total, [from, to]) => t('videoStreams.range', { from, to, total })}
      />
    </Card>
  )
}
