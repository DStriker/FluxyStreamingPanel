import { useEffect, useRef } from 'react'
import { Alert, App, Button, Card, Spin, Tag } from 'antd'
import { useTranslation } from 'react-i18next'
import { useParams } from 'react-router-dom'
import { useTicket } from '../../features/tickets/hooks/queries'
import { useTicketMarkRead, useTicketReply } from '../../features/tickets/hooks/mutations'
import { ticketStatusLabelKey, ticketTagColor } from '../../features/tickets/ticketStatus'
import { canReply } from '../../features/tickets/ticketRules'
import { useProfile } from '../../features/profile/hooks/queries'
import TicketChat from '../../features/tickets/components/TicketChat'
import { messageForError, successTextFor } from '../../utils/http'

/**
 * How often an open conversation is re-read while the page is on screen, in milliseconds.
 *
 * This is a **deliberate deviation** from this application's no-polling stance, recorded in
 * `FluxyBackend/docs/tickets.md` ("A frontend deviation worth recording: the polls"): the
 * other participant answers from a screen this visitor is not looking at, so the thread a
 * client is reading has to show the answer without a manual reload. The poll stops as soon
 * as the ticket is `closed` - a closed conversation accepts no message - and is skipped while
 * one of this page's own writes is in flight.
 */
const POLL_MS = 10_000

/**
 * One of the account's own tickets, in full - the same page for a client and for a reseller.
 *
 * It is the administrator's detail page with the support half removed: no close, no reopen,
 * no delete, and the closed banner carries no action. What remains is the one thing a visitor
 * can do - answer while the conversation is open - and the one thing the page does on its
 * own: the first look marks an unread ticket read.
 *
 * The mark-read is guarded by a ref holding the id it has already marked, for the reason the
 * admin page states at length: a write whose success re-reads the query that decides whether
 * to write again is a loop waiting for a server that has not caught up yet.
 */
export default function TicketPage(_props: { sectionKey: string }) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { id } = useParams()

  const detailQuery = useTicket(id)
  const ticket = detailQuery.data ?? null

  const profileQuery = useProfile()
  const timeZone = profileQuery.data?.timeZone ?? null

  const markRead = useTicketMarkRead()
  const reply = useTicketReply()

  const markedReadRef = useRef<string | null>(null)

  // See the admin page: the effect must not name a mutation or a refetch in its dependencies,
  // because both are new functions on every answer and the effect would then re-run forever.
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
      // A failed mark-read is not worth a toast: the thread reads the same either way and the
      // list will simply keep the dot until it is opened again.
      .catch(() => undefined)
      .finally(() => void refetchRef.current())
  }, [ticketId, unread])

  /**
   * The timer that re-reads an open conversation while the page is on screen.
   *
   * The ref rather than the query in the dependency array is the point - see the admin page:
   * `refetch` changes identity on every answer, and naming it would restart the interval.
   *
   * The poll exists so a support answer arrives without the visitor reloading, which is the
   * one place this application chooses to poll (the reasoning is in
   * `FluxyBackend/docs/tickets.md`). It stops the moment the ticket is `closed`, is torn down
   * on unmount, and skips a tick while the reply or the one-shot mark-read is in flight - a
   * re-read landing mid-write would redraw a thread the write is about to answer.
   */
  const pollRef = useRef<() => void>(() => {})

  useEffect(() => {
    pollRef.current = () => {
      if (markRead.isPending || reply.isPending) return

      void detailQuery.refetch()
    }
  })

  const open = ticket !== null && canReply(ticket.status)

  useEffect(() => {
    if (!open) return

    const timer = setInterval(() => pollRef.current(), POLL_MS)

    return () => clearInterval(timer)
  }, [open])

  /**
   * Answers the ticket. The same two rules as the admin page: report and re-read here, and
   * rethrow so the chat keeps the draft when the server refuses.
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

  return (
    <Card
      className="page-card"
      title={ticket?.subject ?? t('tickets.detailTitle')}
      styles={{ title: { whiteSpace: 'nowrap' } }}
      // The one control this side has: the status tag, read-only. A client closes nothing, so
      // there is no button beside it - the closed banner above the thread says the rest.
      extra={
        ticket && (
          <Tag color={ticketTagColor(ticket.status)} style={{ marginInlineEnd: 0 }}>
            {t(ticketStatusLabelKey(ticket.status))}
          </Tag>
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
          viewerIsAdmin={false}
          timeZone={timeZone}
          sending={reply.isPending}
          onSend={send}
        />
      )}
    </Card>
  )
}
