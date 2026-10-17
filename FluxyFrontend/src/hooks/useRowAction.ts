import { App } from 'antd'
import { useState } from 'react'
import { getCsrfToken } from '../lib/csrf'
import { messageForError, successTextFor } from '../lib/http'
import type { MessageResponse } from '../types'

/** What a row action is called with: the row's identifier and a fresh antiforgery token. */
export interface RowActionArgs {
  id: string
  csrfToken: string | null
}

/**
 * The one action the three tables under an administrator perform on a row - block, unblock,
 * move to a group - and the state that row's own controls read while it runs.
 *
 * What it owns is the part that does not differ between those pages: one id in flight at a
 * time (the row's button spins and its neighbours stay clickable), the token minted at click
 * time rather than read from a cookie (see `getCsrfToken` - a token minted before a session
 * changed is refused, which is exactly what a cached one is), the success text resolved from
 * the answer's code, and a re-read afterwards, because a transition changes what the server
 * considers the row to be and the table draws what it is told.
 *
 * `refetch` is a plain callback rather than the query object: the hook has no other business
 * with the read, and every call site already has the refetch to hand.
 */
export function useRowAction<Row extends { id: string }>(refetch: () => void) {
  const [busyId, setBusyId] = useState<string | null>(null)
  const { message } = App.useApp()

  const runAction = async (
    action: (args: RowActionArgs) => Promise<MessageResponse>,
    row: Row,
  ): Promise<void> => {
    setBusyId(row.id)

    try {
      const result = await action({ id: row.id, csrfToken: await getCsrfToken() })
      message.success(successTextFor(result))
      void refetch()
    } catch (error) {
      message.error(messageForError(error))
    } finally {
      setBusyId(null)
    }
  }

  /**
   * `setBusyId` is handed out for a page's own delete flow, which marks the same row busy
   * while its confirmation runs - a second busy state would be a second spinner on one row.
   */
  return { busyId, setBusyId, runAction }
}
