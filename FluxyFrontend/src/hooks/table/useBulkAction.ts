import { App } from 'antd'
import { useEffect, useState } from 'react'
import type { Key } from 'react'
import { useTranslation } from 'react-i18next'
import { bulkRefusalList } from '../../utils/bulkRefusalList'
import { messageForError, successTextFor } from '../../utils/http'
import type { BulkOperationResponse } from '../../types'

/**
 * The selection a bulk table acts on, and the one run that acts on it.
 *
 * What it owns is everything the two tables with checkboxes would otherwise state twice: the
 * ids the **current page** has marked, pruned on every answer to the ids the page still
 * holds; one run in flight at a time; the reading of the answer - a toast when every row
 * landed and a dialog naming the refusals when at least one did not - and the run itself,
 * which re-reads the list afterwards rather than patching it and clears the selection because
 * the run is over, not because the rows are gone (only deletion guarantees that).
 *
 * The scope is the page, and that is the whole decision: an id list is what the endpoint
 * takes, the server bounds it at one page's worth, and the alternative - "everything that
 * matches this filter" - would mean acting on rows nobody has looked at, some of which the
 * operator may not even be allowed to change. The header checkbox selects what is drawn, and
 * moving to another page drops the ids that are no longer there.
 *
 * The page keeps what differs: which endpoint an action names, the menu that chooses it, the
 * dialogs that ask first, and the permission question ("may this operator change anything
 * here"), which is a fact about the page rather than about the selection. `run` is the
 * endpoint call with the ids and a fresh antiforgery token already in it, so the hook never
 * learns what an action *is*; `onSettled` is for a dialog the run came out of.
 */
export function useBulkAction<Row extends { id: string }>({
  rows,
  refetch,
  partialTitleKey,
}: {
  /** The page's current rows; the selection never outlives them. */
  rows: Row[] | undefined
  /** How the table re-reads after a run, which is the page's own query. */
  refetch: () => void
  /** This table's own `…bulk.partialTitle`, because the sentence is translated per page. */
  partialTitleKey: string
}) {
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const [selectedIds, setSelectedIds] = useState<Key[]>([])
  const [bulkBusy, setBulkBusy] = useState(false)

  useEffect(() => {
    // The selection is this page's, so it is pruned to the ids the page still holds.
    // Returning the same array when nothing moved is not an optimisation but the
    // termination condition: a fresh array on every answer is a fresh reference, a
    // re-render, and - on a page whose list is re-read after every action - a loop.
    if (!rows) return

    setSelectedIds((current) => {
      if (current.length === 0) return current

      const present = new Set(rows.map((row) => row.id))
      const kept = current.filter((id) => present.has(String(id)))

      return kept.length === current.length ? current : kept
    })
  }, [rows])

  /**
   * Reads the answer to a bulk run out loud: a toast when every row landed, and a dialog
   * listing the refusals when at least one did not.
   *
   * The dialog exists because a count is not a reason. "Changed 4 of 5" tells the operator
   * that something was refused and nothing about what - and the reason the row buttons would
   * have said *before* a click (`cannot_block_self` above all) is exactly the reason that now
   * has to arrive after one. Every line is the server's own sentence for that row's code,
   * already translated, so this summarises rather than invents: the same code in the same
   * words the single-row path would have shown. The lines themselves - one per code with a
   * count, because thirty rows refused for one reason are one sentence - are
   * `bulkRefusalList`'s; this decides only that there is a dialog to draw.
   */
  const reportBulk = (result: BulkOperationResponse) => {
    if (result.items.every((item) => item.ok)) {
      message.success(
        successTextFor(result, { done: result.succeeded, total: result.items.length }),
      )
      return
    }

    modal.info({
      title: t(partialTitleKey, {
        done: result.succeeded,
        total: result.items.length,
      }),
      okText: t('actions.ok'),
      content: bulkRefusalList(result.items),
    })
  }

  /**
   * Runs one operation over the selection.
   *
   * Afterwards the list is **re-read** rather than patched, for the reason every single-row
   * action re-reads it: a transition changes what the server considers each row to be, and the
   * page draws what it is told rather than what it guesses. The selection is cleared on the
   * way through, and `onSettled` runs whatever the answer was - a dialog the run came out of
   * has to close whether the run landed or was refused, because once the answer has arrived
   * there is nothing left to ask.
   */
  const applyBulk = async (
    run: () => Promise<BulkOperationResponse>,
    onSettled?: () => void,
  ) => {
    if (selectedIds.length === 0) return

    setBulkBusy(true)

    try {
      const result = await run()

      reportBulk(result)
      setSelectedIds([])
      void refetch()
    } catch (error) {
      message.error(messageForError(error))
    } finally {
      setBulkBusy(false)
      onSettled?.()
    }
  }

  return { selectedIds, setSelectedIds, bulkBusy, applyBulk }
}
