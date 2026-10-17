import { App } from 'antd'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { messageForError, successTextFor } from '../lib/http'
import type { MessageResponse } from '../types'

/** The question a delete asks before it runs, in the page's own words. */
export interface RowDeleteQuestion {
  /** The value the dialog names - a username where the row's own display name is not it. */
  name: string
  /** The three locale keys of the question, this page's own. */
  titleKey: string
  bodyKey: string
  okKey: string
  /** The write itself, from this page's own mutation hook. */
  run: () => Promise<MessageResponse>
}

/**
 * The actions the administrator tables perform on a row - block, unblock, move to another
 * group, start, stop, delete - and the state that row's own controls read while one runs.
 *
 * What it owns is the part that does not differ between those pages: one id in flight at a
 * time (the row's button spins and its neighbours stay clickable), the success text resolved
 * from the answer's code, a re-read afterwards, because a transition changes what the server
 * considers the row to be and the table draws what it is told, and - for the one action that
 * cannot be undone - the question that is asked first.
 *
 * The write itself is **not** this hook's. The token is minted by the mutation hook in
 * `mutations.ts` the caller built `run` from, and what travels through here is only the
 * reporting and the re-read that every one of these actions shares.
 *
 * `refetch` and, where there is a pager, `rows`/`page`/`setPage` are the page's own facts,
 * handed over at construction: they are what deletion's step back off an emptied page needs,
 * and a page without a pager simply passes `refetch` alone.
 */
export function useRowAction<Row extends { id: string }>({
  refetch,
  rows,
  page,
  setPage,
}: {
  refetch: () => void
  /** The rows currently drawn, for the delete flow's step back off an emptied page. */
  rows?: Row[] | undefined
  page?: number
  setPage?: (page: number) => void
}) {
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const [busyId, setBusyId] = useState<string | null>(null)

  /**
   * The last row of a later page: with it gone, that page no longer exists and the server
   * would answer it empty - a blank table under a pager still offering the page the visitor
   * is standing on. Back a page instead, which re-runs the query anyway, so the list and the
   * pager agree again without a second call.
   */
  const afterWrite = (result: MessageResponse, stepBack: boolean) => {
    message.success(successTextFor(result))

    if (stepBack && rows?.length === 1 && page !== undefined && page > 1) setPage?.(page - 1)
    else void refetch()
  }

  /**
   * Runs one row action and reports it. `run` is a closure over the row because the
   * endpoints disagree about what travels beside the id - a group's block is a `patch`, a
   * stream's start is nothing at all - and the caller is the only one that knows which.
   */
  const runAction = async (run: () => Promise<MessageResponse>, row: Row): Promise<void> => {
    setBusyId(row.id)

    try {
      afterWrite(await run(), false)
    } catch (error) {
      message.error(messageForError(error))
    } finally {
      setBusyId(null)
    }
  }

  /** Asks the page's delete question, then runs `question.run` and re-reads. */
  const confirmDelete = (row: Row, question: RowDeleteQuestion): void => {
    modal.confirm({
      title: t(question.titleKey),
      content: t(question.bodyKey, { name: question.name }),
      okText: t(question.okKey),
      okButtonProps: { danger: true },
      cancelText: t('actions.cancel'),
      onOk: async () => {
        setBusyId(row.id)

        try {
          afterWrite(await question.run(), true)
        } catch (error) {
          message.error(messageForError(error))
        } finally {
          setBusyId(null)
        }
      },
    })
  }

  return { busyId, runAction, confirmDelete }
}
