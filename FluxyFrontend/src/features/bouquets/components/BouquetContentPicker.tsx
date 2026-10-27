import { useMemo, useState } from 'react'
import { Button, Checkbox, Empty, Input, Select, Space, Tag, Tooltip, Typography } from 'antd'
import {
  ArrowDownOutlined,
  ArrowLeftOutlined,
  ArrowRightOutlined,
  ArrowUpOutlined,
  SortAscendingOutlined,
} from '@ant-design/icons'
import { hinted } from '../../../utils/hinted'
import { useIsCompact } from '../../../hooks/useIsCompact'
import {
  categoriesOf,
  filterByCategory,
  idsOf,
  isKnownCategory,
  moveSelectedDown,
  moveSelectedUp,
  sortByName,
} from '../contentOrder'
import type { BouquetMember } from '../types'

/**
 * The two lists of the bouquet content manager, and the buttons that move items between
 * them.
 *
 * One component for one kind of content: it draws what the bouquet holds (in the order it
 * holds it) beside what it does not, and reports every move to its caller. It writes
 * nothing itself - the page owns the draft and the save, so a transfer is undoable until
 * the operator says otherwise, and leaving the screen with unsaved moves warns like every
 * other form here does.
 *
 * The order of the left list is the operator's, and it is the order the save sends: the
 * up/down buttons move **every selected row as a block**, and the A-Z button re-orders by
 * name. All three are draft actions of the same weight - a click is a change on screen, not
 * a request.
 *
 * The right list is drawn in the order the server sent it (by name), because "not in the
 * bouquet" has no order worth keeping: nothing about the list means anything until an item
 * is moved across.
 */
export interface BouquetContentPickerProps {
  /** What the bouquet holds of this kind, in the operator's order. */
  members: BouquetMember[]
  /** Everything of this kind the bouquet does not hold, ordered by name. */
  available: BouquetMember[]
  /** Replaces the in-bouquet list. */
  onChange: (members: BouquetMember[]) => void
  /** Whether the visitor may change anything - the whole control is inert without it. */
  disabled?: boolean
  /** Language the A-Z sort compares names in, from `i18n.resolvedLanguage`. */
  locale: string
  /** Every label the control draws, so the feature holds no translation keys. */
  labels: {
    members: string
    available: string
    emptyMembers: string
    emptyAvailable: string
    emptyFiltered: string
    add: string
    remove: string
    moveUp: string
    moveDown: string
    sortAz: string
    filterMembers: string
    filterAvailable: string
    filterCategory: string
    selectAll: string
    clearSelection: string
    noPermission: string
  }
}

export default function BouquetContentPicker({
  members,
  available,
  onChange,
  disabled = false,
  locale,
  labels,
}: BouquetContentPickerProps) {
  // Below the shell's breakpoint the two lists stack, the arrows turn vertical and the
  // filters go full width - see the layout below. The hook is the shell's own, so this
  // control switches at the same width the rail does and a phone never paints the
  // side-by-side layout before the stacked one.
  const compact = useIsCompact()

  // Two selections rather than one: which side an id belongs to is what the buttons read,
  // and an id is only ever in one of the two lists.
  const [memberSelection, setMemberSelection] = useState<ReadonlySet<string>>(new Set())
  const [availableSelection, setAvailableSelection] = useState<ReadonlySet<string>>(new Set())

  // A filter each, applied to what is already loaded. Nothing here is paged, so narrowing
  // the list cannot lie about a total the way a filtered page would.
  const [memberFilter, setMemberFilter] = useState('')
  const [availableFilter, setAvailableFilter] = useState('')

  // A category filter each, beside the name filter and combined with it. It exists for the
  // bulk gesture: pick a section and every item of that section is on screen at once, so
  // "select all" moves the whole category into or out of the package in one action rather
  // than a page of names at a time.
  const [memberCategory, setMemberCategory] = useState<string | null>(null)
  const [availableCategory, setAvailableCategory] = useState<string | null>(null)

  // **Both filters offer the same categories, read off both lists together.** Deriving them
  // from one list was the bug: selecting every item of a category and moving it across left
  // the source list holding none of that category, the option vanished from its own select,
  // and the filter the operator was still using disappeared from under them. A category is a
  // property of the kind of content, not of which side an item currently sits on, so the
  // universe is the union - and it only changes when the content itself does.
  const allCategories = useMemo(() => categoriesOf([...members, ...available]), [members, available])

  // A category that is gone from the universe (the last item of it was deleted, not moved)
  // stops narrowing: the select would otherwise hold a value with no option behind it, and
  // the list would be filtered by something the operator can no longer see or clear.
  const memberCategoryFilter = isKnownCategory(allCategories, memberCategory) ? memberCategory : null
  const availableCategoryFilter = isKnownCategory(allCategories, availableCategory)
    ? availableCategory
    : null

  const visibleMembers = useMemo(
    () => filterByName(filterByCategory(members, memberCategoryFilter), memberFilter),
    [members, memberCategoryFilter, memberFilter],
  )

  const visibleAvailable = useMemo(
    () => filterByName(filterByCategory(available, availableCategoryFilter), availableFilter),
    [available, availableCategoryFilter, availableFilter],
  )

  const canMoveUp = !disabled && toggleTarget(members, memberSelection, 'up').length > 0
  const canMoveDown = !disabled && toggleTarget(members, memberSelection, 'down').length > 0

  /** Moves the ticked rows of the left list into the right one, at their own order. */
  const handOver = () => {
    if (disabled || memberSelection.size === 0) {
      return
    }

    onChange(members.filter((member) => !memberSelection.has(member.id)))
    setMemberSelection(new Set())
  }

  /** Moves the ticked rows of the right list to the **end** of the left one. */
  const takeOver = () => {
    if (disabled || availableSelection.size === 0) {
      return
    }

    // Appended rather than inserted: the operator has not said where in the bouquet the
    // item belongs, and putting it at the end is the only position that changes nothing
    // already arranged. The up/down buttons are how it is then moved into place.
    const arriving = available.filter((member) => availableSelection.has(member.id))

    onChange([...members, ...arriving])
    setAvailableSelection(new Set())
  }

  const moveUp = () => {
    if (disabled) {
      return
    }

    onChange(orderMembers(members, moveSelectedUp(idsOf(members), memberSelection)))
  }

  const moveDown = () => {
    if (disabled) {
      return
    }

    onChange(orderMembers(members, moveSelectedDown(idsOf(members), memberSelection)))
  }

  const sortAlphabetically = () => {
    if (disabled) {
      return
    }

    onChange(sortByName(members, locale))
  }

  return (
    <div
      style={
        compact
          ? // Phone: the two lists stack, and the arrow pair turns to sit **between** them -
            // pointing down into the lower list and up into the upper one, which is the only
            // direction a transfer can mean on a column. The pair is drawn on its own row so
            // it never overlaps either list and never has to be squeezed beside them.
            { display: 'flex', flexDirection: 'column', gap: 12 }
          : {
              display: 'grid',
              gridTemplateColumns: 'minmax(0, 1fr) auto minmax(0, 1fr)',
              gap: 12,
              // Stretch rather than start, so the middle column has the row's full height to
              // centre the arrow pair in - see the arrows below.
              alignItems: 'stretch',
            }
      }
    >
      <ListPanel
        compact={compact}
        title={`${labels.members} (${members.length})`}
        filterValue={memberFilter}
        filterPlaceholder={labels.filterMembers}
        onFilter={setMemberFilter}
        categories={allCategories}
        category={memberCategoryFilter}
        onCategory={setMemberCategory}
        categoryPlaceholder={labels.filterCategory}
        onSelectAll={() => setMemberSelection(allVisibleIds(visibleMembers, memberSelection))}
        onClearSelection={() => setMemberSelection(new Set())}
        selectAllLabel={labels.selectAll}
        clearSelectionLabel={labels.clearSelection}
        selectionCount={memberSelection.size}
        disabled={disabled}
        emptyLabel={
          members.length === 0
            ? labels.emptyMembers
            : labels.emptyFiltered
        }
        isEmpty={visibleMembers.length === 0}
        // The three arrangement buttons sit on the selection row, at its end: they act on
        // what is ticked, so they belong beside the control that ticks it rather than in the
        // heading above it - and the heading is where the list is named, not where it is
        // operated.
        rowActions={
          <Space size={2}>
            {hinted(
              disabled ? labels.noPermission : labels.sortAz,
              <Button
                size="small"
                type="text"
                icon={<SortAscendingOutlined />}
                aria-label={labels.sortAz}
                disabled={disabled || members.length < 2}
                onClick={sortAlphabetically}
              />,
            )}
            {hinted(
              disabled ? labels.noPermission : labels.moveUp,
              <Button
                size="small"
                type="text"
                icon={<ArrowUpOutlined />}
                aria-label={labels.moveUp}
                disabled={!canMoveUp}
                onClick={moveUp}
              />,
            )}
            {hinted(
              disabled ? labels.noPermission : labels.moveDown,
              <Button
                size="small"
                type="text"
                icon={<ArrowDownOutlined />}
                aria-label={labels.moveDown}
                disabled={!canMoveDown}
                onClick={moveDown}
              />,
            )}
          </Space>
        }
      >
        {visibleMembers.map((member) => (
          <Row
            key={member.id}
            member={member}
            checked={memberSelection.has(member.id)}
            disabled={disabled}
            showCategory={!compact}
            onToggle={() => setMemberSelection(toggle(memberSelection, member.id))}
          />
        ))}
      </ListPanel>

      <div
        style={
          compact
            ? // Between the lists, side by side rather than stacked: the pair is one
              // gesture in two directions, and a column of two arrows on a phone costs a
              // row of height for nothing.
              {
                display: 'flex',
                flexDirection: 'row',
                gap: 8,
                alignItems: 'center',
                justifyContent: 'center',
              }
            : {
                display: 'flex',
                flexDirection: 'column',
                gap: 8,
                alignItems: 'center',
                // Centred against the panels as a whole, and **nothing else**. The earlier
                // version added a top padding equal to the panel's header height, on the
                // theory that the arrows should centre on the scrolling boxes rather than on
                // the boxes plus their headings - but with `justify-content: center` that
                // padding pushed the pair down by half its value instead of shifting the
                // centre it was meant to correct, and the arrows sat below the middle.
                // The two panels are the same fixed height with the same fixed header, so
                // centring on them is centring on everything.
                alignSelf: 'stretch',
                justifyContent: 'center',
              }
        }
      >
        {hinted(
          disabled ? labels.noPermission : labels.add,
          <Button
            icon={compact ? <ArrowUpOutlined /> : <ArrowLeftOutlined />}
            aria-label={labels.add}
            disabled={disabled || availableSelection.size === 0}
            onClick={takeOver}
          />,
        )}
        {hinted(
          disabled ? labels.noPermission : labels.remove,
          <Button
            icon={compact ? <ArrowDownOutlined /> : <ArrowRightOutlined />}
            aria-label={labels.remove}
            disabled={disabled || memberSelection.size === 0}
            onClick={handOver}
          />,
        )}
      </div>

      <ListPanel
        compact={compact}
        title={`${labels.available} (${available.length})`}
        filterValue={availableFilter}
        filterPlaceholder={labels.filterAvailable}
        onFilter={setAvailableFilter}
        categories={allCategories}
        category={availableCategoryFilter}
        onCategory={setAvailableCategory}
        categoryPlaceholder={labels.filterCategory}
        onSelectAll={() => setAvailableSelection(allVisibleIds(visibleAvailable, availableSelection))}
        onClearSelection={() => setAvailableSelection(new Set())}
        selectAllLabel={labels.selectAll}
        clearSelectionLabel={labels.clearSelection}
        selectionCount={availableSelection.size}
        disabled={disabled}
        emptyLabel={available.length === 0 ? labels.emptyAvailable : labels.emptyFiltered}
        isEmpty={visibleAvailable.length === 0}
      >
        {visibleAvailable.map((member) => (
          <Row
            key={member.id}
            member={member}
            checked={availableSelection.has(member.id)}
            disabled={disabled}
            showCategory={!compact}
            onToggle={() => setAvailableSelection(toggle(availableSelection, member.id))}
          />
        ))}
      </ListPanel>
    </div>
  )
}

/**
 * One side of the control: the heading with its count, the filters, the selection row, and
 * the rows.
 *
 * A bordered box rather than a table, because these rows have no columns: a name, a tick,
 * and nothing else. The border is what tells the two lists apart at a glance.
 *
 * **The filters stack on a phone.** Side by side in a half-width column the search box was
 * squeezed to a few characters - the two controls each kept their own minimum and the row
 * could not grow - so below the breakpoint the search takes a line of its own and the
 * category select takes the next. Above it they share a row, where there is width for both.
 */
const ListPanel = ({
  compact,
  title,
  rowActions,
  filterValue,
  filterPlaceholder,
  onFilter,
  categories,
  category,
  onCategory,
  categoryPlaceholder,
  onSelectAll,
  onClearSelection,
  selectAllLabel,
  clearSelectionLabel,
  selectionCount,
  disabled,
  emptyLabel,
  isEmpty,
  children,
}: {
  /** Whether the control is drawn for a phone, which stacks the filters and the lists. */
  compact: boolean
  title: string
  /** Buttons that act on the selection, drawn at the end of the selection row. */
  rowActions?: React.ReactNode
  filterValue: string
  filterPlaceholder: string
  onFilter: (value: string) => void
  /** Every category the kind holds at least one item of, sorted by name. */
  categories: { id: string; name: string }[]
  /** Category currently narrowing the list, or null for "all of them". */
  category: string | null
  onCategory: (value: string | null) => void
  categoryPlaceholder: string
  onSelectAll: () => void
  onClearSelection: () => void
  selectAllLabel: string
  clearSelectionLabel: string
  selectionCount: number
  disabled: boolean
  emptyLabel: string
  isEmpty: boolean
  children: React.ReactNode
}) => (
  <div style={{ display: 'flex', flexDirection: 'column', gap: 8, minWidth: 0 }}>
    <Typography.Text strong>{title}</Typography.Text>

    <div style={{ display: 'flex', flexDirection: compact ? 'column' : 'row', gap: 8 }}>
      <Input
        allowClear
        size="small"
        value={filterValue}
        placeholder={filterPlaceholder}
        onChange={(event) => onFilter(event.target.value)}
      />
      {/* The category filter, beside the name filter and combined with it: the two answer
          different questions ("which section?" and "which item of it?") and an operator
          narrowing a list of thousands uses both.
          Drawn whenever the kind has any category at all - including just one - because the
          control has to be there when the operator comes back to the list after moving a
          whole category out of it. Hiding it below two options made it vanish exactly then. */}
      {categories.length > 0 && (
        <Select
          size="small"
          allowClear
          style={compact ? { width: '100%' } : { minWidth: 150 }}
          value={category ?? undefined}
          placeholder={categoryPlaceholder}
          onChange={(value) => onCategory(value ?? null)}
          options={categories.map((entry) => ({ value: entry.id, label: entry.name }))}
          optionFilterProp="label"
          showSearch
        />
      )}
    </div>
    {/* The selection row: what is ticked on the left, and the controls that act on the
        ticks on the right. One row rather than two, because the buttons are about the
        selection and an operator reads them together. */}
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
      <Button size="small" type="link" style={{ padding: 0 }} disabled={disabled} onClick={onSelectAll}>
        {selectAllLabel}
      </Button>
      <Button
        size="small"
        type="link"
        style={{ padding: 0 }}
        disabled={disabled || selectionCount === 0}
        onClick={onClearSelection}
      >
        {clearSelectionLabel}
      </Button>
      {selectionCount > 0 && <Tag>{selectionCount}</Tag>}
      {rowActions && <div style={{ marginLeft: 'auto' }}>{rowActions}</div>}
    </div>

    <div
      style={{
        border: '1px solid var(--ant-color-border, #d9d9d9)',
        borderRadius: 8,
        padding: 8,
        // A **fixed** height rather than a min/max range: the two lists sit side by side and
        // are read as one control, so a list that grew with its content left the shorter one
        // floating at whatever height it happened to have - the right-hand list standing
        // above the left one, which read as two unrelated boxes. A height both share, with
        // its own scroll inside, keeps the pair level whatever either holds. Shorter on a
        // phone, where the two stack and two full-height boxes would be a page of scrolling
        // before the second list begins.
        height: compact ? 220 : 420,
        overflowY: 'auto',
      }}
    >
      {isEmpty ? (
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={emptyLabel} />
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>{children}</div>
      )}
    </div>
  </div>
)

/**
 * One row of either list: a tick, the name and the category it is filed under.
 *
 * **The name gets the room, and the category tag truncates instead.** The other way round
 * was the bug that made channel names unreadably short: a tag is one unbreakable word, so it
 * claimed its full width first and pushed the name into the few characters that were left.
 * The name is what the row is read by, so it is the element that may use the whole line; the
 * category is context, so it is capped and cut with its own tooltip - and on a phone it is
 * dropped altogether, where the row is one narrow column and the tag would leave the name
 * three characters wide.
 */
const Row = ({
  member,
  checked,
  disabled,
  showCategory,
  onToggle,
}: {
  member: BouquetMember
  checked: boolean
  disabled: boolean
  showCategory: boolean
  onToggle: () => void
}) => (
  <label
    style={{
      display: 'flex',
      alignItems: 'center',
      gap: 8,
      padding: '2px 4px',
      borderRadius: 4,
      cursor: disabled ? 'default' : 'pointer',
      minWidth: 0,
    }}
  >
    <Checkbox checked={checked} disabled={disabled} onChange={onToggle} />
    <Typography.Text
      ellipsis={{ tooltip: member.name }}
      style={{ minWidth: 0, flex: 1 }}
      title={member.name}
    >
      {member.name}
    </Typography.Text>
    {showCategory && (
      <Tooltip title={member.categoryName}>
        <Tag
          bordered={false}
          style={{
            marginInlineEnd: 0,
            opacity: 0.75,
            maxWidth: 140,
            overflow: 'hidden',
            textOverflow: 'ellipsis',
            whiteSpace: 'nowrap',
            flexShrink: 0,
          }}
        >
          {member.categoryName}
        </Tag>
      </Tooltip>
    )}
  </label>
)

/** Adds or removes one id from a selection. */
const toggle = (selection: ReadonlySet<string>, id: string): ReadonlySet<string> => {
  const next = new Set(selection)

  if (!next.delete(id)) {
    next.add(id)
  }

  return next
}

/**
 * The ids a "select all" would add: every row the filter currently shows that is not
 * ticked yet, added to what is already ticked.
 *
 * Filtered rather than the whole list, because a button that selects more than the visitor
 * can see is a button whose effect cannot be checked before it is used.
 */
const allVisibleIds = (
  visible: readonly BouquetMember[],
  selection: ReadonlySet<string>,
): ReadonlySet<string> => {
  const next = new Set(selection)

  for (const member of visible) {
    next.add(member.id)
  }

  return next
}

/** Case-insensitive substring filter over what is already loaded. */
const filterByName = (members: readonly BouquetMember[], filter: string): BouquetMember[] => {
  const wanted = filter.trim().toLowerCase()

  return wanted.length === 0
    ? [...members]
    : members.filter((member) => member.name.toLowerCase().includes(wanted))
}

/**
 * The ids the move buttons can move: the selection, minus the rows already at the edge the
 * move would push them past.
 *
 * Answered from the ids the move would produce rather than by comparing indexes here, so the
 * two call sites cannot disagree with the function that actually moves them: what the
 * button's enabled state says is exactly what a click would do.
 */
const toggleTarget = (
  members: readonly BouquetMember[],
  selection: ReadonlySet<string>,
  direction: 'up' | 'down',
): readonly string[] => {
  const ids = idsOf(members)

  return direction === 'up'
    ? moveSelectedUp(ids, selection)
    : moveSelectedDown(ids, selection)
}

/** Rebuilds the member list in a new id order, keeping the names. */
const orderMembers = (members: readonly BouquetMember[], ids: readonly string[]): BouquetMember[] => {
  const byId = new Map(members.map((member) => [member.id, member]))

  return ids.flatMap((id) => {
    const member = byId.get(id)

    return member ? [member] : []
  })
}
