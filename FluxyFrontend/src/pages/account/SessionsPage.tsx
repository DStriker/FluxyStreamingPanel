import { Card, Space, Tag } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import type { SessionSortField } from '../../features/sessions/api'
import { formatTimestamp } from '../../utils/dateFormat'
import { tableResults } from '../../utils/tableResults'
import { agentCell } from '../../utils/tableCells'
import { ResetColumnWidths, ServerTable, TableSearch } from '../../components/table'
import type { ResizableHeaderCellProps } from '../../components/table'
import ConnectionNetworkCell from '../../features/connection-logs/components/ConnectionNetworkCell'
import { useProfile } from '../../features/profile/hooks/queries'
import { useSessionHistory } from '../../features/sessions/hooks/queries'
import { useColumnWidths, useServerTable } from '../../hooks/table'
import type { SessionVisit } from '../../features/sessions/types'

/** The three columns, in the order they are drawn, named by their own keys. */
type SessionColumnKey = 'when' | 'network' | 'agent'

const COLUMN_KEYS: readonly SessionColumnKey[] = ['when', 'network', 'agent']

/**
 * The width each column starts at, and the width it goes back to when asked to reset.
 *
 * Every column has a width of its own rather than letting the browser work it out, which is the
 * whole answer to "what happens when a provider name is very long": a column without one grows
 * to fit its widest cell, so sixty characters of registry name would widen the table and push
 * every column after it sideways. The widths are fixed, the two unbounded columns truncate, and
 * the table scrolls inside its own card instead of dragging the page with it.
 *
 * **Three columns on the owner's correction**, where there were five: the address, the country
 * and the provider are one cell now - the same cell the connection logs draw - because they are
 * one fact about one network rather than three facts a reader compares down a column. The room
 * the two removed columns gave up went to the network cell and to the user agent, which are the
 * two cells that have something long to say.
 *
 * These are *starting* widths, not the answer - the visitor may move them, and the sum of
 * whatever they are is what the table asks the card to be able to scroll (`tableWidth` below).
 * What the visitor may never do is remove one, which is what the floor inside
 * `useColumnWidths` clamps to.
 *
 * The sum is **1140**, not the 1190 the administration's tables reach: this page has no
 * selection column and no actions column, and its own invariant is "the three columns a visitor
 * reads", not a total borrowed from a table with five more.
 */
const DEFAULT_COLUMN_WIDTHS: Record<SessionColumnKey, number> = {
  when: 300,
  network: 440,
  agent: 400,
}

/**
 * Where the widths are remembered between visits.
 *
 * Only here. Resizing is a preference about how one person reads one table, and it is worth
 * surviving a reload precisely because the alternative is re-dragging three columns every time
 * the page is opened. Nothing else about the table is stored: a stored page or filter would
 * be a claim about what is on the server, which is not true just because the browser says so.
 */
const COLUMN_WIDTHS_KEY = 'fluxy.sessions.columnWidths.v2'

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

  /**
   * The five request parameters, the pager and the two headers - all of that is
   * `useServerTable`, which the four other server-paged tables call too. What this page adds
   * is the one thing a shared hook cannot know: the "When" column is keyed `when` for its
   * width and its handle, while the endpoint orders by `visitedAt`; the geo columns offer no
   * sorter at all (see `SessionSortField`), so `ip` is the only other key that can arrive
   * here, and anything that is not `ip` came from clicking "When".
   */
  const table = useServerTable<SessionVisit, SessionSortField>({
    defaultSort: { by: 'visitedAt', order: 'desc' },
    sortFields: ['visitedAt', 'ip'],
    // The merged network cell is keyed `network` for its width and its handle, while the
    // endpoint's own field for the address in it is `ip` - the only geo field it can order by,
    // because the others are resolved while a page is built. Any other key came from "When".
    sortFieldOf: (columnKey) => (columnKey === 'network' ? 'ip' : 'visitedAt'),
  })

  /**
   * The page of visits, fetched through a query whose key **is** the five parameters.
   *
   * That is the whole of what the page used to do by hand: an effect depending on the five
   * plus an `attempt` counter, a `cancelled` flag to keep an abandoned page's answer from
   * landing, and a `loading` boolean. The key changing *is* the new request - `page 1` after
   * a filter change is a different query rather than a state update the effect had to notice
   * - and React Query discards the answer of a query the visitor has moved away from, which
   * is what the per-run flag was for. A failure is the query's own `error`; the retry button
   * calls `refetch` rather than bumping a counter.
   */
  const historyQuery = useSessionHistory(table.query)
  const { data: history, loadError, loading } = tableResults(historyQuery)

  /**
   * The zone, shared with every other page that prints a date - one `['profile']` query per
   * installation rather than one `getProfile()` effect per page. A failure leaves the
   * browser's zone in place and the list still renders, which is the right degradation: the
   * history matters and the exact zone it is printed in does not.
   */
  const profileQuery = useProfile()
  const timeZone = profileQuery.data?.timeZone ?? null

  /**
   * The three widths: their state, their memory between visits, and the drag that changes
   * them. All of that lives in `useColumnWidths`, which the admin's user table calls too -
   * the handle, the listeners and the clamp are the part of this page that reads as if it
   * were simple and is not, and two copies of those rules would be two places for one of
   * them to be quietly wrong where nothing in `typecheck`, `lint` or the build can tell.
   */
  const { widths, tableWidth, atDefaults, reset, resizer: handleFor } =
    useColumnWidths<SessionColumnKey>({
      keys: COLUMN_KEYS,
      defaults: DEFAULT_COLUMN_WIDTHS,
      storageKey: COLUMN_WIDTHS_KEY,
    })

  /**
   * `resolvedLanguage` rather than `language`: the two differ whenever the requested language
   * has no bundle, and then every string falls back to English while `language` still reports
   * the language nobody is being shown. A page reading `Germany` next to `Германия` would be
   * exactly that disagreement, made visible in one cell.
   */
  const locale = i18n.resolvedLanguage ?? i18n.language

  /**
   * A date as this account's own zone and the page's own language show it, falling back to the
   * browser's zone when the stored one is absent or is one this ICU build refuses to format in.
   */
  const formatMoment = (value: string): string => formatTimestamp(value, locale, timeZone)

  /**
   * One column's resize handle, with this page's name for the column.
   *
   * The hook owns the drag, the clamp and the stored widths, but only the page knows that a
   * column is called "When": the handle's accessible name is built here and handed over as an
   * argument, because a translation key is not something a generic hook can hold.
   */
  const resizer = (key: SessionColumnKey): ResizableHeaderCellProps =>
    handleFor(key, t('sessions.resizeColumn', { column: t(`sessions.columns.${key}`) }))

  const columns: TableColumnsType<SessionVisit> = [
    {
      title: t('sessions.columns.when'),
      key: 'when',
      width: widths.when,
      sorter: true,
      sortOrder: table.orderOf('visitedAt'),
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
      // The address, the country and the provider in one cell, drawn by the same component the
      // connection logs use - the owner asked for the same rendering in both tables rather than
      // two answers to "how wide is a provider". It carries **no filter callbacks**: this table's
      // endpoint resolves its geo fields while a page is built and takes no `ip`/`country`/`isp`
      // parameter, so a clickable cell here would be a control that does nothing.
      title: t('sessions.columns.network'),
      key: 'network',
      width: widths.network,
      // Still sortable, and still by the address: `ip` is the only geo field the endpoint can
      // order by (the other two are resolved while a page is built), and the address is now the
      // first line of this cell. Losing the sorter with the column would be a quiet removal of
      // a feature the page had.
      sorter: true,
      sortOrder: table.orderOf('ip'),
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('network'),
      render: (_, visit) => <ConnectionNetworkCell log={visit} locale={locale} />,
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
      width: widths.agent,
      ellipsis: { showTitle: false },
      onHeaderCell: () => resizer('agent'),
      render: agentCell,
    },
  ]

  return (
    <Card
      className="page-card"
      title={t(sectionKey)}
      extra={
        <Space wrap>
          <TableSearch
            value={table.searchText}
            onChange={table.setSearchText}
            onSearch={table.applySearch}
            labelKey="sessions.searchLabel"
            placeholderKey="sessions.searchPlaceholder"
            width={320}
          />
          {/* Disabled rather than hidden when the widths are already the defaults: a control
              that appears and disappears reads as a broken one, and there is nothing to undo
              until somebody has actually dragged something. The icon keeps the label out of
              a toolbar that shares its row with the table's title. */}
          <ResetColumnWidths
            label={t('sessions.resetWidths')}
            disabled={atDefaults}
            onClick={() => reset()}
          />
        </Space>
      }
    >
      <ServerTable<SessionVisit, SessionSortField>
        table={table}
        columns={columns}
        rows={history?.items ?? []}
        total={history?.total ?? 0}
        loading={loading}
        loadError={loadError}
        onRetry={() => historyQuery.refetch()}
        tableWidth={tableWidth}
        emptyText={t(table.query.search ? 'sessions.noMatches' : 'sessions.empty')}
        showTotal={(total, range) =>
          t('sessions.range', { from: range[0], to: range[1], total })
        }
      />
    </Card>
  )
}
