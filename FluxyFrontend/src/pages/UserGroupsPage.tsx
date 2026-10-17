import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { App, Button, Card, Dropdown, Select, Space, Table, Tag } from 'antd'
import type { MenuProps, TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import {
  DeleteOutlined,
  EditOutlined,
  LockOutlined,
  MoreOutlined,
  PlusOutlined,
  TeamOutlined,
  UnlockOutlined,
} from '@ant-design/icons'
import { bulkUserGroups, deleteUserGroup, updateUserGroup } from '../lib/api'
import type {
  BulkUserGroupAction,
  UserGroupSortField,
} from '../lib/api'
import type { Role, UserGroup, UserStatus } from '../types'
import { getCsrfToken } from '../lib/csrf'
import { hinted } from '../lib/hinted'
import { messageForError, successTextFor } from '../lib/http'
import { areaForRole } from '../lib/session'
import { useSession } from '../lib/sessionContext'
import { tableResults } from '../lib/tableResults'
import { idCell, textCell } from '../lib/tableCells'
import LoadErrorAlert from '../components/LoadErrorAlert'
import ResizableHeaderCell from '../components/ResizableHeaderCell'
import type { ResizableHeaderCellProps } from '../components/ResizableHeaderCell'
import TableSearch from '../components/TableSearch'
import { useBulkAction } from '../hooks/useBulkAction'
import { useColumnWidths } from '../hooks/useColumnWidths'
import { useUserGroups } from '../hooks/queries'
import { useRowAction } from '../hooks/useRowAction'
import { useServerTable } from '../hooks/useServerTable'

/** The six columns, in the order they are drawn, named by their own keys. */
type GroupsColumnKey = 'id' | 'name' | 'role' | 'status' | 'permissionsCount' | 'actions'

const COLUMN_KEYS: readonly GroupsColumnKey[] = [
  'id',
  'name',
  'role',
  'status',
  'permissionsCount',
  'actions',
]

/**
 * The width each column starts at, and the width it goes back to when asked to reset.
 *
 * Summed deliberately to the same **1190** the accounts table adds up to, for the reason
 * that one does: with the selection column below the table fills the same **1230** both of
 * them have always filled, and that sum is what has to fit beside the sidebar on one Full
 * HD screen so the action buttons at the far right stay inside the viewport. This table has
 * two fewer columns to spend it on, so the extra room goes where this page's content
 * actually is - `name`, which the server bounds only by uniqueness, and `status`, whose tag
 * and its padding need more than a bare label does. The identifier is narrow for the reason
 * the accounts table keeps its narrow: it prints nine characters and the whole value is a
 * hover away.
 *
 * `name` is 360 rather than the 400 it was before the selection column arrived, and the
 * forty it gave up are exactly the forty that column takes - the checkbox is paid for by the
 * one column this page would have widened anyway, rather than by the table growing or by the
 * identifier losing a third of what it has.
 */
const DEFAULT_COLUMN_WIDTHS: Record<GroupsColumnKey, number> = {
  id: 110,
  name: 360,
  role: 180,
  status: 200,
  permissionsCount: 140,
  actions: 200,
}

/**
 * The width of the checkbox column antd prepends, which is not one of the six and is
 * therefore not part of the hook's sum or its memory - the same fact `UsersPage` records
 * about its own eight.
 */
const SELECTION_WIDTH = 40

/** Where this table remembers the widths the visitor gave its columns. */
const COLUMN_WIDTHS_KEY = 'fluxy.userGroups.columnWidths'

/** The three levels and three states, in the order the server declares them - the two filters read from these. */
const ROLES: Role[] = ['Client', 'Reseller', 'Admin']
const STATUSES: UserStatus[] = ['Unregistered', 'Registered', 'Blocked']

/**
 * Three descriptions, not a ramp.
 *
 * `UserStatus` is not an ordered scale, so the colours say what the state *is* rather than
 * how much of it there is - the same three the accounts table draws, because a group and an
 * account are held to the same vocabulary on this installation and a second set of words
 * for one enum would be a second fact about it.
 */
const STATUS_COLOR: Record<UserStatus, string> = {
  Unregistered: 'warning',
  Registered: 'success',
  Blocked: 'error',
}

interface UserGroupsPageProps {
  /**
   * The menu key every section route carries, passed here for the reason it is passed to
   * every other section page and deliberately unread: the heading is this page's own
   * (`userGroups.listTitle`), because a card repeating the menu item's name in large letters
   * would say the menu again instead of naming the page. For the accounts table that pair is
   * `Управление` beside `Пользователи`; here it is `Группы` beside `Группы пользователей` -
   * two names for the same subject, one naming how you got there and one naming what you
   * are looking at. The locale files say the same above `userGroups.listTitle`.
   */
  sectionKey: string
}

/**
 * The admin's group table - the page behind `nav.items.userGroups`.
 *
 * Shaped after `UsersPage` down to the same pieces, and deliberately so: two tables on one
 * page of one application having different rules for a resize handle, a pager or a sort
 * arrow would be two places for one of those rules to be quietly wrong, which is the same
 * reason `useColumnWidths` and `hinted` are shared rather than copied.
 *
 * **Paging, search, filtering and sorting are all server side**, for the reason the accounts
 * page's are: `total`. A filter applied in the browser would narrow the page on screen while
 * the pager still counted everything, so every change goes back to `GET /admin/user-groups`
 * and any change that is not a page turn resets to page 1. Four of the six headers are
 * controlled - the server can order by four of them (`createdAt` is the fifth and this table
 * draws no column for it), and `id` and `actions` are never fields the endpoint orders by,
 * so a header that flipped its arrow over rows it had not re-fetched would be describing a
 * table that does not exist.
 *
 * The three base groups are the only thing here that is not a plain CRUD row: they may be
 * renamed and nothing else. Their block and delete buttons are **disabled with a reason**
 * rather than hidden, because a control that vanishes teaches nothing about why it was
 * there, and the server refuses the same moves with `409 user_group_immutable` - the
 * disabled button is the UI saying what the API would say.
 */
export default function UserGroupsPage(_props: UserGroupsPageProps) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { message, modal } = App.useApp()
  const session = useSession()

  /**
   * The five request parameters, the pager and the four sortable headers - all of that is
   * `useServerTable`, which the other server-paged tables call too. The mapping needs no
   * help here: the sortable columns are keyed by the server's own field names, so a click
   * hands its own key over as the field. `id` and `actions` have no sorter and never produce
   * an event at all.
   */
  const table = useServerTable<UserGroup, UserGroupSortField>({
    defaultSort: { by: 'name', order: 'asc' },
  })
  const [roleFilter, setRoleFilter] = useState<Role | null>(null)
  const [statusFilter, setStatusFilter] = useState<UserStatus | null>(null)

  /**
   * The page of groups, fetched through a query whose key **is** the parameters - the same
   * shape the accounts table now uses, and the same reason: the key changing *is* the new
   * request, React Query discards the answer of a query the visitor has moved away from,
   * and every successful action re-reads through `refetch()` rather than by bumping an
   * `attempt` counter.
   */
  const groupsQuery = useUserGroups({
    ...table.query,
    role: roleFilter,
    status: statusFilter,
  })
  const { data: groups, loadError, loading } = tableResults(groupsQuery)

  /**
   * The six widths: their state, their memory between visits, and the drag that changes them
   * - all of it `useColumnWidths`, the same hook the accounts table calls.
   */
  const { widths, tableWidth, atDefaults, reset, resizer: handleFor } =
    useColumnWidths<GroupsColumnKey>({
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
   * The selection and the run over it: the ids, the scope, the prune, the busy flag and the
   * summary dialog are `useBulkAction`'s, which the accounts table calls too.
   *
   * A base group is **not** excluded from the selection. The row's own block and delete
   * buttons are disabled with a reason, but a selection is a set the operator is still
   * assembling - and the server answers `user_group_immutable` per entry, which the summary
   * dialog reports by name. Excluding it here would mean the browser deciding, from a flag it
   * was given, that a rule the server owns is worth a rule of its own.
   */
  const { selectedIds, setSelectedIds, bulkBusy, applyBulk } = useBulkAction({
    rows: groups?.items,
    refetch: () => void groupsQuery.refetch(),
    partialTitleKey: 'userGroups.bulk.partialTitle',
  })

  /**
   * The two filters, each starting the pager over: a different list is a different pager,
   * and leaving the visitor on page 3 of a filter that admits one page would show them an
   * empty table while `total` above it said 1.
   */
  const chooseRole = (value: Role | undefined) => {
    setRoleFilter(value ?? null)
    table.resetPage()
  }

  const chooseStatus = (value: UserStatus | undefined) => {
    setStatusFilter(value ?? null)
    table.resetPage()
  }

  /**
   * One column's resize handle, with this page's name for the column.
   *
   * The hook owns the drag, the clamp and the stored widths, but only the page knows what a
   * column is called: the handle's accessible name is built here and handed over as an
   * argument, because a translation key is not something a generic hook can hold.
   */
  const resizer = (key: GroupsColumnKey): ResizableHeaderCellProps =>
    handleFor(key, t('userGroups.resizeColumn', { column: t(`userGroups.columns.${key}`) }))

  /**
   * One row action, and the id whose own buttons spin while it runs - the token, the re-read
   * and the reporting are `useRowAction`'s, which the accounts table calls too.
   */
  const { busyId, setBusyId, runAction } = useRowAction<UserGroup>(
    () => void groupsQuery.refetch(),
  )

  /** Sets a group's state, which is all "block" and "unblock" mean for a group. */
  const setStatus = (group: UserGroup, status: UserStatus) =>
    runAction(
      ({ id, csrfToken }) => updateUserGroup({ id, patch: { status }, csrfToken }),
      group,
    )

  /**
   * Asks before deleting, because this is the only action here that cannot be undone: the
   * other two move a row between states, this removes the row. No password step and no
   * captcha - the admin session plus a fresh antiforgery token are the gate, the same as
   * every other write on this page - but a question first, because one misread row is not
   * recoverable.
   *
   * The body says what happens to the accounts, because that is the question the refusal is
   * about: the server answers `409 user_group_in_use` while any account still names this
   * group, and a dialog that only said "delete?" would leave the visitor to learn that from
   * the error. The `members` count lives on the edit form rather than here - the row would
   * have to fetch it, and the form already has it.
   */
  const confirmDelete = (group: UserGroup) => {
    modal.confirm({
      title: t('userGroups.deleteConfirmTitle'),
      content: t('userGroups.deleteConfirmBody', { name: group.name }),
      okText: t('userGroups.deleteConfirmOk'),
      okButtonProps: { danger: true },
      cancelText: t('actions.cancel'),
      onOk: async () => {
        setBusyId(group.id)

        try {
          const csrfToken = await getCsrfToken()
          const result = await deleteUserGroup({ id: group.id, csrfToken })

          message.success(successTextFor(result))

          // The last row of a later page: with it gone, that page no longer exists and the
          // server would answer it empty - a blank table under a pager still offering the
          // page the visitor is standing on. Back a page instead, which re-runs the query
          // anyway, so the list and the pager agree again without a second call.
          if (groups?.items.length === 1 && table.page > 1) table.setPage(table.page - 1)
          else void groupsQuery.refetch()
        } catch (error) {
          message.error(messageForError(error))
        } finally {
          setBusyId(null)
        }
      },
    })
  }

  /**
   * Runs one operation over the selection: the ids, the token, the reporting and the re-read
   * are `useBulkAction`'s.
   *
   * Block and unblock are names rather than a `status` patch for the same reason the row
   * buttons name them: what the operator means is "block these", and the service decides
   * that this is `PATCH { status: 'Blocked' }`. One place decides that, not three - so a
   * second guess made here about which status means "blocked" would be a second place for
   * the two to disagree.
   */
  const runBulk = (action: BulkUserGroupAction) =>
    applyBulk(async () =>
      bulkUserGroups({
        ids: selectedIds.map(String),
        action,
        csrfToken: await getCsrfToken(),
      }),
    )

  /**
   * Asks before a bulk deletion, for the reason only deletion asks first on a single row:
   * it is the one operation here that cannot be undone, and the question is about the whole
   * selection at once - so the body names the count rather than a name. What the body says
   * about members is the same promise the single-row dialog makes: accounts in these groups
   * are not deleted, they are simply left belonging to nothing until an administrator puts
   * them somewhere.
   */
  const confirmBulkDelete = () => {
    modal.confirm({
      title: t('userGroups.bulk.deleteTitle', { count: selectedIds.length }),
      content: t('userGroups.bulk.deleteBody'),
      okText: t('userGroups.deleteConfirmOk'),
      okButtonProps: { danger: true },
      cancelText: t('actions.cancel'),
      onOk: () => runBulk('delete'),
    })
  }

  /** What the toolbar's menu offers: the three operations, in the order the row actions draw them. */
  const onBulkMenu: MenuProps['onClick'] = ({ key }) => {
    if (key === 'delete') {
      confirmBulkDelete()
      return
    }

    void runBulk(key as BulkUserGroupAction)
  }

  const columns: TableColumnsType<UserGroup> = [
    {
      // Eight characters of the identifier and the rest on hover, for the reason the
      // accounts table does it: a full GUID is 36 characters of hex that nobody reads, and
      // what this column is for is telling two rows apart and picking one to edit.
      title: t('userGroups.columns.id'),
      dataIndex: 'id',
      key: 'id',
      width: widths.id,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('id'),
      render: idCell,
    },
    {
      // The name is what the whole page is read by - it is what the account form's picker
      // shows, what the delete dialog quotes, and the one property a base group may change -
      // so it gets the room and the truncation-with-tooltip rather than a cut with no way to
      // see the rest.
      title: t('userGroups.columns.name'),
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
      // The level rather than the group's own state, drawn from the enum member the server
      // sent. It is the fact an administrator is scanning for ("which of these can sign in
      // where"), and this header hands its own key to the sorter so the column and the order
      // it asks for stay the same thing (`sortBy=role`).
      title: t('userGroups.columns.role'),
      dataIndex: 'role',
      key: 'role',
      width: widths.role,
      sorter: true,
      sortOrder: table.orderOf('role'),
      onHeaderCell: () => resizer('role'),
      render: (value: Role) => t(`profile.roles.${value}`),
    },
    {
      // A tag rather than coloured text, for the reason the accounts table uses one: three
      // states read as three labels next to each other down a column, which is what a
      // scanning administrator is looking for - and the colour is a description, not a rank
      // (`UserStatus` is not an ordered scale, so nothing here says one state is *more*
      // than another).
      //
      // The group's own state, not the effective one of any account in it. The two are
      // related by the same rule everywhere else (an account's effective status is the most
      // restrictive of its own and its group's), but the *group's* status is what this row
      // holds and what `PATCH` writes, so the column draws that.
      title: t('userGroups.columns.status'),
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
      // A number and not a list, for the reason the endpoint answers one: the set is a
      // question the *edit form* asks, and shipping it on every row of every page would
      // answer a question this table never puts. Zero is drawn as zero rather than as a
      // dash - a group that grants nothing is a fact about the group, not a value the
      // server failed to determine.
      title: t('userGroups.columns.permissionsCount'),
      dataIndex: 'permissionsCount',
      key: 'permissionsCount',
      width: widths.permissionsCount,
      sorter: true,
      sortOrder: table.orderOf('permissionsCount'),
      onHeaderCell: () => resizer('permissionsCount'),
    },
    {
      // The four actions as icons with tooltips - icons carry no text of their own, so the
      // name each one gets in the locale files is the only thing that says what pressing it
      // does. Block and unblock are one button that means the opposite depending on which
      // side of the state the row is on, exactly as on the accounts table.
      //
      // Two of them are disabled on the three base groups with the reason still attached:
      // the server answers `409 user_group_immutable` for both, so the greyed button with a
      // sentence is the same fact told before the click rather than a failure explained
      // afterwards. Edit is *not* disabled - a base group may be renamed, and that is the
      // whole of what the form offers for one. "Show accounts" is not disabled either: it
      // is a question about the rows that belong here, and a base group holds as many of
      // them as any other.
      //
      // It reads rather than writes, which is why it sits with the row actions and not in
      // the toolbar: which group's members to look at is a decision made *about this row*,
      // and a toolbar button would have to be told which one - by a selection the table
      // does not have.
      title: t('userGroups.columns.actions'),
      key: 'actions',
      width: widths.actions,
      onHeaderCell: () => resizer('actions'),
      render: (_, group) => {
        const locked = group.isBase
        const busy = busyId === group.id

        return (
          <Space size={4}>
            {hinted(
              t('userGroups.actions.edit'),
              <Button
                type="text"
                size="small"
                icon={<EditOutlined />}
                onClick={() => navigate(`${area}/user-groups/${group.id}`)}
              />,
            )}

            {hinted(
              t('userGroups.actions.showAccounts'),
              <Button
                type="text"
                size="small"
                icon={<TeamOutlined />}
                // Straight to the accounts list with this group already on it. The filter
                // lives in the address (`?groupId=`), so this is the whole of the
                // interaction - no state to hand over, nothing to remember, and the
                // resulting page is one the visitor can reload or send to somebody else
                // and still be looking at the same list.
                onClick={() => navigate(`${area}/users?groupId=${encodeURIComponent(group.id)}`)}
              />,
            )}

            {group.status === 'Blocked' ? (
              hinted(
                locked ? t('userGroups.baseImmutable') : t('userGroups.actions.unblock'),
                <Button
                  type="text"
                  size="small"
                  icon={<UnlockOutlined />}
                  loading={busy}
                  disabled={locked || busy}
                  onClick={() => setStatus(group, 'Registered')}
                />,
              )
            ) : (
              hinted(
                locked ? t('userGroups.baseImmutable') : t('userGroups.actions.block'),
                <Button
                  type="text"
                  size="small"
                  icon={<LockOutlined />}
                  disabled={locked || busy}
                  onClick={() => setStatus(group, 'Blocked')}
                />,
              )
            )}

            {hinted(
              locked ? t('userGroups.baseUndeletable') : t('userGroups.actions.delete'),
              <Button
                danger
                type="text"
                size="small"
                icon={<DeleteOutlined />}
                disabled={locked || busy}
                onClick={() => confirmDelete(group)}
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

  /** Whether the menu may be opened at all: something is selected and nothing else is running. */
  const bulkEnabled = selectedIds.length > 0 && !bulkBusy

  /** The three entries, in the order the row actions draw them. */
  const bulkItems = useMemo<MenuProps['items']>(
    () => [
      { key: 'block', icon: <LockOutlined />, label: t('userGroups.actions.block') },
      { key: 'unblock', icon: <UnlockOutlined />, label: t('userGroups.actions.unblock') },
      { type: 'divider' },
      {
        key: 'delete',
        icon: <DeleteOutlined />,
        danger: true,
        label: t('userGroups.actions.delete'),
      },
    ],
    [t],
  )

  return (
    <Card
      title={t('userGroups.listTitle')}
      // The title pinned to one line, for the reason the accounts table pins its own - see
      // the comment there. This page needs it more than that one does: `Группы
      // пользователей` is the longest heading either table carries, and it sits beside a
      // toolbar that is now the wider of the two.
      styles={{ title: { whiteSpace: 'nowrap' } }}
      extra={
        <Space wrap>
          {/* The selection's controls, first because that is what they act on - the same
              pair the accounts table offers, in the same place, for the reason that table
              offers them there, and folded into one button for the same reason: this row
              shares the card header with a title that is already the longest on either
              page (`Группы пользователей`), so the count rides inside the button the count
              enables rather than beside it. Both states are drawn with nothing selected, for
              the reason the "Reset widths" button below is drawn while disabled: a control
              that appears and disappears reads as a broken one, and the count is what says
              *why* the menu is greyed. */}
          <Dropdown menu={{ items: bulkItems, onClick: onBulkMenu }} disabled={!bulkEnabled}>
            <Button icon={<MoreOutlined />} loading={bulkBusy}>
              {selectedIds.length > 0
                ? t('userGroups.bulk.menuWithCount', { count: selectedIds.length })
                : t('userGroups.bulk.menu')}
            </Button>
          </Dropdown>
          {/* Narrower than the 240/160/165 they were, for the reason the accounts table's
              three are: this row has to fit beside the card's title on one Full HD screen,
              and in Russian it did not. What each loses in room it keeps in its aria-label,
              which still names the whole field. */}
          <TableSearch
            value={table.searchText}
            onChange={table.setSearchText}
            onSearch={table.applySearch}
            labelKey="userGroups.searchLabel"
            placeholderKey="userGroups.searchPlaceholder"
            width={190}
          />
          <Select
            allowClear
            aria-label={t('userGroups.columns.role')}
            style={{ width: 140 }}
            value={roleFilter ?? undefined}
            options={roleOptions}
            placeholder={t('userGroups.filterAnyRole')}
            onChange={chooseRole}
          />
          <Select
            allowClear
            aria-label={t('userGroups.columns.status')}
            style={{ width: 125 }}
            value={statusFilter ?? undefined}
            options={statusOptions}
            placeholder={t('userGroups.filterAnyStatus')}
            onChange={chooseStatus}
          />
          {/* Disabled rather than hidden when the widths are already the defaults: a control
              that appears and disappears reads as a broken one, and there is nothing to undo
              until somebody has actually dragged something. */}
          <Button disabled={atDefaults} onClick={() => reset()}>
            {t('userGroups.resetWidths')}
          </Button>
          {/* The same address the "add" route opens, offered here because this is the screen
              from which adding one is a decision. Its label comes from the form's own title
              rather than from `nav.items.userGroups`, which is the *list's* name - a button
              reading "Groups" above the groups would say what you are already looking at. */}
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => navigate(`${area}/user-groups/add`)}
          >
            {t('userGroups.addTitle')}
          </Button>
        </Space>
      }
    >
      {loadError ? (
        <LoadErrorAlert
          message={loadError}
          loading={loading}
          onRetry={() => groupsQuery.refetch()}
        />
      ) : (
        <Table<UserGroup>
          rowKey="id"
          columns={columns}
          dataSource={groups?.items ?? []}
          loading={loading}
          // `ResizableHeaderCell` rather than the default cell: the handles are a sibling of
          // the header's own content, and the only way to put them there without nesting a
          // focusable span inside antd's sortable `<button>` is to own the cell. It is a
          // module-level function on purpose - a component type created inside this one would
          // be a new type on every repaint, and React would then unmount the handle mid-drag,
          // dropping the pointer with it.
          components={{ header: { cell: ResizableHeaderCell } }}
          rowSelection={{
            selectedRowKeys: selectedIds,
            onChange: (keys) => setSelectedIds(keys),
            columnWidth: SELECTION_WIDTH,
          }}
          // `tableWidth` is the six columns this page owns; the checkbox column antd
          // prepends is a seventh with a width of its own, so it is added here rather than
          // being smuggled into the hook's sum - which would make the stored widths and the
          // reset disagree with the six headers they are supposed to describe.
          scroll={{ x: tableWidth + SELECTION_WIDTH }}
          onChange={table.handleTableChange}
          locale={{
            emptyText: t(
              table.query.search || roleFilter || statusFilter
                ? 'userGroups.noMatches'
                : 'userGroups.empty',
            ),
          }}
          pagination={{
            ...table.pagination,
            total: groups?.total ?? 0,
            showTotal: (total, [from, to]) => t('userGroups.range', { from, to, total }),
          }}
        />
      )}
    </Card>
  )
}
