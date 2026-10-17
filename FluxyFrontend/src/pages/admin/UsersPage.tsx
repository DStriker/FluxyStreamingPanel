import { useMemo, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { App, Button, Card, Dropdown, Modal, Select, Space, Tag } from 'antd'
import type { MenuProps, TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import {
  CheckOutlined,
  DeleteOutlined,
  EditOutlined,
  LockOutlined,
  MoreOutlined,
  PlusOutlined,
  TeamOutlined,
  UnlockOutlined,
} from '@ant-design/icons'
import type { AdminUserSortField, BulkUserAction } from '../../features/users/api'
import { hinted } from '../../lib/hinted'
import { canEdit, canEditAny } from '../../lib/permissions'
import { areaForRole } from '../../lib/session'
import { useSession } from '../../lib/sessionContext'
import { formatTimestamp } from '../../lib/dateFormat'
import { tableResults } from '../../lib/tableResults'
import { idCell, ipCell, textCell } from '../../lib/tableCells'
import { enumParam, withFilter } from '../../lib/tableParams'
import LastSeenCell from '../../features/users/components/LastSeenCell'
import { RowActions, ServerTable, TableSearch } from '../../components/table'
import type { ResizableHeaderCellProps, RowAction } from '../../components/table'
import { useProfile } from '../../features/profile/hooks/queries'
import { useAllUserGroups } from '../../features/user-groups/hooks/queries'
import {
  useBulkUsers,
  useConfirmUserRegistration,
  useDeleteUser,
  useSetUserBlocked,
  useSetUserUnblocked,
} from '../../features/users/hooks/mutations'
import { useUsers } from '../../features/users/hooks/queries'
import { useBulkAction, useColumnWidths, useRowAction, useServerTable } from '../../hooks/table'
import type { AdminUser, UserStatus } from '../../types'

/** The eight columns, in the order they are drawn, named by their own keys. */
type UsersColumnKey =
  | 'id'
  | 'username'
  | 'email'
  | 'group'
  | 'status'
  | 'lastSeenAt'
  | 'ip'
  | 'actions'

const COLUMN_KEYS: readonly UsersColumnKey[] = [
  'id',
  'username',
  'email',
  'group',
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
 * The three content columns are kept deliberately narrow - these eight sum to 1190, and the
 * selection column below takes the total back to the same **1230** the table has always
 * been - so that the table, the sidebar and the card's own padding still fit one Full HD
 * screen without the action buttons at the far right falling outside the viewport. What
 * those columns lose in room they keep in their tooltips: an address or a visit is never
 * *cut*, only shown shortened until you look at it.
 *
 * The identifier column is the narrowest of the eight at 100 rather than the 130 it started
 * at: it draws nine characters (eight of the GUID and an ellipsis) and nothing else, and the
 * twenty it gave up are what let `group` show `Administrators` whole. Widening one costs
 * visible table room; shrinking one that only ever prints nine characters does not.
 *
 * `email` is 160 rather than the 200 it was before the selection column arrived, and the
 * forty it gave up are exactly the forty that column takes - the checkbox is paid for by the
 * one cell that truncates into a tooltip anyway, rather than by the table growing or by one
 * of the narrower columns losing a third of its width.
 */
const DEFAULT_COLUMN_WIDTHS: Record<UsersColumnKey, number> = {
  id: 100,
  username: 170,
  email: 160,
  group: 150,
  status: 150,
  lastSeenAt: 180,
  ip: 130,
  actions: 150,
}

/** Where this table remembers the widths the visitor gave its columns. */
const COLUMN_WIDTHS_KEY = 'fluxy.users.columnWidths'

/** The three states, in the order the server declares them - the status filter reads from these. */
const STATUSES: UserStatus[] = ['Unregistered', 'Registered', 'Blocked']

/**
 * One entry of the group filter: the identifier the query string needs and the name the
 * visitor reads. Both are kept here rather than derived at each render, because the value
 * and the label come from the same row and splitting them would be a second place for the
 * two to drift apart.
 */
interface GroupOption {
  value: string
  label: string
}

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
  const { modal } = App.useApp()
  const session = useSession()

  /**
   * The five request parameters, the pager and the six sortable headers - all of that is
   * `useServerTable`, which the other server-paged tables call too. The mapping needs no
   * help here: the sortable columns are keyed by the server's own field names, so a click
   * hands its own key over as the field. `id` and `actions` have no sorter and never produce
   * an event at all.
   */
  const table = useServerTable<AdminUser, AdminUserSortField>({
    defaultSort: { by: 'username', order: 'asc' },
    sortFields: ['username', 'email', 'group', 'status', 'lastSeenAt', 'ip'],
  })

  /**
   * The two filters, both in the address rather than in this component's state.
   *
   * `?groupId=` is how the groups table sends a visitor over here (its "show accounts"
   * button), and a filter that only existed in memory would be gone by the time the page
   * mounted - the button would land on the unfiltered list and look like it had done
   * nothing. `?status=` follows it for the same reason everything the table shows does: the
   * address is the single source, so it is shareable and a reload keeps the filter, with no
   * effect anywhere keeping two copies in step. `enumParam` judges the status by the values
   * this page's own control can produce, so a hand-edited word draws the unfiltered table
   * rather than a `validation_failed` from the endpoint.
   */
  const [searchParams, setSearchParams] = useSearchParams()
  const groupFilter = searchParams.get('groupId') || null
  const statusFilter = enumParam(searchParams, 'status', STATUSES)
  /** The move dialog, and the group chosen in it. Separate from the selection because it outlives the menu that opened it. */
  const [assignOpen, setAssignOpen] = useState(false)
  const [assignGroupId, setAssignGroupId] = useState<string | null>(null)

  /**
   * The page of accounts, fetched through a query whose key **is** the parameters -
   * replacing the old `loading`/`loadError`/`cancelled`/`attempt` effect, in which the key
   * changing and the request going out were two things an effect had to reconcile by hand.
   * A failure is the query's own `error`; the retry button and every successful action
   * re-read through `refetch()` rather than by bumping a counter.
   */
  const usersQuery = useUsers({
    ...table.query,
    groupId: groupFilter,
    status: statusFilter,
  })
  const { data: users, loadError, loading } = tableResults(usersQuery)

  /**
   * The reading account's zone and grants, answered by the shared `['profile']` query -
   * one fetch per installation instead of a `getProfile()` effect per page.
   *
   * The grant set answers a question the role in the token cannot: `viewUsers` and
   * `editUsers` are gone, and the four write actions below ask about the **row's** role
   * rather than about accounts in general. An empty set after a failed profile call is the
   * safe reading: the API refuses regardless, and a grey button with a reason is a better
   * first paint than a live one that answers 403.
   *
   * Deliberately not `GET /auth/me` - the guest guard polls that on every navigation, and
   * this is a database read the guard has no use for.
   */
  const profileQuery = useProfile()
  const timeZone = profileQuery.data?.timeZone ?? null
  const granted = profileQuery.data?.permissions ?? []

  /**
   * The groups the filter may name, as a query of their own rather than an effect.
   *
   * Kept out of the accounts request on purpose: this is a **different** permission
   * (`viewUserGroups`, not the per-role `view*` permissions the account rows need), so an
   * administrator whose group may read accounts but not groups would have the whole accounts
   * page fail with `403 permission_denied` if the two shared one call. As it is, that
   * visitor's filter simply offers nothing to pick, which is the truth - a group they may not
   * see is not one they could have filtered by.
   */
  const groupsQuery = useAllUserGroups()
  const groupOptions: GroupOption[] = (groupsQuery.data?.items ?? []).map((group) => ({
    value: group.id,
    label: group.name,
  }))

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

  /**
   * The selection and the run over it: the ids, the scope, the prune, the busy flag and the
   * summary dialog are `useBulkAction`'s, which the group table calls too.
   */
  const { selectedIds, setSelectedIds, bulkBusy, applyBulk } = useBulkAction({
    rows: users?.items,
    refetch: () => void usersQuery.refetch(),
    partialTitleKey: 'users.bulk.partialTitle',
  })

  /**
   * The two filters, each one address and one navigation: `withFilter` writes the filter and
   * drops `page` in the same write - a different list is a different pager, and leaving the
   * visitor on page 3 of a filter that admits one page would show them an empty table while
   * `total` above it said 1.
   *
   * `replace` because a change to what is being looked at is not a step worth a history
   * entry, so the back button takes the visitor off the page instead of back through every
   * group they tried.
   */
  const chooseGroup = (value: string | undefined) =>
    setSearchParams(withFilter(searchParams, 'groupId', value), { replace: true })

  const chooseStatus = (value: UserStatus | undefined) =>
    setSearchParams(withFilter(searchParams, 'status', value), { replace: true })

  /**
   * A date as this account's own zone and the page's own language show it, falling back to
   * the browser's zone when the stored one is absent or is one this ICU build refuses to
   * format in.
   */
  const formatMoment = (value: string): string => formatTimestamp(value, locale, timeZone)

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
   * One row action, and the id whose own buttons spin while it runs - the reporting and the
   * re-read are `useRowAction`'s, which the group table calls too. The writes behind the
   * actions are `mutations.ts`'s, one hook per endpoint, so none of them mints a token here.
   */
  const confirmRegistration = useConfirmUserRegistration()
  const setBlocked = useSetUserBlocked()
  const setUnblocked = useSetUserUnblocked()
  const removeUser = useDeleteUser()
  const bulk = useBulkUsers()

  const { busyId, runAction, confirmDelete: askDelete } = useRowAction<AdminUser>({
    refetch: () => void usersQuery.refetch(),
    rows: users?.items,
    page: table.page,
    setPage: table.setPage,
  })

  /**
   * Asks before deleting, because this is the only action here that cannot be undone: the
   * other three move a row between states, this removes the row and takes its sessions, its
   * login-guard rules and its pending changes with it. No password step and no captcha - the
   * admin session plus a fresh antiforgery token are the gate, the same as every other write
   * on this page - but a question first, because one misread row is not recoverable.
   *
   * The question itself, the re-read and the step back off an emptied page are
   * `useRowAction`'s; what is left here is the wording and the write.
   */
  const confirmDelete = (user: AdminUser) =>
    askDelete(user, {
      name: user.username,
      titleKey: 'users.deleteConfirmTitle',
      bodyKey: 'users.deleteConfirmBody',
      okKey: 'users.deleteConfirmOk',
      run: () => removeUser.mutateAsync({ id: user.id }),
    })

  /** Closes the move dialog, whether its run landed, was refused or was cancelled. */
  const closeAssign = () => {
    setAssignOpen(false)
    setAssignGroupId(null)
  }

  /**
   * Runs one operation over the selection: the ids, the reporting and the re-read are
   * `useBulkAction`'s and the request is `useBulkUsers`'s. What is left here is which
   * endpoint each action names - `groupId` belongs to `assign-group` alone and is what the
   * move dialog hands over; the other four never see it - and the dialog that closes on any
   * answer, because once the answer has arrived there is nothing left to ask.
   */
  const runBulk = (action: BulkUserAction, groupId: string | null = null) =>
    applyBulk(
      () => bulk.mutateAsync({ ids: selectedIds.map(String), action, groupId }),
      closeAssign,
    )

  /**
   * Asks before a bulk deletion, for the reason only deletion asks first on a single row:
   * it is the one operation here that cannot be undone, and the question is about the whole
   * selection at once - so the body names the count rather than a name, because there is no
   * one row to name.
   */
  const confirmBulkDelete = () => {
    modal.confirm({
      title: t('users.bulk.deleteTitle', { count: selectedIds.length }),
      content: t('users.bulk.deleteBody'),
      okText: t('users.deleteConfirmOk'),
      okButtonProps: { danger: true },
      cancelText: t('actions.cancel'),
      onOk: () => runBulk('delete'),
    })
  }

  /**
   * What the toolbar's menu offers. The labels are the row actions' own, because this is the
   * same operation over more than one row and a second name for one thing is a second name.
   *
   * The move is the one entry with a label of its own: it does not run, it opens the dialog,
   * and it is the only one that is dropped rather than greyed when it cannot be obeyed - the
   * dialog's list *is* the group list, and an operator whose group may not read groups has
   * no list to choose from, so offering the entry would be offering a dialog with a disabled
   * confirm button and nothing else.
   */
  const onBulkMenu: MenuProps['onClick'] = ({ key }) => {
    if (key === 'assign-group') {
      setAssignOpen(true)
      return
    }

    if (key === 'delete') {
      confirmBulkDelete()
      return
    }

    void runBulk(key as BulkUserAction)
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
      render: idCell,
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
      sortOrder: table.orderOf('username'),
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('username'),
      render: textCell,
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
      sortOrder: table.orderOf('email'),
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('email'),
      render: textCell,
    },
    {
      // The group rather than the level it grants. The level *is* the group's, so a column
      // drawing `Admin` beside a row whose group is named `Administrators` would be two
      // renderings of one fact - and this header hands its own key to the sorter, so the
      // column and the order it asks for stay the same thing (`sortBy=group`) instead of
      // sorting names under a heading that reads as levels.
      title: t('users.columns.group'),
      dataIndex: 'groupName',
      key: 'group',
      width: widths.group,
      sorter: true,
      sortOrder: table.orderOf('group'),
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('group'),
      render: textCell,
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
      sortOrder: table.orderOf('status'),
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
      //
      // A visit that *is* recorded is the link to the rest of them, with this account's
      // identifier already in the filter. The cell is a component of its own rather than an
      // expression here because a `render` is reached only once a row exists, and nothing in
      // `probe:render` ever gets that far - see `LastSeenCell`.
      title: t('users.columns.lastSeenAt'),
      dataIndex: 'lastSeenAt',
      key: 'lastSeenAt',
      width: widths.lastSeenAt,
      sorter: true,
      sortOrder: table.orderOf('lastSeenAt'),
      onHeaderCell: () => resizer('lastSeenAt'),
      render: (value: string | null, user: AdminUser) => (
        <LastSeenCell area={area} user={user} value={value} format={formatMoment} />
      ),
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
      sortOrder: table.orderOf('ip'),
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('ip'),
      render: ipCell,
    },
    {
      // The four actions as icons with tooltips - icons carry no text of their own, so the
      // name each one gets in the locale files is the only thing that says what pressing it
      // does. Two of them appear conditionally: confirming is only a move an `Unregistered`
      // row can make, and block/unblock are one button that means the opposite depending on
      // which side of the state the row is on. All four write actions additionally ask
      // whether this row's role is one the operator may *change*, which is a different
      // question from the one the page was loaded with - see `editable` inside.
      title: t('users.columns.actions'),
      key: 'actions',
      width: widths.actions,
      onHeaderCell: () => resizer('actions'),
      render: (_, user) => {
        const isSelf = user.id === ownId
        const busy = busyId === user.id

        // Whether this row's role is inside the operator's edit grants. The list itself was
        // already filtered to the roles they may *read*, so every row here is readable - but
        // reading is not writing, and `viewAdmins` without `editAdmins` is a page where an
        // administrator is visible and every move on it is refused. Disabling here rather
        // than letting the refusal arrive is the same rule `selfProtected` follows: the UI
        // tells the truth the API would tell, and it says it before the click rather than
        // after it.
        const editable = canEdit(granted, user.role)
        const refused = t('users.noPermission')

        const actions: RowAction[] = [
          // Never gated on `editable`: opening the form is a read, and the row only arrived
          // because `canView` already holds for its role. The form greys its own submit for
          // the same reason this one greys its buttons.
          {
            key: 'edit',
            label: t('users.actions.edit'),
            icon: <EditOutlined />,
            onClick: () => navigate(`${area}/users/${user.id}`),
          },
        ]

        // Confirming is only a move an `Unregistered` row can make.
        if (user.status === 'Unregistered') {
          actions.push({
            key: 'confirm',
            label: editable ? t('users.actions.confirm') : refused,
            icon: <CheckOutlined />,
            loading: busy,
            disabled: busy || !editable,
            onClick: () => runAction(() => confirmRegistration.mutateAsync({ id: user.id }), user),
          })
        }

        // Block and unblock are one button that means the opposite depending on which side
        // of the state the row is on.
        actions.push(
          user.status === 'Blocked'
            ? {
                key: 'unblock',
                label: editable ? t('users.actions.unblock') : refused,
                icon: <UnlockOutlined />,
                loading: busy,
                disabled: busy || !editable,
                onClick: () => runAction(() => setUnblocked.mutateAsync({ id: user.id }), user),
              }
            : {
                key: 'block',
                label: !editable
                  ? refused
                  : isSelf
                    ? t('users.selfProtected')
                    : t('users.actions.block'),
                icon: <LockOutlined />,
                disabled: isSelf || busy || !editable,
                onClick: () => runAction(() => setBlocked.mutateAsync({ id: user.id }), user),
              },
        )

        actions.push({
          key: 'delete',
          label: !editable
            ? refused
            : isSelf
              ? t('users.selfProtected')
              : t('users.actions.delete'),
          icon: <DeleteOutlined />,
          danger: true,
          disabled: isSelf || busy || !editable,
          onClick: () => confirmDelete(user),
        })

        return <RowActions actions={actions} />
      },
    },
  ]

  const statusOptions = useMemo(
    () => STATUSES.map((status) => ({ value: status, label: t(`users.statuses.${status}`) })),
    [t],
  )

  /**
   * Whether the menu may be opened at all: something is selected, nothing else is running,
   * and the operator may change at least one kind of account.
   *
   * The permission question is the page-level one (`canEditAny`), not the per-row one. Which
   * rows are editable is answered per entry by the server - it knows each row's role and the
   * operator's grants for it - and that refusal is precisely what the summary dialog above
   * reports. Greying every entry here would mean deciding in the browser which of thirty
   * rows the server would have refused, with the list's own filter already having hidden
   * some of them.
   */
  const bulkEnabled = canEditAny(granted) && selectedIds.length > 0 && !bulkBusy

  /** The five entries, in the order the menu draws them. */
  const bulkItems = useMemo<MenuProps['items']>(
    () => [
      { key: 'block', icon: <LockOutlined />, label: t('users.actions.block') },
      { key: 'unblock', icon: <UnlockOutlined />, label: t('users.actions.unblock') },
      {
        key: 'confirm-registration',
        icon: <CheckOutlined />,
        label: t('users.actions.confirm'),
      },
      // Dropped rather than greyed when there is nothing to choose from: the dialog's
      // select *is* this same list, and an operator who may not read groups has no list to
      // show them - so the entry would lead to a dialog whose only live control is Cancel.
      ...(groupOptions.length > 0
        ? [{ key: 'assign-group', icon: <TeamOutlined />, label: t('users.bulk.assign') }]
        : []),
      { type: 'divider' },
      { key: 'delete', icon: <DeleteOutlined />, danger: true, label: t('users.actions.delete') },
    ],
    [t, groupOptions.length],
  )

  return (
    <Card
      title={t('users.listTitle')}
      // The title is the one thing in this header that must never yield. Without this, antd
      // shrinks both halves when the row is too wide and the title's own text wraps to a
      // second line - which, on a Full HD screen at 125% scaling and with the longer Russian
      // labels, is exactly what happened. Pinned to one line, the overflow goes where it can
      // actually be handled: the `Space wrap` in `extra` stacks the toolbar's controls onto a
      // second row, which is taller but readable, rather than crushing the page's own heading
      // into two lines. The widths below are what keep that from being needed in the first
      // place; this is what decides which side loses if they ever stop being enough.
      styles={{ title: { whiteSpace: 'nowrap' } }}
      extra={
        <Space wrap>
          {/* The selection's controls, first because that is what they act on, and as one
              button rather than a label beside one. The count rides inside the button the
              count enables - see `menuWithCount` in the locales for why, which is a width
              decision about this row before it is anything else. Both states are drawn with
              nothing selected, for the reason the "Reset widths" button below is drawn while
              disabled: a control that appears and disappears reads as a broken one, and the
              count is what says *why* the menu is greyed. The tooltip carries the permission
              sentence only where there is one - with the grants in place it would be
              repeating the button's own label back at the visitor, which is what the "Add
              user" button beside it already does. */}
          {hinted(
            canEditAny(granted) ? t('users.bulk.menu') : t('users.noPermission'),
            <Dropdown
              menu={{ items: bulkItems, onClick: onBulkMenu }}
              disabled={!bulkEnabled}
            >
              <Button icon={<MoreOutlined />} loading={bulkBusy}>
                {selectedIds.length > 0
                  ? t('users.bulk.menuWithCount', { count: selectedIds.length })
                  : t('users.bulk.menu')}
              </Button>
            </Dropdown>,
          )}
          {/*200 rather than 260, and the two selects below are narrower for the same reason:
              this row has to fit **beside the card's own title** on one Full HD screen, and
              in Russian it does not. `Сбросить ширину`, `Добавить пользователя` and
              `Действия` are all longer than their English twins, so the budget the English
              layout never noticed is the one the Russian one spends. What the search loses in
              room it keeps in its aria-label, which still names the whole field. */}
          <TableSearch
            value={table.searchText}
            onChange={table.setSearchText}
            onSearch={table.applySearch}
            labelKey="users.searchLabel"
            placeholderKey="users.searchPlaceholder"
            width={200}
          />
          <Select
            allowClear
            showSearch
            aria-label={t('users.columns.group')}
            style={{ width: 150 }}
            value={groupFilter ?? undefined}
            options={groupOptions}
            placeholder={t('users.filterAnyGroup')}
            optionFilterProp="label"
            onChange={chooseGroup}
          />
          <Select
            allowClear
            aria-label={t('users.columns.status')}
            style={{ width: 130 }}
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
              no account for that person yet" becomes visible. Greyed for an operator who may
              read accounts but change none of them - creating is one of the writes, and a
              button leading to a form whose own Save is already greyed would be two steps
              into a refusal the first one could have said. */}
          {hinted(
            canEditAny(granted) ? t('nav.items.usersAdd') : t('users.noPermission'),
            <Button
              type="primary"
              icon={<PlusOutlined />}
              disabled={!canEditAny(granted)}
              onClick={() => navigate(`${area}/users/add`)}
            >
              {t('nav.items.usersAdd')}
            </Button>,
          )}
        </Space>
      }
    >
      <ServerTable<AdminUser, AdminUserSortField>
        table={table}
        columns={columns}
        rows={users?.items ?? []}
        total={users?.total ?? 0}
        loading={loading}
        loadError={loadError}
        onRetry={() => usersQuery.refetch()}
        tableWidth={tableWidth}
        emptyText={t(
          groupFilter || statusFilter || table.query.search ? 'users.noMatches' : 'users.empty',
        )}
        showTotal={(total, range) => t('users.range', { from: range[0], to: range[1], total })}
        selection={{ selectedRowKeys: selectedIds, onChange: setSelectedIds }}
      />
      {/* The move, as a dialog rather than a control in the toolbar: the destination is one
          decision about the whole selection, and a select dropped into the toolbar would be
          answering it for a menu that has already closed. The list is the same one the group
          filter loads, fetched for a different purpose and reused here rather than asked for
          a second time - and it is the reason the menu entry disappears instead of greying
          when this list is empty. */}
      <Modal
        title={t('users.bulk.assignTitle')}
        open={assignOpen}
        okText={t('users.bulk.assignOk')}
        okButtonProps={{ disabled: !assignGroupId, loading: bulkBusy }}
        cancelText={t('actions.cancel')}
        onOk={() => runBulk('assign-group', assignGroupId)}
        onCancel={closeAssign}
      >
        <Select
          showSearch
          allowClear
          aria-label={t('users.bulk.assignPlaceholder')}
          style={{ width: '100%' }}
          value={assignGroupId ?? undefined}
          options={groupOptions}
          placeholder={t('users.bulk.assignPlaceholder')}
          optionFilterProp="label"
          onChange={(value) => setAssignGroupId(value ?? null)}
        />
      </Modal>
    </Card>
  )
}
