import { useEffect, useRef, useState } from 'react'
import type { MouseEvent, ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { Alert, Button, Card, Col, Empty, Flex, Row, Statistic, Typography } from 'antd'
import {
  ApiOutlined,
  ArrowDownOutlined,
  ArrowUpOutlined,
  PauseCircleOutlined,
  ReloadOutlined,
  StopOutlined,
  TeamOutlined,
  VideoCameraOutlined,
} from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import DashboardServerCard from '../../features/dashboard/components/DashboardServerCard'
import { useDashboard } from '../../features/dashboard/hooks/queries'
import { liveConnectionsPath, videoStreamsPath } from '../../features/dashboard/links'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'
import { hinted } from '../../utils/hinted'
import { messageForError } from '../../utils/http'

/** How often the snapshot is re-read while the toggle is on, in milliseconds. */
const AUTO_REFRESH_MS = 30_000

/** How many skeleton cards stand in for the server grid before the first answer arrives. */
const SKELETON_SERVERS = 3

/**
 * A `Typography.Link` that behaves as an anchor.
 *
 * `href` is what makes the title reachable by a keyboard and openable in a new tab; the
 * click is taken over only for a plain left click, so the SPA keeps its history and a
 * modified click still does what the browser promised.
 */
const linkProps = (navigate: (path: string) => void, path: string) => ({
  href: path,
  onClick: (event: MouseEvent<HTMLElement>) => {
    event.preventDefault()
    navigate(path)
  },
})

/**
 * The administrator's dashboard: one read of `GET /admin/dashboard` drawn as the
 * installation's totals, one card per server, and the deep links into the screens those
 * numbers describe.
 *
 * This is the **admin** dashboard only. `src/pages/account/DashboardPage.tsx` still serves
 * the client and the reseller, and the two are deliberately separate pages: the account
 * placeholder says the area is not built, while this one is a real screen behind the
 * dashboard's three-grant gate.
 *
 * ## The six states, none of them conflated
 *
 * 1. **First load** - the KPI statistics are in their `loading` state and three skeleton
 *    server cards stand where the grid will be. No zeros are drawn, because a zero is a
 *    claim and nothing has arrived to support one.
 * 2. **Refreshing** - the values stay, the toggle shows its spinner and the timestamp
 *    dims. A re-read never blanks a number the visitor is reading.
 * 3. **Read failed with nothing to show** - the page is the error alert and its Retry
 *    button; the KPI row is not drawn, because there are no numbers to draw.
 * 4. **Refresh failed over existing values** - the values stay under a slim warning that
 *    names the moment they came from, with the same Retry. The screen is stale, not empty.
 * 5. **A server did not answer** - its card draws zeros and an unavailable badge (D5); the
 *    badge, not the number, says the machine was unreachable.
 * 6. **No servers configured** - one full-width empty state under the KPIs.
 *
 * ## Refresh
 *
 * Auto refresh is on by default at 30 seconds, and it is the `ConnectionLogsPage` pattern:
 * a `useRef` holds `refetch` and a `useEffect` holds the interval, so the timer is not
 * restarted by every answer. No `refetchInterval` and no per-page query settings - the
 * shared client's defaults stay what they are.
 */
export default function DashboardPage(_props: { sectionKey: string }) {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const session = useSession()

  const area = session ? areaForRole(session.role) : ''

  const dashboardQuery = useDashboard()
  const data = dashboardQuery.data
  const kpis = data?.kpis
  const loadError = dashboardQuery.isError ? messageForError(dashboardQuery.error) : null

  /**
   * `resolvedLanguage` rather than `language`, for the reason every date formatter in this
   * tree gives: a requested language with no bundle falls back to English while `language`
   * still reports the one nobody is being shown.
   */
  const locale = i18n.resolvedLanguage ?? i18n.language

  /**
   * The separators antd needs to group a number the way this language groups it. antd's
   * own defaults are English (`,` and `.`); a Russian page that printed `1,234` would be
   * one more place the language switch visibly failed. The parts are read off the locale's
   * own formatter rather than tabulated here.
   */
  const numberFormat = new Intl.NumberFormat(locale)
  const groupSeparator =
    numberFormat.formatToParts(12345).find((part) => part.type === 'group')?.value ?? ','
  const decimalSeparator =
    numberFormat.formatToParts(1.5).find((part) => part.type === 'decimal')?.value ?? '.'

  const clockFormatter = new Intl.DateTimeFormat(locale, {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hourCycle: 'h23',
  })

  /** The wall-clock reading of a timestamp, or an empty string when there is none. */
  const clockOf = (timestamp: number): string => {
    const date = new Date(timestamp)
    return Number.isNaN(date.getTime()) ? '' : clockFormatter.format(date)
  }

  /** Whether the toggle re-reads the snapshot. On by default; pausing is one press. */
  const [autoRefresh, setAutoRefresh] = useState(true)

  /**
   * The timer that keeps the snapshot honest.
   *
   * The ref rather than the query in the dependency array is the point: `refetch` is a new
   * function whenever the answer changes, so naming it there would restart the interval
   * after every re-read.
   */
  const refreshRef = useRef<() => void>(() => {})

  useEffect(() => {
    refreshRef.current = () => {
      void dashboardQuery.refetch()
    }
  })

  useEffect(() => {
    if (!autoRefresh) return

    const timer = setInterval(() => refreshRef.current(), AUTO_REFRESH_MS)

    return () => clearInterval(timer)
  }, [autoRefresh])

  /**
   * The moment the numbers on screen came from - the query's own `dataUpdatedAt`, which is
   * when this browser received them, not the server's `generatedAt` (that one is the moment
   * the snapshot was built and can sit behind the cache).
   */
  const updatedAt = data ? clockOf(dashboardQuery.dataUpdatedAt) : ''

  /**
   * What the auto-refresh button says: its action and the interval, because the control is
   * icon-only and this string is its whole accessible name and its tooltip.
   */
  const refreshTip = `${t(autoRefresh ? 'dashboard.admin.pauseRefresh' : 'dashboard.admin.resumeRefresh')} (${t('dashboard.admin.autoRefreshHint', { seconds: AUTO_REFRESH_MS / 1000 })})`

  /**
   * A KPI label with its own glyph, for the title of a `Statistic`.
   *
   * The icon belongs to the label, not to the number, so it is the first child of the title
   * node rather than `Statistic`'s `prefix` (which sits beside the value). It is `aria-hidden`
   * because a link's accessible name must stay exactly the translated label, and it takes no
   * colour or size of its own so the surrounding typography carries it. The inline end margin
   * is a plain style rather than a `Space`, which would make the title a second flex layout
   * inside a 147 px card on a 320 px phone.
   */
  const kpiLabel = (label: string, icon: ReactNode) => (
    <>
      <span aria-hidden style={{ marginInlineEnd: 4 }}>
        {icon}
      </span>
      {label}
    </>
  )

  /**
   * A count card whose title is the link. The title is a real anchor rather than a clickable
   * `Statistic`, so a keyboard meets the way out before the number it belongs to.
   */
  const countCard = (options: { label: string; value: number; path: string; icon: ReactNode }) => (
    <Col xs={12} sm={12} md={12} lg={5} key={options.path + options.label}>
      <Card style={{ height: '100%' }}>
        <Statistic
          title={
            <Typography.Link
              {...linkProps(navigate, options.path)}
              // antd's typography base sets `word-break: break-word`, which splits a long
              // translated word mid-syllable in a 147 px card on a 320 px phone. The title is
              // 12 px here so the longest word of each locale fits, and wrapping happens
              // between words rather than inside one.
              style={{ wordBreak: 'normal', overflowWrap: 'normal', fontSize: 12 }}
            >
              {kpiLabel(options.label, options.icon)}
            </Typography.Link>
          }
          value={options.value}
          loading={dashboardQuery.isPending}
          groupSeparator={groupSeparator}
          decimalSeparator={decimalSeparator}
          styles={{ content: { fontVariantNumeric: 'tabular-nums' } }}
        />
      </Card>
    </Col>
  )

  const retry = (
    <Button
      size="small"
      loading={dashboardQuery.isFetching}
      onClick={() => void dashboardQuery.refetch()}
    >
      {t('dashboard.admin.retry')}
    </Button>
  )

  return (
    <div>
      {/* The heading row is above the state switch on purpose: the address names the page
          even while its data is missing, which is the page-title rule, kept structurally
          without an outer card. Both halves sit on one row while they fit, and `wrap` drops
          the toolbar under the heading on a phone instead of squeezing either half. */}
      <Flex
        justify="space-between"
        align="center"
        gap={12}
        wrap
        style={{ marginBottom: 16 }}
      >
        <Typography.Title level={4} style={{ margin: 0 }}>
          {t('nav.items.dashboard')}
        </Typography.Title>
        <Flex align="center" gap={8}>
          {hinted(
            refreshTip,
            <Button
              icon={autoRefresh ? <PauseCircleOutlined /> : <ReloadOutlined />}
              aria-label={refreshTip}
              loading={dashboardQuery.isFetching}
              onClick={() => setAutoRefresh((current) => !current)}
            />,
          )}
          {updatedAt && (
            <Typography.Text
              type="secondary"
              // The timestamp dims while a re-read is in flight: the values under it are
              // still the old ones, and the line is what says so before the new answer lands.
              style={{ opacity: dashboardQuery.isFetching ? 0.5 : 1 }}
            >
              {t('dashboard.admin.updatedAt', { time: updatedAt })}
            </Typography.Text>
          )}
        </Flex>
      </Flex>

      {loadError && !data ? (
        <Alert
          type="error"
          showIcon
          message={t('dashboard.admin.readFailed')}
          description={loadError}
          action={retry}
        />
      ) : (
        <>
          {/* The stale banner sits above the values it describes, because the values are
              real and must stay: hiding them would throw away the last truth the screen has. */}
          {loadError && data && (
            <Alert
              type="warning"
              showIcon
              style={{ marginBottom: 16 }}
              message={t('dashboard.admin.stale', { time: updatedAt })}
              action={retry}
            />
          )}

          <Row gutter={[16, 16]}>
            {countCard({
              label: t('dashboard.admin.kpis.activeConnections'),
              value: kpis?.activeConnections ?? 0,
              path: liveConnectionsPath(area),
              icon: <ApiOutlined />,
            })}
            {countCard({
              label: t('dashboard.admin.kpis.usersWithActiveConnections'),
              value: kpis?.usersWithActiveConnections ?? 0,
              path: liveConnectionsPath(area),
              icon: <TeamOutlined />,
            })}
            {countCard({
              label: t('dashboard.admin.kpis.streamsRunning'),
              value: kpis?.streamsRunning ?? 0,
              path: videoStreamsPath(area),
              icon: <VideoCameraOutlined />,
            })}
            {countCard({
              label: t('dashboard.admin.kpis.streamsStopped'),
              value: kpis?.streamsStopped ?? 0,
              path: videoStreamsPath(area, 'Stopped'),
              icon: <StopOutlined />,
            })}

            {/* The network card: one card with the two directions side by side. No combined
                total - inbound and outbound are separate measurements, and their sum is a
                number neither server reported. The body padding and the type sizes are one
                step down because four of the row's twenty-four columns is a narrow card:
                at 24 px the two statistics wrap under each other, and the brief asks for a
                pair. */}
            <Col xs={24} sm={24} md={24} lg={4}>
              <Card style={{ height: '100%' }} styles={{ body: { padding: 12 } }}>
                <Flex justify="space-between" gap={8} wrap>
                  <Statistic
                    title={kpiLabel(t('dashboard.admin.kpis.networkIn'), <ArrowDownOutlined />)}
                    value={kpis?.networkInMbps ?? 0}
                    precision={1}
                    suffix="Mbit/s"
                    loading={dashboardQuery.isPending}
                    groupSeparator={groupSeparator}
                    decimalSeparator={decimalSeparator}
                    styles={{
                      title: { fontSize: 12 },
                      content: { fontVariantNumeric: 'tabular-nums', fontSize: 20 },
                      suffix: { fontSize: 12 },
                    }}
                  />
                  <Statistic
                    title={kpiLabel(t('dashboard.admin.kpis.networkOut'), <ArrowUpOutlined />)}
                    value={kpis?.networkOutMbps ?? 0}
                    precision={1}
                    suffix="Mbit/s"
                    loading={dashboardQuery.isPending}
                    groupSeparator={groupSeparator}
                    decimalSeparator={decimalSeparator}
                    styles={{
                      title: { fontSize: 12 },
                      content: { fontVariantNumeric: 'tabular-nums', fontSize: 20 },
                      suffix: { fontSize: 12 },
                    }}
                  />
                </Flex>
                {!dashboardQuery.isPending && (data?.serversUnavailable ?? 0) > 0 && (
                  <Typography.Text
                    type="secondary"
                    style={{ display: 'block', marginTop: 8, fontSize: 12 }}
                  >
                    {t('dashboard.admin.partialTotal', {
                      count: data?.serversUnavailable ?? 0,
                    })}
                  </Typography.Text>
                )}
              </Card>
            </Col>
          </Row>

          <Row gutter={[16, 16]} style={{ marginTop: 16 }}>
            {dashboardQuery.isPending ? (
              Array.from({ length: SKELETON_SERVERS }, (_, index) => (
                <Col xs={24} md={12} xl={8} key={index}>
                  <Card loading style={{ height: '100%' }} />
                </Col>
              ))
            ) : data && data.servers.length === 0 ? (
              <Col xs={24}>
                <Empty description={t('dashboard.admin.empty')} />
              </Col>
            ) : (
              (data?.servers ?? []).map((server) => (
                <Col xs={24} md={12} xl={8} key={server.id}>
                  <DashboardServerCard server={server} area={area} />
                </Col>
              ))
            )}
          </Row>
        </>
      )}
    </div>
  )
}
