import { useSearchParams } from 'react-router-dom'
import { App, Button, Card, Select, Space, Table, Tag } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import type { SessionSortField } from '../lib/api'
import { hinted } from '../lib/hinted'
import { messageForError, textForCode } from '../lib/http'
import { formatTimestamp } from '../lib/dateFormat'
import { tableResults } from '../lib/tableResults'
import { agentCell, countryCell, dashedCell, ipCell, networkCell } from '../lib/tableCells'
import LoadErrorAlert from '../components/LoadErrorAlert'
import ResizableHeaderCell from '../components/ResizableHeaderCell'
import type { ResizableHeaderCellProps } from '../components/ResizableHeaderCell'
import TableSearch from '../components/TableSearch'
import { useRevokeAdminSession } from '../hooks/mutations'
import { useColumnWidths } from '../hooks/useColumnWidths'
import { useAdminSessionHistory, useAllUsers, useProfile } from '../hooks/queries'
import { useServerTable } from '../hooks/useServerTable'
import type { AdminSessionVisit } from '../types'

/** The seven columns, in the order they are drawn, named by their own keys. */
type AdminSessionColumnKey =
  | 'user'
  | 'when'
  | 'ip'
  | 'country'
  | 'network'
  | 'agent'
  | 'actions'

const COLUMN_KEYS: readonly AdminSessionColumnKey[] = [
  'user',
  'when',
  'ip',
  'country',
  'network',
  'agent',
  'actions',
]

/**
 * The width each column starts at, and the width it goes back to when asked to reset.
 *
 * Every column has a width of its own rather than letting the browser work it out, for the
 * reason the visit history's five do: a column with no width grows to fit its widest cell,
 * so sixty characters of registry name or an IPv6 address would widen the table and push
 * everything after it sideways. These seven sum to **1230**, the same as the accounts table
 * and the group table beside it in the same rail - a visitor who can read one whole should
 * be able to read the other, and 1230 is what still fits beside the sidebar on one Full HD
 * screen with the action buttons at the far right inside the viewport.
 *
 * The account column is where the width was found: it carries a name and nothing else, and
 * the server bounds a username at twenty characters, so 170 is what a name needs and the
 * twenty it does not take go to the user agent, which is a sentence. The country column is
 * 120 rather than the 150 the account's own history gives it, because that table has no
 * account column to pay for and this one does - what it loses it keeps in a tooltip.
 *
 * These are *starting* widths, not the answer - see `useColumnWidths`.
 */
const DEFAULT_COLUMN_WIDTHS: Record<AdminSessionColumnKey, number> = {
  user: 170,
  when: 250,
  ip: 150,
  country: 120,
  network: 190,
  agent: 230,
  actions: 120,
}

/**
 * Where the widths are remembered between visits.
 *
 * Only here, and under a key of this table's own: a width stored under the history's key
 * would be a claim about five columns that have since become seven, and the reset would
 * undo one table's arrangement from another's button.
 */
const COLUMN_WIDTHS_KEY = 'fluxy.adminSessions.columnWidths'

/**
 * One entry of the account filter: the identifier the query string needs and the name the
 * visitor reads.
 */
interface UserOption {
  value: string
  label: string
}

/**
 * The installation's visit history - the page behind `nav.items.adminSessions`, headed by
 * `adminSessions.listTitle` rather than by that item's name (the props below say why).
 *
 * It is the account's own history answered over every account the operator may read, and
 * nearly everything it does follows from that one change. The rows are still tokens rather
 * than sessions, so a browser that refreshed from a different network appears twice - which
 * is the point there and the point here: "a sign-in I do not recognise" and "a rotation I do
 * not recognise" are the same alarm. Paging, search, filtering and sorting are all server
 * side for the same reason the two sibling tables make them so: `total` counts what the
 * caller may see, and a filter applied in the browser would narrow one page while the pager
 * still counted everything.
 *
 * What is new is the account column and the button beside each row, and the two are why this
 * is not the history with an extra column. **Ending a session is an act on a chain, not on
 * a row**: several rows of one sign-in carry the same `sessionId`, and the DELETE takes
 * that. So the page re-reads the list after every revoke instead of dropping the row it
 * clicked - one revoke turns every row of that session, on every page, into an ended one,
 * and deciding in the browser which those are would be deciding what the server already
 * knows.
 *
 * Two buttons are drawn greyed on purpose, and each says why before the click rather than
 * refusing after it: a row whose session has already ended or quietly expired (`isActive`)
 * and the rows of the session this page is being read with (`isCurrent`), which the API
 * answers `cannot_revoke_current` for. That is the same rule the active sessions page
 * follows - the UI tells the truth the API would tell.
 *
 * Dates are rendered in the reading account's own time zone, because the profile page
 * promises they are, and the *language* of the dates follows the page rather than the
 * browser for the same reason the country column does: this application has a language
 * switcher, and a date in one language beside a country name in another is that switcher
 * visibly failing halfway down a row.
 */
interface AdminSessionsPageProps {
  /**
   * The menu key every section route carries, passed here for the reason it is passed to
   * every other section page and deliberately unread: the heading is this page's own
   * (`adminSessions.listTitle`), because a card repeating the menu item's name in large
   * letters would say the menu again instead of naming the page. The locale files say the
   * same above that key.
   */
  sectionKey: string
}

export default function AdminSessionsPage(_props: AdminSessionsPageProps) {
  const { t, i18n } = useTranslation()
  const { message } = App.useApp()

  /**
   * The five request parameters, the pager and the two headers - all of that is
   * `useServerTable`, which the four other server-paged tables call too. What this page adds
   * is the one thing a shared hook cannot know: the "When" column is keyed `when` for its
   * width and its handle, while the endpoint orders by `visitedAt`. The account column is a
   * join and the three geo columns are resolved from GeoIP while a page is built, so there
   * is nothing in the row for the endpoint to order by: `ip` is the only other key that can
   * arrive here, and anything that is not `ip` came from clicking "When".
   */
  const table = useServerTable<AdminSessionVisit, SessionSortField>({
    defaultSort: { by: 'visitedAt', order: 'desc' },
    sortFieldOf: (columnKey) => (columnKey === 'ip' ? 'ip' : 'visitedAt'),
  })

  /**
   * The account filter, which lives in the address rather than in this component's state -
   * the same decision the accounts table makes about `?groupId=`, and the same reasons: a
   * filter that only existed in memory would be gone by the time the page mounted, so a
   * "show this account" link would land on the unfiltered list and look like it had done
   * nothing. The query string is the single source, which also makes the address shareable
   * and a reload keep the filter, with no effect anywhere keeping the two in step.
   */
  const [searchParams, setSearchParams] = useSearchParams()
  const userFilter = searchParams.get('userId')
  /**
   * The one revoke this page makes, from `mutations.ts` - the token is minted inside it at
   * call time, never remembered: the server binds a token to the identity that was current
   * when it was minted, and one from before a refresh would be refused as `csrf_invalid`.
   *
   * The id whose button is in flight is read off the mutation itself, as one id rather than
   * a boolean, so the other rows stay clickable while one revoke runs - and every row of
   * that same session shows the spinner, which is the truth: the act is on the chain, not
   * on the row that was pressed.
   */
  const revokeSession = useRevokeAdminSession()
  const busySessionId = revokeSession.isPending
    ? (revokeSession.variables?.sessionId ?? null)
    : null

  /**
   * The page of visits, fetched through a query whose key **is** the parameters - the
   * replacement for the old `loading`/`loadError`/`cancelled`/`attempt` effect. A successful
   * revoke re-reads through `refetch()` rather than by bumping a counter, and it has to be a
   * re-read, because one revoke ends every row of that session.
   */
  const historyQuery = useAdminSessionHistory({ ...table.query, userId: userFilter })
  const { data: history, loadError, loading } = tableResults(historyQuery)

  /**
   * The zone, from the shared `['profile']` query - one fetch per installation rather than
   * a `getProfile()` effect here. A failure leaves the browser's zone in place and the list
   * still renders, which is the right degradation: the history matters and the exact zone it
   * is printed in does not.
   */
  const profileQuery = useProfile()
  const timeZone = profileQuery.data?.timeZone ?? null

  /**
   * The accounts the filter may name, as a query of their own.
   *
   * It asks the accounts endpoint for the ceiling the server allows rather than a second,
   * unpaged route existing for a picker - one route is one thing to keep correct, and the
   * count this call throws away is the same number the accounts table needs. It is also
   * the *same* permission the visits themselves are behind: both are narrowed per target
   * role, so an account this operator may not read can be neither filtered by nor listed.
   * A failure fails alone - no error banner, no retry button, no effect on the visits
   * below: a call that fails here fails for the same reason the visits themselves would,
   * and an alert saying so over a table that is about to say it too would be naming the
   * same fact twice. Past the ceiling the picker offers what it was given, and the visitor
   * narrows the table by the search beside it instead - which matches rows rather than
   * names, so an account the picker has never seen is still reachable by typing one of its
   * addresses.
   */
  const usersQuery = useAllUsers()
  const userOptions: UserOption[] = (usersQuery.data?.items ?? []).map((user) => ({
    value: user.id,
    label: user.username,
  }))

  /**
   * The seven widths: their state, their memory between visits, and the drag that changes
   * them. All of that lives in `useColumnWidths`, which the account and group tables call
   * too - the handle, the listeners and the clamp are the part of a table that reads as if
   * it were simple and is not, and two copies of those rules would be two places for one of
   * them to be quietly wrong where nothing in `typecheck`, `lint` or the build can tell.
   */
  const { widths, tableWidth, atDefaults, reset, resizer: handleFor } =
    useColumnWidths<AdminSessionColumnKey>({
      keys: COLUMN_KEYS,
      defaults: DEFAULT_COLUMN_WIDTHS,
      storageKey: COLUMN_WIDTHS_KEY,
    })

  /**
   * `resolvedLanguage` rather than `language`: the two differ whenever the requested language
   * has no bundle, and then every string falls back to English while `language` still reports
   * the language nobody is being shown. A page reading `Germany` next to `Германия` would be
   * exactly that disagreement, made visible in one cell.
   */
  const locale = i18n.resolvedLanguage ?? i18n.language

  /**
   * The account filter, starting the pager over: a different list is a different pager, and
   * leaving the visitor on page 3 of a filter that admits one page would show them an empty
   * table while `total` above it said 1.
   *
   * It writes to the address and with `replace`: a change to what is being looked at rather
   * than a step worth a history entry, so the back button takes the visitor off the page
   * instead of back through every account they tried.
   */
  const chooseUser = (value: string | undefined) => {
    const next = new URLSearchParams(searchParams)

    if (value) next.set('userId', value)
    else next.delete('userId')

    setSearchParams(next, { replace: true })
    table.resetPage()
  }

  /**
   * Ends the session behind one row, and asks the server for the page again.
   *
   * A re-read rather than a filter, and that is the whole decision: one revoke ends every
   * row of that session on every page, so dropping only the row that was pressed would
   * leave its rotations above and below showing a button that can no longer do anything.
   * The page number is kept, because a visitor on page 3 is still on page 3 - the list got
   * shorter, it did not move.
   */
  const endSession = async (sessionId: string) => {
    try {
      const result = await revokeSession.mutateAsync({ sessionId })

      message.success(textForCode(result.code) ?? t('adminSessions.revoked'))

      void historyQuery.refetch()
    } catch (error) {
      message.error(messageForError(error))
    }
  }

  /**
   * A date as the reading account's own zone and the page's own language show it, falling
   * back to the browser's zone when the stored one is absent or is one this ICU build
   * refuses to format in.
   */
  const formatMoment = (value: string): string => formatTimestamp(value, locale, timeZone)

  /**
   * One column's resize handle, with this page's name for the column.
   *
   * The hook owns the drag, the clamp and the stored widths, but only the page knows that a
   * column is called "When": the handle's accessible name is built here and handed over as
   * an argument, because a translation key is not something a generic hook can hold.
   */
  const resizer = (key: AdminSessionColumnKey): ResizableHeaderCellProps =>
    handleFor(key, t('adminSessions.resizeColumn', { column: t(`adminSessions.columns.${key}`) }))

  /**
   * The row's one action, and the two facts that stop it being possible.
   *
   * Drawn without the tooltip when it is live, because a tooltip repeating the button's own
   * label back at the visitor is noise - `hinted` is here for the greyed state, where the
   * reason a control cannot be pressed is the one thing it cannot say by itself.
   */
  const endAction = (visit: AdminSessionVisit) => {
    const button = (
      <Button
        danger
        size="small"
        disabled={!visit.isActive || visit.isCurrent}
        loading={busySessionId === visit.sessionId}
        onClick={() => void endSession(visit.sessionId)}
      >
        {t('adminSessions.end')}
      </Button>
    )

    if (visit.isActive && !visit.isCurrent) return button

    return hinted(
      visit.isActive ? t('adminSessions.endCurrentHint') : t('adminSessions.endHint'),
      button,
    )
  }

  const columns: TableColumnsType<AdminSessionVisit> = [
    {
      title: t('adminSessions.columns.user'),
      dataIndex: 'username',
      key: 'user',
      width: widths.user,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('user'),
      // The name rather than the identifier: this column answers "whose visit is this" and
      // a GUID answers nothing to a person scanning twenty rows. The identifier is one
      // filter away - the account picker above the table is the same value.
      render: dashedCell,
    },
    {
      title: t('adminSessions.columns.when'),
      key: 'when',
      width: widths.when,
      sorter: true,
      sortOrder: table.orderOf('visitedAt'),
      onHeaderCell: () => resizer('when'),
      render: (_, visit) => (
        <Space size={4} wrap>
          <span>{formatMoment(visit.visitedAt)}</span>
          {visit.isCurrent && (
            <Tag color="processing" style={{ marginInlineEnd: 0 }}>
              {t('adminSessions.current')}
            </Tag>
          )}
          {/* Only drawn for a row whose session is gone, and the reason it is drawn at all
              is that a greyed button below cannot tell "already ended" from "ended a second
              ago by whoever pressed it" - the two look identical on screen and are the same
              refusal to the API. */}
          {!visit.isActive && (
            <Tag style={{ marginInlineEnd: 0 }}>{t('adminSessions.ended')}</Tag>
          )}
        </Space>
      ),
    },
    {
      title: t('adminSessions.columns.ip'),
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
      // A name rather than the code GeoIP produces, for the reason the account's own
      // history gives: nobody reads `DE` as a country - they read Germany, or `Германия` on
      // a Russian page. The name is looked up where the names live (`countryName`, which is
      // the platform's own table), and a null is a dash rather than an error - a loopback
      // address or a missing `.mmdb` is unknown, not broken.
      title: t('adminSessions.columns.country'),
      dataIndex: 'countryCode',
      key: 'country',
      width: widths.country,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('country'),
      render: (value: string | null) => countryCell(value, locale),
    },
    {
      // The one column whose content the server does not bound. An organisation name is a
      // registry's idea of a company and can run to sixty characters, so this is where an
      // unbounded column would have widened the whole table - the width above and the
      // truncation below are what keep the answer to that in this cell and nowhere else.
      title: t('adminSessions.columns.network'),
      key: 'network',
      width: widths.network,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('network'),
      render: (_, visit) => networkCell(visit),
    },
    {
      // The summary replaces the raw string rather than joining it, and the tooltip carries
      // the raw string instead: twenty rows of `Mozilla/5.0 (Windows NT 10.0; Win64; x64)
      // ...` is a wall nobody scans, while `Chrome 140 (Windows 10, x64)` is one line per
      // row - and "is this the client I am looking for" is answered by the whole thing, on
      // hover, as before. A client the parser has no name for keeps its string untouched;
      // see `describeUserAgent`.
      title: t('adminSessions.columns.agent'),
      dataIndex: 'userAgent',
      key: 'agent',
      width: widths.agent,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('agent'),
      render: agentCell,
    },
    {
      title: t('adminSessions.columns.actions'),
      key: 'actions',
      width: widths.actions,
      onHeaderCell: () => resizer('actions'),
      render: (_, visit) => endAction(visit),
    },
  ]

  return (
    <Card
      title={t('adminSessions.listTitle')}
      // Pinned to one line so the *toolbar* is what yields when the row is too wide, for
      // the reason the accounts table pins its own: antd otherwise shrinks both halves and
      // the page's own heading wraps to two lines, which on a Full HD screen at 125%
      // scaling and with the longer Russian labels is exactly what happens. `Space wrap` in
      // `extra` then stacks the controls onto a second row - taller, but readable.
      styles={{ title: { whiteSpace: 'nowrap' } }}
      extra={
        <Space wrap>
          {/* Narrower than the history's own search, for the same budget the accounts
              table's toolbar spends: this row has to fit beside the card's own title on one
              Full HD screen, and in Russian it does not. What the search loses in room it
              keeps in its aria-label, which still names the whole field. */}
          <TableSearch
            value={table.searchText}
            onChange={table.setSearchText}
            onSearch={table.applySearch}
            labelKey="adminSessions.searchLabel"
            placeholderKey="adminSessions.searchPlaceholder"
            width={220}
          />
          <Select
            allowClear
            showSearch
            aria-label={t('adminSessions.filterUserLabel')}
            style={{ width: 180 }}
            value={userFilter ?? undefined}
            options={userOptions}
            placeholder={t('adminSessions.filterAnyUser')}
            optionFilterProp="label"
            onChange={chooseUser}
          />
          {/* Disabled rather than hidden when the widths are already the defaults: a control
              that appears and disappears reads as a broken one, and there is nothing to undo
              until somebody has actually dragged something. */}
          <Button disabled={atDefaults} onClick={() => reset()}>
            {t('adminSessions.resetWidths')}
          </Button>
        </Space>
      }
    >
      {loadError ? (
        <LoadErrorAlert
          message={loadError}
          loading={loading}
          onRetry={() => historyQuery.refetch()}
        />
      ) : (
        <Table<AdminSessionVisit>
          rowKey="id"
          columns={columns}
          dataSource={history?.items ?? []}
          loading={loading}
          // `ResizableHeaderCell` rather than the default cell: the handles are a sibling of
          // the header's own content, and the only way to put them there without nesting a
          // focusable span inside antd's sortable `<button>` is to own the cell. It is a
          // module-level function on purpose - a component type created inside this one would
          // be a new type on every repaint, and React would then unmount the handle
          // mid-drag, dropping the pointer with it.
          components={{ header: { cell: ResizableHeaderCell } }}
          scroll={{ x: tableWidth }}
          onChange={table.handleTableChange}
          locale={{
            emptyText: t(
              userFilter || table.query.search
                ? 'adminSessions.noMatches'
                : 'adminSessions.empty',
            ),
          }}
          pagination={{
            ...table.pagination,
            total: history?.total ?? 0,
            showTotal: (total, range) =>
              t('adminSessions.range', { from: range[0], to: range[1], total }),
          }}
        />
      )}
    </Card>
  )
}
