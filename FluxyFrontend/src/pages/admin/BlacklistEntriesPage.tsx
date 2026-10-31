import { useState } from 'react'
import type { ReactNode } from 'react'
import { App, Button, Card, Form, Modal, Space } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { ClearOutlined, DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons'
import { BLACKLISTS_PAGE_SIZE } from '../../features/blacklists/api'
import type { BlacklistEntryValues, BlacklistSortField } from '../../features/blacklists/api'
import type { BlacklistEntry, BlacklistType } from '../../features/blacklists/types'
import BlacklistEntryFields from '../../features/blacklists/components/BlacklistEntryFields'
import {
  useClearBlacklistEntries,
  useCreateBlacklistEntry,
  useDeleteBlacklistEntry,
  useUpdateBlacklistEntry,
} from '../../features/blacklists/hooks/mutations'
import { useBlacklistEntries } from '../../features/blacklists/hooks/queries'
import { useProfile } from '../../features/profile/hooks/queries'
import { ResetColumnWidths, RowActions, ServerTable, TableSearch } from '../../components/table'
import type { ResizableHeaderCellProps, RowAction } from '../../components/table'
import { useColumnWidths, useRowAction, useServerTable } from '../../hooks/table'
import { formatTimestamp } from '../../utils/dateFormat'
import { ApiError, fieldErrors, messageForError, successTextFor } from '../../utils/http'
import { hinted } from '../../utils/hinted'
import { tableResults } from '../../utils/tableResults'
import { dashedCell, idCell, textCell } from '../../utils/tableCells'
import { actionsColumnWidth } from '../../utils/tableWidths'

/** The six columns, in the order they are drawn. */
type BlacklistColumnKey = 'id' | 'value' | 'blockedAttempts' | 'createdAt' | 'note' | 'actions'

const COLUMN_KEYS: readonly BlacklistColumnKey[] = [
  'id',
  'value',
  'blockedAttempts',
  'createdAt',
  'note',
  'actions',
]

/**
 * The width each column starts at. The six sum to **1190** - the same total every other table
 * here reaches - with the value and the note taking the room the rest does not need, because
 * those are the two columns that carry text nobody wants cut in half. The actions column is
 * exactly as wide as the two buttons a row draws.
 */
const DEFAULT_COLUMN_WIDTHS: Record<BlacklistColumnKey, number> = {
  id: 110,
  value: 340,
  blockedAttempts: 160,
  createdAt: 180,
  note: 260,
  actions: actionsColumnWidth(2),
}

/** Where this table remembers the widths the visitor gave its columns. */
const COLUMN_WIDTHS_KEY = 'fluxy.blacklists.columnWidths'

/**
 * Which list each of the three section routes works on, keyed by the menu key the router hands
 * every section page as its `sectionKey`.
 *
 * The router renders every menu item as `<Page sectionKey={item.labelKey} />` and offers no
 * other prop, so the one fact that separates this page's three instances - the list - has to
 * arrive through the key the item already carries. The keys here are written beside the very
 * table that generates them (`NAVIGATION` in `src/utils/navigation.ts`), and `probe:render`
 * renders each of the three to assert its own heading and its own value heading - a key renamed
 * on the menu side only would fall back to the IP list silently, which is exactly the drift
 * this map plus the probe exists to catch.
 */
const SECTION_KINDS: Record<string, BlacklistType> = {
  'nav.items.blacklistIp': 'ip',
  'nav.items.blacklistIsp': 'isp',
  'nav.items.blacklistUa': 'user-agent',
}

interface BlacklistEntriesPageProps {
  /**
   * The menu key every one of the three routes carries. Read here, unlike most section pages:
   * the three addresses differ only in the list they show, and the key is where that fact
   * travels. See `SECTION_KINDS`.
   */
  sectionKey: string
}

/**
 * One of the three access-control blacklists: the IP, ISP or user-agent entries the future
 * public API will refuse traffic from.
 *
 * **One component behind three menu entries.** The three screens differ in the list they read,
 * the heading they draw and the two labels around the value field - and in nothing else - so a
 * page per kind would be three copies of one table to keep in step. The list arrives through
 * `sectionKey`, mapped by `SECTION_KINDS`.
 *
 * Paging, search and sorting are server side, for the reason every other table's are: `total`.
 * **There is no selection column, no bulk menu and no auto-refresh** - a blacklist entry is one
 * rule an operator maintains, not a set they run together, and a counter that only changes when
 * enforcement runs is not a number worth polling for.
 *
 * The add and edit dialog is one `Modal` inside the page rather than two form pages, because
 * an entry is two fields: a second screen for two inputs would be a page whose only content is
 * the button that brought the visitor to it. Edit prefills from the row, which already carries
 * both fields - a fetch by URL would add a request and a spinner for data on screen.
 */
export default function BlacklistEntriesPage({ sectionKey }: BlacklistEntriesPageProps) {
  const { t, i18n } = useTranslation()
  const { message, modal } = App.useApp()

  const type = SECTION_KINDS[sectionKey] ?? 'ip'

  const table = useServerTable<BlacklistEntry, BlacklistSortField>({
    // The default order the specification names: newest added first.
    defaultSort: { by: 'createdAt', order: 'desc' },
    sortFields: ['value', 'blockedAttempts', 'createdAt'],
    pageSize: BLACKLISTS_PAGE_SIZE,
  })

  const entriesQuery = useBlacklistEntries({ type, ...table.query })
  const { data: entries, loadError, loading } = tableResults(entriesQuery)

  // The pair is its own grant, like every other resource: reading the list needs
  // `viewBlacklists` and changing it needs `editBlacklists`. Absent a profile answer the page
  // draws every write disabled rather than guessing that the visitor may write. The same
  // answer carries the account's time zone, which the date column is printed in.
  const profile = useProfile()
  const granted = profile.data?.permissions ?? []
  const canEdit = granted.includes('editBlacklists')

  const locale = i18n.resolvedLanguage ?? i18n.language
  const timeZone = profile.data?.timeZone ?? null

  const { widths, tableWidth, atDefaults, reset, resizer: handleFor } =
    useColumnWidths<BlacklistColumnKey>({
      keys: COLUMN_KEYS,
      defaults: DEFAULT_COLUMN_WIDTHS,
      storageKey: COLUMN_WIDTHS_KEY,
    })

  const createEntry = useCreateBlacklistEntry()
  const updateEntry = useUpdateBlacklistEntry()
  const removeEntry = useDeleteBlacklistEntry()
  const clearEntries = useClearBlacklistEntries()

  const { busyId, confirmDelete: askDelete } = useRowAction<BlacklistEntry>({
    refetch: () => void entriesQuery.refetch(),
    rows: entries?.items,
    page: table.page,
    setPage: table.setPage,
  })

  /** Whether the dialog is open, and the row it edits when it is. */
  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState<BlacklistEntry | null>(null)
  /** The dialog's write is in flight - either half of it, since one dialog submits both. */
  const saving = createEntry.isPending || updateEntry.isPending

  const [form] = Form.useForm<BlacklistEntryValues>()

  /**
   * The heading of one column, which is also the name the resize handle announces.
   *
   * The value column's heading is per-kind and lives one level deeper than the other five, so
   * this cannot be a plain `t(\`blacklists.columns.${key}\`)`: for `value` that path is an
   * object rather than a string, and i18next answers an object with its own warning and the
   * key in place of the text. One function is what keeps the heading and the handle's label
   * from being spelled two different ways.
   */
  const headingOf = (key: BlacklistColumnKey): string =>
    key === 'value'
      ? t(`blacklists.columns.value.${type}`)
      : t(`blacklists.columns.${key}`)

  const resizer = (key: BlacklistColumnKey): ResizableHeaderCellProps =>
    handleFor(key, t('blacklists.resizeColumn', { column: headingOf(key) }))

  /** Opens the add dialog with an empty form. */
  const openCreate = () => {
    form.resetFields()
    setCreating(true)
  }

  /** Opens the edit dialog with the row's value and note already in it. */
  const openEdit = (entry: BlacklistEntry) => {
    form.setFieldsValue({ value: entry.value, note: entry.note ?? '' })
    setEditing(entry)
  }

  /** Submits whichever dialog is open, mapping field errors under the inputs they belong to. */
  const submitDialog = async () => {
    const values = await form.validateFields()

    try {
      const result = editing
        ? await updateEntry.mutateAsync({ id: editing.id, values })
        : await createEntry.mutateAsync({ type, values })

      message.success(successTextFor(result))
      setCreating(false)
      setEditing(null)
      void entriesQuery.refetch()
    } catch (error) {
      const fields = fieldErrors(error instanceof ApiError ? error.errors : null)

      if (fields.length > 0) {
        form.setFields(
          fields.map((field) => ({
            // The server keys refusals by the camelCase JSON name, which for this form is the
            // name the `Form.Item` collects - the cast bridges two type systems, not two
            // spellings, exactly as on the category and bouquet dialogs.
            name: field.name as keyof BlacklistEntryValues,
            errors: field.errors,
          })),
        )
      } else {
        message.error(messageForError(error))
      }
    }
  }

  const confirmDelete = (entry: BlacklistEntry) =>
    askDelete(entry, {
      name: entry.value,
      titleKey: 'blacklists.deleteConfirmTitle',
      bodyKey: 'blacklists.deleteConfirmBody',
      okKey: 'blacklists.deleteConfirmOk',
      run: () => removeEntry.mutateAsync({ id: entry.id }),
    })

  /**
   * Empties the whole list, after asking once.
   *
   * The command names the type and not the search, so it clears the list the operator is on
   * rather than the rows a filter happens to admit - and that is exactly why it asks. It is
   * deliberately **not** disabled on an empty or search-narrowed view: `total` counts what the
   * search admits, so a button greyed out because the filtered view is empty would be lying
   * about a list that still holds rows. The confirmation is the guard.
   */
  const confirmClear = () => {
    modal.confirm({
      title: t('blacklists.clearConfirmTitle', { list: t(`blacklists.titles.${type}`) }),
      content: t('blacklists.clearConfirmText', { list: t(`blacklists.titles.${type}`) }),
      okText: t('blacklists.clearAll'),
      okButtonProps: { danger: true },
      cancelText: t('actions.cancel'),
      onOk: async () => {
        try {
          const result = await clearEntries.mutateAsync({ type })

          message.success(successTextFor(result))
          void entriesQuery.refetch()
        } catch (error) {
          message.error(messageForError(error))
        }
      },
    })
  }

  const columns: TableColumnsType<BlacklistEntry> = [
    {
      title: headingOf('id'),
      dataIndex: 'id',
      key: 'id',
      width: widths.id,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('id'),
      render: idCell,
    },
    {
      // The column this page is read by, and the one whose heading says which list it is: an
      // address on the IP screen, a provider on the ISP one, a raw user agent on the UA one.
      // The heading is per-kind rather than a single "Value", because three identical screens
      // under three menu entries is exactly what the entry above the table must disambiguate.
      title: headingOf('value'),
      dataIndex: 'value',
      key: 'value',
      width: widths.value,
      sorter: true,
      sortOrder: table.orderOf('value'),
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('value'),
      render: textCell,
    },
    {
      // The counter the enforcement increments. A number in a narrow column, centred: the
      // values are small and a column of right-aligned digits under a word reads worse.
      title: headingOf('blockedAttempts'),
      dataIndex: 'blockedAttempts',
      key: 'blockedAttempts',
      width: widths.blockedAttempts,
      sorter: true,
      sortOrder: table.orderOf('blockedAttempts'),
      align: 'center',
      onHeaderCell: () => resizer('blockedAttempts'),
    },
    {
      title: headingOf('createdAt'),
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: widths.createdAt,
      sorter: true,
      sortOrder: table.orderOf('createdAt'),
      onHeaderCell: () => resizer('createdAt'),
      render: (value: string | null) => formatTimestamp(value, locale, timeZone),
    },
    {
      // The note an operator left beside the entry, truncated into a tooltip like the value:
      // it is free text of up to a thousand characters and the row is one line.
      title: headingOf('note'),
      dataIndex: 'note',
      key: 'note',
      width: widths.note,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('note'),
      render: dashedCell,
    },
    {
      title: headingOf('actions'),
      key: 'actions',
      width: widths.actions,
      onHeaderCell: () => resizer('actions'),
      render: (_, entry) => {
        const busy = busyId === entry.id

        const actions: RowAction[] = [
          {
            key: 'edit',
            label: canEdit ? t('blacklists.actions.edit') : t('blacklists.noPermission'),
            icon: <EditOutlined />,
            disabled: !canEdit || busy,
            onClick: () => openEdit(entry),
          },
          {
            key: 'delete',
            label: canEdit ? t('blacklists.actions.delete') : t('blacklists.noPermission'),
            icon: <DeleteOutlined />,
            danger: true,
            loading: busy,
            disabled: !canEdit || busy,
            onClick: () => confirmDelete(entry),
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
      // The heading names the list, because the three addresses of this page differ in nothing
      // else - and the card saying only "Blacklists" on all three would leave the visitor
      // reading three identical screens with no way to tell which one they are on.
      title={t(`blacklists.titles.${type}`)}
      extra={
        <Space wrap>
          <TableSearch
            value={table.searchText}
            onChange={table.setSearchText}
            onSearch={table.applySearch}
            labelKey="blacklists.searchLabel"
            placeholderKey="blacklists.searchPlaceholder"
            width={200}
          />
          <ResetColumnWidths
            label={t('blacklists.resetWidths')}
            disabled={atDefaults}
            onClick={() => reset()}
          />
          {/* Immediately left of the add button, on the owner's call. Drawn but never disabled
              for a short or filtered list: `total` counts what the search admits, and a clear
              button greyed out because the filtered view is empty would be lying about a list
              that still holds rows. The confirmation is what guards it. */}
          {hintedButton(
            !canEdit,
            t('blacklists.noPermission'),
            <Button
              danger
              icon={<ClearOutlined />}
              disabled={!canEdit}
              loading={clearEntries.isPending}
              onClick={confirmClear}
            >
              {t('blacklists.clearAll')}
            </Button>,
          )}
          {hintedButton(
            !canEdit,
            t('blacklists.noPermission'),
            <Button type="primary" icon={<PlusOutlined />} disabled={!canEdit} onClick={openCreate}>
              {t('blacklists.addTitle')}
            </Button>,
          )}
        </Space>
      }
    >
      <ServerTable<BlacklistEntry, BlacklistSortField>
        table={table}
        columns={columns}
        rows={entries?.items ?? []}
        total={entries?.total ?? 0}
        loading={loading}
        loadError={loadError}
        onRetry={() => void entriesQuery.refetch()}
        tableWidth={tableWidth}
        emptyText={t(filtered ? 'blacklists.noMatches' : 'blacklists.empty')}
        showTotal={(total, [from, to]) => t('blacklists.range', { from, to, total })}
      />

      <Modal
        title={editing ? t('blacklists.editTitle') : t('blacklists.addTitle')}
        open={creating || editing !== null}
        onOk={() => void submitDialog()}
        confirmLoading={saving}
        onCancel={() => {
          setCreating(false)
          setEditing(null)
        }}
        destroyOnHidden
      >
        <Form form={form} layout="vertical">
          {/* Value and note only: the type is the screen, the counter belongs to the
              enforcement and the added date is stamped once. */}
          <BlacklistEntryFields type={type} />
        </Form>
      </Modal>
    </Card>
  )
}

/**
 * A button that carries the reason it is disabled.
 *
 * `hinted` alone would put a tooltip on every state, including the enabled one where the reason
 * is noise; here the wrapper appears only when the visitor may not write at all, which is the
 * one case the control cannot explain itself. The same helper the bouquets table draws.
 */
const hintedButton = (disabled: boolean, reason: string, button: ReactNode): ReactNode =>
  disabled ? hinted(reason, button) : button
