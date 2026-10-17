import { createContext, useContext, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { Alert, App, Button, Card, Form, Input, Modal, Space, Table } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import {
  DeleteOutlined,
  EditOutlined,
  EyeOutlined,
  HolderOutlined,
  PlusOutlined,
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
import { CONTENT_TYPE_PARAMS } from '../lib/api'
import type { ContentCategory, ContentType } from '../types'
import { ApiError, fieldErrors, messageForError, successTextFor } from '../lib/http'
import { RowActions } from '../components/table'
import type { RowAction } from '../components/table'
import {
  useCreateContentCategory,
  useDeleteContentCategory,
  useReorderContentCategories,
  useUpdateContentCategory,
} from '../hooks/mutations'
import { useContentCategories } from '../hooks/queries'
import { useRowAction } from '../hooks/table'

/**
 * Which kind of content each of the four section pages works on, keyed by the menu key
 * the router hands every section page as its `sectionKey`.
 *
 * The router renders every menu item as `<Page sectionKey={item.labelKey} />` and offers
 * no other prop, so the type - the one fact that separates this page's four instances -
 * has to arrive through the key the item already carries. The keys here are written beside
 * the very table that generates them (`CONTENT_SECTIONS` in `src/lib/navigation.ts`
 * builds `nav.items.<section>Categories` from its own keys), and `probe:render` asserts
 * that each of the four opens a page whose menu key resolves - so a rename on one side
 * that leaves the other behind lands here as a page with the wrong heading, which the
 * probe reads rather than trusting the compiler to catch.
 */
const SECTION_TYPES: Record<string, ContentType> = {
  'nav.items.videoStreamsCategories': 'VideoStream',
  'nav.items.audioStreamsCategories': 'AudioStream',
  'nav.items.videoOnDemandCategories': 'VideoOnDemand',
  'nav.items.seriesCategories': 'Series',
}

interface ContentCategoriesPageProps {
  /**
   * The menu key every section route carries. Unlike most section pages, this one reads
   * it: the four addresses behind it differ only in the kind of content they list, and
   * the key is where that fact travels. See `SECTION_TYPES` for why.
   */
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
 * from anywhere on the row (which would make selecting text in a name cell start a drag).
 */
interface RowDragContextValue {
  setActivatorNodeRef?: (element: HTMLElement | null) => void
  attributes?: DraggableAttributes
  listeners?: SyntheticListenerMap
}

const RowDragContext = createContext<RowDragContextValue>({})

/** The row antd renders, wrapped in the sortable behaviour keyed by its own row key. */
function SortableRow(props: Record<string, unknown> & { children?: ReactNode }) {
  // `data-row-key` is what antd puts on every `<tr>`; with `rowKey="id"` it is the
  // category's identifier, which is exactly the id the sortable context orders by.
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

/**
 * The handle a row is dragged by. It alone carries the listeners: a drag that began
 * anywhere on the row would turn every click on the row's own buttons into a question of
 * whether it moved first, and would drag a row the visitor only meant to read.
 */
function DragHandle({ label }: { label: string }) {
  const { setActivatorNodeRef, attributes, listeners } = useContext(RowDragContext)

  return (
    <Button
      ref={setActivatorNodeRef}
      type="text"
      size="small"
      icon={<HolderOutlined />}
      aria-label={label}
      title={label}
      style={{ cursor: 'grab' }}
      {...attributes}
      {...listeners}
    />
  )
}

/**
 * The categories table of one content section - the page behind each
 * `nav.items.<section>Categories` entry, four addresses over one component.
 *
 * **The order is the feature, and it is arranged by dragging but saved by a button.** The
 * rows arrive in the order the operator last saved (`sort_order` on the server, `ORDER BY`
 * on the read); a drag moves them on screen only, and the whole arrangement reaches the
 * server through one explicit `PUT order` when the visitor presses "Save order". One
 * arrangement, one request - however many rows the visitor moved to build it - rather than
 * a write per drop, which is what made the per-drop request the wrong shape: a visitor
 * arranging a dozen categories is not doing a dozen pieces of work, they are doing one,
 * and only they know when it is finished.
 *
 * While the arrangement is unsaved the page says so: the save and discard controls sit in
 * the toolbar drawn but disabled, and enable the moment the order on screen differs from
 * the order on the server - the same "drawn even when disabled" convention the bulk menu
 * and the width reset follow. Discarding is a click, saving is the request; anything else
 * that re-reads the list (adding, deleting) keeps the unsaved arrangement and applies it
 * around the change, because a drag the visitor has already made is not a draft to lose.
 *
 * The row actions are the two this domain has today: showing the content filed under the
 * category (drawn disabled with a reason, because no content tables exist yet - the same
 * "the UI tells the truth the API would tell" rule the base groups' buttons follow) and
 * deleting, which asks first, because it is the one action that cannot be undone.
 */
export default function ContentCategoriesPage({ sectionKey }: ContentCategoriesPageProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()

  const type = SECTION_TYPES[sectionKey] ?? 'VideoStream'
  const typeParam = CONTENT_TYPE_PARAMS[type]

  const categoriesQuery = useContentCategories(typeParam)
  const serverItems = categoriesQuery.data?.items
  const loadError = categoriesQuery.isError ? messageForError(categoriesQuery.error) : null
  const loading = categoriesQuery.isFetching

  /**
   * The writes this page names, from `mutations.ts` - one hook per endpoint, so none of them
   * mints a token here, and each half of the dialog reads its own in-flight state. The row
   * whose delete is in flight, and the question the delete asks, are `useRowAction`'s: this
   * table has no pager, so there are no rows/page/setPage to hand over.
   */
  const createCategory = useCreateContentCategory()
  const updateCategory = useUpdateContentCategory()
  const reorder = useReorderContentCategories()
  const removeCategory = useDeleteContentCategory()

  const { busyId, confirmDelete: askDelete } = useRowAction<ContentCategory>({
    refetch: () => void categoriesQuery.refetch(),
  })

  /**
   * The arrangement the visitor has built and not saved yet, as the identifiers of the
   * type's categories in screen order - or null while the screen still shows the server's
   * order. Held apart from the query cache on purpose: the cache is what the server last
   * confirmed, and an arrangement that is not confirmed yet has no business in it.
   */
  const [pendingIds, setPendingIds] = useState<string[] | null>(null)
  /** Whether the add dialog is open, and the one it renames when open. */
  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState<ContentCategory | null>(null)
  /** Whether the dialog's own write is in flight - either half of it, since one dialog submits both. */
  const saving = createCategory.isPending || updateCategory.isPending

  const [form] = Form.useForm<DialogValues>()

  /**
   * Drag only on purpose, and only after the pointer has actually moved: without the
   * distance constraint a click on a handle would begin a drag on the way down, and a
   * row would follow the mouse the visitor only meant to steady their hand with. The
   * keyboard sensor keeps the same move reachable without a pointer at all.
   */
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 5 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  )

  /** The ids of the type's categories in the order the server last confirmed. */
  const serverIds = useMemo(() => (serverItems ?? []).map((category) => category.id), [serverItems])

  /**
   * The rows the table draws: the pending arrangement when there is one, the server's
   * order otherwise. A category created while an arrangement is unsaved is **appended**,
   * and a deleted one drops out - the visitor's moves survive both, because neither
   * action says anything about the order they were building.
   */
  const items = useMemo<ContentCategory[]>(() => {
    const list = serverItems ?? []

    if (!pendingIds) return list

    const byId = new Map(list.map((category) => [category.id, category]))
    const kept = pendingIds
      .map((id) => byId.get(id))
      .filter((category): category is ContentCategory => category !== undefined)
    const added = list.filter((category) => !pendingIds.includes(category.id))

    return [...kept, ...added]
  }, [serverItems, pendingIds])

  /** Whether what is on screen is not what the server holds - the state the toolbar reports. */
  const dirty =
    pendingIds !== null &&
    (pendingIds.length !== serverIds.length ||
      pendingIds.some((id, index) => id !== serverIds[index]))

  const handleDragEnd = (event: DragEndEvent) => {
    const { active, over } = event

    if (!over || active.id === over.id || reorder.isPending) return

    const ids = items.map((category) => category.id)
    const oldIndex = ids.indexOf(String(active.id))
    const newIndex = ids.indexOf(String(over.id))

    // A foreign id or a race with a re-read can leave both lookups empty; either way there
    // is no move to apply, and inventing one from -1s would scramble the list.
    if (oldIndex < 0 || newIndex < 0) return

    setPendingIds(arrayMove(ids, oldIndex, newIndex))
  }

  /**
   * Sends the arrangement on screen as the new order: one request, the whole list, and
   * the server rewrites positions 1..N over it atomically. The optimistic cache write of
   * the per-drop version is gone with the per-drop request - by the time this runs, the
   * arrangement is already on screen, and the re-read below is what confirms it.
   */
  const saveOrder = async () => {
    if (!dirty || reorder.isPending) return

    try {
      const result = await reorder.mutateAsync({
        type: typeParam,
        orderedIds: items.map((category) => category.id),
      })

      message.success(successTextFor(result))
      setPendingIds(null)
      void categoriesQuery.refetch()
    } catch (error) {
      message.error(messageForError(error))
    }
  }

  /** Puts the server's order back on screen. The arrangement was never sent, so nothing is asked of the server. */
  const discardOrder = () => setPendingIds(null)

  /** Opens the add dialog with an empty form. */
  const openCreate = () => {
    form.resetFields()
    setCreating(true)
  }

  /** Opens the rename dialog with the row's current name in it. */
  const openEdit = (category: ContentCategory) => {
    form.setFieldsValue({ name: category.name })
    setEditing(category)
  }

  /** Submits whichever dialog is open, mapping field errors under the one input there is. */
  const submitDialog = async () => {
    const values = await form.validateFields()

    try {
      const result = editing
        ? await updateCategory.mutateAsync({ id: editing.id, name: values.name })
        : await createCategory.mutateAsync({ values: { name: values.name, type: typeParam } })

      message.success(successTextFor(result))
      setCreating(false)
      setEditing(null)
      void categoriesQuery.refetch()
    } catch (error) {
      const fields = fieldErrors(error instanceof ApiError ? error.errors : null)

      if (fields.length > 0) {
        form.setFields(
          fields.map((field) => ({
            // The server keys refusals by the camelCase JSON name, which for this form is
            // the one string the `Form.Item` collects - the cast bridges two type systems,
            // not two spellings, exactly as on the group form.
            name: field.name as keyof DialogValues,
            errors: field.errors,
          })),
        )
      } else {
        message.error(messageForError(error))
      }
    }
  }

  /**
   * Asks before deleting, because this is the only action here that cannot be undone -
   * and, until the content tables exist, the only guard there is: the server refuses
   * nothing, so the question is not a habit but the whole of the protection.
   *
   * The question itself and the re-read are `useRowAction`'s - this table has no pager, so
   * there is no step back to ask of it; what is left here is the wording and the write.
   */
  const confirmDelete = (category: ContentCategory) =>
    askDelete(category, {
      name: category.name,
      titleKey: 'contentCategories.deleteConfirmTitle',
      bodyKey: 'contentCategories.deleteConfirmBody',
      okKey: 'contentCategories.deleteConfirmOk',
      run: () => removeCategory.mutateAsync({ id: category.id }),
    })

  const columns: TableColumnsType<ContentCategory> = [
    {
      // The handle, and nothing else: the column exists so the affordance has a place to
      // sit and the header can name what the rows are for. Its own width is the width of
      // the button plus the cell padding, and not a pixel more - the name beside it is
      // what this table is read by.
      title: t('contentCategories.columns.sort'),
      key: 'sort',
      width: 48,
      align: 'center',
      render: () => <DragHandle label={t('contentCategories.dragHint')} />,
    },
    {
      // No width of its own on purpose: with one content column and two narrow ones
      // beside it, the name stretching to fill is the whole of the layout this table
      // needs, and a fixed layout with truncation would be machinery built for tables
      // with eight columns, not for one.
      title: t('contentCategories.columns.name'),
      dataIndex: 'name',
      key: 'name',
    },
    {
      title: t('contentCategories.columns.actions'),
      key: 'actions',
      width: 120,
      render: (_, category) => {
        const busy = busyId === category.id

        const actions: RowAction[] = [
          // Disabled rather than hidden, for the reason every disabled control on this
          // application's tables gives: a button that vanishes teaches nothing about why it
          // was there, and "the content list is not built yet" is the fact an operator is
          // owed before they go looking for it.
          {
            key: 'members',
            label: t('contentCategories.membersSoon'),
            icon: <EyeOutlined />,
            disabled: true,
          },
          {
            key: 'edit',
            label: t('contentCategories.actions.edit'),
            icon: <EditOutlined />,
            disabled: busy || reorder.isPending,
            onClick: () => openEdit(category),
          },
          {
            key: 'delete',
            label: t('contentCategories.actions.delete'),
            icon: <DeleteOutlined />,
            danger: true,
            loading: busy,
            disabled: busy || reorder.isPending,
            onClick: () => confirmDelete(category),
          },
        ]

        return <RowActions actions={actions} />
      },
    },
  ]

  return (
    <Card
      // The heading names the kind of content the categories belong to, because the four
      // addresses of this page differ in nothing else - and a card that said only
      // "Categories" on all four would leave the visitor reading four identical screens
      // with no way to tell which one they are on.
      title={t(`contentCategories.titles.${type}`)}
      extra={
        <Space wrap>
          {/* Drawn while disabled, for the reason the bulk menu and the width reset are:
              a control that appears and disappears reads as a broken one, and the greyed
              state is what says the order on screen is the order on the server. */}
          <Button disabled={!dirty || reorder.isPending} onClick={discardOrder}>
            {t('contentCategories.discardOrder')}
          </Button>
          <Button
            type="primary"
            disabled={!dirty || reorder.isPending}
            loading={reorder.isPending}
            onClick={() => void saveOrder()}
          >
            {t('contentCategories.saveOrder')}
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            {t('contentCategories.addTitle')}
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
            <Button size="small" onClick={() => categoriesQuery.refetch()} loading={loading}>
              {t('actions.retry')}
            </Button>
          }
        />
      ) : (
        <DndContext
          sensors={sensors}
          collisionDetection={closestCenter}
          onDragEnd={handleDragEnd}
        >
          <SortableContext items={items.map((category) => category.id)} strategy={verticalListSortingStrategy}>
            <Table<ContentCategory>
              rowKey="id"
              columns={columns}
              dataSource={items}
              loading={loading}
              // The row is the sortable unit: `components.body.row` is where the drag
              // behaviour has to attach, and a module-level component rather than one
              // created here for the reason every table in this application states - a
              // component type made inside a render would be new on every repaint, and
              // React would unmount the row mid-drag.
              components={{ body: { row: SortableRow } }}
              pagination={false}
              locale={{ emptyText: t('contentCategories.empty') }}
            />
          </SortableContext>
        </DndContext>
      )}

      <Modal
        title={editing ? t('contentCategories.editTitle') : t('contentCategories.addTitle')}
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
          <Form.Item
            name="name"
            label={t('contentCategories.nameLabel')}
            rules={[
              { required: true, whitespace: true, message: t('contentCategories.nameRequired') },
              { max: 100, message: t('contentCategories.nameLength') },
            ]}
          >
            <Input maxLength={100} />
          </Form.Item>
        </Form>
      </Modal>
    </Card>
  )
}
