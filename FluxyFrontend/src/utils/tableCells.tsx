import { Typography } from 'antd'
import type { ReactNode } from 'react'
import UserAgentCell from '../components/table/UserAgentCell'
import { TruncatedCell } from '../components/table'

/**
 * The cell renderers the table pages were writing out by hand, and the rules under them.
 *
 * None of these is formatting for its own sake - each is a decision about what a cell shows:
 * an identifier eight characters wide with the whole value on hover; a value the server could
 * not determine drawn as a dash, because a loopback address or a missing geo database is
 * unknown and not broken; a user agent summarised to one scannable line with the raw string
 * kept for the tooltip.
 *
 * The network and country cells are not here. They are components now
 * (`features/connection-logs/components/ConnectionNetworkCell`, shared by the connection logs
 * and the three tables that used to have separate address, country and provider columns),
 * because they have two lines and a fallback state of their own; `networkLabel` in
 * `./networkLabel.ts` is the rule about the second line, which is a fact about a row rather
 * than about markup.
 *
 * They are functions rather than components - a column's `render` wants a function, and a
 * component here would be a new component type per column per repaint. `hinted` is the same
 * kind of thing. `agentCell` is the exception that proves it: it delegates to `UserAgentCell`,
 * because that cell is clickable in one of its tables and one rendering has to serve both.
 */

/** Unknown is not broken: a value the server could not determine is drawn as a dash. */
const orDash = (value: string | number | null | undefined): string =>
  value === null || value === undefined || value === '' ? '—' : String(value)

/** An identifier: eight characters and an ellipsis, the whole value on hover. */
export const idCell = (value: string): ReactNode => (
  <TruncatedCell text={`${value.slice(0, 8)}…`} full={value} />
)

/** The whole value, the column cutting it only when it does not fit. */
export const textCell = (value: string): ReactNode => <TruncatedCell text={value} full={value} />

/** A text cell for a field the server may leave empty. */
export const dashedCell = (value: string | null | undefined): ReactNode => (
  <TruncatedCell text={orDash(value)} full={value} />
)

/** An address, which is read character by character rather than as a word. */
export const ipCell = (value: string | null): ReactNode => (
  <Typography.Text code>
    <TruncatedCell text={orDash(value)} full={value} />
  </Typography.Text>
)

/** A user agent as one scannable line, the raw string kept for the tooltip. */
export const agentCell = (value: string | null): ReactNode => <UserAgentCell value={value} />
