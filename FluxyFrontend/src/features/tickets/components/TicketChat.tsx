import { useEffect, useRef, useState, useSyncExternalStore } from 'react'
import type { KeyboardEvent as ReactKeyboardEvent } from 'react'
import { Alert, Button, Input, theme, Typography } from 'antd'
import { CheckOutlined, SendOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import type { TicketReply, TicketStatus } from '../types'
import { canReply } from '../ticketRules'
import { formatChatTime } from '../../../utils/dateFormat'
import { useIsCompact } from '../../../hooks/useIsCompact'

/**
 * One ticket's conversation, drawn the same way for both sides.
 *
 * **The sides are fixed, not mirrored.** A client's message is always on the left and an
 * administrator's always on the right, whichever of the two is reading: a conversation whose
 * sides swapped with the viewer would make a screenshot of it mean two different things, and
 * a person comparing what they sent with what they were told would have to re-learn the
 * layout on every screen. What *does* follow the viewer is the read mark: it is drawn under
 * the messages this viewer wrote, because the receipt belongs to the author.
 *
 * **It fetches nothing.** The page owns both queries and every write and hands this component
 * the conversation, the status, the viewer's time zone and callbacks; the component owns only
 * what is genuinely its own - the draft in the composer, the scroll position, and the rule
 * about when a new message may move it.
 *
 * **The composer obeys the breakpoint.** On a desktop, Enter sends and Shift+Enter starts a
 * new line, which is what a chat field is expected to do; on the shell's compact layout,
 * Enter must insert a line, because a phone keyboard offers no Shift and the send button is
 * the only way out of the field.
 */

/** How close to the bottom still counts as "reading the newest message", in pixels. */
const NEAR_BOTTOM_PX = 48

/**
 * The width at which a bubble is allowed a little more room.
 *
 * 768 is the shell's own phone boundary - the point where the area title and the username
 * disappear from the header (`src/index.css`) - and it is stated here for the same reason
 * `useIsCompact` states 991: the bubble's `85%` and the rest of the compact layout have to
 * change at one width, or a phone gets a bubble narrower than the column it sits in while
 * everything around it has already gone compact.
 *
 * The shape is `useIsCompact`'s, module-level and subscribed once, with `false` as the server
 * snapshot so `probe:render` still measures the desktop bubble.
 */
const WIDE_BUBBLE_QUERY = '(max-width: 768px)'

let bubbleMedia: MediaQueryList | null = null
const bubbleListeners = new Set<() => void>()

const notifyBubbleListeners = (): void => bubbleListeners.forEach((listener) => listener())

const ensureBubbleMedia = (): MediaQueryList | null => {
  if (bubbleMedia !== null) return bubbleMedia
  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return null
  bubbleMedia = window.matchMedia(WIDE_BUBBLE_QUERY)
  return bubbleMedia
}

const subscribeBubble = (listener: () => void): (() => void) => {
  const target = ensureBubbleMedia()
  bubbleListeners.add(listener)
  target?.addEventListener('change', notifyBubbleListeners)
  return () => {
    bubbleListeners.delete(listener)
    if (bubbleListeners.size === 0) target?.removeEventListener('change', notifyBubbleListeners)
  }
}

const bubbleSnapshot = (): boolean => ensureBubbleMedia()?.matches ?? false

/** Whether the viewport is narrow enough for the wider bubble. */
const useWideBubble = (): boolean =>
  useSyncExternalStore(subscribeBubble, bubbleSnapshot, bubbleSnapshot)

export default function TicketChat({
  replies,
  status,
  viewerIsAdmin,
  timeZone,
  sending,
  onSend,
  onReopen,
  reopening = false,
}: {
  /** The conversation, oldest first, exactly as the detail endpoint answered. */
  replies: TicketReply[]
  /** The ticket's state: `closed` removes the composer and adds the banner. */
  status: TicketStatus
  /** Whether the reader is an administrator, which decides which messages are "own". */
  viewerIsAdmin: boolean
  /** The viewer's IANA zone, read from `useProfile` by the page. */
  timeZone: string | null
  /** Whether the reply write is in flight - the send button's own spinner. */
  sending: boolean
  /** Sends one message; the page owns the mutation, the toast and the re-read. */
  onSend: (message: string) => Promise<void>
  /** Reopens a closed ticket. Present on the administrator's screen only. */
  onReopen?: () => void
  /** Whether the reopen write is in flight. */
  reopening?: boolean
}) {
  const { t, i18n } = useTranslation()
  const { token } = theme.useToken()
  const narrow = useWideBubble()
  const compact = useIsCompact()
  const locale = i18n.resolvedLanguage ?? i18n.language

  const [draft, setDraft] = useState('')
  const listRef = useRef<HTMLDivElement>(null)

  /**
   * Whether the reader was at the bottom when they last scrolled.
   *
   * The rule the whole scroll behaviour rests on: a message arriving while somebody is
   * reading back through the thread must not throw them to the end, because the one thing
   * they were doing was reading. The ref rather than state is deliberate - it is read inside
   * an effect and written inside a scroll handler, and a state update for every pixel of a
   * scroll would repaint the whole conversation.
   */
  const atBottomRef = useRef(true)

  const onScroll = () => {
    const element = listRef.current
    if (!element) return

    const distance = element.scrollHeight - element.scrollTop - element.clientHeight
    atBottomRef.current = distance <= NEAR_BOTTOM_PX
  }

  // A message that was just appended is followed only by a reader who was already at the
  // end. The first answer to the detail read scrolls too - `atBottomRef` starts true - which
  // is what makes an opened ticket show its newest message rather than its first.
  useEffect(() => {
    const element = listRef.current
    if (!element) return
    if (!atBottomRef.current) return

    element.scrollTop = element.scrollHeight
  }, [replies.length])

  /**
   * Sends one message.
   *
   * The draft is cleared **after** the page's callback resolves, not before: a refused or
   * unreachable send must not eat the text somebody just wrote. The page reports the failure
   * and rejects, and this `catch` is what keeps that rejection from becoming unhandled - the
   * sentence is already on screen as a toast.
   */
  const send = () => {
    const message = draft.trim()
    if (!message || sending) return

    void onSend(message)
      .then(() => setDraft(''))
      .catch(() => undefined)
  }

  const onKeyDown = (event: ReactKeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key !== 'Enter' || event.shiftKey) return
    // A phone has no Shift key, so Enter is the only newline it can produce.
    if (compact) return

    event.preventDefault()
    send()
  }

  /** Whether this reply was written by the person reading the screen. */
  const isOwn = (reply: TicketReply): boolean =>
    viewerIsAdmin ? reply.isAdminReply : !reply.isAdminReply

  const bubbleMaxWidth = narrow ? '85%' : '75%'
  const open = canReply(status)

  return (
    <div>
      {!open && (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: token.marginSM }}
          // `title` rather than the deprecated `message`: antd 6 renamed the prop, and a new
          // banner that warned on every render would be noise the older components could not
          // teach us to ignore.
          title={t('tickets.closedNotice')}
          action={
            viewerIsAdmin && onReopen ? (
              <Button size="small" loading={reopening} onClick={onReopen}>
                {t('tickets.actions.reopen')}
              </Button>
            ) : undefined
          }
        />
      )}

      <div
        ref={listRef}
        onScroll={onScroll}
        style={{
          display: 'flex',
          flexDirection: 'column',
          gap: token.marginSM,
          overflowY: 'auto',
          maxHeight: '55vh',
          paddingInlineEnd: token.paddingXS,
        }}
      >
        {replies.length === 0 ? (
          <Typography.Text type="secondary" style={{ display: 'block', padding: token.padding }}>
            {t('tickets.noMessages')}
          </Typography.Text>
        ) : (
          replies.map((reply) => {
            const own = isOwn(reply)
            const admin = reply.isAdminReply

            return (
              <div
                key={reply.id}
                style={{
                  display: 'flex',
                  flexDirection: 'column',
                  // The side is the *author's*, fixed for both viewers: a client's message is
                  // always on the left and an administrator's always on the right.
                  alignItems: admin ? 'flex-end' : 'flex-start',
                }}
              >
                <div
                  style={{
                    maxWidth: bubbleMaxWidth,
                    wordBreak: 'break-word',
                    whiteSpace: 'pre-wrap',
                    borderRadius: token.borderRadiusLG,
                    padding: `${token.paddingSM}px ${token.padding}px`,
                    background: admin ? token.colorPrimaryBg : token.colorFillSecondary,
                    color: token.colorText,
                  }}
                >
                  {reply.message}
                </div>

                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: token.marginXXS,
                    marginTop: token.marginXXS,
                  }}
                >
                  <Typography.Text
                    style={{ color: token.colorTextTertiary, fontSize: token.fontSizeSM }}
                  >
                    {formatChatTime(reply.createdAt, locale, timeZone)}
                  </Typography.Text>
                  {/* The receipt belongs to the author: it says whether the other side has
                      read *this* message, and a mark under somebody else's message would be
                      describing a read that person is not the subject of. */}
                  {own && (
                    <CheckOutlined
                      aria-label={t(reply.readAt ? 'tickets.readMarkRead' : 'tickets.readMarkUnread')}
                      style={{
                        fontSize: token.fontSizeSM,
                        color: reply.readAt ? token.colorPrimary : token.colorTextQuaternary,
                      }}
                    />
                  )}
                </div>
              </div>
            )
          })
        )}
      </div>

      {open && (
        <div
          style={{
            display: 'flex',
            alignItems: 'flex-end',
            gap: token.marginSM,
            marginTop: token.marginSM,
          }}
        >
          <Input.TextArea
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            onKeyDown={onKeyDown}
            autoSize={{ minRows: 1, maxRows: 4 }}
            aria-label={t('tickets.messageLabel')}
            style={{ flex: 1 }}
          />
          <Button
            type="primary"
            icon={<SendOutlined />}
            disabled={draft.trim() === ''}
            loading={sending}
            onClick={send}
          >
            {t('tickets.send')}
          </Button>
        </div>
      )}
    </div>
  )
}
