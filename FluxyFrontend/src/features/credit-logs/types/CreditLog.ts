/**
 * One entry of the reseller credit log, as `GET /admin/credit-logs` answers with it.
 *
 * The row is the audit trail of one change to a reseller's balance: the reseller whose
 * balance moved, the administrator who wrote the row, and the signed change itself. `amount`
 * is the delta - positive is credit issued, negative is credit spent, and zero is the entry
 * an account created with no initial credit carries.
 *
 * `description` is the sentence the write path stored, deliberately in English: it is the
 * auditable record rather than a rendering of the reader's language, and the search box
 * matches it. The screen draws its own wording from `amount`.
 *
 * `issuedByUsername` is null on a reseller-spent row - that write path does not exist yet, and
 * a spend has no issuing administrator - and also when the administrator's account has since
 * been deleted, which the foreign key turns into a null identifier.
 */
export interface CreditLog {
  /** Identifier of the ledger entry, as text. */
  id: string
  /** Identifier of the reseller whose balance moved, as text - the edit form's target. */
  resellerId: string
  /** Login name of that reseller as it reads today. */
  resellerUsername: string
  /** Identifier of the administrator who wrote the row, or null. */
  issuedById: string | null
  /** Login name of that administrator, or null when there was no issuer. */
  issuedByUsername: string | null
  /** Signed change to the balance: positive issued, negative spent. */
  amount: number
  /** English sentence describing the change, as the audit trail stores it. */
  description: string
  /** When the entry was written, as an ISO timestamp. */
  createdAt: string
}
