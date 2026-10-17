import { Typography } from 'antd'
import type { ReactNode } from 'react'
import TruncatedCell from '../components/table/TruncatedCell'
import { countryName } from './countryName'
import { describeUserAgent } from './userAgent'

/**
 * The cell renderers the table pages were writing out by hand, and the dash rule under them.
 *
 * None of these is formatting for its own sake - each is a decision about what a cell shows:
 * an identifier eight characters wide with the whole value on hover; a value the server could
 * not determine drawn as a dash, because a loopback address or a missing geo database is
 * unknown and not broken; an address read character by character, set in code; a user agent
 * summarised to one scannable line with the raw string kept for the tooltip; a country shown
 * by name in the page's own language; a network named with its AS number. Written beside a
 * column each reads as obvious, which is exactly how six copies of the same decision drift
 * apart without failing `typecheck`, `lint` or the build.
 *
 * They are functions rather than components - a column's `render` wants a function, and a
 * component here would be a new component type per column per repaint. `hinted` is the same
 * kind of thing.
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
export const agentCell = (value: string | null): ReactNode => {
  if (!value) return '—'
  return <TruncatedCell text={describeUserAgent(value) ?? value} full={value} />
}

/** The country's own name in the page's language, or a dash when there is no code. */
export const countryCell = (value: string | null, locale: string): ReactNode => {
  const name = countryName(value, locale)
  return name === null ? '—' : <TruncatedCell text={name} full={name} />
}

/** A network as its organisation and AS number, or whichever half the server knows. */
export const networkCell = (visit: {
  organization: string | null
  autonomousSystemNumber: number | null
}): ReactNode => {
  const { organization, autonomousSystemNumber } = visit

  if (organization === null && autonomousSystemNumber === null) return '—'

  const label =
    autonomousSystemNumber === null
      ? (organization as string)
      : organization === null
        ? `AS${autonomousSystemNumber}`
        : `${organization} · AS${autonomousSystemNumber}`

  return <TruncatedCell text={label} full={label} />
}
