import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { App, Button, Card, Dropdown, Image, Input, InputNumber, Modal, Select, Space, Tag } from 'antd'
import type { MenuProps, TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import {
  CaretRightOutlined,
  ClockCircleOutlined,
  CloudSyncOutlined,
  DatabaseOutlined,
  DeleteOutlined,
  EditOutlined,
  EyeOutlined,
  MoreOutlined,
  PauseCircleOutlined,
  PlusOutlined,
  ReloadOutlined,
  StopOutlined,
  SwapOutlined,
  TagOutlined,
} from '@ant-design/icons'
import { CONTENT_TYPE_PARAMS } from '../../features/content-categories/api'
import { VIDEO_STREAMS_PAGE_SIZE } from '../../features/video-streams/api'
import type {
  BulkVideoStreamAction,
  BulkVideoStreamsPayload,
  BulkVideoStreamType,
  VideoStreamSortField,
} from '../../features/video-streams/api'
import StreamStateCell from '../../features/video-streams/components/StreamStateCell'
import type { VideoStream, VideoStreamStatus, VideoStreamType } from '../../features/video-streams/types'
import { hinted } from '../../utils/hinted'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'
import { tableResults } from '../../utils/tableResults'
import { dashedCell, idCell, textCell } from '../../utils/tableCells'
import { enumParam, withFilter } from '../../utils/tableParams'
import { actionsColumnWidth } from '../../utils/tableWidths'
import { ResetColumnWidths, RowActions, ServerTable, TableSearch } from '../../components/table'
import type { ResizableHeaderCellProps, RowAction } from '../../components/table'
import { useContentCategories } from '../../features/content-categories/hooks/queries'
import { useProfile } from '../../features/profile/hooks/queries'
import {
  useBulkVideoStreams,
  useDeleteVideoStream,
  useRestartVideoStream,
  useStartVideoStream,
  useStopVideoStream,
} from '../../features/video-streams/hooks/mutations'
import { useVideoStreams } from '../../features/video-streams/hooks/queries'
import { useBulkAction, useColumnWidths, useRowAction, useServerTable } from '../../hooks/table'

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
 * Summed to **1190**, and the selection column `ServerTable` adds in front of them is the
 * 40 that makes **1230** - the same total the accounts and the groups tables reach with
 * their checkbox, and for the same reason: all three sit under one sidebar, and a visitor
 * who can see one whole should be able to see the other. The two content columns are narrow
 * *because* their cells truncate into tooltips - the identifier prints eight characters with
 * the full GUID on hover, and the active source is whatever an address happens to be - while
 * the name keeps the room, because it is the one thing on this page nothing truncates a
 * second time.
 *
 * The state column is 150 because it carries two lines: the status, and the kind of stream
 * under it. The longest of the six kinds in Russian - "Перенаправление" - has to fit
 * without being cut, and the room comes out of the active source, which is an address that
 * truncates into a tooltip anyway.
 *
 * The actions column is not one of these numbers: it is exactly as wide as the five buttons
 * a row draws (`actionsColumnWidth`), and the room that frees goes to the name - the one
 * thing on this page that nothing truncates a second time. The eight plus the selection
 * column still add up to 1230.
 */
const FIXED_COLUMN_WIDTHS = {
  id: 100,
  icon: 70,
  category: 170,
  activeSource: 180,
  status: 150,
  epg: 90,
}

/** The width the five buttons of a row's cluster need, and nothing more. */
const ACTIONS_WIDTH = actionsColumnWidth(5)

/** The width the eight columns add up to; the selection column takes the total to 1230. */
const TABLE_WIDTH = 1190

const DEFAULT_COLUMN_WIDTHS: Record<StreamsColumnKey, number> = {
  ...FIXED_COLUMN_WIDTHS,
  // The name takes the room the actions column does not need, so the total is the one this
  // table has always had.
  name:
    TABLE_WIDTH -
    Object.values(FIXED_COLUMN_WIDTHS).reduce((sum, width) => sum + width, 0) -
    ACTIONS_WIDTH,
  actions: ACTIONS_WIDTH,
}

/** Where this table remembers the widths the visitor gave its columns. */
const COLUMN_WIDTHS_KEY = 'fluxy.videoStreams.columnWidths'

/** The five bulk edits that ask for a value; the transitions and deletion run from the menu. */
type BulkDialog = 'move' | 'type' | 'delay' | 'archive' | 'restream'

/** The four restream settings as the bulk dialog collects them; a blank string clears one. */
interface BulkRestreamValues {
  userAgent: string
  proxy: string
  cookies: string
  headers: string
}

/** The dialog's starting point, and the state every close returns to. */
const EMPTY_RESTREAM: BulkRestreamValues = {
  userAgent: '',
  proxy: '',
  cookies: '',
  headers: '',
}

/**
 * The three kinds a bulk type change may choose.
 *
 * The other three need a payload only the single-row form can collect - a series identifier,
 * a video-on-demand identifier or a list of files - so offering them here would be a menu
 * whose every row is refused.
 */
const BULK_TYPES: BulkVideoStreamType[] = ['Permanent', 'OnDemand', 'Redirect']

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

interface VideoStreamsPageProps {
  /**
   * The menu key this route carries, accepted and deliberately unread - the same as on
   * `UserGroupsPage` and for the same reason. The card is headed by its own
   * `videoStreams.listTitle`, because the menu item here names the action
   * (`Manage video streams`/`Управление`) and a card repeating it would name neither the
   * page nor its subject. The two names are `Video streams` beside `Manage video streams`,
   * one naming what is looked at and one naming how the visitor got there.
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
 * Start, stop and restart are real actions and are the transitions the row performs; play is
 * drawn **disabled with the reason attached**, the same "tell the truth before the click"
 * rule the base-group buttons follow: the relaying process behind it does not exist yet, and
 * a button that answered with a 404 would say less than a greyed one.
 *
 * **A selection makes the same operations available over many rows**, behind the
 * `editStreams` grant the list itself does not need: the toolbar's menu carries the three
 * transitions, deletion, and the four edits whose value fits one dialog (category, type,
 * delay, archive, restream). The per-row half stays exactly as it was - a bulk toolbar is a
 * second way to reach the same writes, not a replacement for the buttons somebody already
 * knows.
 */
export default function VideoStreamsPage(_props: VideoStreamsPageProps) {
  const { t } = useTranslation()
  const { modal } = App.useApp()
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
   * Whether this operator may change streams at all, read from the account's grants.
   *
   * The list itself needs `viewStreams`, so an account that can see this page may not
   * necessarily edit anything on it. The bulk toolbar is the one control whose enablement is
   * asked here rather than left to the server: it is a second way to reach writes the row
   * buttons already gate on the same grant, and a live toolbar over rows whose own buttons
   * do nothing would be a menu of refusals.
   */
  const profileQuery = useProfile()
  const granted = profileQuery.data?.permissions ?? []
  const canEdit = granted.includes('editStreams')

  /**
   * The selection and the run over it: the ids, the scope, the prune, the busy flag and the
   * summary dialog are `useBulkAction`'s, which the accounts and groups tables call too.
   * Warnings are reported by the same hook - a run where every row landed but one lost a
   * setting still has something to say - and the dialog's lines are `bulkResultList`'s.
   */
  const { selectedIds, setSelectedIds, bulkBusy, applyBulk } = useBulkAction({
    rows: streams?.items,
    refetch: () => void streamsQuery.refetch(),
    partialTitleKey: 'videoStreams.bulk.partialTitle',
  })

  /**
   * Which edit dialog is open, and the values the five of them collect. One discriminator
   * rather than five booleans: only one dialog can be open, and five flags would be five
   * places to forget a reset. The values are cleared by `closeBulkDialog`, so opening a
   * second dialog after closing the first never inherits what the first one held.
   */
  const [bulkDialog, setBulkDialog] = useState<BulkDialog | null>(null)
  const [bulkCategoryId, setBulkCategoryId] = useState<string | null>(null)
  const [bulkType, setBulkType] = useState<BulkVideoStreamType | null>(null)
  const [bulkDelay, setBulkDelay] = useState(0)
  const [bulkArchive, setBulkArchive] = useState(0)
  const [bulkRestream, setBulkRestream] = useState<BulkRestreamValues>(EMPTY_RESTREAM)

  const closeBulkDialog = () => {
    setBulkDialog(null)
    setBulkCategoryId(null)
    setBulkType(null)
    setBulkDelay(0)
    setBulkArchive(0)
    setBulkRestream(EMPTY_RESTREAM)
  }

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
  const restartStream = useRestartVideoStream()
  const removeStream = useDeleteVideoStream()
  const bulk = useBulkVideoStreams()

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

  /**
   * Runs one operation over the selection: the ids, the reporting, the re-read and the
   * clearance are `useBulkAction`'s, and the request itself is `useBulkVideoStreams`'s.
   *
   * `onSettled` closes a dialog the run came out of. A dialog closes on any answer, because
   * once the answer has arrived there is nothing left to ask - the summary dialog that the
   * hook may open on top of it is a *report*, not a second question.
   */
  const runBulk = (
    payload: Omit<BulkVideoStreamsPayload, 'ids'>,
    onSettled?: () => void,
  ) =>
    applyBulk(
      () => bulk.mutateAsync({ payload: { ...payload, ids: selectedIds.map(String) } }),
      onSettled,
    )

  /**
   * Asks before deleting a selection, for the reason only deletion asks first on a single
   * row: it is the one operation here that cannot be undone, and the question is about the
   * whole selection at once - so the body names a count rather than a name, because there is
   * no one row to name.
   */
  const confirmBulkDelete = () => {
    modal.confirm({
      title: t('videoStreams.bulk.deleteTitle', { count: selectedIds.length }),
      content: t('videoStreams.bulk.deleteBody'),
      okText: t('videoStreams.deleteConfirmOk'),
      okButtonProps: { danger: true },
      cancelText: t('actions.cancel'),
      onOk: () => runBulk({ action: 'delete' }),
    })
  }

  /**
   * What the toolbar's menu offers. The transitions and deletion run straight from the menu;
   * the five edits open the dialog that collects their one value. The labels are the row
   * actions' own where the operation is the same one, because a second name for one thing is
   * a second name.
   */
  const onBulkMenu: MenuProps['onClick'] = ({ key }) => {
    switch (key as BulkVideoStreamAction) {
      case 'start':
      case 'stop':
      case 'restart':
        void runBulk({ action: key as BulkVideoStreamAction })
        break
      case 'delete':
        confirmBulkDelete()
        break
      case 'move-category':
        setBulkDialog('move')
        break
      case 'change-type':
        setBulkDialog('type')
        break
      case 'change-delay':
        setBulkDialog('delay')
        break
      case 'change-archive':
        setBulkDialog('archive')
        break
      case 'change-restream':
        setBulkDialog('restream')
        break
    }
  }

  /** The nine entries, in the order the menu draws them. */
  const bulkItems = useMemo<MenuProps['items']>(
    () => [
      { key: 'start', icon: <CaretRightOutlined />, label: t('videoStreams.bulk.start') },
      { key: 'stop', icon: <StopOutlined />, label: t('videoStreams.bulk.stop') },
      { key: 'restart', icon: <ReloadOutlined />, label: t('videoStreams.bulk.restart') },
      { type: 'divider' },
      { key: 'move-category', icon: <SwapOutlined />, label: t('videoStreams.bulk.move') },
      { key: 'change-type', icon: <TagOutlined />, label: t('videoStreams.bulk.type') },
      { key: 'change-delay', icon: <ClockCircleOutlined />, label: t('videoStreams.bulk.delay') },
      { key: 'change-archive', icon: <DatabaseOutlined />, label: t('videoStreams.bulk.archive') },
      {
        key: 'change-restream',
        icon: <CloudSyncOutlined />,
        label: t('videoStreams.bulk.restream'),
      },
      { type: 'divider' },
      {
        key: 'delete',
        icon: <DeleteOutlined />,
        danger: true,
        label: t('videoStreams.bulk.delete'),
      },
    ],
    [t],
  )

  /**
   * Whether the menu may be opened at all: something is selected, nothing else is running,
   * and the operator holds `editStreams`.
   *
   * Which rows a particular edit can carry is answered per row by the server - a move that
   * collides with a name already used in the destination refuses that row alone, and a delay
   * on an on-demand stream is refused with `validation_failed` - and that refusal is exactly
   * what the summary dialog reports. Greying entries here would mean deciding in the browser
   * which of a hundred rows the server would have refused, against a page whose own filter
   * may already have hidden half of them.
   */
  const bulkEnabled = canEdit && selectedIds.length > 0 && !bulkBusy

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
      //
      // The kind of stream rides under the state rather than in a column of its own, which
      // is the same question the table asked before it had a `type` column: a running
      // `Permanent` stream and a running `FilePlaylist` stream are different things, and
      // the row has to say which one it is. Nine columns would have meant a ninth width to
      // divide; two facts in one cell is what the column was widened for.
      title: t('videoStreams.columns.status'),
      dataIndex: 'status',
      key: 'status',
      width: widths.status,
      sorter: true,
      sortOrder: table.orderOf('status'),
      onHeaderCell: () => resizer('status'),
      render: (value: VideoStreamStatus, stream: VideoStream) => (
        <StreamStateCell status={value} type={stream.type} />
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
      // Start, stop and restart are the transitions the row can make: starting a stream that
      // is already running is a success rather than a complaint, so a double click is not a
      // failure. Play is drawn **disabled with the reason still attached** - the relaying
      // process behind it does not exist yet, and a greyed button with a sentence says that
      // before the click, where a live button answering 404 would only say something failed
      // after it.
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
          // The relaying process behind play does not exist yet: a greyed button with a
          // sentence says that before the click, where a live button answering 404 would only
          // say something failed after it.
          {
            key: 'restart',
            label: t('videoStreams.actions.restart'),
            icon: <ReloadOutlined />,
            loading: busy,
            disabled: busy,
            onClick: () =>
              runAction(() => restartStream.mutateAsync({ id: stream.id }), stream),
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
      className="page-card"
      title={t('videoStreams.listTitle')}
      // The title pinned to one line, for the reason the other two tables pin theirs: this
      // row shares the card header with a toolbar that is now the widest of the three.
      styles={{ title: { whiteSpace: 'nowrap' } }}
      extra={
        <Space wrap>
          {/* Icon-only, with its whole sentence in the tooltip: see `refreshTip` for why
              the label itself left the row. Drawn either way rather than appearing once it
              is paused, for the reason the reset button below is drawn while disabled - a
              control that comes and goes reads as a broken one. It leads the toolbar because
              it describes the whole table, while the menu beside it acts on a selection that
              is empty on first paint. */}
          {hinted(
            refreshTip,
            <Button
              icon={autoRefresh ? <PauseCircleOutlined /> : <ReloadOutlined />}
              aria-label={refreshTip}
              onClick={() => setAutoRefresh((current) => !current)}
            />,
          )}
          {/* The selection's menu, beside the refresh and before the filters: it acts on the
              rows, and as one button rather than a label beside one - the same shape the
              accounts and groups tables draw. The count rides inside the button the count
              enables, and both states are drawn with nothing selected so the count is what
              says *why* the menu is greyed. The tooltip carries the permission sentence only
              where there is none - with the grant in place it would repeat the button's own
              label back at the visitor. */}
          {hinted(
            canEdit ? t('videoStreams.bulk.menu') : t('videoStreams.noPermission'),
            <Dropdown menu={{ items: bulkItems, onClick: onBulkMenu }} disabled={!bulkEnabled}>
              <Button icon={<MoreOutlined />} loading={bulkBusy}>
                {selectedIds.length > 0
                  ? t('videoStreams.bulk.menuWithCount', { count: selectedIds.length })
                  : t('videoStreams.bulk.menu')}
              </Button>
            </Dropdown>,
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
              until somebody has actually dragged something. The icon keeps the label out of
              a toolbar that shares its row with the table's title. */}
          <ResetColumnWidths
            label={t('videoStreams.resetWidths')}
            disabled={atDefaults}
            onClick={() => reset()}
          />
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
        selection={{ selectedRowKeys: selectedIds, onChange: setSelectedIds }}
      />

      {/* The move, as a dialog rather than a control in the toolbar: where the selection goes
          is one decision about all of it, and a select dropped into a menu that has already
          closed could not ask at the right moment. The list is the same one the filter loads,
          reused rather than asked for a second time. */}
      <Modal
        title={t('videoStreams.bulk.moveTitle')}
        open={bulkDialog === 'move'}
        okText={t('videoStreams.bulk.apply')}
        okButtonProps={{ disabled: !bulkCategoryId, loading: bulkBusy }}
        cancelText={t('actions.cancel')}
        onOk={() =>
          runBulk(
            { action: 'move-category', categoryId: bulkCategoryId ?? undefined },
            closeBulkDialog,
          )
        }
        onCancel={closeBulkDialog}
      >
        <Select
          showSearch
          allowClear
          aria-label={t('videoStreams.bulk.movePlaceholder')}
          style={{ width: '100%' }}
          value={bulkCategoryId ?? undefined}
          options={categoryOptions}
          placeholder={t('videoStreams.bulk.movePlaceholder')}
          optionFilterProp="label"
          onChange={(value) => setBulkCategoryId(value ?? null)}
        />
      </Modal>

      {/* The type, with the three kinds a bulk change can keep whole and the sentence that
          says what happens to settings the new kind cannot carry. The server clears them and
          reports each clearance as a warning on the row, which is why this is allowed at all
          rather than refused. */}
      <Modal
        title={t('videoStreams.bulk.typeTitle')}
        open={bulkDialog === 'type'}
        okText={t('videoStreams.bulk.apply')}
        okButtonProps={{ disabled: !bulkType, loading: bulkBusy }}
        cancelText={t('actions.cancel')}
        onOk={() =>
          runBulk({ action: 'change-type', type: bulkType ?? undefined }, closeBulkDialog)
        }
        onCancel={closeBulkDialog}
      >
        <Select
          aria-label={t('videoStreams.bulk.typePlaceholder')}
          style={{ width: '100%' }}
          value={bulkType ?? undefined}
          options={BULK_TYPES.map((type) => ({
            value: type,
            label: t(`videoStreams.types.${type}`),
          }))}
          placeholder={t('videoStreams.bulk.typePlaceholder')}
          onChange={(value) => setBulkType(value ?? null)}
        />
        <p style={{ margin: '12px 0 0' }}>{t('videoStreams.bulk.typeHint')}</p>
      </Modal>

      {/* The two numbers. Zero is a value rather than an absence on both, matching the
          single-row form: a permanent stream with a zero delay is a stream with no delay, and
          zero days keeps no archive. */}
      <Modal
        title={t('videoStreams.bulk.delayTitle')}
        open={bulkDialog === 'delay'}
        okText={t('videoStreams.bulk.apply')}
        okButtonProps={{ loading: bulkBusy }}
        cancelText={t('actions.cancel')}
        onOk={() => runBulk({ action: 'change-delay', delayMinutes: bulkDelay }, closeBulkDialog)}
        onCancel={closeBulkDialog}
      >
        <label style={{ display: 'block' }}>
          <span style={{ display: 'block', marginBottom: 4 }}>
            {t('videoStreams.bulk.delayLabel')}
          </span>
          <InputNumber
            min={0}
            style={{ width: '100%' }}
            value={bulkDelay}
            onChange={(value) => setBulkDelay(value ?? 0)}
          />
        </label>
        <p style={{ margin: '12px 0 0' }}>{t('videoStreams.bulk.delayHint')}</p>
      </Modal>

      <Modal
        title={t('videoStreams.bulk.archiveTitle')}
        open={bulkDialog === 'archive'}
        okText={t('videoStreams.bulk.apply')}
        okButtonProps={{ loading: bulkBusy }}
        cancelText={t('actions.cancel')}
        onOk={() =>
          runBulk({ action: 'change-archive', archiveDays: bulkArchive }, closeBulkDialog)
        }
        onCancel={closeBulkDialog}
      >
        <label style={{ display: 'block' }}>
          <span style={{ display: 'block', marginBottom: 4 }}>
            {t('videoStreams.bulk.archiveLabel')}
          </span>
          <InputNumber
            min={0}
            style={{ width: '100%' }}
            value={bulkArchive}
            onChange={(value) => setBulkArchive(value ?? 0)}
          />
        </label>
        <p style={{ margin: '12px 0 0' }}>{t('videoStreams.bulk.archiveHint')}</p>
      </Modal>

      {/* The four restream settings are replaced as a group, so the dialog says that once and
          lets a blank field clear its setting - the same whole-list rule the single-row form
          follows, in the same request shape. */}
      <Modal
        title={t('videoStreams.bulk.restreamTitle')}
        open={bulkDialog === 'restream'}
        okText={t('videoStreams.bulk.apply')}
        okButtonProps={{ loading: bulkBusy }}
        cancelText={t('actions.cancel')}
        onOk={() =>
          runBulk(
            {
              action: 'change-restream',
              restreamUserAgent: bulkRestream.userAgent,
              restreamProxy: bulkRestream.proxy,
              restreamCookies: bulkRestream.cookies,
              restreamHeaders: bulkRestream.headers,
            },
            closeBulkDialog,
          )
        }
        onCancel={closeBulkDialog}
      >
        <p style={{ margin: '0 0 12px' }}>{t('videoStreams.bulk.restreamHint')}</p>
        <label style={{ display: 'block', marginBottom: 12 }}>
          <span style={{ display: 'block', marginBottom: 4 }}>
            {t('videoStreams.bulk.restreamUserAgent')}
          </span>
          <Input
            value={bulkRestream.userAgent}
            placeholder={t('videoStreamsForm.userAgentPlaceholder')}
            onChange={(event) =>
              setBulkRestream((current) => ({ ...current, userAgent: event.target.value }))
            }
          />
        </label>
        <label style={{ display: 'block', marginBottom: 12 }}>
          <span style={{ display: 'block', marginBottom: 4 }}>
            {t('videoStreams.bulk.restreamProxy')}
          </span>
          <Input
            value={bulkRestream.proxy}
            placeholder={t('videoStreamsForm.proxyPlaceholder')}
            onChange={(event) =>
              setBulkRestream((current) => ({ ...current, proxy: event.target.value }))
            }
          />
        </label>
        <label style={{ display: 'block', marginBottom: 12 }}>
          <span style={{ display: 'block', marginBottom: 4 }}>
            {t('videoStreams.bulk.restreamCookies')}
          </span>
          <Input
            value={bulkRestream.cookies}
            placeholder={t('videoStreamsForm.cookiesPlaceholder')}
            onChange={(event) =>
              setBulkRestream((current) => ({ ...current, cookies: event.target.value }))
            }
          />
        </label>
        <label style={{ display: 'block' }}>
          <span style={{ display: 'block', marginBottom: 4 }}>
            {t('videoStreams.bulk.restreamHeaders')}
          </span>
          <Input
            value={bulkRestream.headers}
            placeholder={t('videoStreamsForm.headersPlaceholder')}
            onChange={(event) =>
              setBulkRestream((current) => ({ ...current, headers: event.target.value }))
            }
          />
        </label>
      </Modal>
    </Card>
  )
}
