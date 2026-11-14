import { useEffect, useRef } from 'react'
import { Alert, App, Button, Card, Space, Spin, Tag } from 'antd'
import { useTranslation } from 'react-i18next'
import { useParams } from 'react-router-dom'
import {
  CheckOutlined,
  EyeInvisibleOutlined,
  FolderOpenOutlined,
} from '@ant-design/icons'
import { useAdminTicket } from '../../features/tickets/hooks/queries'
import {
  useMarkRead,
  useMarkUnread,
  useReply,
  useClose,
  useReopen,
} from '../../features/tickets/hooks/mutations'
import { ticketStatusLabelKey, ticketTagColor } from '../../features/tickets/ticketStatus'
import { canReply } from '../../features/tickets/ticketRules'
import { useProfile } from '../../features/profile/hooks/queries'
import TicketChat from '../../features/tickets/components/TicketChat'
import { messageForError, successTextFor } from '../../utils/http'

/**
 * How often an open conversation is re-read while the page is on screen, in milliseconds.
 *
 * This is a **deliberate deviation** from this application's no-polling stance for the other
 * read screens, and the reasoning lives in `FluxyBackend/docs/tickets.md` ("A frontend
 * deviation worth recording: the polls"): a ticket is a conversation with a second
 * participant, so an answer written from the other side should arrive without the visitor
 * reloading. The poll stops the moment the ticket is `closed` - no new message can be
 * written then - and is skipped while a write of this page's own is in flight, so a poll can
 * never land under a composer that is sending.
 */
const POLL_MS = 10_000

/**
 * One ticket in full, as the administrator reads and answers it.
 *
 * **The conversation is drawn by `TicketChat`, and this page owns everything around it**: the
 * read, the four writes, the toasts and the re-reads. That split is what lets the account's
 * own detail page draw the identical thread with the same component and a different set of
 * controls.
 *
 * **The first look marks the ticket read, once.** A ticket arriving as `unread` is the
 * administrator not having seen the newest message; opening it is the read. The guard is a
 * ref holding the id that has already been marked, so the re-read the write triggers cannot
 * loop: the second answer arrives with `read`, and even if it did not, the id is already in
 * the ref and nothing fires again. That loop is not hypothetical - a mutation whose success
 * handler refetches the very query that decides whether to fire it is the textbook case.
 *
 * The title is the ticket's own subject on every state that has one, and the generic word
 * while the subject is still being read or could not be. The card never loses its title: the
 * address names the page even when the row does not arrive.
 */
export default function TicketPage(_props: { sectionKey: string }) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { id } = useParams()

  const detailQuery = useAdminTicket(id)
  const ticket = detailQuery.data ?? null

  const profileQuery = useProfile()
  const timeZone = profileQuery.data?.timeZone ?? null

  const markRead = useMarkRead()
  const markUnread = useMarkUnread()
  const closeTicket = useClose()
  const reopenTicket = useReopen()
  const reply = useReply()

  /**
   * The id already marked read on this mount. A ref rather than state for the reason the
   * comment above gives: it is written inside an effect and must not itself schedule a
   * render that re-runs the effect.
   */
  const markedReadRef = useRef<string | null>(null)

  /**
   * The two things the one-shot effect reaches for, kept in refs.
   *
   * A mutation's `mutateAsync` and a query's `refetch` are new functions whenever their owner
   * re-renders, so naming them in the effect's dependency array would fire the effect on
   * every answer - which is exactly the loop the id guard is there to survive, not to invite.
   * The ref is written by an effect of its own, the shape the auto-refresh timers here use.
   */
  const markReadRef = useRef(markRead.mutateAsync)
  const refetchRef = useRef(detailQuery.refetch)

  useEffect(() => {
    markReadRef.current = markRead.mutateAsync
    refetchRef.current = detailQuery.refetch
  })

  const ticketId = ticket?.id
  const unread = ticket?.status === 'unread'

  useEffect(() => {
    if (!ticketId || !unread) return
    if (markedReadRef.current === ticketId) return

    markedReadRef.current = ticketId

    void markReadRef
      .current({ id: ticketId })
      // A failed mark-read is not worth a toast: the conversation is readable either way, and
      // the row simply stays unread until the next visit. It is still re-read so the tag above
      // describes what the server holds rather than what this page hoped it would.
      .catch(() => undefined)
      .finally(() => void refetchRef.current())
  }, [ticketId, unread])

  /**
   * The timer that re-reads an open conversation while the page is on screen.
   *
   * The ref rather than the query in the dependency array is the point, exactly as on the
   * lists: `refetch` is a new function whenever the answer changes, so naming it there would
   * restart the interval after every poll. The interval stops as soon as the ticket is
   * `closed` (nothing can be written then) and is torn down on unmount by the effect's own
   * cleanup.
   *
   * **A poll never races a write of this page's own.** A refetch landing between a reply's
   * POST and the answer that renders it, or under a mark-read that is still in flight, would
   * draw the thread twice at best and re-describe the status at worst - so the timer skips a
   * tick while any of this page's mutations is pending. The reply's own success handler
   * refetches immediately, so the message still arrives without waiting for the next tick.
   */
  const pollRef = useRef<() => void>(() => {})

  useEffect(() => {
    pollRef.current = () => {
      if (
        reply.isPending ||
        markRead.isPending ||
        markUnread.isPending ||
        closeTicket.isPending ||
        reopenTicket.isPending
      ) {
        return
      }

      void detailQuery.refetch()
    }
  })

  const open = ticket !== null && canReply(ticket.status)

  useEffect(() => {
    if (!open) return

    const timer = setInterval(() => pollRef.current(), POLL_MS)

    return () => clearInterval(timer)
  }, [open])

  /** Runs one head action, reports it and re-reads the conversation. */
  const run = async (call: () => Promise<{ code: string; message: string }>) => {
    try {
      const result = await call()

      message.success(successTextFor(result))
      void detailQuery.refetch()
    } catch (error) {
      message.error(messageForError(error))
    }
  }

  /**
   * Answers the ticket: the page reports the outcome and re-reads the conversation.
   *
   * It **rejects** on a refusal, which is what tells the chat component to keep the draft -
   * the toast is already on screen, and the words somebody just typed are not something to
   * throw away because the server said no.
   */
  const send = async (text: string) => {
    if (!ticketId) return

    try {
      const result = await reply.mutateAsync({ id: ticketId, values: { message: text } })

      message.success(successTextFor(result))
      void detailQuery.refetch()
    } catch (error) {
      message.error(messageForError(error))
      throw error
    }
  }

  const title = ticket?.subject ?? t('tickets.detailTitle')
  const closed = ticket?.status === 'closed'
  const busy =
    markUnread.isPending || closeTicket.isPending || reopenTicket.isPending

  return (
    <Card
      className="page-card"
      title={title}
      styles={{ title: { whiteSpace: 'nowrap' } }}
      extra={
        ticket && (
          <Space wrap>
            <Tag color={ticketTagColor(ticket.status)} style={{ marginInlineEnd: 0 }}>
              {t(ticketStatusLabelKey(ticket.status))}
            </Tag>
            {closed ? (
              <Button
                icon={<FolderOpenOutlined />}
                loading={reopenTicket.isPending}
                disabled={busy}
                onClick={() => void run(() => reopenTicket.mutateAsync({ id: ticket.id }))}
              >
                {t('tickets.actions.reopen')}
              </Button>
            ) : (
              <Button
                icon={<CheckOutlined />}
                loading={closeTicket.isPending}
                disabled={busy}
                onClick={() => void run(() => closeTicket.mutateAsync({ id: ticket.id }))}
              >
                {t('tickets.actions.close')}
              </Button>
            )}
            <Button
              icon={<EyeInvisibleOutlined />}
              loading={markUnread.isPending}
              // Already unread means there is nothing to mark; the button still shows, so
              // the head does not change shape between two rows of the same list.
              disabled={busy || ticket.status === 'unread'}
              onClick={() => void run(() => markUnread.mutateAsync({ id: ticket.id }))}
            >
              {t('tickets.actions.markUnread')}
            </Button>
          </Space>
        )
      }
    >
      {detailQuery.isError ? (
        <Alert
          type="error"
          showIcon
          title={messageForError(detailQuery.error)}
          action={
            <Button
              size="small"
              loading={detailQuery.isFetching}
              onClick={() => void detailQuery.refetch()}
            >
              {t('actions.retry')}
            </Button>
          }
        />
      ) : !ticket ? (
        <div style={{ textAlign: 'center', padding: 48 }}>
          <Spin />
        </div>
      ) : (
        <TicketChat
          replies={ticket.replies}
          status={ticket.status}
          viewerIsAdmin
          timeZone={timeZone}
          sending={reply.isPending}
          onSend={send}
          onReopen={() => void run(() => reopenTicket.mutateAsync({ id: ticket.id }))}
          reopening={reopenTicket.isPending}
        />
      )}
    </Card>
  )
}
