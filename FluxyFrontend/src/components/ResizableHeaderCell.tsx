import type { MouseEvent as ReactMouseEvent, PointerEvent as ReactPointerEvent, ReactNode, TdHTMLAttributes } from 'react'

/**
 * A table header cell that carries a resize handle at its right edge.
 *
 * The handle is a sibling of the header's own content rather than part of it, and that is
 * the point of going through `components.header.cell` at all. For a sortable column antd wraps
 * the title in a `<button>`; a handle inside that button would be a focusable element nested
 * in a button, and pressing it would sort the column as well as resize it. Sibling and
 * absolutely positioned, it takes the pointer itself and the button never sees the click.
 *
 * **Everything antd owns stays on the cell, and that split is the whole difficulty.**
 * `useSorter.js` does not add to what `onHeaderCell` returned - it *overwrites* it: `aria-label`
 * becomes the column title unconditionally (line 184), `tabIndex` becomes 0 (line 186),
 * `className` and `title` are merged, `onClick` is replaced by the sorter, and `onKeyDown` is
 * wrapped so that Enter sorts before the original handler is called at all. So the props are
 * dismantled here rather than forwarded wholesale: the cell keeps its `tabIndex`, its
 * `aria-label`, its click and its key handler, because a sortable header that lost them would
 * be a header a keyboard could no longer sort by - and the handle keeps the separator's own
 * role, bounds and pointer handlers, plus a `resizeLabel` that is not an ARIA attribute of the
 * DOM at all but the one channel antd does not write through.
 *
 * Two tab stops on a sortable column is therefore deliberate, not a duplication: the cell is
 * named "When" and sorts on Enter, the handle is named "Resize the When column" and moves on
 * the arrow keys, and neither name describes the other's job.
 *
 * `position: relative` is set inline rather than in the stylesheet because it is this cell's
 * own containing block - the handle positions against it and `overflow: hidden` clips to it.
 * Without it the nearest positioned ancestor inside antd's header is nowhere, and the handle
 * would end up against the table. Both boxes are the same box: a cell has no border, so the
 * padding box the handle is positioned against ends exactly where the column does - which is
 * also why the handle can sit at `right: 0` and still be visible under `overflow: hidden`,
 * since anything *past* that edge is what would be cut.
 *
 * Defined at module scope deliberately - a component type created inside a page would be a
 * new type on every repaint, React would unmount the handle mid-drag, and the pointer would
 * go with it. The class name it stamps (`table-col-resizer`) and the one the drag puts on
 * `body` (`table-col-resizing`) are the shared spellings of the two tables that use it.
 */
export type ResizableHeaderCellProps = Omit<
  TdHTMLAttributes<HTMLTableCellElement>,
  'onPointerDown' | 'onDoubleClick'
> & {
  children?: ReactNode
  /**
   * The handle's own name.
   *
   * Not spelled `aria-label` on purpose: antd writes `aria-label` onto a sortable header cell
   * itself, after this, with the column title - so it would arrive here already replaced and
   * would be handed to whichever element the component gave it to, which is how the first
   * version of this file ended up naming its handles "When" on the two sortable columns and
   * "Resize the Country column" on the other three. This key is the component's own, and it is
   * a plain `string` so the return type still satisfies `React.TdHTMLAttributes` without a cast.
   */
  resizeLabel?: string
  /** Widened to `Element` because these are bound to the handle, not to the cell. */
  onPointerDown?: (event: ReactPointerEvent<Element>) => void
  onDoubleClick?: (event: ReactMouseEvent<Element>) => void
}

export default function ResizableHeaderCell(props: ResizableHeaderCellProps) {
  const {
    children,
    className,
    style,
    role,
    'aria-orientation': orientation,
    'aria-valuenow': valueNow,
    'aria-valuemin': valueMin,
    'aria-valuemax': valueMax,
    resizeLabel,
    onPointerDown,
    onDoubleClick,
    ...cellProps
  } = props

  return (
    <th {...cellProps} className={className} style={{ ...style, position: 'relative' }}>
      {children}
      {resizeLabel === undefined ? null : (
        <span
          className="table-col-resizer"
          role={role}
          aria-orientation={orientation}
          aria-label={resizeLabel}
          aria-valuenow={valueNow}
          aria-valuemin={valueMin}
          aria-valuemax={valueMax}
          /* The handle's own tab stop, and not the cell's. For a sortable column the cell's
             `tabIndex` - which stays in `cellProps` above - belongs to antd's Enter-to-sort, so
             taking it here would leave the header unfocusable and the keyboard with no way to
             sort at all. Two stops, two names: the cell says "When" and sorts, this says
             "Resize the When column" and moves. */
          tabIndex={0}
          onPointerDown={onPointerDown}
          onDoubleClick={onDoubleClick}
          /* A sortable header sorts on any click that bubbles to it, and a click on the handle
             sorted nothing. `preventDefault` on `pointerdown` is deliberately *not* relied on
             for this - whether it suppresses the compatibility mouse events is left to the
             implementation - and suppressing those events would also cost the double-click that
             restores this one column. Stopping propagation here is the guarantee instead. */
          onClick={(event) => event.stopPropagation()}
        />
      )}
    </th>
  )
}
