import { Badge, Card, Descriptions, Flex, Progress, Space, Tag, theme, Typography } from 'antd'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import type { DashboardServer } from '../types'
import { clampPercent, formatMbps, formatUptime, ringTier } from '../format'
import { liveConnectionsPath } from '../links'

/**
 * The ring size and the gap between rings, stated once.
 *
 * Three rings at 72 px with two 8 px gaps is 232 px, which stays inside a 320 px phone once
 * the card's padding is taken: 232 + 32 = 264 < 320. That is why the block is a fixed
 * three-across `Flex` rather than a wrapping `Row` - a break that left one ring alone under
 * two would split one reading into two rows.
 */
const RING_SIZE = 72
const RING_GAP = 8

/**
 * A whole count as the card prints it - never a decimal, never `NaN`, never a dash.
 *
 * The counts are integers on the wire; this is the one place a `number` becomes their text,
 * and the fallback keeps a malformed answer from printing `NaN` on a screenshot.
 */
const countText = (value: number): string =>
  Number.isFinite(value) ? String(Math.trunc(value)) : '0'

/**
 * One server's card: its name as the way into its live connections, the aggregates
 * attributed to it, the throughput and uptime the probe reported, and three load rings.
 *
 * **An unavailable server draws zeros, not dashes** (the owner's D5). `0` and `0.0` are
 * what the probe answered with, and the badge at the top of the body is what says the
 * machine could not be reached - the number alone must never be read as "idle". The rings go
 * to 0 % in the neutral rail and stay grey rather than turning red: an unreachable machine
 * is a gap in the measurement, not a critical load.
 *
 * The card takes no callbacks. It builds its own links and reads its own translations, so
 * the page above holds no per-row closures and a link cannot disagree with the one beside it.
 */
export default function DashboardServerCard({
  server,
  area,
}: {
  server: DashboardServer
  /** The signed-in prefix (`/admin`), which every link hangs off. */
  area: string
}) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { token } = theme.useToken()

  /** Whether the numbers are real. An unavailable row is drawn as zeros below. */
  const available = server.available

  /** A reading as the card may print it: the probe's value, or the zero D5 asks for. */
  const value = (reading: number): number => (available ? reading : 0)

  /** The colour a ring is drawn in, and the exception state the critical tier carries. */
  const ringOf = (reading: number) => {
    if (!available) return { strokeColor: token.colorFillSecondary, status: undefined }

    const tier = ringTier(reading)

    if (tier === 'critical') {
      return { strokeColor: token.colorError, status: 'exception' as const }
    }

    return {
      strokeColor: tier === 'warn' ? token.colorWarning : token.colorPrimary,
      status: undefined,
    }
  }

  const rings = [
    { key: 'cpu', reading: server.cpuPercent },
    { key: 'ram', reading: server.ramPercent },
    { key: 'disk', reading: server.diskPercent },
  ] as const

  const target = liveConnectionsPath(area, server.id)

  return (
    <Card
      style={{ height: '100%' }}
      title={
        <Space wrap size={8}>
          {/* A real anchor rather than a click handler on a span: the `href` is what makes
              the name reachable by a keyboard and openable in a new tab. The click is taken
              over for a plain left click so the SPA keeps its history. */}
          <Typography.Link
            href={target}
            onClick={(event) => {
              event.preventDefault()
              navigate(target)
            }}
          >
            {server.name}
          </Typography.Link>
          {server.status === 'Disabled' && (
            <Tag style={{ marginInlineEnd: 0 }}>
              {t('dashboard.admin.statuses.Disabled')}
            </Tag>
          )}
        </Space>
      }
    >
      {!available && (
        <div style={{ marginBottom: 12 }}>
          <Badge status="default" text={t('dashboard.admin.unavailable')} />
        </div>
      )}

      <Descriptions column={1} size="small">
        <Descriptions.Item label={t('dashboard.admin.server.activeConnections')}>
          {countText(value(server.activeConnections))}
        </Descriptions.Item>
        <Descriptions.Item label={t('dashboard.admin.server.preferredServerUsers')}>
          {countText(value(server.preferredServerUsers))}
        </Descriptions.Item>
        <Descriptions.Item label={t('dashboard.admin.server.usersWithActiveConnections')}>
          {countText(value(server.usersWithActiveConnections))}
        </Descriptions.Item>
        <Descriptions.Item label={t('dashboard.admin.server.networkIn')}>
          {formatMbps(value(server.networkInMbps))}
        </Descriptions.Item>
        <Descriptions.Item label={t('dashboard.admin.server.networkOut')}>
          {formatMbps(value(server.networkOutMbps))}
        </Descriptions.Item>
        <Descriptions.Item label={t('dashboard.admin.server.uptime')}>
          {formatUptime(value(server.uptimeSeconds))}
        </Descriptions.Item>
      </Descriptions>

      {/* `justify="center"` centres the group in the body at every column width, and the
          `maxWidth` that used to stop a left-anchored group from stretching is gone with it.
          `wrap={false}` keeps the three-across reading: 3x72 + 2x8 is 232 px, inside a phone's
          card once the 32 px of padding is added. */}
      <Flex justify="center" align="flex-start" gap={RING_GAP} wrap={false} style={{ marginTop: 12 }}>
        {rings.map(({ key, reading }) => {
          // `clampPercent` on the visible value, then the tier decision on the reading: the
          // helper clamps first itself, so a 150 % answer is critical and a -5 % one normal.
          const percent = available ? clampPercent(reading) : 0
          const { strokeColor, status } = ringOf(reading)
          const label = t(`dashboard.admin.rings.${key}`)

          return (
            <div
              key={key}
              role="group"
              aria-label={`${label}: ${Math.round(percent)}%`}
              style={{ textAlign: 'center' }}
            >
              <Progress
                type="circle"
                size={RING_SIZE}
                strokeWidth={8}
                percent={percent}
                strokeColor={strokeColor}
                railColor={token.colorFillSecondary}
                status={status}
                format={(shown) => `${Math.round(shown ?? 0)}%`}
              />
              {/* 12 px and secondary: the number is inside the circle, and this line names
                  what the circle measures. */}
              <div
                style={{ fontSize: 12, color: token.colorTextSecondary, marginTop: 4 }}
              >
                {label}
              </div>
            </div>
          )
        })}
      </Flex>
    </Card>
  )
}
