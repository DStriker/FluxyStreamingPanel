import {
  Alert,
  App,
  Button,
  Card,
  Col,
  Descriptions,
  Row,
  Space,
  Spin,
  Tag,
  Tooltip,
} from 'antd'
import { useTranslation } from 'react-i18next'
import { useQueryClient } from '@tanstack/react-query'
import { messageForError, textForCode } from '../../utils/http'
import { countryName } from '../../utils/countryName'
import { describeUserAgent } from '../../utils/userAgent'
import { useProfile } from '../../features/profile/hooks/queries'
import {
  useRevokeActiveSession,
  useRevokeOtherActiveSessions,
} from '../../features/sessions/hooks/mutations'
import { useActiveSessions } from '../../features/sessions/hooks/queries'
import type { ActiveSession, ActiveSessionList } from '../../features/sessions/types'

/**
 * Dates, by locale and zone, remembered.
 *
 * The same cache as the visit history keeps, for the same reason: building an
 * `Intl.DateTimeFormat` is the expensive part, and a card that repaints after one revoke
 * would otherwise rebuild one per timestamp. `null` is a remembered refusal as much as a
 * formatter is a remembered answer - a zone this ICU build does not carry would otherwise be
 * rediscovered by every card of every repaint.
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
    // A zone stored by an older build, or a zone this ICU build does not carry, makes
    // `Intl.DateTimeFormat` throw before it returns anything - and an exception thrown while
    // rendering a card would take the whole page down over one timestamp.
    formatter = null
  }

  dateFormatters.set(key, formatter)
  return formatter
}

/**
 * The account's active sessions: one card per sign-in that is still running, each with the
 * way to end it.
 *
 * The opposite question to the visit history next to it in the menu, and the deliberate
 * opposite in every shape: that page lists tokens so a rotation is visible, this one lists
 * sessions so one can be ended - one row per sign-in, described by the freshest live token
 * of its chain. No table and no paging, unlike the history: an account holds a handful of
 * sessions at most, so a pager would have nothing to turn, and a grid of cards answers the
 * only question this page has - *what is still signed in, and how do I stop it* - by keeping
 * the way to stop it inside the thing it describes. The history's own `total` is what forced
 * its table to stay a table; there is no `total` here.
 *
 * Two rules the cards follow from the server's refusals rather than duplicating them: the
 * current session's button is disabled (the server answers `cannot_revoke_current` for that
 * id, so the disabled button is the UI telling the truth the API would tell anyway), and
 * every other card's button calls one id through `revokeActiveSession`. The "end all but
 * this one" button at the top revokes by exclusion - the endpoint spares whatever the token
 * says the caller is using, which is why no card can name which one to keep.
 *
 * Dates follow the account's own time zone and the page's language for exactly the reasons
 * the visit history states: the profile page promises "Dates and times are shown in this
 * zone", and this application has a language switcher, so a date in one language beside a
 * country name in another would be that switcher visibly failing on a card.
 *
 * Every render lands before the first answer arrives, so the loading state is a spinner over
 * an empty grid rather than a page pretending to have no sessions.
 */
export default function ActiveSessionsPage({ sectionKey }: { sectionKey: string }) {
  const { t, i18n } = useTranslation()
  const { message, modal } = App.useApp()
  const queryClient = useQueryClient()

  /**
   * The session list, fetched through the `['sessions', 'active']` query.
   *
   * Two consequences of that rather than the old `loading`/`loadError`/`cancelled` effect:
   * the zone comes from the shared `['profile']` query instead of a `getProfile()` effect of
   * this page's own, and the retry button and the bulk revoke re-read through `refetch()`
   * rather than by bumping an `attempt` counter. A single revoke still takes its card out of
   * the list without a re-read - `setQueryData` rather than `setList` - because the answer
   * the endpoint just gave is that this session is gone, and each session is exactly one
   * card: nothing else on any page can hold a second card for it (unlike the token history,
   * where a revoke changes rows the screen is not showing).
   */
  const listQuery = useActiveSessions()
  const list = listQuery.data ?? null
  const loadError = listQuery.isError ? messageForError(listQuery.error) : null
  const profileQuery = useProfile()
  const timeZone = profileQuery.data?.timeZone ?? null

  /**
   * The two revokes this page makes, from `mutations.ts`. Each mints its antiforgery token
   * at call time, never remembered: the server binds a token to the identity that was
   * current when it was minted, and one from before a refresh is refused as `csrf_invalid`.
   *
   * The in-flight state is the mutations' own: the card whose button spins is the one whose
   * id the single-session revoke is carrying - one id rather than a boolean, so the other
   * cards stay clickable while one revoke runs - and the toolbar's spinner is the exclusion
   * revoke's `isPending`.
   */
  const revokeSession = useRevokeActiveSession()
  const revokeOthers = useRevokeOtherActiveSessions()
  const revoking = revokeSession.isPending ? (revokeSession.variables?.sessionId ?? null) : null
  const revokingAll = revokeOthers.isPending

  /**
   * `resolvedLanguage` rather than `language`: the two differ whenever the requested language
   * has no bundle, and then every string falls back to English while `language` still reports
   * the language nobody is being shown. A country name in one language beside a date in
   * another would be exactly that disagreement, made visible on one card.
   */
  const locale = i18n.resolvedLanguage ?? i18n.language

  // In a static render and on the way in: pending before the first answer. During a
  // re-read `isFetching` is what keeps the bulk button and the retry button honest.
  const loading = listQuery.isPending || listQuery.isFetching

  /**
   * A date as this account's own zone and the page's own language show it, falling back to
   * the browser's zone when the stored one is absent or is one this ICU build refuses to
   * format in.
   */
  const formatMoment = (value: string): string => {
    const date = new Date(value)

    if (Number.isNaN(date.getTime())) return '—'

    const formatter =
      dateFormatterFor(locale, timeZone ?? undefined) ?? dateFormatterFor(locale, undefined)

    return formatter ? formatter.format(date) : '—'
  }

  /** What the card's title says the client is, or the raw string when nothing can be read. */
  const clientLabel = (userAgent: string | null): string => {
    if (!userAgent) return t('activeSessions.agentUnknown')
    return describeUserAgent(userAgent) ?? userAgent
  }

  /** The network line: the organization, the autonomous system, or a sentence saying neither. */
  const networkLabel = (session: ActiveSession): string => {
    const name = session.organization
    const asn = session.autonomousSystemNumber

    if (name && asn !== null) return `${name} (AS${asn})`
    if (name) return name
    if (asn !== null) return `AS${asn}`
    return t('activeSessions.networkUnknown')
  }

  /**
   * Ends one session, and takes its card out of the list on success.
   *
   * The card is removed rather than the whole list re-read: the answer the endpoint just
   * gave is that this session is gone, and a session is exactly one card here, so no other
   * visible thing is outdated by knowing it. A failure leaves the card where it is - the
   * refusal is the truth about that session and hiding it would leave the visitor wondering
   * whether it was ended after all.
   */
  const endSession = async (session: ActiveSession) => {
    try {
      const result = await revokeSession.mutateAsync({ sessionId: session.id })

      message.success(textForCode(result.code) ?? t('activeSessions.revoked'))

      queryClient.setQueryData<ActiveSessionList>(['sessions', 'active'], (previous) =>
        previous
          ? { items: previous.items.filter((item) => item.id !== session.id) }
          : previous,
      )
    } catch (error) {
      message.error(messageForError(error))
    }
  }

  /**
   * Asks before ending every other session, because the act cannot be undone from this page
   * - those devices have to sign in again - and because one click of a misread button would
   * otherwise end them all. The count in the dialog is the count of cards on screen: what the
   * visitor is agreeing to is exactly what the list in front of them shows, and the server
   * answers with its own number afterwards for the two to be compared.
   *
   * No password step and no extra throttle: the session itself is the proof of ownership,
   * which is the same rule sign-out follows, and a window here would make the second alarm of
   * the day a refusal.
   */
  const confirmEndOthers = () => {
    const others = list ? list.items.filter((item) => !item.isCurrent).length : 0
    if (others === 0) return

    modal.confirm({
      title: t('activeSessions.endAllConfirmTitle'),
      content: t('activeSessions.endAllConfirmBody'),
      okText: t('activeSessions.endAllConfirmOk'),
      okButtonProps: { danger: true },
      cancelText: t('actions.cancel'),
      onOk: async () => {
        try {
          const result = await revokeOthers.mutateAsync()

          message.success(
            result.revokedCount > 0
              ? t('activeSessions.revokedOthers', { count: result.revokedCount })
              : t('activeSessions.revokedOthersNone'),
          )

          // Re-read rather than filter: the endpoint reports a number, and rebuilding the
          // cards from that number would mean deciding here which card is which - while the
          // server already knows, and the current one is the only card that must remain.
          void listQuery.refetch()
        } catch (error) {
          message.error(messageForError(error))
        }
      },
    })
  }

  const others = list ? list.items.filter((item) => !item.isCurrent).length : 0

  return (
    // One card frames the whole subject - the heading, the way to end everything else, and
    // the session cards themselves - for the reason the visit history's frame gives: a heading
    // floating beside a box of content would be a second box around a single subject, and this
    // is a section of the account area like any other. The bulk button lives in `extra` next
    // to the title, where the history keeps its search and its width reset, so the page's one
    // action sits where every other section puts its controls.
    <Card
      title={t(sectionKey)}
      extra={
        <Button
          danger
          disabled={others === 0 || loading || loadError !== null}
          loading={revokingAll}
          onClick={confirmEndOthers}
        >
          {t('activeSessions.endAll')}
        </Button>
      }
    >
      {loadError ? (
        <Alert
          type="error"
          showIcon
          message={loadError}
          action={
            <Button size="small" onClick={() => listQuery.refetch()} loading={loading}>
              {t('actions.retry')}
            </Button>
          }
        />
      ) : loading && !list ? (
        <Spin />
      ) : (
        <Row gutter={[16, 16]}>
          {(list?.items ?? []).map((session) => (
            <Col key={session.id} xs={24} md={12} xl={8}>
              <Card
                style={{ height: '100%' }}
                title={
                  <Space wrap>
                    <span>{clientLabel(session.userAgent)}</span>
                    {session.isCurrent && <Tag color="blue">{t('activeSessions.current')}</Tag>}
                  </Space>
                }
                actions={[
                  <Tooltip
                    key="end"
                    // Named with the way out rather than only the refusal: "cannot end this
                    // one" without "sign out instead" would be a dead end on a page whose
                    // whole purpose is ending sessions.
                    title={session.isCurrent ? t('activeSessions.endCurrentHint') : undefined}
                  >
                    <Button
                      danger
                      type="text"
                      disabled={session.isCurrent}
                      loading={revoking === session.id}
                      onClick={() => void endSession(session)}
                    >
                      {t('activeSessions.end')}
                    </Button>
                  </Tooltip>,
                ]}
              >
                <Descriptions column={1} size="small">
                  <Descriptions.Item label={t('activeSessions.lastSeen')}>
                    {formatMoment(session.lastSeenAt)}
                  </Descriptions.Item>
                  <Descriptions.Item label={t('sessions.columns.ip')}>
                    {session.ip ?? t('activeSessions.ipUnknown')}
                  </Descriptions.Item>
                  <Descriptions.Item label={t('sessions.columns.country')}>
                    {/* A name rather than the code GeoIP produces, and a dash for a null: a
                        loopback address or a missing `.mmdb` is unknown, not broken. */}
                    {session.countryCode
                      ? countryName(session.countryCode, locale) ?? session.countryCode
                      : '—'}
                  </Descriptions.Item>
                  <Descriptions.Item label={t('sessions.columns.network')}>
                    {networkLabel(session)}
                  </Descriptions.Item>
                  <Descriptions.Item label={t('sessions.columns.agent')}>
                    {/* The raw string rather than the summary: a card has the room the table
                        cell did not, and "is this exactly my browser" deserves the exact
                        value instead of a one-line guess. */}
                    {session.userAgent ?? '—'}
                  </Descriptions.Item>
                </Descriptions>
              </Card>
            </Col>
          ))}

          {list && list.items.length === 0 && (
            <Col xs={24}>
              <Alert type="info" showIcon message={t('activeSessions.empty')} />
            </Col>
          )}
        </Row>
      )}
    </Card>
  )
}
