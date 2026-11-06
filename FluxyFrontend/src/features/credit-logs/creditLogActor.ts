import type { CreditLog } from './types'

/** The dash a cell draws for a value the server could not determine (U+2014). */
const DASH = '\u2014'

/**
 * **Who this change is attributed to**, as the "Issued by" cell words it.
 *
 * The rule is the ledger's own, not a formatting choice:
 *
 * 1. **An issuer on the row wins.** Every row the admin panel writes - a create, or an update
 *    that raises or lowers the balance - names the acting administrator, so the cell names
 *    them. This is the ordinary case, including a decrease: it is still an administrative edit.
 * 2. **A negative row with no issuer is a reseller-spent row.** That write path does not exist
 *    yet; `issued_by_id` will be null there because the reseller spent their own credit and no
 *    administrator issued it. The cell names the account whose balance moved.
 * 3. **A zero-or-positive row with no issuer lost its issuer.** The foreign key is
 *    `ON DELETE SET NULL`, so deleting the administrator leaves the history standing with a
 *    dash - the history survives its issuer's deletion.
 *
 * Extracted here rather than left inline in the page because no render in `probe:render` ever
 * reaches a cell `render`, and this rule has three branches that a wrong implementation would
 * keep compiling through.
 */
export const creditLogActorName = (log: CreditLog): string => {
  if (log.issuedById != null) return log.issuedByUsername ?? DASH
  if (log.amount < 0) return log.resellerUsername

  return DASH
}

/**
 * **The colour of the amount**, as `Typography.Text`'s `type`: green for credit issued, red
 * for everything else.
 *
 * `amount > 0` is the only green case. Zero is red rather than neutral on the owner's call:
 * it is still an entry that changed nothing, and the two colours here mean "money in" and
 * "money out or nothing came in", not a three-step scale.
 */
export const creditLogTone = (amount: number): 'success' | 'danger' =>
  amount > 0 ? 'success' : 'danger'
