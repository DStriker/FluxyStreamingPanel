import type { ReactNode } from 'react'
import i18n from '../i18n'
import type { BulkItemResponse } from '../types'
import { textForCode } from './http'

/** One line of the summary: a code and how many rows it applies to. */
interface SummaryLine {
  code: string
  count: number
}

/**
 * Groups codes so thirty rows refused for one reason are one sentence rather than thirty.
 *
 * The count is what makes the grouping honest: the same code appearing on four rows is a
 * fact the operator should see, not four identical lines to read past.
 */
const group = (codes: readonly string[]): SummaryLine[] => {
  const lines: SummaryLine[] = []
  const at = new Map<string, number>()

  for (const code of codes) {
    const index = at.get(code)

    if (index === undefined) {
      at.set(code, lines.length)
      lines.push({ code, count: 1 })
      continue
    }

    // `lines[index]` is looked up once rather than read twice: under
    // `noUncheckedIndexedAccess` an index that came from a `Map` is still an index, and
    // the compiler is right that nothing here proves the slot is filled.
    const previous = lines[index]

    if (previous) lines[index] = { code, count: previous.count + 1 }
  }

  return lines
}

/** The lines themselves, one per code, with a count only when there is more than one. */
const lines = (entries: readonly SummaryLine[]) => (
  <ul style={{ margin: 0, paddingInlineStart: 18 }}>
    {entries.map((entry) => (
      <li key={entry.code}>
        {textForCode(entry.code) ?? entry.code}
        {entry.count > 1 ? ` (${entry.count})` : ''}
      </li>
    ))}
  </ul>
)

/**
 * The body of a bulk run's summary dialog: what was refused and what a successful row had to
 * clear.
 *
 * The dialog exists because a count is not a reason. "Changed 4 of 5" tells the operator that
 * something was refused and nothing about what - and the reason the row buttons would have
 * said *before* a click (`cannot_block_self` above all) is exactly the reason that now has to
 * arrive after one. Every line is the server's own sentence for that row's code, already
 * translated through `textForCode`, so this summarises rather than invents: the same code in
 * the same words the single-row path would have shown. A code this build has no translation
 * for comes back as `null`, and the line falls back to the code itself.
 *
 * **Warnings are a third answer beside success and refusal.** A bulk type change that cleared
 * an archive length landed, so it is listed under its own heading instead of being counted as
 * a refusal - and a run where every row landed but one cleared a setting still opens this
 * dialog, because the operator has to be told what the change cost. The two headings appear
 * only when both kinds of line exist; a dialog with one kind is described by its own title.
 *
 * The grouping is the point rather than a detail: a run over thirty rows refusing all thirty
 * for one reason is one sentence, not thirty - and four rows that each had their archive
 * cleared are one line with a count of four.
 *
 * Called as a function rather than declared as a component, for the reason `hinted` and
 * `tableCells` are functions: the only call site is a `.ts` hook handing it to `modal.info`'s
 * `content`, which wants a node rather than a component type, and a `.ts` file has no JSX to
 * place a component with. It is also why `useBulkAction` is a `.ts` file - the markup lives
 * here.
 */
export const bulkResultList = (items: readonly BulkItemResponse[]): ReactNode => {
  const refusals = group(items.filter((item) => !item.ok).map((item) => item.code))
  const warnings = group(
    items.flatMap((item) => (item.warnings ?? []).map((warning) => warning.code)),
  )

  if (refusals.length === 0 && warnings.length === 0) return null

  const both = refusals.length > 0 && warnings.length > 0

  return (
    <div>
      {refusals.length > 0 && (
        <section>
          {both && <p style={{ margin: '0 0 4px' }}>{i18n.t('bulk.refused')}</p>}
          {lines(refusals)}
        </section>
      )}
      {warnings.length > 0 && (
        <section style={{ marginTop: refusals.length > 0 ? 12 : 0 }}>
          {both && <p style={{ margin: '0 0 4px' }}>{i18n.t('bulk.warnings')}</p>}
          {lines(warnings)}
        </section>
      )}
    </div>
  )
}
