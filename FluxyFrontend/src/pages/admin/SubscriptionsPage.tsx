import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import {
  App,
  Button,
  Card,
  Dropdown,
  Form,
  Input,
  Modal,
  Select,
  Space,
  Tag,
  Typography,
} from 'antd'
import type { MenuProps, TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import {
  CalendarOutlined,
  ClockCircleOutlined,
  DeleteOutlined,
  EditOutlined,
  MoreOutlined,
  PauseCircleOutlined,
  PlusOutlined,
  ReloadOutlined,
  StopOutlined,
  SwapOutlined,
  TagOutlined,
  UnorderedListOutlined,
  UserSwitchOutlined,
} from '@ant-design/icons'
import { SUBSCRIPTIONS_PAGE_SIZE } from '../../features/subscriptions/api'
import { isMac } from '../../features/subscriptions/subscriptionRules'
import type { BulkSubscriptionAction, SubscriptionSortField } from '../../features/subscriptions/api'
import SubscriptionSubrows from '../../features/subscriptions/components/SubscriptionSubrows'
import type {
  SubscriptionMacKind,
  SubscriptionsListActions,
} from '../../features/subscriptions/components/SubscriptionsListContext'
import { SubscriptionsListContext } from '../../features/subscriptions/components/SubscriptionsListContext'
import type { Subscription, SubscriptionStatus } from '../../features/subscriptions/types'
import { SUBSCRIPTION_STATUSES } from '../../features/subscriptions/subscriptionOptions'
import { useProfile } from '../../features/profile/hooks/queries'
import { useAllUsers } from '../../features/users/hooks/queries'
import {
  useBulkSubscriptions,
  useDeleteSubscription,
  useGeneratePlaylistId,
  usePatchSubscription,
  useProlongSubscription,
} from '../../features/subscriptions/hooks/mutations'
import { useSubscriptions } from '../../features/subscriptions/hooks/queries'
import { useTariffPlans } from '../../features/tariff-plans/hooks/queries'
import { hinted } from '../../utils/hinted'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'
import { tableResults } from '../../utils/tableResults'
import { dashedCell } from '../../utils/tableCells'
import { enumParam, withFilter } from '../../utils/tableParams'
import { actionsColumnWidth } from '../../utils/tableWidths'
import { ResetColumnWidths, RowActions, ServerTable, TableSearch } from '../../components/table'
import type { ResizableHeaderCellProps, RowAction } from '../../components/table'
import { useBulkAction, useColumnWidths, useRowAction, useServerTable } from '../../hooks/table'
import { formatTimestamp } from '../../utils/dateFormat'
import { ApiError, messageForError, successTextFor, textForCode } from '../../utils/http'

/** How often the table re-reads itself while the toggle below is on, in milliseconds. */
const AUTO_REFRESH_MS = 30_000

/** The five columns, in the order they are drawn. */
type SubscriptionColumnKey = 'name' | 'expiry' | 'connections' | 'creator' | 'actions'

const COLUMN_KEYS: readonly SubscriptionColumnKey[] = [
  'name',
  'expiry',
  'connections',
  'creator',
  'actions',
]

/**
 * The width each column starts at, summing to **1190**; the selection column takes them to the
 * same **1230** the other administrator tables reach.
 *
 * The name column takes everything the four fixed ones leave, because it is the two-line cell the
 * whole table is read by - owner, plan, creation date and the status tag - and it is the only one
 * that truncates no second time. The actions column is exactly as wide as the four buttons a row
 * draws (`actionsColumnWidth`).
 */
const FIXED_COLUMN_WIDTHS = {
  expiry: 170,
  connections: 150,
  creator: 180,
}

const ACTIONS_WIDTH = actionsColumnWidth(4)
const TABLE_WIDTH = 1190

const DEFAULT_COLUMN_WIDTHS: Record<SubscriptionColumnKey, number> = {
  ...FIXED_COLUMN_WIDTHS,
  name:
    TABLE_WIDTH -
    Object.values(FIXED_COLUMN_WIDTHS).reduce((sum, width) => sum + width, 0) -
    ACTIONS_WIDTH,
  actions: ACTIONS_WIDTH,
}

/** Where this table remembers the widths the visitor gave its columns. */
const COLUMN_WIDTHS_KEY = 'fluxy.subscriptions.columnWidths'

/** The status tag's colour, which describes a state rather than ranking them. */
const STATUS_COLOR: Record<SubscriptionStatus, string> = {
  active: 'green',
  trial: 'gold',
  blocked: 'red',
  expired: 'red',
}

/** Which MAC dialog is open, and the row it is about. */
interface MacDialogState {
  subscription: Subscription
  kind: SubscriptionMacKind
}

/** The two dialogs a bulk run needs a value for. */
type BulkDialog = 'tariff' | 'creator'

interface SubscriptionsPageProps {
  /** The menu key this route carries, accepted and deliberately unread. */
  sectionKey: string
}

/**
 * The admin's subscriptions table - the page behind `nav.items.subscriptionsManage`.
 *
 * Shaped after `VideoStreamsPage` down to the same pieces, because two tables on one page of one
 * application having different rules for a resize handle, a pager, a sort arrow or an auto-refresh
 * toggle would be two places for one of those rules to be quietly wrong.
 *
 * **Paging, searching, filtering and sorting are server side**, for the reason every other table's
 * are: `total`. The status is *computed* by the server, so a filter here would be an answer the
 * browser could not reproduce - the tag and the filter must come from the same rule.
 *
 * **The service subrows are forced-open expanded rows.** Every row is expanded, there is no
 * chevron and no interaction: the strips below a row are part of it rather than something hidden
 * behind a disclosure, which is what the owner asked for.
 *
 * The **auto-refresh** re-reads every 30 seconds, on by default, through the same icon-only button
 * the streams page draws.
 */
export default function SubscriptionsPage(_props: SubscriptionsPageProps) {
  const { t, i18n } = useTranslation()
  const { modal, message } = App.useApp()
  const navigate = useNavigate()
  const session = useSession()

  const table = useServerTable<Subscription, SubscriptionSortField>({
    defaultSort: { by: 'createdAt', order: 'desc' },
    sortFields: ['createdAt', 'expireAt'],
    pageSize: SUBSCRIPTIONS_PAGE_SIZE,
  })

  /**
   * The three filters, in the address rather than in this component's state - the decision every
   * server-paged table makes about what it is showing: the address is the single source, so it is
   * shareable and a reload keeps the filter.
   */
  const [searchParams, setSearchParams] = useSearchParams()
  const ownerFilter = searchParams.get('ownerId') || null
  const creatorFilter = searchParams.get('creatorId') || null
  const statusFilter = enumParam<SubscriptionStatus>(searchParams, 'status', SUBSCRIPTION_STATUSES)

  /** Whether the table re-reads itself. On by default, and pausing is one press. */
  const [autoRefresh, setAutoRefresh] = useState(true)

  const subscriptionsQuery = useSubscriptions({
    ...table.query,
    ownerId: ownerFilter,
    creatorId: creatorFilter,
    status: statusFilter,
  })
  const { data: subscriptions, loadError, loading } = tableResults(subscriptionsQuery)

  // The filters' option lists, each in a query of its own so a failure of one is a failure of one.
  const profileQuery = useProfile()
  const granted = profileQuery.data?.permissions ?? []
  const canEdit = granted.includes('editSubscriptions')

  const usersQuery = useAllUsers()
  const users = usersQuery.data?.items ?? []

  const plansQuery = useTariffPlans({
    page: 1,
    pageSize: 1000,
    search: '',
    sortBy: 'name',
    sortOrder: 'asc',
  })
  const plans = plansQuery.data?.items ?? []

  const { widths, tableWidth, atDefaults, reset, resizer: handleFor } =
    useColumnWidths<SubscriptionColumnKey>({
      keys: COLUMN_KEYS,
      defaults: DEFAULT_COLUMN_WIDTHS,
      storageKey: COLUMN_WIDTHS_KEY,
    })

  const area = session ? areaForRole(session.role) : ''
  const timeZone = profileQuery.data?.timeZone ?? null
  const language = i18n.resolvedLanguage ?? i18n.language

  // The writes a row performs, and the generator the playlist strip uses.
  const removeSubscription = useDeleteSubscription()
  const patchField = usePatchSubscription()
  const generatePlaylistId = useGeneratePlaylistId()
  const bulk = useBulkSubscriptions()

  const prolong = useProlongSubscription()

  const { busyId, runAction, confirmDelete: askDelete } = useRowAction<Subscription>({
    refetch: () => void subscriptionsQuery.refetch(),
    rows: subscriptions?.items,
    page: table.page,
    setPage: table.setPage,
  })

  const { selectedIds, setSelectedIds, bulkBusy, applyBulk } = useBulkAction({
    rows: subscriptions?.items,
    refetch: () => void subscriptionsQuery.refetch(),
    partialTitleKey: 'subscriptions.bulk.partialTitle',
  })

  /** The MAC dialog and the two bulk value dialogs. */
  const [macForm] = Form.useForm<{ mac: string }>()
  const [macDialog, setMacDialog] = useState<MacDialogState | null>(null)
  const [macValue, setMacValue] = useState('')
  const [bulkDialog, setBulkDialog] = useState<BulkDialog | null>(null)
  const [bulkTariffId, setBulkTariffId] = useState<string | null>(null)
  const [bulkCreatorId, setBulkCreatorId] = useState<string | null>(null)

  const closeMacDialog = () => {
    setMacDialog(null)
    setMacValue('')
    macForm.resetFields()
  }

  const closeBulkDialog = () => {
    setBulkDialog(null)
    setBulkTariffId(null)
    setBulkCreatorId(null)
  }

  /**
   * The timer that keeps the table honest about a fact the server computes from the clock.
   *
   * The ref rather than the query in the dependency array is the whole point: `refetch` is a new
   * function whenever the answer changes, so naming it here would restart the interval after every
   * re-read. The ref is written on every render and read on every tick, so the interval is created
   * once per change of the toggle and always calls the newest fetch.
   */
  const refreshRef = useRef<() => void>(() => {})

  useEffect(() => {
    refreshRef.current = () => {
      void subscriptionsQuery.refetch()
    }
  })

  useEffect(() => {
    if (!autoRefresh) return

    const timer = setInterval(() => refreshRef.current(), AUTO_REFRESH_MS)

    return () => clearInterval(timer)
  }, [autoRefresh])

  const chooseOwner = (value: string | undefined) =>
    setSearchParams(withFilter(searchParams, 'ownerId', value), { replace: true })

  const chooseCreator = (value: string | undefined) =>
    setSearchParams(withFilter(searchParams, 'creatorId', value), { replace: true })

  const chooseStatus = (value: SubscriptionStatus | undefined) =>
    setSearchParams(withFilter(searchParams, 'status', value), { replace: true })

  const resizer = (key: SubscriptionColumnKey): ResizableHeaderCellProps =>
    handleFor(key, t('subscriptions.resizeColumn', { column: t(`subscriptions.columns.${key}`) }))

  const confirmDelete = (subscription: Subscription) =>
    askDelete(subscription, {
      name: subscription.username,
      titleKey: 'subscriptions.deleteConfirmTitle',
      bodyKey: 'subscriptions.deleteConfirmBody',
      okKey: 'subscriptions.deleteConfirmOk',
      run: () => removeSubscription.mutateAsync({ id: subscription.id }),
    })

  /**
   * Blocks the owner of one row through the account block, reusing the same endpoint the accounts
   * screen posts to so the sessions are revoked and an already-blocked owner is a success.
   */
  const blockOwner = (subscription: Subscription) =>
    modal.confirm({
      title: t('subscriptions.blockOwnerConfirmTitle'),
      content: t('subscriptions.blockOwnerConfirmBody', { name: subscription.username }),
      okText: t('subscriptions.blockOwnerConfirmOk'),
      okButtonProps: { danger: true },
      cancelText: t('actions.cancel'),
      onOk: async () => {
        // The single-row block goes through the bulk endpoint because that is the one the
        // account block is exposed on; its per-row answer is the report, so this reads it the
        // same way the bulk run does rather than through `useRowAction`, whose result type is
        // one message.
        try {
          const result = await bulk.mutateAsync({
            payload: { action: 'block-owner', ids: [subscription.id] },
          })

          const item = result.items[0]

          if (item?.ok) message.success(successTextFor(result))
          else message.error(item ? textForCode(item.code) ?? item.message : messageForError(null))

          void subscriptionsQuery.refetch()
        } catch (error) {
          message.error(messageForError(error))
        }
      },
    })

  /**
   * Mints a playlist id and stores it on one subscription.
   *
   * The mint is its own request and the store is a field patch, because the two are different
   * things: the generator writes nothing, and the patch changes one field without touching the
   * ones a row strip never saw.
   */
  const mintPlaylist = (subscription: Subscription) => {
    void generatePlaylistId
      .mutateAsync({})
      .then((answer) =>
        patchField.mutateAsync({
          id: subscription.id,
          patch: { playlistId: answer.playlistId },
        }),
      )
      .then(() => void subscriptionsQuery.refetch())
      .catch((error) => void message.error(messageForError(error)))
  }

  /**
   * The last-service refusal, as a plain sentence.
   *
   * A subrow action is a click on **one** row, so the bulk "changed N of M, the rest were refused"
   * summary is the wrong sentence for it (design decision 16). The server refuses this case with
   * `validation_failed` naming the service fields, and the honest client answer is a message that
   * says what happened rather than a report about a selection that was never made.
   */
  const refuseLastService = () =>
    void message.error({
      content: t('subscriptions.lastServiceBody'),
      key: 'subscription-last-service',
    })

  /** Whether a refusal from a strip is the last-service one. */
  const isLastServiceRefusal = (error: unknown): boolean =>
    error instanceof ApiError &&
    error.errors !== null &&
    ['playlistId', 'magMac', 'enigma2Mac'].some((field) => field in error.errors!)

  /**
   * The actions one row's service strips perform. Built once per render into a value the context
   * hands down, since `expandedRowRender` is a function of the row alone and has no prop path of
   * its own. Every one of them is the single-field patch, and every refusal is reported with
   * single-row wording.
   */
  const listActions: SubscriptionsListActions = {
    canEdit,
    copy: (value) => {
      void navigator.clipboard?.writeText(value)
      void message.success(t('subscriptions.services.copied'))
    },
    regenerate: (subscription) => mintPlaylist(subscription),
    addPlaylist: (subscription) => mintPlaylist(subscription),
    changeMac: (subscription, kind) => {
      const current = kind === 'mag' ? (subscription.magMac ?? '') : (subscription.enigma2Mac ?? '')

      setMacDialog({ subscription, kind })
      setMacValue(current)
      macForm.setFieldsValue({ mac: current })
    },
    // An empty string is "clear" on this API, which is what makes one request express both the
    // removal and the change of a field. A refusal is reported here rather than by
    // `useRowAction`, because the last-service case has its own sentence.
    clearPlaylist: (subscription) =>
      clearField(subscription, { playlistId: '' }),
    clearMag: (subscription) =>
      clearField(subscription, { magMac: '' }),
    clearEnigma2: (subscription) =>
      clearField(subscription, { enigma2Mac: '' }),
  }

  /**
   * Clears one service field and reports the outcome in single-row words.
   *
   * `useRowAction` is deliberately not used here: its error path is a toast carrying the server's
   * sentence, and the last-service refusal is better said as "the last service cannot be removed"
   * than as a field-name validation message. A success is a toast from the answer's code, which is
   * the same wording the addressed endpoint gives.
   */
  const clearField = async (
    subscription: Subscription,
    patch: { playlistId?: string; magMac?: string; enigma2Mac?: string },
  ) => {
    try {
      const answer = await patchField.mutateAsync({ id: subscription.id, patch })
      message.success(successTextFor(answer))
      void subscriptionsQuery.refetch()
    } catch (error) {
      if (isLastServiceRefusal(error)) {
        refuseLastService()
        return
      }

      message.error(messageForError(error))
    }
  }

  const saveMac = () => {
    if (!macDialog) return

    const value = (macForm.getFieldValue('mac') as string | undefined ?? macValue).trim()

    // A blank clears the device, which is the same field clear the strip's delete action performs.
    void runAction(
      () =>
        patchField.mutateAsync({
          id: macDialog.subscription.id,
          patch:
            macDialog.kind === 'mag' ? { magMac: value } : { enigma2Mac: value },
        }),
      macDialog.subscription,
    )

    closeMacDialog()
  }

  /**
   * Runs one operation over the selection: the ids, the reporting, the re-read and the clearance
   * are `useBulkAction`'s, and the request itself is `useBulkSubscriptions`'s.
   */
  const runBulk = (
    payload: Omit<Parameters<typeof bulk.mutateAsync>[0]['payload'], 'ids'>,
    onSettled?: () => void,
  ) =>
    applyBulk(
      () => bulk.mutateAsync({ payload: { ...payload, ids: selectedIds.map(String) } }),
      onSettled,
    )

  /** Asks before deleting a selection, the one bulk operation that cannot be undone. */
  const confirmBulkDelete = () => {
    modal.confirm({
      title: t('subscriptions.bulk.deleteTitle', { count: selectedIds.length }),
      content: t('subscriptions.bulk.deleteBody'),
      okText: t('subscriptions.deleteConfirmOk'),
      okButtonProps: { danger: true },
      cancelText: t('actions.cancel'),
      onOk: () => runBulk({ action: 'delete' }),
    })
  }

  /** Asks before blocking a selection, which ends sessions on every account in it. */
  const confirmBulkBlock = () => {
    modal.confirm({
      title: t('subscriptions.bulk.blockOwnerTitle', { count: selectedIds.length }),
      content: t('subscriptions.bulk.blockOwnerBody'),
      okText: t('subscriptions.blockOwnerConfirmOk'),
      okButtonProps: { danger: true },
      cancelText: t('actions.cancel'),
      onOk: () => runBulk({ action: 'block-owner' }),
    })
  }

  const onBulkMenu: MenuProps['onClick'] = ({ key }) => {
    switch (key as BulkSubscriptionAction) {
      case 'change-tariff':
        setBulkDialog('tariff')
        break
      case 'change-creator':
        setBulkDialog('creator')
        break
      case 'delete':
        confirmBulkDelete()
        break
      case 'delete-playlist':
        runBulk({ action: 'delete-playlist' })
        break
      case 'delete-mag':
        runBulk({ action: 'delete-mag' })
        break
      case 'delete-enigma2':
        runBulk({ action: 'delete-enigma2' })
        break
      case 'prolong':
        runBulk({ action: 'prolong' })
        break
      case 'block-owner':
        confirmBulkBlock()
        break
    }
  }

  /** The seven entries, in the order the menu draws them. */
  const bulkItems = useMemo<MenuProps['items']>(
    () => [
      { key: 'change-tariff', icon: <TagOutlined />, label: t('subscriptions.bulk.changeTariff') },
      {
        key: 'change-creator',
        icon: <UserSwitchOutlined />,
        label: t('subscriptions.bulk.changeCreator'),
      },
      { key: 'prolong', icon: <CalendarOutlined />, label: t('subscriptions.bulk.extend') },
      { type: 'divider' },
      {
        key: 'delete-playlist',
        icon: <UnorderedListOutlined />,
        label: t('subscriptions.bulk.deletePlaylist'),
      },
      { key: 'delete-mag', icon: <SwapOutlined />, label: t('subscriptions.bulk.deleteMag') },
      {
        key: 'delete-enigma2',
        icon: <ClockCircleOutlined />,
        label: t('subscriptions.bulk.deleteEnigma2'),
      },
      { type: 'divider' },
      { key: 'block-owner', icon: <StopOutlined />, label: t('subscriptions.bulk.blockOwner') },
      {
        key: 'delete',
        icon: <DeleteOutlined />,
        danger: true,
        label: t('subscriptions.bulk.delete'),
      },
    ],
    [t],
  )

  const bulkEnabled = canEdit && selectedIds.length > 0 && !bulkBusy

  /**
   * The name cell: owner, plan, creation date and one status tag, wrapping to a second line.
   *
   * Four facts in one cell rather than four columns, because they answer one question together -
   * whose subscription this is, what it is sold under, when it was issued and whether it works -
   * and a reader scanning the table reads them as one block.
   */
  const nameCell = (subscription: Subscription) => (
    <Space direction="vertical" size={2} style={{ lineHeight: 1.4, width: '100%' }}>
      <Space size={6} wrap>
        {/* The owner and the plan are links to the screens that own them: an operator looking at
            a row is one click from the account and the price list rather than from a search. Both
            are `extra` routes with no menu item of their own, which is exactly what a link is for. */}
        <Typography.Link
          onClick={() => navigate(`${area}/users/${subscription.userId}`)}
          style={{ fontWeight: 500 }}
        >
          {subscription.username}
        </Typography.Link>
        {subscription.status !== 'active' && (
          <Tag color={STATUS_COLOR[subscription.status]} style={{ marginInlineEnd: 0 }}>
            {t(`subscriptions.statuses.${subscription.status}`)}
          </Tag>
        )}
      </Space>
      <Space size={6} wrap style={{ opacity: 0.75, fontSize: 12 }}>
        {subscription.tariffPlanId === null ? (
          <span>{t('subscriptions.noPlan')}</span>
        ) : (
          <Typography.Link
            onClick={() => navigate(`${area}/tariff-plans/${subscription.tariffPlanId}`)}
            style={{ fontSize: 12, opacity: 0.9 }}
          >
            {subscription.tariffPlanName ?? t('subscriptions.noPlan')}
          </Typography.Link>
        )}
        <span>·</span>
        <span>{formatTimestamp(subscription.createdAt, language, timeZone)}</span>
      </Space>
    </Space>
  )

  /**
   * The expiry cell: "never" for a null expiry, red for one in the past and green otherwise.
   *
   * The colour is a description of the fact rather than a rank: an expired subscription reads red
   * because it does not work, not because red means "worse than yellow".
   */
  const expiryCell = (expireAt: string | null) => {
    if (expireAt === null) {
      return <span style={{ opacity: 0.75 }}>{t('subscriptions.neverExpires')}</span>
    }

    const expired = new Date(expireAt).getTime() <= Date.now()

    return (
      <span style={{ color: expired ? '#ff4d4f' : '#52c41a' }}>
        {formatTimestamp(expireAt, language, timeZone)}
      </span>
    )
  }

  const columns: TableColumnsType<Subscription> = [
    {
      // The two-line cell the whole table is read by, and the one that carries the creation date -
      // so its header is what controls the `createdAt` order. The default order is `createdAt`
      // desc; without this header no control reached it at all (design decision 17).
      title: t('subscriptions.columns.name'),
      key: 'createdAt',
      dataIndex: 'createdAt',
      width: widths.name,
      sorter: true,
      sortOrder: table.orderOf('createdAt'),
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('name'),
      render: (_, subscription) => nameCell(subscription),
    },
    {
      // The key **is** the server's sort field, exactly as the name column's `createdAt` is. antd
      // hands a sorter's `columnKey` over as `sortBy`, and `useServerTable` judges it against
      // `['createdAt', 'expireAt']` - so `key: 'expiry'` was rejected and the request silently
      // fell back to `createdAt` while the arrow never lit. `dataIndex` stays the data path.
      title: t('subscriptions.columns.expiry'),
      dataIndex: 'expireAt',
      key: 'expireAt',
      width: widths.expiry,
      sorter: true,
      sortOrder: table.orderOf('expireAt'),
      onHeaderCell: () => resizer('expiry'),
      render: (value: string | null) => expiryCell(value),
    },
    {
      title: t('subscriptions.columns.connections'),
      dataIndex: 'maxConnections',
      key: 'connections',
      width: widths.connections,
      align: 'center',
      onHeaderCell: () => resizer('connections'),
      render: (value: number | null) =>
        value === null ? (
          <span style={{ opacity: 0.75 }}>{t('subscriptions.unlimited')}</span>
        ) : (
          value
        ),
    },
    {
      title: t('subscriptions.columns.creator'),
      dataIndex: 'creatorUsername',
      key: 'creator',
      width: widths.creator,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('creator'),
      render: (value: string | null) => dashedCell(value),
    },
    {
      // The four row actions: edit, kill connections (a disabled stub), block owner and delete.
      title: t('subscriptions.columns.actions'),
      key: 'actions',
      width: widths.actions,
      onHeaderCell: () => resizer('actions'),
      render: (_, subscription) => {
        const busy = busyId === subscription.id

        const actions: RowAction[] = [
          {
            key: 'edit',
            label: canEdit ? t('subscriptions.actions.edit') : t('subscriptions.noPermission'),
            icon: <EditOutlined />,
            disabled: !canEdit,
            onClick: () => navigate(`${area}/subscriptions/${subscription.id}`),
          },
          // Prolongation needs a plan to take its length from, so a plan-less row draws the
          // button disabled with the reason - the server refuses the same request with a
          // validation error naming the plan field.
          {
            key: 'prolong',
            label: !canEdit
              ? t('subscriptions.noPermission')
              : subscription.tariffPlanId === null
                ? t('subscriptions.prolongNeedsPlan')
                : t('subscriptions.actions.extend'),
            icon: <CalendarOutlined />,
            disabled: !canEdit || busy || subscription.tariffPlanId === null,
            onClick: () =>
              runAction(() => prolong.mutateAsync({ id: subscription.id }), subscription),
          },
          // The streaming subsystem that would terminate connections does not exist yet: a greyed
          // button with a sentence says that before the click, where a live button answering 404
          // would only say something failed after it.
          {
            key: 'kill',
            label: t('subscriptions.actions.killConnections'),
            icon: <StopOutlined />,
            disabled: true,
          },
          {
            key: 'block-owner',
            label: canEdit
              ? t('subscriptions.actions.blockOwner')
              : t('subscriptions.noPermission'),
            icon: <UserSwitchOutlined />,
            danger: true,
            disabled: !canEdit || busy,
            onClick: () => blockOwner(subscription),
          },
          {
            key: 'delete',
            label: canEdit ? t('subscriptions.actions.delete') : t('subscriptions.noPermission'),
            icon: <DeleteOutlined />,
            danger: true,
            disabled: !canEdit || busy,
            onClick: () => confirmDelete(subscription),
          },
        ]

        return <RowActions actions={actions} />
      },
    },
  ]

  const ownerOptions = useMemo(
    () => users.map((user) => ({ value: user.id, label: user.username })),
    [users],
  )

  // The issuers: users whose role is an administrator or a reseller, which is the rule the server
  // enforces. Offering a client here would be a menu whose every entry is refused.
  const creatorOptions = useMemo(
    () =>
      users
        .filter((user) => user.role === 'Admin' || user.role === 'Reseller')
        .map((user) => ({ value: user.id, label: user.username })),
    [users],
  )

  const planOptions = useMemo(
    () => plans.map((plan) => ({ value: plan.id, label: plan.name })),
    [plans],
  )

  const statusOptions = useMemo(
    () =>
      SUBSCRIPTION_STATUSES.map((status) => ({
        value: status,
        label: t(`subscriptions.statuses.${status}`),
      })),
    [t],
  )

  const filtered = Boolean(table.query.search || ownerFilter || creatorFilter || statusFilter)

  /**
   * What the auto-refresh button says while it is greyed *or* live.
   *
   * It is a tooltip rather than a label, and the button is icon-only: the row is already the widest
   * toolbar in the application, and a Russian `Продолжить автообновление` in it would push the
   * table sideways for a control that is one glyph wide.
   */
  const refreshTip = `${t(autoRefresh ? 'subscriptions.pauseRefresh' : 'subscriptions.resumeRefresh')} (${t('subscriptions.autoRefreshHint', { seconds: AUTO_REFRESH_MS / 1000 })})`

  /**
   * Every row is expanded and the expand column carries no chevron: the strips below a row are
   * part of it, not something hidden behind a disclosure.
   */
  /**
   * Marks a row that is selected so its strips can follow.
   *
   * antd paints an expanded row's `<tr>` with its own fixed background, so the strips under a
   * selected subscription stayed unhighlighted however the row was selected. rc-table puts this
   * class on **body rows only** - the expanded `<tr>` is a row rc-table renders after it and never
   * names - so the stylesheet reaches the strips with the adjacent-sibling selector
   * (`tr.subscription-row-selected + tr.ant-table-expanded-row`) rather than through this class.
   */
  const rowClassName = (subscription: Subscription) =>
    selectedIds.some((id) => String(id) === subscription.id) ? 'subscription-row-selected' : ''

  const expandable = useMemo(
    () => ({
      // Forced-open strips with no toggle at all: the empty chevron column was the wide left gap
      // *and* the extra width that pushed the table past the card's right edge (design decision
      // 17). The strips are indented by their own cell instead.
      showExpandColumn: false,
      expandedRowKeys: (subscriptions?.items ?? []).map((row) => row.id),
      expandIcon: () => null,
      expandRowByClick: false,
      expandedRowRender: (subscription: Subscription) => (
        <SubscriptionSubrows subscription={subscription} />
      ),
    }),
    [subscriptions?.items],
  )

  return (
    <SubscriptionsListContext.Provider value={listActions}>
      <Card
        className="page-card"
        title={t('subscriptions.listTitle')}
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
            {hinted(
              canEdit ? t('subscriptions.bulk.menu') : t('subscriptions.noPermission'),
              <Dropdown menu={{ items: bulkItems, onClick: onBulkMenu }} disabled={!bulkEnabled}>
                <Button icon={<MoreOutlined />} loading={bulkBusy}>
                  {selectedIds.length > 0
                    ? t('subscriptions.bulk.menuWithCount', { count: selectedIds.length })
                    : t('subscriptions.bulk.menu')}
                </Button>
              </Dropdown>,
            )}
            <TableSearch
              value={table.searchText}
              onChange={table.setSearchText}
              onSearch={table.applySearch}
              labelKey="subscriptions.searchLabel"
              placeholderKey="subscriptions.searchPlaceholder"
              width={190}
            />
            <Select
              allowClear
              showSearch
              aria-label={t('subscriptions.filterAnyOwner')}
              style={{ width: 150 }}
              value={ownerFilter ?? undefined}
              options={ownerOptions}
              placeholder={t('subscriptions.filterAnyOwner')}
              optionFilterProp="label"
              onChange={chooseOwner}
            />
            <Select
              allowClear
              showSearch
              aria-label={t('subscriptions.filterAnyCreator')}
              style={{ width: 150 }}
              value={creatorFilter ?? undefined}
              options={creatorOptions}
              placeholder={t('subscriptions.filterAnyCreator')}
              optionFilterProp="label"
              onChange={chooseCreator}
            />
            <Select
              allowClear
              aria-label={t('subscriptions.filterAnyStatus')}
              style={{ width: 135 }}
              value={statusFilter ?? undefined}
              options={statusOptions}
              placeholder={t('subscriptions.filterAnyStatus')}
              onChange={chooseStatus}
            />
            <ResetColumnWidths
              label={t('subscriptions.resetWidths')}
              disabled={atDefaults}
              onClick={() => reset()}
            />
            {/* `hinted` only when the button is actually disabled: wrapping a working control in a
                "your group does not allow this" tooltip claims a refusal that never happens. */}
            {canEdit ? (
              <Button
                type="primary"
                icon={<PlusOutlined />}
                onClick={() => navigate(`${area}/subscriptions/add`)}
              >
                {t('subscriptions.addTitle')}
              </Button>
            ) : (
              hinted(
                t('subscriptions.noPermission'),
                <Button type="primary" icon={<PlusOutlined />} disabled>
                  {t('subscriptions.addTitle')}
                </Button>,
              )
            )}
          </Space>
        }
      >
        <ServerTable<Subscription, SubscriptionSortField>
          table={table}
          columns={columns}
          rows={subscriptions?.items ?? []}
          total={subscriptions?.total ?? 0}
          loading={loading}
          loadError={loadError}
          onRetry={() => void subscriptionsQuery.refetch()}
          tableWidth={tableWidth}
          emptyText={t(filtered ? 'subscriptions.noMatches' : 'subscriptions.empty')}
          showTotal={(total, [from, to]) => t('subscriptions.range', { from, to, total })}
          selection={{ selectedRowKeys: selectedIds, onChange: setSelectedIds }}
          expandable={expandable}
          rowClassName={rowClassName}
        />

        {/* The MAC dialog: one field, the row and the device kind it is about. A blank value
            clears the device, which is the same field clear the strip's delete action performs. */}
        <Modal
          title={t('subscriptions.changeMacTitle')}
          open={macDialog !== null}
          okText={t('subscriptions.changeMacOk')}
          cancelText={t('actions.cancel')}
          onOk={() => void macForm.submit()}
          onCancel={closeMacDialog}
        >
          {/* An antd `Input` inside an antd `Form`, not a raw `<input>`: the project uses antd
              controls only, and the form is what gives the field its label, its validation and its
              refusal placement for free. The value is canonicalized on save (the server accepts
              either separator and either case and stores `AA:BB:CC:DD:EE:FF`), and a blank value
              clears the device. */}
          <Form form={macForm} layout="vertical" onFinish={saveMac}>
            <Form.Item
              name="mac"
              label={t('subscriptions.changeMacLabel')}
              rules={[
                {
                  validator: (_, value: string | undefined) => {
                    const trimmed = (value ?? '').trim()

                    // Blank clears the device, so it is a valid answer rather than an error.
                    if (trimmed.length === 0) return Promise.resolve()

                    return isMac(trimmed)
                      ? Promise.resolve()
                      : Promise.reject(new Error(t('subscriptions.changeMacInvalid')))
                  },
                },
              ]}
            >
              <Input
                value={macValue}
                onChange={(event) => setMacValue(event.target.value)}
                placeholder="AA:BB:CC:DD:EE:FF"
                maxLength={17}
              />
            </Form.Item>
          </Form>
        </Modal>

        {/* The bulk plan change. The list is the one the filter loads, reused rather than asked
            for a second time; the sentence below says what a clearance costs. */}
        <Modal
          title={t('subscriptions.bulk.changeTariffTitle')}
          open={bulkDialog === 'tariff'}
          okText={t('subscriptions.bulk.apply')}
          okButtonProps={{ disabled: !bulkTariffId, loading: bulkBusy }}
          cancelText={t('actions.cancel')}
          onOk={() =>
            runBulk({ action: 'change-tariff', tariffPlanId: bulkTariffId ?? undefined }, closeBulkDialog)
          }
          onCancel={closeBulkDialog}
        >
          <Select
            showSearch
            allowClear
            aria-label={t('subscriptions.bulk.changeTariffPlaceholder')}
            style={{ width: '100%' }}
            value={bulkTariffId ?? undefined}
            options={planOptions}
            placeholder={t('subscriptions.bulk.changeTariffPlaceholder')}
            optionFilterProp="label"
            onChange={(value) => setBulkTariffId(value ?? null)}
          />
          <p style={{ margin: '12px 0 0' }}>{t('subscriptions.bulk.changeTariffHint')}</p>
        </Modal>

        {/* The bulk issuer change. Only administrators and resellers are offered, which is the
            rule the server enforces per row. */}
        <Modal
          title={t('subscriptions.bulk.changeCreatorTitle')}
          open={bulkDialog === 'creator'}
          okText={t('subscriptions.bulk.apply')}
          okButtonProps={{ disabled: !bulkCreatorId, loading: bulkBusy }}
          cancelText={t('actions.cancel')}
          onOk={() =>
            runBulk(
              { action: 'change-creator', resellerId: bulkCreatorId ?? undefined },
              closeBulkDialog,
            )
          }
          onCancel={closeBulkDialog}
        >
          <Select
            showSearch
            allowClear
            aria-label={t('subscriptions.bulk.changeCreatorPlaceholder')}
            style={{ width: '100%' }}
            value={bulkCreatorId ?? undefined}
            options={creatorOptions}
            placeholder={t('subscriptions.bulk.changeCreatorPlaceholder')}
            optionFilterProp="label"
            onChange={(value) => setBulkCreatorId(value ?? null)}
          />
        </Modal>
      </Card>
    </SubscriptionsListContext.Provider>
  )
}
