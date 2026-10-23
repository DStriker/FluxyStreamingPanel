import { Tag, theme } from 'antd'
import { useTranslation } from 'react-i18next'
import { TruncatedCell } from '../../../components/table'
import type { AudioStreamStatus, AudioStreamType } from '../types'

/**
 * Two descriptions, not a ramp.
 *
 * `AudioStreamStatus` is not an ordered scale - a stream is broadcasting or it is not, and
 * neither is "more" of anything than the other - so the colours say what the state *is*.
 */
const STATUS_COLOR: Record<AudioStreamStatus, string> = {
  Stopped: 'default',
  Running: 'success',
}

/**
 * The state cell of the audio streams table: the status, and the kind of stream under it.
 *
 * Two facts in one column, because the kind is what tells the status apart: an on-demand
 * audio stream that is running and a redirecting one that is running are not the same thing,
 * and a table that shows only the first of the two words is saying half of it. The kind is
 * the quieter of the two - a muted line under the tag - because the state is what a visitor
 * scans the column for, while the kind is what they look up once they have found the row
 * they meant.
 *
 * The line truncates rather than widening its column, and the whole name stays one hover
 * away: the visitor may narrow the column, and the kind is a name rather than a word.
 */
export default function StreamStateCell({
  status,
  type,
}: {
  status: AudioStreamStatus
  type: AudioStreamType
}) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const kind = t(`audioStreams.types.${type}`)

  return (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: 2 }}>
      <Tag color={STATUS_COLOR[status]} style={{ marginInlineEnd: 0 }}>
        {t(`audioStreams.statuses.${status}`)}
      </Tag>
      {/* The kind in the muted small type antd's own group titles use, cut with an ellipsis
          rather than allowed to widen the column - and the full name on hover, because the
          longest of the two in Russian ("Перенаправление") is a name rather than a word. */}
      <span
        style={{
          display: 'block',
          maxWidth: '100%',
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          whiteSpace: 'nowrap',
          fontSize: token.fontSizeSM,
          lineHeight: token.lineHeightSM,
          color: token.colorTextDescription,
        }}
      >
        <TruncatedCell text={kind} full={kind} />
      </span>
    </div>
  )
}
