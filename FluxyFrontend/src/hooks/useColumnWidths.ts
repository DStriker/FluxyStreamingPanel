import { useEffect, useRef, useState } from 'react'
import type { KeyboardEvent as ReactKeyboardEvent, PointerEvent as ReactPointerEvent } from 'react'
import type { ResizableHeaderCellProps } from '../components/ResizableHeaderCell'

/**
 * The floor and the ceiling of a dragged column.
 *
 * The floor is a header width rather than zero: a column narrowed to nothing is a column whose
 * resize handle can no longer be reached to widen it again, and the handle lives at the right
 * edge of the cell. The ceiling exists so a drag cannot produce a table wider than any screen
 * could show - 1200 per column is already past that.
 */
const MIN_COLUMN_WIDTH = 90
const MAX_COLUMN_WIDTH = 1200

/**
 * What one table's columns start at, where they are remembered, and which of them exist.
 *
 * Every value is passed in rather than assumed, because two tables now share this: their
 * columns differ, their defaults differ, and a remembered width under one key would be a
 * claim about the other table if a single key were used for both. `keys` and `defaults` are
 * module-level constants in the page - an inline array would be a new array on every render,
 * and the effect that persists the widths would then write them on every render too.
 */
interface UseColumnWidthsOptions<K extends string> {
  /** The column keys, in the order the columns are drawn. */
  keys: readonly K[]
  /** The width each column starts at, and the width it goes back to when asked to reset. */
  defaults: Record<K, number>
  /** Where the widths are remembered between visits. */
  storageKey: string
}

/** What the table needs from its columns: the widths, the scroll width, and the handles. */
export interface ColumnWidthsHandle<K extends string> {
  /** The current width of every column, clamped. */
  widths: Record<K, number>
  /** Whatever the widths add up to, and so the width the table has before it scrolls. */
  tableWidth: number
  /** Whether a reset would change anything - the state a reset button is disabled in. */
  atDefaults: boolean
  /** Puts every column back and drops the stored copy, so a future default still applies. */
  reset: () => void
  /** The props one column's resize handle carries, for `onHeaderCell`. */
  resizer: (key: K, label: string) => ResizableHeaderCellProps
}

const clampWidth = (value: number): number =>
  Math.min(MAX_COLUMN_WIDTH, Math.max(MIN_COLUMN_WIDTH, value))

const sameWidths = <K extends string>(
  keys: readonly K[],
  one: Record<K, number>,
  other: Record<K, number>,
): boolean => keys.every((key) => one[key] === other[key])

/**
 * The remembered widths, or the defaults when there are none to remember.
 *
 * Everything that can go wrong falls back to the defaults rather than to nothing: a browser in
 * private mode refuses storage, a corrupted entry is not JSON, a value that is a string is not
 * a width, and the probes render this under Node, where `localStorage` does not exist at all
 * and reaching for it is a `ReferenceError`. None of those is a reason for the table not to
 * draw, and the last one is why the whole body sits in a `try` rather than behind a `typeof`
 * guard the compiler would then be entitled to call unnecessary.
 */
const readWidths = <K extends string>(
  keys: readonly K[],
  defaults: Record<K, number>,
  storageKey: string,
): Record<K, number> => {
  const widths: Record<K, number> = { ...defaults }

  try {
    const stored = localStorage.getItem(storageKey)
    if (!stored) return widths

    const parsed: unknown = JSON.parse(stored)
    if (typeof parsed !== 'object' || parsed === null) return widths

    for (const key of keys) {
      const value = (parsed as Record<string, unknown>)[key]
      if (typeof value === 'number' && Number.isFinite(value)) widths[key] = clampWidth(value)
    }
  } catch {
    return { ...defaults }
  }

  return widths
}

/**
 * The column widths of one table: their state, their memory, and the drag that changes them.
 *
 * Extracted when the admin user table was written, because resizing is the part of the visit
 * history that reads as if it were simple and is not - the handle must not be nested in antd's
 * sortable `<button>`, the drag must survive leaving the ten pixels it started on, the width
 * must be measured from where the drag *began* rather than accumulated, and text selection is
 * held by a class on `body` rather than by a cancelled `pointerdown` that would take the
 * double-click and the focus with it. Two copies of those rules would be two places for one of
 * them to be quietly wrong, and nothing in `typecheck`, `lint` or the build can tell.
 *
 * The two things that stay with the page are the labels (`resizer` takes the handle's own
 * accessible name as an argument, because only the page knows that a column is called "When")
 * and the columns themselves - this hook never sees antd's `Table`.
 */
export function useColumnWidths<K extends string>({
  keys,
  defaults,
  storageKey,
}: UseColumnWidthsOptions<K>): ColumnWidthsHandle<K> {
  const [widths, setWidths] = useState<Record<K, number>>(() =>
    readWidths(keys, defaults, storageKey),
  )

  // The drag that is in progress, if any, so that leaving the page mid-drag still ends it.
  // The listeners live on `window` and would otherwise outlive this component until the next
  // pointerup, and the class on `body` - which is what keeps the cursor a resize cursor while
  // the pointer is over the table rather than over the handle - would outlive them both.
  const endResizeRef = useRef<(() => void) | null>(null)

  useEffect(() => {
    return () => {
      endResizeRef.current?.()
    }
  }, [])

  useEffect(() => {
    try {
      if (sameWidths(keys, widths, defaults)) localStorage.removeItem(storageKey)
      else localStorage.setItem(storageKey, JSON.stringify(widths))
    } catch {
      // The widths are still in state and the table is still resizable; only the memory of
      // them between visits is lost, which is worth failing silently over. Storage is
      // unavailable under the render probe and in a private window, so this is not a rare path.
    }
  }, [widths, keys, defaults, storageKey])

  const setWidth = (key: K, next: number) =>
    setWidths((previous) => ({ ...previous, [key]: clampWidth(next) }))

  /**
   * Starts a drag, from the handle to `window`.
   *
   * The listeners go on `window` rather than on the handle because a drag outlives the thing
   * it started on: at any useful speed the pointer leaves the ten-pixel handle immediately,
   * and without capture it would stop receiving moves. `window` is always there, and removing
   * the listeners on the way out - through `endResizeRef`, so an unmount ends the drag too - is
   * what keeps a half-finished one from writing widths into a table that no longer exists.
   *
   * Text selection is stopped by the class on `body` rather than by `preventDefault()`. This
   * handler runs before the `mousedown` that would begin a selection, so `user-select: none`
   * is already in force by the time the browser asks; and unlike cancelling `pointerdown`, it
   * changes nothing else about what follows. Cancelling is the conventional one-liner and is
   * the wrong trade here: whether it suppresses the compatibility `mousedown` - and with it the
   * `click`, the `dblclick` that restores this one column and the focus that makes the arrow
   * keys work - is left to the implementation rather than promised by the specification. The
   * class also holds the cursor a resize cursor over the whole table, not only over the ten
   * pixels of the handle itself.
   */
  const beginResize = (key: K, event: ReactPointerEvent<Element>) => {
    // The primary button only: a right-click or a middle-click on the handle is not a drag.
    if (event.button !== 0) return

    const startX = event.clientX
    const startWidth = widths[key]

    const onMove = (move: PointerEvent) => {
      // Always from the width the drag *began* at, never from the last one written: an
      // accumulator would compound the delta and the column would run away from the pointer.
      setWidth(key, startWidth + (move.clientX - startX))
    }

    const finish = () => {
      window.removeEventListener('pointermove', onMove)
      window.removeEventListener('pointerup', finish)
      window.removeEventListener('pointercancel', finish)
      document.body.classList.remove('table-col-resizing')
      endResizeRef.current = null
    }

    document.body.classList.add('table-col-resizing')
    window.addEventListener('pointermove', onMove)
    window.addEventListener('pointerup', finish)
    window.addEventListener('pointercancel', finish)
    endResizeRef.current = finish
  }

  /**
   * The keyboard half of the same action, on the handle that `role="separator"` makes
   * focusable. Sixteen pixels a step - the same as a line of text at this scale - and `shift`
   * for four of them at a time. `Home` puts the column back, which is the keyboard's half of
   * the double-click; `Enter` is deliberately absent, because for a sortable column antd has
   * already wrapped this handler and sorts the column *before* calling it, so a reset that
   * answered `Enter` would move the width and turn the sort arrow at the same time. The
   * card-level reset button reaches the same result for a keyboard user and says what it does.
   *
   * The handler is on the cell rather than on the handle: for a sortable column antd wraps it
   * and puts the wrapper there, and for a non-sortable one nothing else is listening either
   * way. A key pressed on the handle bubbles to it, so both arrangements end up here.
   */
  const nudgeColumn = (key: K, event: ReactKeyboardEvent<HTMLTableCellElement>) => {
    const step = event.shiftKey ? 64 : 16

    if (event.key === 'ArrowLeft') setWidth(key, widths[key] - step)
    else if (event.key === 'ArrowRight') setWidth(key, widths[key] + step)
    else if (event.key === 'Home') setWidth(key, defaults[key])
    else return

    // Both so the table does not scroll sideways under the arrow keys and so the handle does
    // not follow them: the point of the key press is the width, not the scroll position.
    event.preventDefault()
  }

  /**
   * The props one column's resize handle carries, from `onHeaderCell`.
   *
   * Called with the key rather than reading it off the column, because `ColumnType['key']` is
   * optional and this way the compiler is the thing that notices a column with no key instead
   * of a handle that quietly resizes nothing.
   *
   * What is *not* here matters more than what is: no `aria-label`, no `tabIndex`, no `onClick`
   * and no `title`. `useSorter` writes all four onto a sortable header cell itself, after
   * this, so all four belong to the cell - and a handle that asked for any of them would get
   * the column's own name and would compete with the header for them. The handle's name comes
   * from the caller (only the page knows what the column is called), the component hands it
   * through `resizeLabel`, and it hardcodes the `tabIndex` it needs. The rest are plain
   * `React.TdHTMLAttributes` keys, which is why the return type needs no cast.
   */
  const resizer = (key: K, label: string): ResizableHeaderCellProps => ({
    role: 'separator',
    'aria-orientation': 'vertical',
    'aria-valuenow': widths[key],
    'aria-valuemin': MIN_COLUMN_WIDTH,
    'aria-valuemax': MAX_COLUMN_WIDTH,
    resizeLabel: label,
    onPointerDown: (event) => beginResize(key, event),
    onDoubleClick: () => setWidth(key, defaults[key]),
    onKeyDown: (event) => nudgeColumn(key, event),
  })

  return {
    widths,
    tableWidth: keys.reduce((total, key) => total + widths[key], 0),
    atDefaults: sameWidths(keys, widths, defaults),
    reset: () => setWidths({ ...defaults }),
    resizer,
  }
}
