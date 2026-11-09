import { Tooltip, Typography } from 'antd'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { countryName } from '../../../utils/countryName'
import { networkLabel } from '../../../utils/networkLabel'
import type { NetworkCellRow } from '../../../utils/networkLabel'
import { flagUrl } from '../flag'

/**
 * The one network cell both table families draw: the country's flag and the address on the first
 * line, the provider on the second.
 *
 * It started as the connection log's cell and is now **shared with the two visit histories**, for
 * the owner's reason: the same rendering in both. A second copy would have been a second answer to
 * "how wide is a provider", "is the address set in code" and "is the flag clickable", and the two
 * would have drifted the moment one of them changed.
 *
 * The differences between the call sites are the differences between the data sources, and they
 * arrive as props rather than as branches:
 *
 * - the visits' organisation and AS number are two fields the server resolved, so the provider
 *   line is built from them by `networkLabel`; the connection log stores one provider string;
 * - the connection log and the user audit log both take the three filters (their country,
 *   address and provider are stored columns), so the visits pass no `onFilter` and the two logs
 *   pass their own writer. A fact with no filter to write is drawn as plain text - a control
 *   that would do nothing is worse than a word.
 *
 * **A flag that cannot be loaded falls back to the two-letter code and stops being clickable.**
 * The service may be down, may have no picture for a code, or the browser may be offline; the code
 * is what the image stood for, so the row still says which country the session came from.
 *
 * The tooltip is the *name* of the country in the page's own language, from `countryName`: `DE`
 * means nothing to a person, `Germany` does, and `Германия` does on a Russian page.
 *
 * **Two lines, and only the provider is smaller.** The address is plain text at the cell's own
 * size - no `<code>`, on the owner's correction: the monospace treatment was one more detail than
 * the field needs, and antd's `font-size: 85%` made the address the one thing in the row printed
 * under the row's size. The provider sits one step under it and dimmer, and truncates inside its
 * own line so a long organisation name never widens the column.
 */

/**
 * The provider's size: **the owner's one smaller size in this cell**, and the only font size this
 * file states.
 *
 * Everything else inherits the table cell's own size, which is what the started-at column prints
 * in. 12 is not `0.85 × the cell` either: it is the size the owner named, and it happens to sit
 * one step under the 14 antd prints a table body in.
 */
const ISP_FONT_SIZE = 12

/** The dash a field the server could not determine is drawn as. */
const DASH = '—'

/** The three filters this cell can write when its table has them. */
export type NetworkFilterKey = 'country' | 'ip' | 'isp'

export default function ConnectionNetworkCell({
  log,
  locale,
  onFilter,
}: {
  /** The row this cell belongs to. */
  log: NetworkCellRow
  /** The page's own language, for the country name. */
  locale: string
  /**
   * Writes one filter into the address, dropping `page` in the same write - or absent when the
   * table reading this cell has no such filter (the visit histories).
   *
   * The key is the query parameter and the value is what the cell read off the row, so the cell
   * does not decide which parameter a fact belongs to twice.
   */
  onFilter?: (key: NetworkFilterKey, value: string) => void
}) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 2, minWidth: 0 }}>
      {/* The first line: the flag and the address, both at the cell's own size. */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 6, minWidth: 0 }}>
        <Flag
          countryCode={log.countryCode}
          countryNames={countryName(log.countryCode, locale)}
          onFilter={onFilter}
        />
        <Address ip={log.ip} onFilter={onFilter} />
      </div>
      {/* The second line: the provider alone, so a long organisation name never widens the
          column and never pushes the address out of it. */}
      <Provider isp={networkLabel(log)} onFilter={onFilter} />
    </div>
  )
}

/**
 * The flag, the code it fell back to, or a dash.
 *
 * The failed state is this component's own state rather than a DOM swap inside `onError`: a node
 * replaced behind React's back is a node the next render puts back, and the fallback would flip in
 * and out as the table repainted. The image is fixed at 16x12 so a load that fails late does not
 * move the cell around it.
 *
 * The letter fallback states **no** font size either: it stands in for the image, and an image is
 * the cell's size like the address beside it.
 */
function Flag({
  countryCode,
  countryNames,
  onFilter,
}: {
  countryCode: string | null
  countryNames: string | null
  onFilter?: (key: NetworkFilterKey, value: string) => void
}) {
  const code = countryCode?.trim().toUpperCase() ?? ''
  const [failedFor, setFailedFor] = useState<string | null>(null)

  if (code === '' || !countryNames) return <>{DASH}</>

  // The letter fallback, not clickable: the image that named the filter is what failed, so the
  // value the visitor can read is the whole of what this cell has to say about the country.
  if (failedFor === code) return <>{code}</>

  const picture = (
    <img
      src={flagUrl(code)}
      alt=""
      width={16}
      height={12}
      style={{ display: 'block', borderRadius: 1 }}
      onError={() => setFailedFor(code)}
    />
  )

  // A table with no country filter draws the same picture without offering a click that would do
  // nothing - the tooltip still names the country, which is what the picture means.
  if (!onFilter) return <Tooltip title={countryNames}>{picture}</Tooltip>

  return (
    <Tooltip title={countryNames}>
      <button
        type="button"
        aria-label={countryNames}
        onClick={() => onFilter('country', code)}
        style={FLAG_BUTTON_STYLE}
      >
        {picture}
      </button>
    </Tooltip>
  )
}

/**
 * The address as plain text, as its own filter - unless it is a loopback.
 *
 * **Not a link when there is nothing to filter by**, which is the visits' case: a link that
 * writes no parameter is a control that lies about what it does.
 */
function Address({
  ip,
  onFilter,
}: {
  ip: string | null
  onFilter?: (key: NetworkFilterKey, value: string) => void
}) {
  const { t } = useTranslation()

  if (!ip) return <>{DASH}</>

  // A loopback address is where the server itself called from - a fact worth drawing and not a
  // network to narrow a list by, since filtering on it answers this row and nothing else.
  if (!onFilter || isLoopback(ip)) return <span>{ip}</span>

  return (
    <Typography.Link
      onClick={() => onFilter('ip', ip)}
      aria-label={t('common.filterBy', { value: ip })}
    >
      {ip}
    </Typography.Link>
  )
}

/**
 * The provider, on the second line, smaller and dimmer than the two facts above it.
 *
 * It is the only element in the cell that is set below the cell's size, and the only one that
 * truncates on its own line: the organisation name is whatever the registry called the company,
 * so it is given the width of the cell and a tooltip rather than a share of a row.
 */
function Provider({
  isp,
  onFilter,
}: {
  isp: string | null
  onFilter?: (key: NetworkFilterKey, value: string) => void
}) {
  const { t } = useTranslation()

  if (!isp) return <span style={{ opacity: 0.75 }}>{DASH}</span>

  const style = {
    fontSize: ISP_FONT_SIZE,
    opacity: 0.75,
    display: 'block',
    maxWidth: '100%',
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
  } as const

  if (!onFilter) {
    return (
      <Tooltip title={isp}>
        <span style={style}>{isp}</span>
      </Tooltip>
    )
  }

  return (
    <Tooltip title={isp}>
      <Typography.Link
        style={style}
        onClick={() => onFilter('isp', isp)}
        aria-label={t('common.filterBy', { value: isp })}
      >
        {isp}
      </Typography.Link>
    </Tooltip>
  )
}

/** The flag button's own reset, so the glyph sits in the row like text rather than like a control. */
const FLAG_BUTTON_STYLE = {
  padding: 0,
  border: 'none',
  background: 'none',
  cursor: 'pointer',
  lineHeight: 0,
  flex: 'none',
} as const

/** Whether an address is the loopback of either family, or a v4-mapped form of IPv6's. */
const isLoopback = (ip: string): boolean => {
  const value = ip.trim().toLowerCase()

  return (
    value === '::1' ||
    value === '0:0:0:0:0:0:0:1' ||
    value === '::ffff:127.0.0.1' ||
    value.startsWith('127.')
  )
}
