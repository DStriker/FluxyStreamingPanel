import { useEffect, useRef, useState } from 'react'
import type {
  KeyboardEvent as ReactKeyboardEvent,
  MouseEvent as ReactMouseEvent,
  PointerEvent as ReactPointerEvent,
  ReactNode,
  TdHTMLAttributes,
} from 'react'
import { Alert, Button, Card, Input, Space, Table, Tag, Tooltip, Typography } from 'antd'
import type { TableColumnsType, TableProps } from 'antd'
import { useTranslation } from 'react-i18next'
import { getSessionHistory, getProfile } from '../lib/api'
import type { SessionSortField, SessionSortOrder } from '../lib/api'
import { messageForError } from '../lib/http'
import { countryName } from '../lib/countryName'
import { describeUserAgent } from '../lib/userAgent'
import type { SessionHistoryResponse, SessionVisit } from '../types'

/**
 * How many visits one page holds. The server's own default, kept in step by hand because the
 * two are one contract written twice - and because a page size the pager draws from has to be
 * known before the first answer arrives, or the pager would show one row per page until it did.
 */
const PAGE_SIZE = 20

/** The sizes the visitor may pick, in the same order the pager shows them. */
const PAGE_SIZE_OPTIONS = [10, 20, 50, 100]

/** The five columns, in the order they are drawn, named by their own keys. */
type SessionColumnKey = 'when' | 'ip' | 'country' | 'network' | 'agent'

const COLUMN_KEYS: readonly SessionColumnKey[] = ['when', 'ip', 'country', 'network', 'agent']

/**
 * The width each column starts at, and the width it goes back to when asked to reset.
 *
 * Every column has a width of its own rather than letting the browser work it out, which is the
 * whole answer to "what happens when a provider name is very long": a column without one grows
 * to fit its widest cell, so sixty characters of registry name would widen the table and push
 * every column after it sideways. The widths are fixed, the two unbounded columns truncate, and
 * the table scrolls inside its own card instead of dragging the page with it.
 *
 * These are *starting* widths, not the answer - the visitor may move them, and the sum of
 * whatever they are is what the table asks the card to be able to scroll (`tableWidth` below).
 * What the visitor may never do is remove one, which is what the floor in `clampColumnWidth`
 * is for.
 *
 * The country column used to be 110 for a code; it is 150 because the cell now carries a name.
 */
const DEFAULT_COLUMN_WIDTHS: Record<SessionColumnKey, number> = {
  when: 300,
  ip: 170,
  country: 150,
  network: 240,
  agent: 280,
}

/**
 * How far a column may be dragged.
 *
 * The floor is a header width rather than zero: a column narrowed to nothing is a column whose
 * resize handle can no longer be reached to widen it again, and the handle lives at the right
 * edge of the cell. The ceiling exists so a drag cannot produce a table wider than any screen
 * could show - 1200 per column is already past that.
 */
const MIN_COLUMN_WIDTH = 90
const MAX_COLUMN_WIDTH = 1200

/**
 * Where the widths are remembered between visits.
 *
 * Only here. Resizing is a preference about how one person reads one table, and it is worth
 * surviving a reload precisely because the alternative is re-dragging five columns every time
 * the page is opened. Nothing else about the table is stored: a stored page or filter would
 * be a claim about what is on the server, which is not true just because the browser says so.
 */
const COLUMN_WIDTHS_KEY = 'fluxy.sessions.columnWidths'

type ColumnWidths = Record<SessionColumnKey, number>

const clampColumnWidth = (value: number): number =>
  Math.min(MAX_COLUMN_WIDTH, Math.max(MIN_COLUMN_WIDTH, value))

const sameWidths = (one: ColumnWidths, other: ColumnWidths): boolean =>
  COLUMN_KEYS.every((key) => one[key] === other[key])

/**
 * The remembered widths, or the defaults when there are none to remember.
 *
 * Everything that can go wrong falls back to the defaults rather than to nothing: a browser in
 * private mode refuses storage, a corrupted entry is not JSON, a value that is a string is not
 * a width, and the probes render this under Node, where `localStorage` does not exist at all
 * and reaching for it is a `ReferenceError`. None of those is a reason for the table not to
 * draw, and the last one is why the whole body sits in a `try` rather than behind a
 * `typeof` guard the compiler would then be entitled to call unnecessary.
 */
function readColumnWidths(): ColumnWidths {
  const widths: ColumnWidths = { ...DEFAULT_COLUMN_WIDTHS }

  try {
    const stored = localStorage.getItem(COLUMN_WIDTHS_KEY)
    if (!stored) return widths

    const parsed: unknown = JSON.parse(stored)
    if (typeof parsed !== 'object' || parsed === null) return widths

    for (const key of COLUMN_KEYS) {
      const value = (parsed as Record<string, unknown>)[key]
      if (typeof value === 'number' && Number.isFinite(value)) widths[key] = clampColumnWidth(value)
    }
  } catch {
    return { ...DEFAULT_COLUMN_WIDTHS }
  }

  return widths
}

/**
 * A cell whose whole value does not fit, with the rest of it on hover.
 *
 * Three columns need this: a user agent is a sentence, a provider name is whatever the registry
 * called the company, and an IPv6 address is thirty-nine characters. None of them can be
 * allowed to widen its column, and none of them may be silently cut off either - the point of
 * the page is to read what is there.
 *
 * `full` is a separate value rather than always the text, because the network column shows one
 * line while the cell may hold both a name and an AS number. It is deliberately not antd's own
 * cell tooltip: that one takes the value it was given and an ellipsised cell has been cut
 * before the browser ever builds a `title`.
 */
function TruncatedCell({ text, full }: { text: string; full?: string | null }) {
  if (!full) return <>{text}</>

  return (
    <Tooltip
      title={full}
      styles={{ container: { maxWidth: 560, whiteSpace: 'normal', wordBreak: 'break-word' } }}
    >
      <span>{text}</span>
    </Tooltip>
  )
}

/**
 * A table header cell that carries a resize handle at its right edge.
 *
 * The handle is a sibling of the header's own content rather than part of it, and that is the
 * point of going through `components.header.cell` at all. For a sortable column antd wraps the
 * title in a `<button>`; a handle inside that button would be a focusable element nested in a
 * button, and pressing it would sort the column as well as resize it. Sibling and absolutely
 * positioned, it takes the pointer itself and the button never sees the click.
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
 */
type ResizableHeaderCellProps = Omit<
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

function ResizableHeaderCell(props: ResizableHeaderCellProps) {
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
          className="sessions-col-resizer"
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

/**
 * Dates, by locale and zone, remembered.
 *
 * Building an `Intl.DateTimeFormat` is the expensive part - eight milliseconds for a hundred
 * rows on this machine, which is half of a frame once the table repaints on every step of a
 * column drag. Looking a date up in an existing one is two orders of magnitude cheaper, so the
 * expensive half happens once per locale and zone instead of once per cell per frame.
 *
 * `null` is a remembered *refusal* as much as a formatter is a remembered answer: a zone this
 * ICU build does not carry would otherwise be re-discovered by every row of every repaint.
 */
const dateFormatters = new Map<string, Intl.DateTimeFormat | null>()

function dateFormatterFor(locale: string, timeZone: string | undefined): Intl.DateTimeFormat | null {
  const key = `${locale}|${timeZone ?? ''}`
  const cached = dateFormatters.get(key)
  if (cached !== undefined) return cached

  let formatter: Intl.DateTimeFormat | null = null

  try {
    formatter = new Intl.DateTimeFormat(locale, {
      dateStyle: 'medium',
      timeStyle: 'medium',
      ...(timeZone ? { timeZone } : {}),
    })
  } catch {
    // Not decoration: a zone stored by an older build, or a zone this ICU build does not
    // carry, makes `Intl.DateTimeFormat` throw before it returns anything - and an exception
    // thrown while rendering a row would take the whole page down over one timestamp.
    formatter = null
  }

  dateFormatters.set(key, formatter)
  return formatter
}

/**
 * The account's visit history: one read-only row per token the account ever held.
 *
 * Read only, and that is the whole shape of the page. There is no form here, no action column
 * and no selection - a history is a record of what happened, so the only things this page can
 * do are read it, turn its pages, narrow it, order it and arrange it. Anything else would
 * imply the visitor could edit a fact the server keeps precisely so that it cannot be edited.
 *
 * One card holds all of it. The heading, the search and the table are one thing to look at -
 * a second card carrying a sentence about the first would be a second box around a single
 * subject, and the sentence is the kind of copy a page only needs while it has no way to say
 * the same thing by working. The search replaces it: it states what the rows are by accepting
 * what you are looking for.
 *
 * Search and sort are sent to the server rather than applied to the rows on screen, and that is
 * not a preference. The pager shows `total`, and a filter applied here would count what it is
 * about to hide - the visitor would be offered four pages of five rows when the filter admitted
 * one. The same applies to the ordering: sorting one page would look correct until they turned
 * to the second. Both are therefore parameters of the request, and both reset the pager,
 * because a filtered list starts at its first page and page 4 of a new order is not where
 * anybody asked to be.
 *
 * The rows are tokens rather than sessions, so a browser that refreshed from a different network
 * appears twice. That is the point: "a sign-in I do not recognise" and "a rotation I do not
 * recognise" are the same alarm, and hiding the second behind an aggregation of the first would
 * hide the moment the network actually changed.
 *
 * Dates are rendered in the account's own time zone rather than the browser's, because the
 * profile page says they are - "Dates and times are shown in this zone" is a promise the whole
 * account area has to keep, and a history page that answered in the browser's zone would be
 * the one page where the setting visibly did nothing. The zone is read once on mount: it
 * changes only from the profile page, which is a navigation away, so re-reading it per page
 * would buy nothing. The *language* of the dates follows the page rather than the browser for
 * the same reason the country column does: this application has a language switcher, and a
 * date printed in one language beside a country name printed in another is the switcher
 * visibly failing halfway down a row.
 */
export default function SessionsPage({ sectionKey }: { sectionKey: string }) {
  const { t, i18n } = useTranslation()

  const [history, setHistory] = useState<SessionHistoryResponse | null>(null)
  const [timeZone, setTimeZone] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(PAGE_SIZE)
  // What is typed and what the server has been asked are two different things: one request per
  // keystroke would be a request per character of a user agent, and the answer to the first
  // nine would arrive after the tenth and put a stale page on screen.
  const [searchText, setSearchText] = useState('')
  const [search, setSearch] = useState('')
  const [sortBy, setSortBy] = useState<SessionSortField>('visitedAt')
  const [sortOrder, setSortOrder] = useState<SessionSortOrder>('desc')
  // Bumped by the retry button rather than by the page changing, so a failed load can be asked
  // for again without pretending the visitor navigated somewhere else.
  const [attempt, setAttempt] = useState(0)
  const [columnWidths, setColumnWidths] = useState<ColumnWidths>(readColumnWidths)

  /**
   * `resolvedLanguage` rather than `language`: the two differ whenever the requested language
   * has no bundle, and then every string falls back to English while `language` still reports
   * the language nobody is being shown. A page reading `Germany` next to `Германия` would be
   * exactly that disagreement, made visible in one cell.
   */
  const locale = i18n.resolvedLanguage ?? i18n.language

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
      if (sameWidths(columnWidths, DEFAULT_COLUMN_WIDTHS)) localStorage.removeItem(COLUMN_WIDTHS_KEY)
      else localStorage.setItem(COLUMN_WIDTHS_KEY, JSON.stringify(columnWidths))
    } catch {
      // The widths are still in state and the table is still resizable; only the memory of
      // them between visits is lost, which is worth failing silently over. Storage is
      // unavailable under the render probe and in a private window, so this is not a rare path.
    }
  }, [columnWidths])

  useEffect(() => {
    // The zone is read for the dates below, not for this page's own state: a failure here leaves
    // the browser's zone in place and the list still renders, which is the right degradation -
    // the history matters and the exact zone it is printed in does not.
    let cancelled = false

    getProfile()
      .then((profile) => {
        if (!cancelled) setTimeZone(profile.timeZone)
      })
      .catch(() => {})

    return () => {
      cancelled = true
    }
  }, [])

  useEffect(() => {
    // A generation of one per effect run rather than one shared counter: an answer from the
    // page the visitor just left must not overwrite the page they moved to, and an effect that
    // is torn down mid-flight must not write into a table that has already drawn its next row.
    let cancelled = false

    setLoading(true)

    getSessionHistory({ page, pageSize, search, sortBy, sortOrder })
      .then((answer) => {
        if (cancelled) return
        setHistory(answer)
        setLoadError(null)
      })
      .catch((error) => {
        if (cancelled) return
        setHistory(null)
        setLoadError(messageForError(error))
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [page, pageSize, search, sortBy, sortOrder, attempt])

  /**
   * Applies what is in the search box, from the field itself rather than from a button, so the
   * empty box empties the list the moment it becomes empty.
   */
  const applySearch = (value: string) => {
    const next = value.trim()
    const current = search.trim()

    if (next === current) return

    setSearch(next)
    setPage(1)
  }

  /**
   * Reads the pager and the sortable headers together, because antd reports both through one
   * handler and the two behave differently: a new page keeps the order, a new order starts the
   * pages over.
   */
  const handleTableChange: TableProps<SessionVisit>['onChange'] = (
    pagination,
    _filters,
    sorter,
    extra,
  ) => {
    const nextPageSize = pagination.pageSize ?? PAGE_SIZE

    if (nextPageSize !== pageSize) {
      // A different page size is a different pager, so this starts again at the first page
      // rather than leaving the visitor on a page number that no longer means what it did.
      setPageSize(nextPageSize)
      setPage(1)
      return
    }

    if (extra.action === 'paginate') {
      setPage(pagination.current ?? 1)
      return
    }

    setPage(1)

    const single = Array.isArray(sorter) ? sorter[0] : sorter

    if (!single?.order) {
      // The header was clicked through its sorted state to none, which is the visitor asking
      // for no order in particular. The default is then what the server is being sent, and the
      // header has to say so - a table that quietly applies its default order while no arrow is
      // showing is telling them the column is unsorted when it is not.
      setSortBy('visitedAt')
      setSortOrder('desc')
      return
    }

    setSortBy(single.columnKey === 'ip' ? 'ip' : 'visitedAt')
    setSortOrder(single.order === 'ascend' ? 'asc' : 'desc')
  }

  /**
   * A date as this account's own zone and the page's own language show it, falling back to the
   * browser's zone when the stored one is absent or is one this ICU build refuses to format in.
   */
  const formatMoment = (value: string): string => {
    const date = new Date(value)

    if (Number.isNaN(date.getTime())) return '—'

    const formatter =
      dateFormatterFor(locale, timeZone ?? undefined) ?? dateFormatterFor(locale, undefined)

    return formatter ? formatter.format(date) : '—'
  }

  /** A value the server could not determine, drawn as a dash rather than as an empty cell. */
  const orDash = (value: string | number | null | undefined): string =>
    value === null || value === undefined || value === '' ? '—' : String(value)

  /**
   * Which order a column is in, or nothing when it is not the one the page is sorted by.
   *
   * The arrows are controlled rather than left to antd: the server always applies an order, so
   * a header that showed no arrow while rows arrived newest-first would be describing a table
   * that does not exist. Only the two stored columns can be sorted at all - see the note on
   * `SessionSortField`.
   */
  const orderOf = (key: SessionSortField) =>
    sortBy === key ? (sortOrder === 'asc' ? 'ascend' : 'descend') : null

  const setColumnWidth = (key: SessionColumnKey, next: number) =>
    setColumnWidths((previous) => ({ ...previous, [key]: clampColumnWidth(next) }))

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
  const beginResize = (key: SessionColumnKey, event: ReactPointerEvent<Element>) => {
    // The primary button only: a right-click or a middle-click on the handle is not a drag.
    if (event.button !== 0) return

    const startX = event.clientX
    const startWidth = columnWidths[key]

    const onMove = (move: PointerEvent) => {
      // Always from the width the drag *began* at, never from the last one written: an
      // accumulator would compound the delta and the column would run away from the pointer.
      setColumnWidth(key, startWidth + (move.clientX - startX))
    }

    const finish = () => {
      window.removeEventListener('pointermove', onMove)
      window.removeEventListener('pointerup', finish)
      window.removeEventListener('pointercancel', finish)
      document.body.classList.remove('sessions-col-resizing')
      endResizeRef.current = null
    }

    document.body.classList.add('sessions-col-resizing')
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
   * and puts the wrapper there, and for the other three nothing else is listening either way.
   * A key pressed on the handle bubbles to it, so both arrangements end up here.
   */
  const nudgeColumn = (key: SessionColumnKey, event: ReactKeyboardEvent<HTMLTableCellElement>) => {
    const step = event.shiftKey ? 64 : 16

    if (event.key === 'ArrowLeft') setColumnWidth(key, columnWidths[key] - step)
    else if (event.key === 'ArrowRight') setColumnWidth(key, columnWidths[key] + step)
    else if (event.key === 'Home') setColumnWidth(key, DEFAULT_COLUMN_WIDTHS[key])
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
   * the column's own name and would compete with the header for them. The handle's name goes
   * through `resizeLabel`, the component's own channel, and it hardcodes the `tabIndex` it
   * needs. The rest are plain `React.TdHTMLAttributes` keys, which is why the return type needs
   * no cast.
   */
  const resizer = (key: SessionColumnKey): ResizableHeaderCellProps => ({
    role: 'separator',
    'aria-orientation': 'vertical',
    'aria-valuenow': columnWidths[key],
    'aria-valuemin': MIN_COLUMN_WIDTH,
    'aria-valuemax': MAX_COLUMN_WIDTH,
    resizeLabel: t('sessions.resizeColumn', { column: t(`sessions.columns.${key}`) }),
    onPointerDown: (event) => beginResize(key, event),
    onDoubleClick: () => setColumnWidth(key, DEFAULT_COLUMN_WIDTHS[key]),
    onKeyDown: (event) => nudgeColumn(key, event),
  })

  /** Whatever the five widths add up to, and so the width the table has before it scrolls. */
  const tableWidth = COLUMN_KEYS.reduce((total, key) => total + columnWidths[key], 0)

  const columns: TableColumnsType<SessionVisit> = [
    {
      title: t('sessions.columns.when'),
      key: 'when',
      width: columnWidths.when,
      sorter: true,
      sortOrder: orderOf('visitedAt'),
      onHeaderCell: () => resizer('when'),
      render: (_, visit) => (
        <Space size={8} wrap>
          <span>{formatMoment(visit.visitedAt)}</span>
          {visit.isCurrent && (
            <Tag color="processing" style={{ marginInlineEnd: 0 }}>
              {t('sessions.current')}
            </Tag>
          )}
        </Space>
      ),
    },
    {
      title: t('sessions.columns.ip'),
      dataIndex: 'ip',
      key: 'ip',
      width: columnWidths.ip,
      sorter: true,
      sortOrder: orderOf('ip'),
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('ip'),
      render: (value: string | null) => (
        <Typography.Text code>
          <TruncatedCell text={orDash(value)} full={value} />
        </Typography.Text>
      ),
    },
    {
      // A code is what GeoIP produces and what the allow lists are written against, but nobody
      // reads `DE` as a country - they read Germany, or `Германия` on a Russian page. The name
      // is looked up where the names live (`countryName`, which is the platform's own table),
      // and the column is 150 rather than the 110 a two-letter code needed because a name does
      // not fit in that. The tooltip is the name rather than the code: the cell may still be
      // cut for `South Georgia and the South Sandwich Islands`, and the code is not what this
      // column is showing anyone.
      title: t('sessions.columns.country'),
      dataIndex: 'countryCode',
      key: 'country',
      width: columnWidths.country,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('country'),
      render: (value: string | null) => {
        const name = countryName(value, locale)
        return name === null ? '—' : <TruncatedCell text={name} full={name} />
      },
    },
    {
      // The one column whose content the server does not bound. An organisation name is a
      // registry's idea of a company and can run to sixty characters, so this is where an
      // unbounded column would have widened the whole table - the width above and the
      // truncation below are what keep the answer to that in this cell and nowhere else.
      title: t('sessions.columns.network'),
      key: 'network',
      width: columnWidths.network,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('network'),
      render: (_, visit) => {
        const { organization, autonomousSystemNumber } = visit

        if (organization === null && autonomousSystemNumber === null) return '—'

        const label =
          autonomousSystemNumber === null
            ? (organization as string)
            : organization === null
              ? `AS${autonomousSystemNumber}`
              : `${organization} · AS${autonomousSystemNumber}`

        return <TruncatedCell text={label} full={label} />
      },
    },
    {
      // The summary replaces the raw string rather than joining it, and the tooltip carries the
      // raw string instead: twenty rows of `Mozilla/5.0 (Windows NT 10.0; Win64; x64) ...` is
      // a wall nobody scans, while `Chrome 140 (Windows 10, x64)` is one line per row - and
      // "is this the client I own" is answered by the whole thing, on hover, as before. A
      // client the parser has no name for keeps its string untouched; see `describeUserAgent`.
      title: t('sessions.columns.agent'),
      dataIndex: 'userAgent',
      key: 'agent',
      width: columnWidths.agent,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('agent'),
      render: (value: string | null) => {
        if (!value) return '—'
        return <TruncatedCell text={describeUserAgent(value) ?? value} full={value} />
      },
    },
  ]

  return (
    <Card
      title={t(sectionKey)}
      extra={
        <Space wrap>
          <Input.Search
            allowClear
            aria-label={t('sessions.searchLabel')}
            placeholder={t('sessions.searchPlaceholder')}
            style={{ width: 320 }}
            value={searchText}
            onChange={(event) => {
              setSearchText(event.target.value)

              // An emptied box empties the list straight away. Waiting for Enter would mean the
              // visitor clears the field, sees the old filter still applied, and has to press
              // something they just finished typing into.
              if (event.target.value === '') applySearch('')
            }}
            onSearch={applySearch}
          />
          {/* Disabled rather than hidden when the widths are already the defaults: a control
              that appears and disappears reads as a broken one, and there is nothing to undo
              until somebody has actually dragged something. */}
          <Button
            disabled={sameWidths(columnWidths, DEFAULT_COLUMN_WIDTHS)}
            onClick={() => setColumnWidths({ ...DEFAULT_COLUMN_WIDTHS })}
          >
            {t('sessions.resetWidths')}
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
            <Button size="small" onClick={() => setAttempt((value) => value + 1)} loading={loading}>
              {t('actions.retry')}
            </Button>
          }
        />
      ) : (
        <Table<SessionVisit>
          rowKey="id"
          columns={columns}
          dataSource={history?.items ?? []}
          loading={loading}
          // `ResizableHeaderCell` rather than the default cell: the handles are a sibling of the
          // header's own content, and the only way to put them there without nesting a focusable
          // span inside antd's sortable `<button>` is to own the cell. It is a module-level
          // function on purpose - a component type created inside this one would be a new type
          // on every repaint, and React would then unmount the handle mid-drag, dropping the
          // pointer with it.
          components={{ header: { cell: ResizableHeaderCell } }}
          scroll={{ x: tableWidth }}
          onChange={handleTableChange}
          locale={{ emptyText: t(search ? 'sessions.noMatches' : 'sessions.empty') }}
          pagination={{
            current: page,
            pageSize,
            total: history?.total ?? 0,
            showSizeChanger: true,
            pageSizeOptions: PAGE_SIZE_OPTIONS,
            showTotal: (total, range) =>
              t('sessions.range', { from: range[0], to: range[1], total }),
          }}
        />
      )}
    </Card>
  )
}
