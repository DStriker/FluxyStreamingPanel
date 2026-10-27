import { createContext, useContext, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { Alert, App, Button, Card, Form, Input, Modal, Space, Table } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import {
  DeleteOutlined,
  EditOutlined,
  HolderOutlined,
  PlusOutlined,
  UnorderedListOutlined,
} from '@ant-design/icons'
import {
  DndContext,
  KeyboardSensor,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors,
} from '@dnd-kit/core'
import type { DragEndEvent, DraggableAttributes } from '@dnd-kit/core'
import {
  SortableContext,
  arrayMove,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import type { SyntheticListenerMap } from '@dnd-kit/core/dist/hooks/utilities'
import { useNavigate } from 'react-router-dom'
import { bouquetItemCount } from '../../features/bouquets/types'
import type { Bouquet } from '../../features/bouquets/types'
import { ApiError, fieldErrors, messageForError, successTextFor } from '../../utils/http'
import { actionsColumnWidth } from '../../utils/tableWidths'
import { RowActions } from '../../components/table'
import type { RowAction } from '../../components/table'
import {
  useCreateBouquet,
  useDeleteBouquet,
  useRenameBouquet,
  useReorderBouquets,
} from '../../features/bouquets/hooks/mutations'
import { useBouquets } from '../../features/bouquets/hooks/queries'
import { useProfile } from '../../features/profile/hooks/queries'
import { useRowAction } from '../../hooks/table'
import { useSession } from '../../utils/sessionContext'
import { areaForRole } from '../../utils/session'
import { hinted } from '../../utils/hinted'

interface BouquetsPageProps {
  /** The menu key the route carries; accepted and deliberately unread - the card names the list. */
  sectionKey: string
}

/** What the one field of both dialogs collects. */
interface DialogValues {
  name: string
}

/**
 * What the sortable row hands to the handle cell inside it.
 *
 * antd builds the cells of a row from the column config, so the `useSortable` hook that
 * lives on the row cannot pass its listeners down as props - there is no prop path from a
 * row component into one cell of it. A context is the whole of the bridge: the row
 * provides, the handle consumes, and the drag begins from the handle alone rather than
 * from anywhere on the row.
 */
interface RowDragContextValue {
  setActivatorNodeRef?: (element: HTMLElement | null) => void
  attributes?: DraggableAttributes
  listeners?: SyntheticListenerMap
}

const RowDragContext = createContext<RowDragContextValue>({})

/** The row antd renders, wrapped in the sortable behaviour keyed by its own row key. */
function SortableRow(props: Record<string, unknown> & { children?: ReactNode }) {
  const rowKey = String(props['data-row-key'] ?? '')
  const {
    attributes,
    listeners,
    setNodeRef,
    setActivatorNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: rowKey })

  const value = useMemo<RowDragContextValue>(
    () => ({ setActivatorNodeRef, attributes, listeners }),
    [setActivatorNodeRef, attributes, listeners],
  )

  return (
    <RowDragContext.Provider value={value}>
      <tr
        {...props}
        ref={setNodeRef}
        style={{
          ...(props.style as Record<string, unknown> | undefined),
          transform: CSS.Translate.toString(transform),
          transition,
          ...(isDragging ? { position: 'relative', zIndex: 999 } : {}),
        }}
      >
        {props.children}
      </tr>
    </RowDragContext.Provider>
  )
}

/** The handle a row is dragged by, and the only thing that starts a drag. */
function DragHandle({ label, disabled }: { label: string; disabled: boolean }) {
  const { setActivatorNodeRef, attributes, listeners } = useContext(RowDragContext)

  return (
    <Button
      ref={setActivatorNodeRef}
      type="text"
      size="small"
      icon={<HolderOutlined />}
      aria-label={label}
      title={label}
      disabled={disabled}
      style={{ cursor: disabled ? 'not-allowed' : 'grab' }}
      {...attributes}
      {...listeners}
    />
  )
}

/**
 * The bouquets table - the packages content is delivered in.
 *
 * **The order is the feature, and it is arranged by dragging but saved by a button**, for
 * the reason the categories table gives: the rows arrive in the order the operator last
 * saved, a drag moves them on screen only, and the whole arrangement reaches the server
 * through one explicit `PUT order`. One arrangement, one request, however many rows moved.
 *
 * **Deleting is refused while the bouquet holds anything.** The server says so with a
 * count, and the button says so before the click - it is disabled with the reason on the
 * row's own tooltip, because a refused request is a worse way to learn it than a greyed
 * control that explains itself. Emptying a bouquet is the content manager's job, which is
 * the row's second action.
 *
 * The two writes that make up the domain - the package and its content - are separate
 * screens: this one owns the names and their order, and `BouquetContentPage` owns what is
 * inside each one.
 */
export default function BouquetsPage(_props: BouquetsPageProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const navigate = useNavigate()
  const session = useSession()

  const bouquetsQuery = useBouquets()
  const serverItems = bouquetsQuery.data?.items
  const loadError = bouquetsQuery.isError ? messageForError(bouquetsQuery.error) : null
  const loading = bouquetsQuery.isFetching

  // The bouquet pair is its own grant, like every other content resource: reading the list
  // needs `viewBouquets` and changing it needs `editBouquets`. Absent a profile answer the
  // page draws every write disabled rather than guessing that the visitor may write.
  const granted = useProfile().data?.permissions ?? []
  const canEdit = granted.includes('editBouquets')

  const createBouquet = useCreateBouquet()
  const renameBouquet = useRenameBouquet()
  const reorder = useReorderBouquets()
  const removeBouquet = useDeleteBouquet()

  const { busyId, confirmDelete: askDelete } = useRowAction<Bouquet>({
    refetch: () => void bouquetsQuery.refetch(),
  })

  /**
   * The arrangement the visitor has built and not saved yet, as identifiers in screen
   * order - or null while the screen still shows the server's order. Held apart from the
   * query cache on purpose: the cache is what the server last confirmed, and an
   * arrangement that is not confirmed yet has no business in it.
   */
  const [pendingIds, setPendingIds] = useState<string[] | null>(null)
  const [creating, setCreating] = useState(false)
  const [renaming, setRenaming] = useState<Bouquet | null>(null)
  const saving = createBouquet.isPending || renameBouquet.isPending

  const [form] = Form.useForm<DialogValues>()

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 5 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  )

  const serverIds = useMemo(() => (serverItems ?? []).map((bouquet) => bouquet.id), [serverItems])

  /** The rows the table draws: the pending arrangement when there is one. */
  const items = useMemo<Bouquet[]>(() => {
    const list = serverItems ?? []

    if (!pendingIds) return list

    const byId = new Map(list.map((bouquet) => [bouquet.id, bouquet]))
    const kept = pendingIds
      .map((id) => byId.get(id))
      .filter((bouquet): bouquet is Bouquet => bouquet !== undefined)
    const added = list.filter((bouquet) => !pendingIds.includes(bouquet.id))

    return [...kept, ...added]
  }, [serverItems, pendingIds])

  /** Whether what is on screen is not what the server holds - the state the toolbar reports. */
  const dirty =
    pendingIds !== null &&
    (pendingIds.length !== serverIds.length || pendingIds.some((id, index) => id !== serverIds[index]))

  const handleDragEnd = (event: DragEndEvent) => {
    const { active, over } = event

    if (!over || active.id === over.id || reorder.isPending || !canEdit) return

    const ids = items.map((bouquet) => bouquet.id)
    const oldIndex = ids.indexOf(String(active.id))
    const newIndex = ids.indexOf(String(over.id))

    // A foreign id or a race with a re-read can leave both lookups empty; either way there
    // is no move to apply, and inventing one from -1s would scramble the list.
    if (oldIndex < 0 || newIndex < 0) return

    setPendingIds(arrayMove(ids, oldIndex, newIndex))
  }

  /** Sends the arrangement on screen as the new order: one request, the whole list. */
  const saveOrder = async () => {
    if (!dirty || reorder.isPending) return

    try {
      const result = await reorder.mutateAsync({ orderedIds: items.map((bouquet) => bouquet.id) })

      message.success(successTextFor(result))
      setPendingIds(null)
      void bouquetsQuery.refetch()
    } catch (error) {
      message.error(messageForError(error))
    }
  }

  const discardOrder = () => setPendingIds(null)

  const openCreate = () => {
    form.resetFields()
    setCreating(true)
  }

  const openRename = (bouquet: Bouquet) => {
    form.setFieldsValue({ name: bouquet.name })
    setRenaming(bouquet)
  }

  const submitDialog = async () => {
    const values = await form.validateFields()

    try {
      const result = renaming
        ? await renameBouquet.mutateAsync({ id: renaming.id, name: values.name })
        : await createBouquet.mutateAsync({ name: values.name })

      message.success(successTextFor(result))
      setCreating(false)
      setRenaming(null)
      void bouquetsQuery.refetch()
    } catch (error) {
      const fields = fieldErrors(error instanceof ApiError ? error.errors : null)

      if (fields.length > 0) {
        form.setFields(
          fields.map((field) => ({
            // The server keys refusals by the camelCase JSON name, which for this form is
            // the one string the `Form.Item` collects - the cast bridges two type systems,
            // not two spellings, exactly as on the categories page.
            name: field.name as keyof DialogValues,
            errors: field.errors,
          })),
        )
      } else {
        message.error(messageForError(error))
      }
    }
  }

  const confirmDelete = (bouquet: Bouquet) =>
    askDelete(bouquet, {
      name: bouquet.name,
      titleKey: 'bouquets.deleteConfirmTitle',
      bodyKey: 'bouquets.deleteConfirmBody',
      okKey: 'bouquets.deleteConfirmOk',
      run: () => removeBouquet.mutateAsync({ id: bouquet.id }),
    })

  /** The bouquet's content manager, which is a page of its own and not a dialog. */
  const openContent = (bouquet: Bouquet) => {
    const area = session ? areaForRole(session.role) : '/admin'

    navigate(`${area}/bouquets/${encodeURIComponent(bouquet.id)}/content`)
  }

  const columns: TableColumnsType<Bouquet> = [
    {
      title: t('bouquets.columns.sort'),
      key: 'sort',
      width: 48,
      align: 'center',
      render: () => <DragHandle label={t('bouquets.dragHint')} disabled={!canEdit} />,
    },
    {
      title: t('bouquets.columns.name'),
      dataIndex: 'name',
      key: 'name',
      render: (name: string, bouquet) => (
        <Space>
          <span>{name}</span>
          {/* The row is a package; the emptiness rule is the one fact about it that decides
              whether it can be deleted, so it is said in a word rather than inferred from
              four zeroes. */}
          {bouquetItemCount(bouquet) === 0 && (
            <span style={{ opacity: 0.55 }}>{t('bouquets.empty')}</span>
          )}
        </Space>
      ),
    },
    {
      // The four counts, one column each: a single total would answer "is it empty" and
      // nothing else, while an operator about to open a package wants to know what is in it.
      // Centred rather than right-aligned, because a column of single digits read against
      // the left edge of its own heading is easier to scan than one floating under the end
      // of a word.
      title: t('bouquets.columns.videoStreams'),
      dataIndex: 'videoStreamCount',
      key: 'videoStreamCount',
      width: 130,
      align: 'center',
    },
    {
      title: t('bouquets.columns.audioStreams'),
      dataIndex: 'audioStreamCount',
      key: 'audioStreamCount',
      width: 130,
      align: 'center',
    },
    {
      title: t('bouquets.columns.videoOnDemand'),
      dataIndex: 'videoOnDemandCount',
      key: 'videoOnDemandCount',
      width: 130,
      align: 'center',
    },
    {
      title: t('bouquets.columns.series'),
      dataIndex: 'seriesCount',
      key: 'seriesCount',
      width: 110,
      align: 'center',
    },
    {
      title: t('bouquets.columns.actions'),
      key: 'actions',
      width: actionsColumnWidth(3),
      render: (_, bouquet) => {
        const busy = busyId === bouquet.id
        const busyReason = busy ? '' : (bouquetItemCount(bouquet) > 0 ? t('bouquets.notEmpty') : '')

        const actions: RowAction[] = [
          {
            key: 'content',
            label: t('bouquets.actions.content'),
            icon: <UnorderedListOutlined />,
            // Opening the manager needs the read grant; the manager itself decides whether
            // the visitor may change anything, so this action is not gated on the write.
            disabled: busy || reorder.isPending,
            onClick: () => openContent(bouquet),
          },
          {
            key: 'rename',
            label: t('bouquets.actions.rename'),
            icon: <EditOutlined />,
            disabled: busy || reorder.isPending || !canEdit,
            onClick: () => openRename(bouquet),
          },
          {
            key: 'delete',
            label: busyReason || t('bouquets.actions.delete'),
            icon: <DeleteOutlined />,
            danger: true,
            loading: busy,
            // Disabled while the package holds anything, with the reason as the label the
            // tooltip shows: the server refuses the same click, and a button that reaches
            // the refusal teaches nothing before it lands.
            disabled: busy || reorder.isPending || busyReason.length > 0 || !canEdit,
            onClick: () => confirmDelete(bouquet),
          },
        ]

        return <RowActions actions={actions} />
      },
    },
  ]

  return (
    <Card
      className="page-card"
      // The card names the list, not the menu item that opened it: the menu says how the
      // visitor got here, and the card says what they are looking at.
      title={t('bouquets.listTitle')}
      extra={
        <Space wrap>
          {hintedButton(
            !canEdit,
            t('bouquets.noPermission'),
            <Button disabled={!dirty || reorder.isPending || !canEdit} onClick={discardOrder}>
              {t('bouquets.discardOrder')}
            </Button>,
          )}
          {hintedButton(
            !canEdit,
            t('bouquets.noPermission'),
            <Button
              type="primary"
              disabled={!dirty || reorder.isPending || !canEdit}
              loading={reorder.isPending}
              onClick={() => void saveOrder()}
            >
              {t('bouquets.saveOrder')}
            </Button>,
          )}
          {hintedButton(
            !canEdit,
            t('bouquets.noPermission'),
            <Button type="primary" icon={<PlusOutlined />} disabled={!canEdit} onClick={openCreate}>
              {t('bouquets.addTitle')}
            </Button>,
          )}
        </Space>
      }
    >
      {loadError ? (
        <Alert
          type="error"
          showIcon
          message={loadError}
          action={
            <Button size="small" onClick={() => bouquetsQuery.refetch()} loading={loading}>
              {t('actions.retry')}
            </Button>
          }
        />
      ) : (
        <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
          <SortableContext items={items.map((bouquet) => bouquet.id)} strategy={verticalListSortingStrategy}>
            <Table<Bouquet>
              rowKey="id"
              columns={columns}
              dataSource={items}
              loading={loading}
              // The row is the sortable unit, and the component is defined at module scope
              // for the reason every table here states: a component type made inside a render
              // would be new on every repaint, and React would unmount the row mid-drag.
              components={{ body: { row: SortableRow } }}
              pagination={false}
              locale={{ emptyText: t('bouquets.emptyList') }}
            />
          </SortableContext>
        </DndContext>
      )}

      <Modal
        title={renaming ? t('bouquets.renameTitle') : t('bouquets.addTitle')}
        open={creating || renaming !== null}
        onOk={() => void submitDialog()}
        confirmLoading={saving}
        onCancel={() => {
          setCreating(false)
          setRenaming(null)
        }}
        destroyOnHidden
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="name"
            label={t('bouquets.nameLabel')}
            rules={[
              { required: true, whitespace: true, message: t('bouquets.nameRequired') },
              { max: 100, message: t('bouquets.nameLength') },
            ]}
            extra={t('bouquets.nameHint')}
          >
            <Input maxLength={100} />
          </Form.Item>
        </Form>
      </Modal>
    </Card>
  )
}

/**
 * A button that carries the reason it is disabled.
 *
 * `hinted` alone would put a tooltip on every state, including the enabled one where the
 * reason is noise; here the wrapper appears only when the visitor may not write at all,
 * which is the one case the control cannot explain itself.
 */
const hintedButton = (disabled: boolean, reason: string, button: ReactNode): ReactNode =>
  disabled ? hinted(reason, button) : button
