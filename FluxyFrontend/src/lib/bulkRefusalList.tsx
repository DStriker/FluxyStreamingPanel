import type { ReactNode } from 'react'
import type { BulkItemResponse } from '../types'
import { textForCode } from './http'

/**
 * The refusal lines of a bulk run's summary dialog, one per code, each with how many rows it
 * refused.
 *
 * The dialog exists because a count is not a reason. "Changed 4 of 5" tells the operator that
 * something was refused and nothing about what - and the reason the row buttons would have
 * said *before* a click (`cannot_block_self` above all) is exactly the reason that now has to
 * arrive after one. Every line is the server's own sentence for that row's code, already
 * translated through `textForCode`, so this summarises rather than invents: the same code in
 * the same words the single-row path would have shown. A code this build has no translation
 * for comes back as `null`, and the line falls back to the code itself.
 *
 * The grouping is the point rather than a detail: a run over thirty rows refusing all thirty
 * for one reason is one sentence, not thirty. The count appears in parentheses only when
 * there is more than one, because "Refused (1)" is noise.
 *
 * Called as a function rather than declared as a component, for the reason `hinted` and
 * `tableCells` are functions: the only call site is a `.ts` hook handing it to `modal.info`'s
 * `content`, which wants a node rather than a component type, and a `.ts` file has no JSX to
 * place a component with. It is also why `useBulkAction` is a `.ts` file - the markup lives
 * here.
 *
 * The caller draws this only when at least one item is not `ok`; a run that landed entirely
 * is a toast, and an empty list under a dialog titled with a failure would be a contradiction.
 */
export const bulkRefusalList = (items: readonly BulkItemResponse[]): ReactNode => {
  const refusals: { code: string; count: number }[] = []
  const at = new Map<string, number>()

  for (const item of items) {
    if (item.ok) continue

    const index = at.get(item.code)

    if (index === undefined) {
      at.set(item.code, refusals.length)
      refusals.push({ code: item.code, count: 1 })
    } else {
      // `refusals[index]` is looked up once rather than read twice: under
      // `noUncheckedIndexedAccess` an index that came from a `Map` is still an index, and
      // the compiler is right that nothing here proves the slot is filled.
      const previous = refusals[index]

      if (previous) refusals[index] = { code: item.code, count: previous.count + 1 }
    }
  }

  return (
    <ul style={{ margin: 0, paddingInlineStart: 18 }}>
      {refusals.map((refusal) => (
        <li key={refusal.code}>
          {textForCode(refusal.code) ?? refusal.code}
          {refusal.count > 1 ? ` (${refusal.count})` : ''}
        </li>
      ))}
    </ul>
  )
}
