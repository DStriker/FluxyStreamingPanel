import { Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { describeUserAgent } from '../../utils/userAgent'
import TruncatedCell from './TruncatedCell'

/**
 * A user agent as one scannable line, with the raw string kept for the tooltip - and, when its
 * table can filter by it, as the way to do that.
 *
 * **One component for all three tables.** The owner asked for the connection logs and the visit
 * histories to render a user agent the same way, and a second copy of "summarise it, keep the raw
 * string, truncate into a tooltip" is exactly the kind of detail two tables stop agreeing about
 * the first time one of them is touched.
 *
 * The summary replaces the raw string rather than joining it: twenty rows of
 * `Mozilla/5.0 (Windows NT 10.0; Win64; x64) ...` is a wall nobody scans, while
 * `Chrome 140 (Windows 10, x64)` is one line per row - and "is this the client I own" is answered
 * by the whole thing, on hover. `describeUserAgent`'s `null` means "no name for this one", and
 * then the string is drawn exactly as it is.
 *
 * `onFilter` is optional because only the connection log can filter by a user agent: the visit
 * history resolves its geo fields at read time and has no such column to narrow by. A table that
 * cannot filter gets plain text rather than a link that would do nothing.
 */
export default function UserAgentCell({
  value,
  onFilter,
}: {
  /** The user agent the row stored, or null when it carried none. */
  value: string | null
  /** Writes the exact value as a filter, or absent when the table cannot filter by it. */
  onFilter?: (value: string) => void
}) {
  const { t } = useTranslation()

  if (!value) return <>{'—'}</>

  const cell = <TruncatedCell text={describeUserAgent(value) ?? value} full={value} />

  if (!onFilter) return cell

  return (
    <Typography.Link
      onClick={() => onFilter(value)}
      aria-label={t('common.filterBy', { value })}
    >
      {cell}
    </Typography.Link>
  )
}
