import type { FieldErrors } from './FieldErrors'

/**
 * How one row inside a bulk operation ended - the frontend's copy of `BulkItemResponse`.
 *
 * `id` is the row, not an index, so a caller can put the verdict back against the row it
 * asked about without having to trust that the server answered in order. `code` is the very
 * same code the single-row endpoint answers with (`cannot_block_self`,
 * `user_group_immutable`, `user_blocked`), which is the whole point: a client that already
 * knows how to branch on those does not need a second vocabulary for the same fact inside a
 * bulk run, and the locale files already carry every one of them.
 *
 * `ok` is what turns that code into a yes-or-no. It is read off the status the code maps to
 * on the server, so a refusal added there counts as a refusal here without this file having
 * to be told - and a client never has to parse English to learn whether anything happened.
 *
 * `errors` is `null` whenever nothing was rejected, the same shape `MessageResponse` gives.
 */
export interface BulkItemResponse {
  /** The row this entry is about, as a uuid string. */
  id: string
  /** Whether the operation changed that row, or the state it asked for already held. */
  ok: boolean
  /** Machine readable outcome for this row alone, snake_case. */
  code: string
  /** English fallback for this row alone. */
  message: string
  /** Rejected fields when the input was the problem, or `null`. */
  errors: FieldErrors
}
