import { useEffect, useMemo, useState } from 'react'
import type { ReactElement, ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  Alert,
  App,
  Button,
  Card,
  Input,
  Select,
  Space,
  Table,
  Tag,
  Tooltip,
  Typography,
} from 'antd'
import type { TableColumnsType, TableProps } from 'antd'
import { useTranslation } from 'react-i18next'
import {
  CheckOutlined,
  DeleteOutlined,
  EditOutlined,
  LockOutlined,
  PlusOutlined,
  UnlockOutlined,
} from '@ant-design/icons'
import {
  confirmUserRegistration,
  deleteUser,
  getProfile,
  getUsers,
  setUserBlocked,
  setUserUnblocked,
} from '../lib/api'
import type { AdminUserSortField, AdminUserSortOrder } from '../lib/api'
import { getCsrfToken } from '../lib/csrf'
import { messageForError, textForCode } from '../lib/http'
import { areaForRole } from '../lib/session'
import { useSession } from '../lib/sessionContext'
import { formatTimestamp } from '../lib/dateFormat'
import ResizableHeaderCell from '../components/ResizableHeaderCell'
import type { ResizableHeaderCellProps } from '../components/ResizableHeaderCell'
import TruncatedCell from '../components/TruncatedCell'
import { useColumnWidths } from '../hooks/useColumnWidths'
import type { AdminUser, AdminUserList, MessageResponse, Role, UserStatus } from '../types'

/**
 * How many accounts one page holds. The server's own default, kept in step by hand because
 * the two are one contract written twice - and because a page size the pager draws from has
 * to be known before the first answer arrives, or the pager would show one row per page
 * until it did.
 */
const PAGE_SIZE = 20

/** The sizes the visitor may pick, in the same order the pager shows them. The server's ceiling is 100. */
const PAGE_SIZE_OPTIONS = [10, 20, 50, 100]

/** The eight columns, in the order they are drawn, named by their own keys. */
type UsersColumnKey =
  | 'id'
  | 'username'
  | 'email'
  | 'role'
  | 'status'
  | 'lastSeenAt'
  | 'ip'
  | 'actions'

const COLUMN_KEYS: readonly UsersColumnKey[] = [
  'id',
  'username',
  'email',
  'role',
  'status',
  'lastSeenAt',
  'ip',
  'actions',
]

/**
 * The width each column starts at, and the width it goes back to when asked to reset.
 *
 * Every column has a width of its own rather than letting the browser work it out, because
 * a column with no width grows to fit its widest cell - and the widest cell here is an
 * email address, which the server bounds at 254 characters. Fixed layout plus a width per
 * column is what keeps that length inside its own cell instead of dragging the whole table
 * sideways; the widths are a starting point, not the answer (see `useColumnWidths`).
 *
 * The three content columns are kept deliberately narrow - the whole table sums to 1230 -
 * so that the table, the sidebar and the card's own padding still fit one Full HD screen
 * without the action buttons at the far right falling outside the viewport. What those
 * columns lose in room they keep in their tooltips: an address or a visit is never *cut*,
 * only shown shortened until you look at it.
 */
const DEFAULT_COLUMN_WIDTHS: Record<UsersColumnKey, number> = {
  id: 120,
  username: 170,
  email: 200,
  role: 130,
  status: 150,
  lastSeenAt: 180,
  ip: 130,
  actions: 150,
}

/** Where this table remembers the widths the visitor gave its columns. */
const COLUMN_WIDTHS_KEY = 'fluxy.users.columnWidths'

/** The three roles and three states, in the order the server declares them - the two filters read from these. */
const ROLES: Role[] = ['Client', 'Reseller', 'Admin']
const STATUSES: UserStatus[] = ['Unregistered', 'Registered', 'Blocked']

/**
 * The three states as colours.
 *
 * `UserStatus` is not an ordered scale - states one account moves between, not levels of
 * access - so these are three descriptions rather than three steps of one ramp: grey for a
 * row that has not confirmed itself, green for a working account, red for one that is shut
 * off. Nothing here compares one to another.
 */
const STATUS_COLOR: Record<UserStatus, 'default' | 'success' | 'error'> = {
  Unregistered: 'default',
  Registered: 'success',
  Blocked: 'error',
}

/**
 * A tooltip around a row action that also works while the button is disabled.
 *
 * A native `<button disabled>` swallows the pointer events a tooltip listens for, and two of
 * these buttons are disabled *on purpose* - the ones this server refuses for the account the
 * page is signed in with (`cannot_block_self`, `cannot_delete_self`). The span is what keeps
 * the reason reachable: a greyed button that says nothing is a dead control, and the whole
 * point of disabling it rather than letting the refusal arrive as a toast was to say why
 * before the click.
 */
const hinted = (title: string, button: ReactNode): ReactElement => (
  <Tooltip title={title}>
    <span style={{ display: 'inline-flex' }}>{button}</span>
  </Tooltip>
)

/**
 * The administrator's account list - the page behind `nav.items.usersManage`, headed by
 * `users.listTitle` rather than by that item's name (the props below say why).
 *
 * Filtering, sorting and paging are all **server side**, and `total` is the reason: a filter
 * applied in the browser to the rows already on screen would narrow one page while the pager
 * still counted everything (four pages of five rows when the filter admitted one), which is
 * the same lie at both ends that the visit history refuses to tell. So every change goes back
 * to `GET /admin/users` with its parameters, any change that is not a page turn resets to
 * page 1, and the six sortable headers are **controlled** - the server always applies an
 * order, and a header showing no arrow over rows arriving A-to-Z would describe a table that
 * does not exist.
 *
 * The columns follow `SessionsPage` down to the shared `ResizableHeaderCell`,
 * `TruncatedCell` and `useColumnWidths`: two tables on one page of the same application
 * having different rules for a resize handle would be two places for one of those rules to
 * be quietly wrong.
 *
 * Last visit and address come from the server's own answer (`lastSeenAt`, `lastIp` - the
 * freshest refresh token of that account), printed in the reading account's time zone for
 * the reason the history prints its dates there: the profile page promises "Dates and times
 * are shown in this zone", and a table that answered in the browser's would be the one place
 * that promise visibly failed. A never-signed-in account draws `—`, because a missing visit
 * is a fact about the account rather than an error.
 *
 * Three of the four row actions are transitions the server owns (`confirm-registration`,
 * `block`, `unblock`) - it stamps and clears `registeredAt`, revokes sessions, decides which
 * state an unblock returns to - so each is a request and a re-read rather than a status
 * edited here. Only deletion asks first, because only deletion cannot be undone.
 */
interface UsersPageProps {
  /**
   * The menu key every section route carries, passed here for the reason it is passed to
   * every other section page and deliberately unread: the heading is this page's own
   * (`users.listTitle`), because a card repeating the menu item's name in large letters
   * would say the menu again instead of naming the page. The locale files say the same
   * above `users.listTitle`.
   */
  sectionKey: string
}

export default function UsersPage(_props: UsersPageProps) {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const { message, modal } = App.useApp()
  const session = useSession()

  const [users, setUsers] = useState<AdminUserList | null>(null)
  const [timeZone, setTimeZone] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(PAGE_SIZE)
  // What is typed and what the server has been asked are two different things: one request
  // per keystroke would be a request per character of an email address, and the answer to
  // the first nine would arrive after the tenth and put a stale page on screen.
  const [searchText, setSearchText] = useState('')
  const [search, setSearch] = useState('')
  const [roleFilter, setRoleFilter] = useState<Role | null>(null)
  const [statusFilter, setStatusFilter] = useState<UserStatus | null>(null)
  const [sortBy, setSortBy] = useState<AdminUserSortField>('username')
  const [sortOrder, setSortOrder] = useState<AdminUserSortOrder>('asc')
  // The row whose action is in flight - its buttons show a spinner and refuse a second
  // click, and nothing else about the page changes until the answer lands.
  const [busyId, setBusyId] = useState<string | null>(null)
  // Bumped by the retry button and by every successful action, so one re-read of the list
  // is all a row transition or a dropped connection needs.
  const [attempt, setAttempt] = useState(0)

  /**
   * The eight widths: their state, their memory between visits, and the drag that changes
   * them - all of it `useColumnWidths`, the same hook the visit history calls. The handle,
   * the listeners and the clamp are the part of a table that reads as if it were simple and
   * is not, and two copies of those rules would be two places for one of them to be wrong
   * where nothing in `typecheck`, `lint` or the build can tell.
   */
  const { widths, tableWidth, atDefaults, reset, resizer: handleFor } =
    useColumnWidths<UsersColumnKey>({
      keys: COLUMN_KEYS,
      defaults: DEFAULT_COLUMN_WIDTHS,
      storageKey: COLUMN_WIDTHS_KEY,
    })

  /**
   * `resolvedLanguage` rather than `language`: the two differ whenever the requested language
   * has no bundle, and then every string falls back to English while `language` still reports
   * the language nobody is being shown - a date in one language beside a role name in another
   * would be that disagreement, made visible in one row.
   */
  const locale = i18n.resolvedLanguage ?? i18n.language

  // The area this page hangs from, for the row edit button and the way to the add form.
  // `null` only under the probes, which render no provider: `RequireAuth` publishes one for
  // every visitor this page can have, and without it the links are inert rather than wrong.
  const area = session ? areaForRole(session.role) : ''

  /**
   * The account this page is being read with. Two of the four row actions are refused for it
   * on the server, so their buttons are disabled here - the same "the UI tells the truth the
   * API would tell" rule the current session's button follows on the active sessions page.
   */
  const ownId = session?.userId ?? null

  useEffect(() => {
    // The zone is read for the dates below, not for this page's own state: a failure here
    // leaves the browser's zone in place and the list still renders, which is the right
    // degradation - the accounts matter and the exact zone their visits are printed in do not.
    let cancelled = false

    getProfile()
      .then((profile) => {
        if (!cancelled) setTimeZone(profile.timeZone)
      })
      .catch(() => {})

    return () => {
      cancelled = true
    }
  }, [])

  useEffect(() => {
    // A generation of one per effect run rather than one shared counter: an answer from the
    // page the visitor just left must not overwrite the page they moved to, and an effect
    // that is torn down mid-flight must not write into a table that has drawn its next row.
    let cancelled = false

    setLoading(true)

    getUsers({
      page,
      pageSize,
      search,
      role: roleFilter,
      status: statusFilter,
      sortBy,
      sortOrder,
    })
      .then((answer) => {
        if (cancelled) return
        setUsers(answer)
        setLoadError(null)
      })
      .catch((error) => {
        if (cancelled) return
        setUsers(null)
        setLoadError(messageForError(error))
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [page, pageSize, search, roleFilter, statusFilter, sortBy, sortOrder, attempt])

  /**
   * Applies what is in the search box, from the field itself rather than from a button, so
   * the empty box empties the list the moment it becomes empty.
   */
  const applySearch = (value: string) => {
    const next = value.trim()
    const current = search.trim()

    if (next === current) return

    setSearch(next)
    setPage(1)
  }

  /**
   * The two filters, each starting the pager over: a different list is a different pager,
   * and leaving the visitor on page 3 of a filter that admits one page would show them an
   * empty table while `total` above it said 1.
   */
  const chooseRole = (value: Role | undefined) => {
    setRoleFilter(value ?? null)
    setPage(1)
  }

  const chooseStatus = (value: UserStatus | undefined) => {
    setStatusFilter(value ?? null)
    setPage(1)
  }

  /**
   * Reads the pager and the sortable headers together, because antd reports both through one
   * handler and the two behave differently: a new page keeps the order, a new order starts
   * the pages over.
   */
  const handleTableChange: TableProps<AdminUser>['onChange'] = (
    pagination,
    _filters,
    sorter,
    extra,
  ) => {
    const nextPageSize = pagination.pageSize ?? PAGE_SIZE

    if (nextPageSize !== pageSize) {
      setPageSize(nextPageSize)
      setPage(1)
      return
    }

    if (extra.action === 'paginate') {
      setPage(pagination.current ?? 1)
      return
    }

    setPage(1)

    const single = Array.isArray(sorter) ? sorter[0] : sorter

    if (!single?.order) {
      // The header was clicked through its sorted state to none, which is the visitor asking
      // for no order in particular. The default is then what the server is being sent, and the
      // header has to say so - a table quietly applying its default order while no arrow is
      // showing would tell them the column is unsorted when it is not.
      setSortBy('username')
      setSortOrder('asc')
      return
    }

    // The sortable columns are keyed by the server's own field names, so the key *is* the
    // field and no mapping table exists to get wrong. The two unsortable columns (`id`,
    // `actions`) never produce a sorter event at all - `sorter` is only set on the six that
    // the endpoint can order by - which is why the cast below never sees them.
    setSortBy(single.columnKey as AdminUserSortField)
    setSortOrder(single.order === 'ascend' ? 'asc' : 'desc')
  }

  /**
   * A date as this account's own zone and the page's own language show it, falling back to
   * the browser's zone when the stored one is absent or is one this ICU build refuses to
   * format in.
   */
  const formatMoment = (value: string): string => formatTimestamp(value, locale, timeZone)

  /** A value the server could not determine, drawn as a dash rather than as an empty cell. */
  const orDash = (value: string | number | null | undefined): string =>
    value === null || value === undefined || value === '' ? '—' : String(value)

  /**
   * Which order a column is in, or nothing when it is not the one the page is sorted by.
   *
   * Controlled rather than left to antd: the server always applies an order, so a header
   * that showed no arrow while rows arrived A-to-Z would be describing a table that does not
   * exist. Six of the eight can be sorted - the other two are not fields the endpoint orders
   * by, and a header that flipped its arrow and changed nothing would be worse than a header
   * with no arrow at all.
   */
  const orderOf = (key: AdminUserSortField) =>
    sortBy === key ? (sortOrder === 'asc' ? 'ascend' : 'descend') : null

  /**
   * One column's resize handle, with this page's name for the column.
   *
   * The hook owns the drag, the clamp and the stored widths, but only the page knows what a
   * column is called: the handle's accessible name is built here and handed over as an
   * argument, because a translation key is not something a generic hook can hold.
   */
  const resizer = (key: UsersColumnKey): ResizableHeaderCellProps =>
    handleFor(key, t('users.resizeColumn', { column: t(`users.columns.${key}`) }))

  /**
   * One row action, run and reported the way every other page reports one: the server's own
   * sentence on success, `messageForError` on a refusal, and the list **re-read** afterwards
   * rather than patched here - a transition changes what the server considers the row to be
   * (a stamped `registeredAt`, revoked sessions), and the page draws what it is told, not
   * what it guesses.
   */
  const runAction = async (
    action: (args: { id: string; csrfToken: string | null }) => Promise<MessageResponse>,
    user: AdminUser,
  ) => {
    setBusyId(user.id)

    try {
      const result = await action({ id: user.id, csrfToken: await getCsrfToken() })
      message.success(textForCode(result.code) ?? result.message)
      setAttempt((value) => value + 1)
    } catch (error) {
      message.error(messageForError(error))
    } finally {
      setBusyId(null)
    }
  }

  /**
   * Asks before deleting, because this is the only action here that cannot be undone: the
   * other three move a row between states, this removes the row and takes its sessions, its
   * login-guard rules and its pending changes with it. No password step and no captcha - the
   * admin session plus a fresh antiforgery token are the gate, the same as every other write
   * on this page - but a question first, because one misread row is not recoverable.
   */
  const confirmDelete = (user: AdminUser) => {
    modal.confirm({
      title: t('users.deleteConfirmTitle'),
      content: t('users.deleteConfirmBody', { name: user.username }),
      okText: t('users.deleteConfirmOk'),
      okButtonProps: { danger: true },
      cancelText: t('actions.cancel'),
      onOk: async () => {
        setBusyId(user.id)

        try {
          const csrfToken = await getCsrfToken()
          const result = await deleteUser({ id: user.id, csrfToken })

          message.success(textForCode(result.code) ?? result.message)

          // The last row of a later page: with it gone, that page no longer exists and the
          // server would answer it empty - a blank table under a pager still offering the
          // page the visitor is standing on. Back a page instead, which re-runs the load
          // effect anyway, so the list and the pager agree again without a second call.
          if (users?.items.length === 1 && page > 1) setPage(page - 1)
          else setAttempt((value) => value + 1)
        } catch (error) {
          message.error(messageForError(error))
        } finally {
          setBusyId(null)
        }
      },
    })
  }

  const columns: TableColumnsType<AdminUser> = [
    {
      // Eight characters of the identifier and the rest on hover. A full GUID is 36
      // characters of hex that nobody reads - what this column is for is telling two rows
      // apart and picking one to edit - so the short form is what the column shows and the
      // tooltip is what makes it the *same* identifier rather than a different, truncated one.
      title: t('users.columns.id'),
      dataIndex: 'id',
      key: 'id',
      width: widths.id,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('id'),
      render: (value: string) => <TruncatedCell text={`${value.slice(0, 8)}…`} full={value} />,
    },
    {
      // Bounded at 20 characters by the server, but 20 characters still do not fit the
      // column this table starts at - so the same truncation-plus-tooltip the unbounded
      // columns use, for consistency rather than for need.
      title: t('users.columns.username'),
      dataIndex: 'username',
      key: 'username',
      width: widths.username,
      sorter: true,
      sortOrder: orderOf('username'),
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('username'),
      render: (value: string) => <TruncatedCell text={value} full={value} />,
    },
    {
      // The column with the widest natural content on the page: an address is bounded at 254
      // characters, which no table should ever be asked to grow to. The width above and the
      // truncation below keep that length inside this cell and nowhere else.
      title: t('users.columns.email'),
      dataIndex: 'email',
      key: 'email',
      width: widths.email,
      sorter: true,
      sortOrder: orderOf('email'),
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('email'),
      render: (value: string) => <TruncatedCell text={value} full={value} />,
    },
    {
      title: t('users.columns.role'),
      dataIndex: 'role',
      key: 'role',
      width: widths.role,
      sorter: true,
      sortOrder: orderOf('role'),
      onHeaderCell: () => resizer('role'),
      render: (value: Role) => t(`profile.roles.${value}`),
    },
    {
      // A tag rather than coloured text: three states of one account read as three labels
      // next to each other down a column, which is what a scanning administrator is looking
      // for - and the colour is a description, not a rank (`UserStatus` is not an ordered
      // scale, so nothing here says one state is *more* than another).
      title: t('users.columns.status'),
      dataIndex: 'status',
      key: 'status',
      width: widths.status,
      sorter: true,
      sortOrder: orderOf('status'),
      onHeaderCell: () => resizer('status'),
      render: (value: UserStatus) => (
        <Tag color={STATUS_COLOR[value]} style={{ marginInlineEnd: 0 }}>
          {t(`users.statuses.${value}`)}
        </Tag>
      ),
    },
    {
      // Never rather than an error: an account that has never signed in has no last visit,
      // and the column says so with the same dash a value the server could not determine
      // gets. The ordering of the nulls is the server's (`ChainNullsLast`) - both directions
      // end with these rows rather than opening with them.
      title: t('users.columns.lastSeenAt'),
      dataIndex: 'lastSeenAt',
      key: 'lastSeenAt',
      width: widths.lastSeenAt,
      sorter: true,
      sortOrder: orderOf('lastSeenAt'),
      onHeaderCell: () => resizer('lastSeenAt'),
      render: (value: string | null) => (value ? formatMoment(value) : '—'),
    },
    {
      // The address of that same visit, in its own column rather than beside the time: the
      // two are what an administrator scans together ("same evening, different network"),
      // and both are sortable because both are fields the endpoint orders by.
      title: t('users.columns.ip'),
      dataIndex: 'lastIp',
      key: 'ip',
      width: widths.ip,
      sorter: true,
      sortOrder: orderOf('ip'),
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('ip'),
      render: (value: string | null) => (
        <Typography.Text code>
          <TruncatedCell text={orDash(value)} full={value} />
        </Typography.Text>
      ),
    },
    {
      // The four actions as icons with tooltips - icons carry no text of their own, so the
      // name each one gets in the locale files is the only thing that says what pressing it
      // does. Two of them appear conditionally: confirming is only a move an `Unregistered`
      // row can make, and block/unblock are one button that means the opposite depending on
      // which side of the state the row is on.
      title: t('users.columns.actions'),
      key: 'actions',
      width: widths.actions,
      onHeaderCell: () => resizer('actions'),
      render: (_, user) => {
        const isSelf = user.id === ownId
        const busy = busyId === user.id

        return (
          <Space size={4}>
            {hinted(
              t('users.actions.edit'),
              <Button
                type="text"
                size="small"
                icon={<EditOutlined />}
                onClick={() => navigate(`${area}/users/${user.id}`)}
              />,
            )}

            {user.status === 'Unregistered' &&
              hinted(
                t('users.actions.confirm'),
                <Button
                  type="text"
                  size="small"
                  icon={<CheckOutlined />}
                  loading={busy}
                  disabled={busy}
                  onClick={() => runAction(confirmUserRegistration, user)}
                />,
              )}

            {user.status === 'Blocked' ? (
              hinted(
                t('users.actions.unblock'),
                <Button
                  type="text"
                  size="small"
                  icon={<UnlockOutlined />}
                  loading={busy}
                  disabled={busy}
                  onClick={() => runAction(setUserUnblocked, user)}
                />,
              )
            ) : (
              hinted(
                isSelf ? t('users.selfProtected') : t('users.actions.block'),
                <Button
                  type="text"
                  size="small"
                  icon={<LockOutlined />}
                  disabled={isSelf || busy}
                  onClick={() => runAction(setUserBlocked, user)}
                />,
              )
            )}

            {hinted(
              isSelf ? t('users.selfProtected') : t('users.actions.delete'),
              <Button
                danger
                type="text"
                size="small"
                icon={<DeleteOutlined />}
                disabled={isSelf || busy}
                onClick={() => confirmDelete(user)}
              />,
            )}
          </Space>
        )
      },
    },
  ]

  const roleOptions = useMemo(
    () => ROLES.map((role) => ({ value: role, label: t(`profile.roles.${role}`) })),
    [t],
  )

  const statusOptions = useMemo(
    () => STATUSES.map((status) => ({ value: status, label: t(`users.statuses.${status}`) })),
    [t],
  )

  return (
    <Card
      title={t('users.listTitle')}
      extra={
        <Space wrap>
          <Input.Search
            allowClear
            aria-label={t('users.searchLabel')}
            placeholder={t('users.searchPlaceholder')}
            style={{ width: 260 }}
            value={searchText}
            onChange={(event) => {
              setSearchText(event.target.value)

              // An emptied box empties the list straight away. Waiting for Enter would mean
              // the visitor clears the field, sees the old filter still applied, and has to
              // press something they just finished typing into.
              if (event.target.value === '') applySearch('')
            }}
            onSearch={applySearch}
          />
          <Select
            allowClear
            aria-label={t('users.columns.role')}
            style={{ width: 150 }}
            value={roleFilter ?? undefined}
            options={roleOptions}
            placeholder={t('users.filterAnyRole')}
            onChange={chooseRole}
          />
          <Select
            allowClear
            aria-label={t('users.columns.status')}
            style={{ width: 165 }}
            value={statusFilter ?? undefined}
            options={statusOptions}
            placeholder={t('users.filterAnyStatus')}
            onChange={chooseStatus}
          />
          {/* Disabled rather than hidden when the widths are already the defaults: a control
              that appears and disappears reads as a broken one, and there is nothing to undo
              until somebody has actually dragged something. */}
          <Button disabled={atDefaults} onClick={() => reset()}>
            {t('users.resetWidths')}
          </Button>
          {/* The same address the sidebar's own "Add user" opens, offered here because this
              is the screen from which adding one is a decision: the list is where "there is
              no account for that person yet" becomes visible. */}
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => navigate(`${area}/users/add`)}
          >
            {t('nav.items.usersAdd')}
          </Button>
        </Space>
      }
    >
      {loadError ? (
        <Alert
          type="error"
          showIcon
          message={loadError}
          action={
            <Button size="small" onClick={() => setAttempt((value) => value + 1)} loading={loading}>
              {t('actions.retry')}
            </Button>
          }
        />
      ) : (
        <Table<AdminUser>
          rowKey="id"
          columns={columns}
          dataSource={users?.items ?? []}
          loading={loading}
          // `ResizableHeaderCell` rather than the default cell: the handles are a sibling of
          // the header's own content, and the only way to put them there without nesting a
          // focusable span inside antd's sortable `<button>` is to own the cell. It is a
          // module-level function on purpose - a component type created inside this one would
          // be a new type on every repaint, and React would then unmount the handle mid-drag,
          // dropping the pointer with it.
          components={{ header: { cell: ResizableHeaderCell } }}
          scroll={{ x: tableWidth }}
          onChange={handleTableChange}
          locale={{
            emptyText: t(
              roleFilter || statusFilter || search ? 'users.noMatches' : 'users.empty',
            ),
          }}
          pagination={{
            current: page,
            pageSize,
            total: users?.total ?? 0,
            showSizeChanger: true,
            pageSizeOptions: PAGE_SIZE_OPTIONS,
            showTotal: (total, range) => t('users.range', { from: range[0], to: range[1], total }),
          }}
        />
      )}
    </Card>
  )
}
