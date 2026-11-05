import { useEffect, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { App, Button, Card, DatePicker, Segmented, Select, Space, Tag, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import type { Dayjs } from 'dayjs'
import { useTranslation } from 'react-i18next'
import { ClearOutlined, PauseCircleOutlined, ReloadOutlined, StopOutlined } from '@ant-design/icons'
import { CONNECTION_LOGS_PAGE_SIZE } from '../../features/connection-logs/api'
import type { ConnectionLogSortField, ConnectionLogState } from '../../features/connection-logs/api'
import type { ConnectionLog, ConnectionStreamKind } from '../../features/connection-logs/types'
import { durationText } from '../../features/connection-logs/duration'
import {
  STARTED_FROM_KEY,
  STARTED_TO_KEY,
  rangeFromPicker,
  rangePickerValue,
  startRangeForMode,
} from '../../features/connection-logs/startRange'
import {
  ACTIVITY_COLUMN_KEYS,
  ACTIVITY_COLUMN_WIDTHS,
  COLUMN_WIDTHS_KEYS,
  LIVE_COLUMN_KEYS,
  LIVE_COLUMN_WIDTHS,
} from '../../features/connection-logs/layout'
import type { ConnectionLogColumnKey } from '../../features/connection-logs/layout'
import ConnectionNetworkCell from '../../features/connection-logs/components/ConnectionNetworkCell'
import UserAgentCell from '../../components/table/UserAgentCell'
import { useClearConnectionLogs } from '../../features/connection-logs/hooks/mutations'
import { useConnectionLogs } from '../../features/connection-logs/hooks/queries'
import { useAudioStreams } from '../../features/audio-streams/hooks/queries'
import { useVideoStreams } from '../../features/video-streams/hooks/queries'
import { useAllUsers } from '../../features/users/hooks/queries'
import { useProfile } from '../../features/profile/hooks/queries'
import { ResetColumnWidths, RowActions, ServerTable, TableSearch } from '../../components/table'
import type { ResizableHeaderCellProps, RowAction } from '../../components/table'
import { useColumnWidths, useServerTable } from '../../hooks/table'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'
import { formatDay, formatTimestamp } from '../../utils/dateFormat'
import { hinted } from '../../utils/hinted'
import { messageForError, successTextFor } from '../../utils/http'
import { enumParam, withFilter } from '../../utils/tableParams'
import { tableResults } from '../../utils/tableResults'
import { dashedCell } from '../../utils/tableCells'

/** How often the table re-reads itself while the toggle below is on, in milliseconds. */
const AUTO_REFRESH_MS = 30_000

/** The two media the screen offers, in the order the segmented control draws them. */
const KINDS: readonly ConnectionStreamKind[] = ['video', 'audio']

/**
 * The two modes of this page, keyed by the menu item that opens them.
 *
 * The router renders every menu item as `<Page sectionKey={item.labelKey} />` and offers no
 * other prop, so the one fact that separates the two addresses - which slice of the log is
 * being read - arrives through the key the item already carries. The keys are written beside
 * the very table that generates them (`NAVIGATION` in `src/utils/navigation.ts`), and
 * `probe:render` renders both to assert each one is its own screen.
 *
 * **An unknown key is a throw, not a fallback.** A key renamed on the menu side only would
 * otherwise draw the live list under an activity heading, and nothing in `typecheck`, `lint`
 * or the build could see it. A throw reaches the router's error fallback, and the render probe
 * fails loudly on the page it could not draw.
 */
const SECTION_MODES: Record<string, ConnectionLogMode> = {
  'nav.items.liveConnections': 'live',
  'nav.items.subscriptionActivity': 'activity',
}

/**
 * What each mode asks the endpoint for, and the controls, column set and widths it draws.
 *
 * The numbers it carries come from `features/connection-logs/layout.ts` rather than being
 * written here: the two modes disagree about the column list, the starting widths and the
 * storage key, and a page that held them inline was a page where changing one of them could
 * not be checked from outside. See that module for the widths themselves and for the invariant
 * the unit test pins. `showsStartRange` is the fourth difference and lives here rather than in
 * `layout.ts` because it is a control's presence, not a measurement.
 */
interface ConnectionLogModeSpec {
  /** The `state` parameter: live is `endedAt IS NULL`, activity is everything. */
  state: ConnectionLogState
  /**
   * Whether this mode draws the end column, and **no** actions column (a finished session has
   * nothing to end). The live mode is the exact opposite on both counts, which is why one flag
   * answers both questions rather than two that could disagree.
   */
  showsEnd: boolean
  /**
   * Whether this mode draws the start-time range control and sends its two bounds.
   *
   * **Only the activity view does.** The live view is "every session still open", and a start
   * bound on it would narrow that to "still open and started since…" on a screen that shows no
   * control saying so. A `startedFrom`/`startedTo` in a live address is therefore **ignored**
   * rather than stripped - see `startRangeForMode` - and no range tag is drawn either, because
   * the tag's only source is the range this flag admits.
   */
  showsStartRange: boolean
  /** The columns this mode draws, in order. */
  columns: readonly ConnectionLogColumnKey[]
  /** The width each of those columns starts at. */
  widths: Record<ConnectionLogColumnKey, number>
  /** Where this mode remembers the widths the visitor gave its columns. */
  storageKey: string
}

const MODE_SPECS: Record<ConnectionLogMode, ConnectionLogModeSpec> = {
  live: {
    state: 'live',
    showsEnd: false,
    showsStartRange: false,
    columns: LIVE_COLUMN_KEYS,
    widths: LIVE_COLUMN_WIDTHS,
    storageKey: COLUMN_WIDTHS_KEYS.live,
  },
  activity: {
    state: 'all',
    showsEnd: true,
    showsStartRange: true,
    columns: ACTIVITY_COLUMN_KEYS,
    widths: ACTIVITY_COLUMN_WIDTHS,
    storageKey: COLUMN_WIDTHS_KEYS.activity,
  },
}

type ConnectionLogMode = 'live' | 'activity'

/** The filters this page reads out of the address, beyond the shared five parameters. */
const FILTER_KEYS = [
  'country',
  'ip',
  'isp',
  'userAgent',
  'subscriptionId',
  'ownerId',
  'streamId',
] as const

type FilterKey = (typeof FILTER_KEYS)[number]

/** One option of the two pickers, and one filter tag's label. */
interface Option {
  value: string
  label: string
}

interface ConnectionLogsPageProps {
  /**
   * The menu key this route carries. Read here, unlike most section pages: the two addresses
   * differ only in the slice of the log they show, and the key is where that fact travels. See
   * `SECTION_MODES`.
   */
  sectionKey: string
}

/**
 * The connection log - one component behind **two** menu entries.
 *
 * `nav.items.liveConnections` opens the sessions that have not ended, with no end column and a
 * kill action drawn disabled because the process that would end one does not exist yet.
 * `nav.items.subscriptionActivity` opens the whole log, with the end time and its computed
 * duration beside it, and no kill action at all. Everything else - the filters, the toolbar,
 * the column widths mechanics, the clear-all command - is one implementation, because two
 * screens that differed in their rules for a filter tag or a pager would be two places for one
 * of them to be quietly wrong.
 *
 * **Every parameter lives in the address**, including the six filters, the medium and the
 * state. That is what makes a drill-down from a subscription row a link: the page is opened
 * with `?subscriptionId=…` already in it, and it arrives filtered rather than looking as if the
 * click had done nothing.
 *
 * **The network cell is this page's click-through.** Each of the three facts in it writes its
 * own filter, and the write drops `page` in the same navigation so a click from page four
 * lands on page one of the narrower list.
 *
 * ## The outer shell exists so the two modes do not share a component instance
 *
 * Both entries render this module, so react-router replaces the *props* of one mounted
 * component rather than unmounting it - `mode` is not part of any key on the route, and React
 * does not remount on a prop change. Everything the component holds in `useState` therefore
 * survived the switch: the paused auto-refresh travelled from the live page to the activity
 * one, and - worse - the column widths did, so the activity table was drawn with the live
 * mode's six-column arrangement, then **wrote it under the activity key**, which is what made
 * the page keep the wrong look after a reload.
 *
 * The split is the fix, and it is deliberately two components rather than a `key` on something
 * inside the page: the state that must not survive is this component's own, so the instance
 * that owns it has to be the one that ends. `ConnectionLogScreen` below is that instance; this
 * shell resolves the section key to a mode and hands it over as a `key`, which is React's own
 * "this is a different thing now" signal. A key renamed on the menu side only still throws
 * here, before any of the screen's hooks run.
 */
export default function ConnectionLogsPage({ sectionKey }: ConnectionLogsPageProps) {
  const mode = SECTION_MODES[sectionKey]

  if (!mode) {
    // Deliberately not a fallback: a key renamed on the menu side only must fail loudly, in the
    // probe and in the browser's error fallback, rather than draw one of the two lists under
    // the other's heading.
    throw new Error(`ConnectionLogsPage: unknown section key "${sectionKey}".`)
  }

  return <ConnectionLogScreen key={mode} mode={mode} />
}

/**
 * One mode's screen: the query, the filters, the widths and the toolbar, all belonging to the
 * mode it was mounted for.
 *
 * `mode` is a plain prop here rather than a `sectionKey` to resolve, because the shell above has
 * already decided - and because every value this component derives from it (the state filter,
 * the column list, the widths, the storage key) comes from one `MODE_SPECS` entry, so no two of
 * them can be read from different modes.
 */
function ConnectionLogScreen({ mode }: { mode: ConnectionLogMode }) {
  const { t, i18n } = useTranslation()
  const { message, modal } = App.useApp()
  const navigate = useNavigate()
  const session = useSession()

  const spec = MODE_SPECS[mode]

  const table = useServerTable<ConnectionLog, ConnectionLogSortField>({
    defaultSort: { by: 'startedAt', order: 'desc' },
    // `endedAt` is a field the endpoint honours on both modes (the live list has nothing to
    // order by it, but a hand-edited link naming it is not a 400), while only the activity
    // table draws a header for it.
    sortFields: ['startedAt', 'endedAt'],
    pageSize: CONNECTION_LOGS_PAGE_SIZE,
  })

  const [searchParams, setSearchParams] = useSearchParams()

  // The medium, its default being the endpoint's own. A hand-edited value that names nothing
  // draws the video list rather than answering a 400, the same rule `enumParam` keeps.
  const kind = enumParam<ConnectionStreamKind>(searchParams, 'kind', KINDS) ?? 'video'

  /** Whether the table re-reads itself. On by default, and pausing is one press. */
  const [autoRefresh, setAutoRefresh] = useState(true)

  const filters = Object.fromEntries(
    FILTER_KEYS.map((key) => [key, searchParams.get(key) || null]),
  ) as Record<FilterKey, string | null>

  // The start-time range is two parameters but one control, so it is read and written as a
  // pair rather than through `FILTER_KEYS`: closing its tag must clear both sides in one write.
  // Read through the mode gate: the live view has no range control, so it sends no bounds even
  // when the address carries them (they are ignored rather than stripped - see the module).
  const startedRange = startRangeForMode(searchParams, spec.showsStartRange)

  const logsQuery = useConnectionLogs({
    kind,
    state: spec.state,
    ...table.query,
    subscriptionId: filters.subscriptionId,
    ownerId: filters.ownerId,
    streamId: filters.streamId,
    ip: filters.ip,
    country: filters.country,
    isp: filters.isp,
    userAgent: filters.userAgent,
    startedFrom: startedRange.from,
    startedTo: startedRange.to,
  })
  const { data: logs, loadError, loading } = tableResults(logsQuery)

  const profileQuery = useProfile()
  const granted = profileQuery.data?.permissions ?? []
  const canEdit = granted.includes('editSubscriptions')
  const timeZone = profileQuery.data?.timeZone ?? null
  const locale = i18n.resolvedLanguage ?? i18n.language

  // The two pickers' option lists, each in a query of its own so a failure of one offers an
  // empty picker rather than failing the table.
  const usersQuery = useAllUsers()
  const userOptions: Option[] = (usersQuery.data?.items ?? []).map((user) => ({
    value: user.id,
    label: user.username,
  }))

  const videosQuery = useVideoStreams({
    page: 1,
    pageSize: 1000,
    search: '',
    sortBy: 'name',
    sortOrder: 'asc',
  })
  const audiosQuery = useAudioStreams({
    page: 1,
    pageSize: 1000,
    search: '',
    sortBy: 'name',
    sortOrder: 'asc',
  })

  const streamOptions: Option[] = (
    kind === 'audio' ? (audiosQuery.data?.items ?? []) : (videosQuery.data?.items ?? [])
  ).map((stream) => ({ value: stream.id, label: stream.name }))

  const area = session ? areaForRole(session.role) : ''

  /**
   * The widths of **this mode's** columns.
   *
   * Every one of the three inputs is the mode's own: its column list, its starting widths and
   * its storage key. Passing the other mode's values is what produced the two owner-reported
   * bugs this call was rewritten for - a table that read a seven-column arrangement into a six
   * column one, and a reset that wrote the live mode's numbers under the activity key.
   */
  const { widths, tableWidth, atDefaults, reset, resizer: handleFor } =
    useColumnWidths<ConnectionLogColumnKey>({
      keys: spec.columns,
      defaults: spec.widths,
      storageKey: spec.storageKey,
    })

  const clearLogs = useClearConnectionLogs()

  /**
   * The timer that keeps a live screen honest about sessions ending.
   *
   * The ref rather than the query in the dependency array is the point: `refetch` is a new
   * function whenever the answer changes, so naming it here would restart the interval after
   * every re-read.
   */
  const refreshRef = useRef<() => void>(() => {})

  useEffect(() => {
    refreshRef.current = () => {
      void logsQuery.refetch()
    }
  })

  useEffect(() => {
    if (!autoRefresh) return

    const timer = setInterval(() => refreshRef.current(), AUTO_REFRESH_MS)

    return () => clearInterval(timer)
  }, [autoRefresh])

  /** Writes one filter, dropping `page` in the same write. */
  const choose = (key: string, value: string | null | undefined) =>
    setSearchParams(withFilter(searchParams, key, value), { replace: true })

  /**
   * Switches the medium and drops a stream filter that named the other one in the same write.
   *
   * A `streamId` of a video stream is a filter no audio row can match, so keeping it would
   * draw an empty table that looked like a broken one.
   */
  const chooseKind = (value: ConnectionStreamKind) => {
    const next = withFilter(searchParams, 'kind', value === 'video' ? null : value)

    next.delete('streamId')

    setSearchParams(next, { replace: true })
  }

  /**
   * Writes the whole start-time range in one navigation.
   *
   * The two bounds are one control, so they are one write: two `choose` calls would produce a
   * read of the half-written range in between. Both writes drop `page`, which is why the second
   * `withFilter` starts from the address the first one produced rather than from the original.
   */
  const chooseRange = (value: [Dayjs | null, Dayjs | null] | null) => {
    const range = value ? rangeFromPicker(value, timeZone) : { from: null, to: null }
    const withFrom = withFilter(searchParams, STARTED_FROM_KEY, range.from)

    setSearchParams(withFilter(withFrom, STARTED_TO_KEY, range.to), { replace: true })
  }

  /**
   * Empties the whole log, after asking once.
   *
   * The command names nothing - no filter, no medium, no state - and the dialog says exactly
   * that, because the button's own words ("clear all logs") are a promise about the table and
   * not about the rows on screen. It is deliberately not disabled on an empty or filtered view:
   * a button greyed out because the filtered slice is empty would be lying about a log that
   * still holds rows. The confirmation is the guard.
   */
  const confirmClear = () => {
    modal.confirm({
      title: t('connectionLogs.clearConfirmTitle'),
      content: t('connectionLogs.clearConfirmBody'),
      okText: t('connectionLogs.clearConfirmOk'),
      okButtonProps: { danger: true },
      cancelText: t('actions.cancel'),
      onOk: async () => {
        try {
          const result = await clearLogs.mutateAsync({})

          message.success(successTextFor(result))
          void logsQuery.refetch()
        } catch (error) {
          message.error(messageForError(error))
        }
      },
    })
  }

  const resizer = (key: ConnectionLogColumnKey): ResizableHeaderCellProps =>
    handleFor(key, t('connectionLogs.resizeColumn', { column: t(`connectionLogs.columns.${key}`) }))

  /**
   * The subscription cell: the identifier, as a link into its edit form.
   *
   * **One fact, not two.** It used to carry the owner's name in parentheses beside the
   * identifier, and the owner asked for the two to be their own columns - a row that names two
   * things in one cell is a row whose two sorts, widths and click targets are one. Both are
   * links, and neither states a size: the cell's own font is the row's font.
   */
  const subscriptionCell = (log: ConnectionLog) => (
    <Typography.Link onClick={() => navigate(`${area}/subscriptions/${log.subscriptionId}`)}>
      {log.subscriptionId.slice(0, 8)}…
    </Typography.Link>
  )

  /** The user cell: the owner's login name, as a link into the account form. */
  const userCell = (log: ConnectionLog) => (
    <Typography.Link onClick={() => navigate(`${area}/users/${log.ownerId}`)}>
      {log.username}
    </Typography.Link>
  )

  /**
   * The stream cell: a link into the table the kind names, or a dash when the row is gone.
   *
   * The link states no size, for the same reason the subscription cell's links do not: the
   * stream title is one of the columns the owner asked to read at the cell's own size.
   */
  const streamCell = (log: ConnectionLog) => {
    if (log.streamTitle === null) return dashedCell(null)

    const path = log.streamKind === 'audio' ? 'audio-streams' : 'video-streams'

    return (
      <Typography.Link onClick={() => navigate(`${area}/${path}/${log.streamId}`)}>
        {log.streamTitle}
      </Typography.Link>
    )
  }

  const columns: TableColumnsType<ConnectionLog> = [
    {
      title: t('connectionLogs.columns.subscription'),
      dataIndex: 'subscriptionId',
      key: 'subscription',
      width: widths.subscription,
      onHeaderCell: () => resizer('subscription'),
      render: (_, log) => subscriptionCell(log),
    },
    {
      title: t('connectionLogs.columns.user'),
      dataIndex: 'username',
      key: 'user',
      width: widths.user,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('user'),
      render: (_, log) => userCell(log),
    },
    {
      title: t('connectionLogs.columns.stream'),
      dataIndex: 'streamTitle',
      key: 'stream',
      width: widths.stream,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('stream'),
      render: (_, log) => streamCell(log),
    },
    {
      title: t('connectionLogs.columns.network'),
      key: 'network',
      width: widths.network,
      onHeaderCell: () => resizer('network'),
      render: (_, log) => <ConnectionNetworkCell log={log} locale={locale} onFilter={choose} />,
    },
    {
      // The same summary the visit history prints, and the log's way to narrow itself to one
      // client: the whole point of the column is "who is behind this connection", and the
      // next question is always "which of these rows are them".
      title: t('connectionLogs.columns.agent'),
      dataIndex: 'userAgent',
      key: 'agent',
      width: widths.agent,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('agent'),
      render: (value: string | null) => (
        <UserAgentCell value={value} onFilter={(agent) => choose('userAgent', agent)} />
      ),
    },
    {
      title: t('connectionLogs.columns.startedAt'),
      dataIndex: 'startedAt',
      key: 'startedAt',
      width: widths.startedAt,
      sorter: true,
      sortOrder: table.orderOf('startedAt'),
      onHeaderCell: () => resizer('startedAt'),
      render: (value: string) => formatTimestamp(value, locale, timeZone),
    },
    // The end column exists only on the activity view: a live session has no end to show, and
    // a column of dashes under a heading would be a column that says nothing.
    ...(spec.showsEnd
      ? [
          {
            title: t('connectionLogs.columns.endedAt'),
            dataIndex: 'endedAt',
            key: 'endedAt',
            width: widths.endedAt,
            sorter: true,
            sortOrder: table.orderOf('endedAt'),
            onHeaderCell: () => resizer('endedAt'),
            render: (value: string | null, log: ConnectionLog) => {
              // An open session has no end time, and a bare dash says "unknown" rather than
              // "still running". The tag is the existing vocabulary for that state: the visit
              // history marks the session being read with one, and this says the same thing
              // about a connection.
              if (value === null) {
                return (
                  <Tag color="processing" style={{ marginInlineEnd: 0 }}>
                    {t('connectionLogs.live')}
                  </Tag>
                )
              }

              return (
                <Space size={4} wrap>
                  <span>{formatTimestamp(value, locale, timeZone)}</span>
                  <span style={{ opacity: 0.75 }}>
                    (
                    {durationText(log.startedAt, log.endedAt, {
                      hour: t('connectionLogs.duration.hour'),
                      minute: t('connectionLogs.duration.minute'),
                      second: t('connectionLogs.duration.second'),
                    })}
                    )
                  </span>
                </Space>
              )
            },
          } satisfies TableColumnsType<ConnectionLog>[number],
        ]
      : []),
    // The actions column exists on the live view alone: a finished session has nothing to act
    // on, so the activity table draws no column at all (see `ACTIVITY_COLUMN_KEYS`).
    ...(spec.showsEnd
      ? []
      : [
          {
            title: t('connectionLogs.columns.actions'),
            key: 'actions',
            width: widths.actions,
            onHeaderCell: () => resizer('actions'),
            render: () => {
              // The streaming subsystem that would terminate a connection does not exist yet: a
              // greyed button with a sentence says that before the click, where a live button
              // answering 404 would only say something failed after it. The same wording the
              // subscriptions row uses for its own stub.
              const actions: RowAction[] = [
                {
                  key: 'kill',
                  label: t('subscriptions.actions.killConnections'),
                  icon: <StopOutlined />,
                  disabled: true,
                },
              ]

              return <RowActions actions={actions} />
            },
          } satisfies TableColumnsType<ConnectionLog>[number],
        ]),
  ]

  const ownerFilterLabel =
    userOptions.find((option) => option.value === filters.ownerId)?.label ?? filters.ownerId
  const streamFilterLabel =
    streamOptions.find((option) => option.value === filters.streamId)?.label ?? filters.streamId

  /**
   * Every active filter as a closable tag, in a fixed order rather than the order they were
   * applied: a toolbar whose tags moved around as they were removed would be a row the visitor
   * has to re-read each time.
   *
   * A tag may carry its own `clear` when it stands for more than one parameter: the start-time
   * range is one control but two query-string keys, and closing its tag must remove both in one
   * navigation or the list would be re-read against half a range.
   */
  const activeFilters: { key: string; label: string; clear?: () => void }[] = []

  // `startedRange` is already the mode-gated value (`startRangeForMode`), so the live view can
  // never reach this branch: a screen with no picker shows no tag for a range it is not applying.
  if (startedRange.from || startedRange.to) {
    const from = formatDay(startedRange.from, locale, timeZone)
    const to = formatDay(startedRange.to, locale, timeZone)

    // A single-sided range names only the side that is set: a tag reading "– 1 May" would
    // suggest the other end had been cleared rather than never given.
    const label = from && to
      ? t('connectionLogs.filters.startedRange', { from, to })
      : from
        ? t('connectionLogs.filters.startedRangeFrom', { from })
        : t('connectionLogs.filters.startedRangeTo', { to })

    activeFilters.push({
      key: 'startedRange',
      label,
      clear: () => chooseRange(null),
    })
  }

  if (filters.country) {
    activeFilters.push({
      key: 'country',
      label: t('connectionLogs.filters.country', { value: filters.country }),
    })
  }
  if (filters.ip) {
    activeFilters.push({ key: 'ip', label: t('connectionLogs.filters.ip', { value: filters.ip }) })
  }
  if (filters.isp) {
    activeFilters.push({
      key: 'isp',
      label: t('connectionLogs.filters.isp', { value: filters.isp }),
    })
  }
  if (filters.userAgent) {
    // The raw string, not its summary: a tag reading "Chrome 140 (Windows 10, x64)" would name
    // a value the filter does not compare against, and two clients with the same summary would
    // look like one filter.
    activeFilters.push({
      key: 'userAgent',
      label: t('connectionLogs.filters.userAgent', { value: filters.userAgent }),
    })
  }
  if (filters.subscriptionId) {
    activeFilters.push({
      key: 'subscriptionId',
      label: t('connectionLogs.filters.subscription', {
        value: `${filters.subscriptionId.slice(0, 8)}…`,
      }),
    })
  }
  if (filters.ownerId) {
    activeFilters.push({
      key: 'ownerId',
      label: t('connectionLogs.filters.owner', { value: ownerFilterLabel }),
    })
  }
  if (filters.streamId) {
    activeFilters.push({
      key: 'streamId',
      label: t('connectionLogs.filters.stream', { value: streamFilterLabel }),
    })
  }

  const filtered = Boolean(table.query.search) || activeFilters.length > 0

  /**
   * What the auto-refresh button says while it is greyed *or* live.
   *
   * It is the button's whole accessible name and its tooltip, because the control is icon-only:
   * the heading already shares its line with a medium switch, two pickers and the clear command,
   * and a worded label would push the table sideways for a control that is one glyph wide.
   */
  const refreshTip = `${t(autoRefresh ? 'connectionLogs.pauseRefresh' : 'connectionLogs.resumeRefresh')} (${t('connectionLogs.autoRefreshHint', { seconds: AUTO_REFRESH_MS / 1000 })})`

  return (
    <Card
      className="page-card"
      // The heading names the slice, never the menu item: the two addresses of this page differ
      // in the rows they show, and a card saying only "Connection logs" on both would leave the
      // visitor reading two different screens with one name.
      title={t(`connectionLogs.titles.${mode}`)}
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
          {/* The search leads the controls after the toggle, and the filters follow it on the
              owner's correction: the box narrows *what* is listed, and each picker below narrows
              it further by one column - a reader who is looking for one row reaches the box
              before deciding which column to narrow by. */}
          <TableSearch
            value={table.searchText}
            onChange={table.setSearchText}
            onSearch={table.applySearch}
            labelKey="connectionLogs.searchLabel"
            placeholderKey="connectionLogs.searchPlaceholder"
            width={190}
          />
          <Select
            allowClear
            showSearch
            aria-label={t('connectionLogs.filterAnyOwner')}
            style={{ width: 150 }}
            value={filters.ownerId ?? undefined}
            options={userOptions}
            placeholder={t('connectionLogs.filterAnyOwner')}
            optionFilterProp="label"
            onChange={(value) => choose('ownerId', value)}
          />
          <Select
            allowClear
            showSearch
            aria-label={t('connectionLogs.filterAnyStream')}
            style={{ width: 160 }}
            value={filters.streamId ?? undefined}
            options={streamOptions}
            placeholder={t('connectionLogs.filterAnyStream')}
            optionFilterProp="label"
            onChange={(value) => choose('streamId', value)}
          />
          <Segmented
            aria-label={t('connectionLogs.kindLabel')}
            value={kind}
            options={[
              { value: 'video', label: t('connectionLogs.kinds.video') },
              { value: 'audio', label: t('connectionLogs.kinds.audio') },
            ]}
            onChange={(value) => chooseKind(value as ConnectionStreamKind)}
          />
          <ResetColumnWidths
            label={t('connectionLogs.resetWidths')}
            disabled={atDefaults}
            onClick={() => reset()}
          />
          {canEdit ? (
            <Button
              danger
              icon={<ClearOutlined />}
              loading={clearLogs.isPending}
              onClick={confirmClear}
            >
              {t('connectionLogs.clearAll')}
            </Button>
          ) : (
            hinted(
              t('connectionLogs.noPermission'),
              <Button danger icon={<ClearOutlined />} disabled>
                {t('connectionLogs.clearAll')}
              </Button>,
            )
          )}
        </Space>
      }
    >
      {/* The start-time range is a row of its own, under the card head and above the tag row and
          the table: it is the one filter whose control is too wide to share the toolbar without
          pushing the owner picker, the stream picker and the medium switch onto a second line
          *inside* the heading. **The activity view alone draws it** - the live view is "every
          session still open", and a start bound would narrow that on a screen showing no control
          that says so (`spec.showsStartRange`, read by `startRangeForMode` too). The value is
          derived from the address rather than held in state, so a drill-down link that already
          carries a range arrives showing it; the conversion to the account's zone lives in
          `startRange.ts`. */}
      {spec.showsStartRange && (
        <div style={{ marginBottom: 12 }}>
          <DatePicker.RangePicker
            allowClear
            aria-label={t('connectionLogs.filterStartedRange')}
            style={{ width: 250 }}
            value={rangePickerValue(startedRange, timeZone)}
            placeholder={[
              t('connectionLogs.filterStartedFrom'),
              t('connectionLogs.filterStartedTo'),
            ]}
            onChange={(value) => chooseRange(value as [Dayjs | null, Dayjs | null] | null)}
          />
        </div>
      )}

      {activeFilters.length > 0 && (
        <Space wrap size={4} style={{ marginBottom: 12 }}>
          {activeFilters.map((filter) => (
            <Tag
              key={filter.key}
              closable
              onClose={(event) => {
                event.preventDefault()

                if (filter.clear) {
                  filter.clear()
                  return
                }

                choose(filter.key, null)
              }}
            >
              {filter.label}
            </Tag>
          ))}
        </Space>
      )}

      <ServerTable<ConnectionLog, ConnectionLogSortField>
        table={table}
        columns={columns}
        rows={logs?.items ?? []}
        total={logs?.total ?? 0}
        loading={loading}
        loadError={loadError}
        onRetry={() => void logsQuery.refetch()}
        tableWidth={tableWidth}
        emptyText={t(filtered ? 'connectionLogs.noMatches' : 'connectionLogs.empty')}
        showTotal={(total, [from, to]) => t('connectionLogs.range', { from, to, total })}
      />
    </Card>
  )
}
