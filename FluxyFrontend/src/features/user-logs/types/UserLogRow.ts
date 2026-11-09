import type { UserLogRecordType } from './UserLogRecordType'

/**
 * One entry of the user audit log, as `GET /admin/user-logs` answers with it.
 *
 * The row is one attempt on an account - a registration, a sign-in, a password reset, a
 * profile change, a logout - with its outcome and the network it came from.
 *
 * **`accountId` and `username` answer two different questions.** `accountId` is the stored
 * account, and it is null when the attempt named no existing account (a sign-in for a login
 * name nobody holds) and when that account has since been deleted - the foreign key nulls
 * rather than cascades, because the evidence outlives the account. `username` is resolved in
 * three steps and the order is the contract: the account's current login name when the row
 * still names one, otherwise the identifier the visitor typed (recovered from the stored
 * description), otherwise null. So a null `accountId` with a non-null `username` is a
 * refused attempt against an unknown account, and the table draws that name as plain text -
 * there is no account to link to.
 *
 * `description` is the sentence the write path stored, deliberately in English: it is the
 * auditable record rather than a rendering of the reader's language, and the search box
 * matches it. The screen speaks its own words for the record type.
 *
 * The user agent is stored server-side and is deliberately **not** in this contract, so the
 * table has no agent column.
 */
export interface UserLogRow {
  /** Identifier of the log entry, as text. */
  id: string
  /** Account the entry is about, or null when it named none or the account was deleted. */
  accountId: string | null
  /** Current login name, else the typed identifier, else null. */
  username: string | null
  /** Which kind of event this row records, as its wire name. */
  recordType: UserLogRecordType
  /** Whether the attempt ended successfully. */
  isSuccess: boolean
  /** English sentence describing the event, as the audit trail stores it. */
  description: string
  /** Address the event came from, or null. */
  ip: string | null
  /** Provider the address belonged to, or null. */
  isp: string | null
  /** Upper-case ISO 3166-1 alpha-2 country of the address, or null. */
  countryCode: string | null
  /** When the entry was written, as an ISO timestamp. */
  createdAt: string
}
