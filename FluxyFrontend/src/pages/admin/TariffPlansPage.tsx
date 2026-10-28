import { useNavigate } from 'react-router-dom'
import type { ReactNode } from 'react'
import { Button, Card, Space, Tag, Tooltip } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons'
import { TARIFF_PLAN_PAGE_SIZE } from '../../features/tariff-plans/api'
import type { TariffPlanSortField } from '../../features/tariff-plans/api'
import { formatTariffPlanDuration } from '../../features/tariff-plans/durationText'
import type { TariffPlan } from '../../features/tariff-plans/types'
import { useDeleteTariffPlan } from '../../features/tariff-plans/hooks/mutations'
import { useTariffPlans } from '../../features/tariff-plans/hooks/queries'
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

/** The eight columns, in the order they are drawn. */
type PlanColumnKey =
  | 'id'
  | 'name'
  | 'cost'
  | 'duration'
  | 'services'
  | 'connections'
  | 'bouquets'
  | 'actions'

const COLUMN_KEYS: readonly PlanColumnKey[] = [
  'id',
  'name',
  'cost',
  'duration',
  'services',
  'connections',
  'bouquets',
  'actions',
]

/**
 * The width each column starts at. The fixed ones plus the actions column leave the name the
 * rest of the **1190** total every table here reaches, because a plan's name is the one thing
 * an operator scans the list by and the one column nothing truncates a second time.
 *
 * The actions column is wider than `actionsColumnWidth(2)` alone would make it, on the owner's
 * call: two icon buttons fit in the exact width, and the extra room is what keeps the pair off
 * the row's edge when a longer locale is in play. The trial tag lives beside the name rather
 * than in a column of its own, so it costs the name's width and nothing else.
 */
const FIXED_COLUMN_WIDTHS = {
  id: 100,
  cost: 110,
  duration: 130,
  services: 210,
  connections: 130,
  bouquets: 120,
}

const ACTIONS_WIDTH = actionsColumnWidth(2) + 16
const TABLE_WIDTH = 1190

const DEFAULT_COLUMN_WIDTHS: Record<PlanColumnKey, number> = {
  ...FIXED_COLUMN_WIDTHS,
  name:
    TABLE_WIDTH -
    Object.values(FIXED_COLUMN_WIDTHS).reduce((sum, width) => sum + width, 0) -
    ACTIONS_WIDTH,
  actions: ACTIONS_WIDTH,
}

/** Where this table remembers the widths the visitor gave its columns. */
const COLUMN_WIDTHS_KEY = 'fluxy.tariffPlans.columnWidths'

interface TariffPlansPageProps {
  /** The menu key this route carries, accepted and deliberately unread. */
  sectionKey: string
}

/**
 * The admin's tariff plans - the price list a subscription is sold under: the page behind
 * `nav.items.tariffPlans`.
 *
 * Paging, search and sorting are server side, for the reason every other table's are:
 * `total`. **There is no selection column and no bulk menu** - the owner asked for neither,
 * and a plan is one row an operator edits rather than a set they run together. **There is no
 * drag either**: a plan has no position, and the list is ordered by the column the visitor
 * sorted it by.
 *
 * The row's two actions are the two the domain has: edit, which opens the form, and delete,
 * which asks first because it is the one irreversible move.
 *
 * The card is headed `tariffPlans.listTitle` rather than by the menu item's own name, the
 * reason every table here is: the rail says how the visitor got here, and the card says what
 * they are looking at.
 */
export default function TariffPlansPage(_props: TariffPlansPageProps) {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const session = useSession()

  const table = useServerTable<TariffPlan, TariffPlanSortField>({
    defaultSort: { by: 'name', order: 'asc' },
    sortFields: ['name', 'cost', 'createdAt'],
    pageSize: TARIFF_PLAN_PAGE_SIZE,
  })

  const plansQuery = useTariffPlans(table.query)
  const { data: plans, loadError, loading } = tableResults(plansQuery)

  const profileQuery = useProfile()
  const granted = profileQuery.data?.permissions ?? []
  const canEdit = granted.includes('editTariffPlans')

  const { widths, tableWidth, atDefaults, reset, resizer: handleFor } =
    useColumnWidths<PlanColumnKey>({
      keys: COLUMN_KEYS,
      defaults: DEFAULT_COLUMN_WIDTHS,
      storageKey: COLUMN_WIDTHS_KEY,
    })

  const area = session ? areaForRole(session.role) : ''

  const removePlan = useDeleteTariffPlan()

  const { busyId, confirmDelete: askDelete } = useRowAction<TariffPlan>({
    refetch: () => void plansQuery.refetch(),
    rows: plans?.items,
    page: table.page,
    setPage: table.setPage,
  })

  const resizer = (key: PlanColumnKey): ResizableHeaderCellProps =>
    handleFor(key, t('tariffPlans.resizeColumn', { column: t(`tariffPlans.columns.${key}`) }))

  const confirmDelete = (plan: TariffPlan) =>
    askDelete(plan, {
      name: plan.name,
      titleKey: 'tariffPlans.deleteConfirmTitle',
      bodyKey: 'tariffPlans.deleteConfirmBody',
      okKey: 'tariffPlans.deleteConfirmOk',
      run: () => removePlan.mutateAsync({ id: plan.id }),
    })

  /**
   * The name cell: the plan's name with the trial tag beside it.
   *
   * The tag moved here from a column of its own on the owner's call. A trial is a property of
   * the plan rather than a value a reader compares down the table, so it rides beside the one
   * word it qualifies - and the column that held it gives its room to the name.
   */
  const nameCell = (name: string, plan: TariffPlan): ReactNode => (
    <Space size={6}>
      <Tooltip title={name}>
        <span
          style={{
            overflow: 'hidden',
            textOverflow: 'ellipsis',
            whiteSpace: 'nowrap',
            maxWidth: Math.max(widths.name - 90, 40),
          }}
        >
          {name}
        </span>
      </Tooltip>
      {plan.isTrial && <Tag color="gold">{t('tariffPlans.trialBadge')}</Tag>}
    </Space>
  )

  /**
   * The services and formats cell, drawn as two lines: delivery on the first, output formats on
   * the second.
   *
   * The two sets were separate columns and are one now, on the owner's call: they answer one
   * question between them - what does a subscription on this plan actually get - and a reader
   * comparing plans reads the pair together. The second line is muted so the delivery methods,
   * which are what a customer asks about, stay the first thing the eye lands on.
   */
  const servicesCell = (plan: TariffPlan): ReactNode => {
    const delivery: ReactNode[] = []

    if (plan.supportsM3u) delivery.push(<Tag key="m3u">{t('tariffPlans.delivery.m3u')}</Tag>)
    if (plan.supportsMag) delivery.push(<Tag key="mag">{t('tariffPlans.delivery.mag')}</Tag>)
    if (plan.supportsEnigma2)
      delivery.push(<Tag key="enigma2">{t('tariffPlans.delivery.enigma2')}</Tag>)

    const formats: ReactNode[] = []

    if (plan.outputMpegTs)
      formats.push(<Tag key="mpegts">{t('tariffPlans.formats.mpegTs')}</Tag>)
    if (plan.outputHls) formats.push(<Tag key="hls">{t('tariffPlans.formats.hls')}</Tag>)

    return (
      <Space direction="vertical" size={2} style={{ lineHeight: 1.4 }}>
        <Space size={4} wrap>
          {delivery}
        </Space>
        <Space size={4} wrap style={{ opacity: 0.75 }}>
          {formats}
        </Space>
      </Space>
    )
  }

  const columns: TableColumnsType<TariffPlan> = [
    {
      title: t('tariffPlans.columns.id'),
      dataIndex: 'id',
      key: 'id',
      width: widths.id,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('id'),
      render: idCell,
    },
    {
      title: t('tariffPlans.columns.name'),
      dataIndex: 'name',
      key: 'name',
      width: widths.name,
      sorter: true,
      sortOrder: table.orderOf('name'),
      onHeaderCell: () => resizer('name'),
      render: nameCell,
    },
    {
      title: t('tariffPlans.columns.cost'),
      dataIndex: 'cost',
      key: 'cost',
      width: widths.cost,
      sorter: true,
      sortOrder: table.orderOf('cost'),
      // Centred, and the number alone: the column's own heading says what the unit is, and the
      // owner asked for the word "credits" not to be repeated on every row.
      align: 'center',
      onHeaderCell: () => resizer('cost'),
      render: (cost: number) => cost,
    },
    {
      title: t('tariffPlans.columns.duration'),
      key: 'duration',
      width: widths.duration,
      // Centred like the other numeric columns: a period is a value a reader compares down the
      // table, not a sentence at the start of a line.
      align: 'center',
      onHeaderCell: () => resizer('duration'),
      // Always drawn, so translation has one place to say "3 дня" in the language the visitor
      // reads; the number and the noun agree through `formatTariffPlanDuration`, which knows the
      // three Russian forms this application's locale bundles cannot express.
      render: (_, plan) =>
        formatTariffPlanDuration(
          plan.durationValue,
          plan.durationUnit,
          i18n.resolvedLanguage,
        ),
    },
    {
      title: t('tariffPlans.columns.services'),
      key: 'services',
      width: widths.services,
      onHeaderCell: () => resizer('services'),
      render: (_, plan) => servicesCell(plan),
    },
    {
      title: t('tariffPlans.columns.connections'),
      dataIndex: 'maxConnections',
      key: 'connections',
      width: widths.connections,
      align: 'center',
      onHeaderCell: () => resizer('connections'),
      render: (count: number) => count,
    },
    {
      title: t('tariffPlans.columns.bouquets'),
      dataIndex: 'bouquetCount',
      key: 'bouquets',
      width: widths.bouquets,
      align: 'center',
      onHeaderCell: () => resizer('bouquets'),
      render: (count: number) => count,
    },
    {
      title: t('tariffPlans.columns.actions'),
      key: 'actions',
      width: widths.actions,
      onHeaderCell: () => resizer('actions'),
      render: (_, plan) => {
        const busy = busyId === plan.id

        const actions: RowAction[] = [
          {
            key: 'edit',
            label: canEdit ? t('tariffPlans.actions.edit') : t('tariffPlans.noPermission'),
            icon: <EditOutlined />,
            disabled: !canEdit,
            onClick: () => navigate(`${area}/tariff-plans/${plan.id}`),
          },
          {
            key: 'delete',
            label: canEdit ? t('tariffPlans.actions.delete') : t('tariffPlans.noPermission'),
            icon: <DeleteOutlined />,
            danger: true,
            disabled: !canEdit || busy,
            onClick: () => confirmDelete(plan),
          },
        ]

        return <RowActions actions={actions} />
      },
    },
  ]

  const filtered = Boolean(table.query.search)

  return (
    <Card
      className="page-card"
      title={t('tariffPlans.listTitle')}
      extra={
        <Space wrap>
          <TableSearch
            value={table.searchText}
            onChange={table.setSearchText}
            onSearch={table.applySearch}
            labelKey="tariffPlans.searchLabel"
            placeholderKey="tariffPlans.searchPlaceholder"
            width={220}
          />
          <ResetColumnWidths
            label={t('tariffPlans.resetWidths')}
            disabled={atDefaults}
            onClick={() => reset()}
          />
          {hinted(
            t('tariffPlans.noPermission'),
            <Button
              type="primary"
              icon={<PlusOutlined />}
              disabled={!canEdit}
              onClick={() => navigate(`${area}/tariff-plans/add`)}
            >
              {t('tariffPlans.addTitle')}
            </Button>,
          )}
        </Space>
      }
    >
      <ServerTable<TariffPlan, TariffPlanSortField>
        table={table}
        columns={columns}
        rows={plans?.items ?? []}
        total={plans?.total ?? 0}
        loading={loading}
        loadError={loadError}
        onRetry={() => void plansQuery.refetch()}
        tableWidth={tableWidth}
        emptyText={t(filtered ? 'tariffPlans.noMatches' : 'tariffPlans.empty')}
        showTotal={(total, [from, to]) => t('tariffPlans.range', { from, to, total })}
      />
    </Card>
  )
}
