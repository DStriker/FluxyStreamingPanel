import { Tooltip } from 'antd'

/**
 * A cell whose whole value does not fit, with the rest of it on hover.
 *
 * Three kinds of column need this: a user agent is a sentence, a provider name is whatever
 * the registry called the company, and an IPv6 address is thirty-nine characters. None of
 * them can be allowed to widen its column, and none of them may be silently cut off either -
 * the point of the table is to read what is there.
 *
 * `full` is a separate value rather than always the text, because the network column shows
 * one line while the cell may hold both a name and an AS number. It is deliberately not
 * antd's own cell tooltip: that one takes the value it was given and an ellipsised cell has
 * been cut before the browser ever builds a `title`.
 *
 * Shared between the visit history and the admin's user table because the two want exactly
 * this and nothing else - a value, the whole of it, and one cell that stays narrow.
 */
export default function TruncatedCell({
  text,
  full,
}: {
  /** What the cell shows, already shortened to fit. */
  text: string
  /** The uncut value, or nothing when the two are the same value. */
  full?: string | null
}) {
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
