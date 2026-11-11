import type { MagEventType } from './types'

/**
 * The frontend's mirror of the server's `MagEventRules`: which operation carries a message,
 * and what that message must be.
 *
 * The send form refuses a value before a request is made, and the refusal has to be the one
 * the server would have given - otherwise a field that passed here comes back as
 * `validation_failed` under `message`, with nothing on screen predicting it. The three rules
 * below are stated in the same order the server applies them:
 *
 * 1. The four portal/device operations carry **no payload at all**. A message sent anyway is
 *    dropped by the server, so the form draws no second field for them.
 * 2. Everything else requires a non-blank message, trimmed at its edges, of at most
 *    `MESSAGE_MAX_LENGTH` characters.
 * 3. A `playChannel` message is the video stream's identifier and must be a uuid. The rule is
 *    shape only - whether a stream carries that identifier is a fact about another table and
 *    deliberately not a condition of sending the operation.
 *
 * The checks are pure and unit-tested case for case against `MagEventRulesTests`; the form's
 * `Form.Item` rules are the only consumers.
 */

/** Longest message a row may store, mirroring `MagEventListLimits.MessageMaxLength`. */
export const MESSAGE_MAX_LENGTH = 1000

/** What the second field of the send form is, for one operation. */
export type MagEventMessageRule =
  /** No second field: the operation carries nothing. */
  | 'none'
  /** A free-text message, required and trimmed. */
  | 'text'
  /** A video stream identifier, required and uuid-shaped. */
  | 'channel'

/**
 * The canonical `8-4-4-4-12` uuid spelling a `playChannel` payload must carry - the form
 * `Guid.ToString()` produces and the only one the stream picker can hand over. The server reads
 * the same text with `Guid.TryParse`, which is wider (it also accepts the N, B, P and X forms),
 * so this check is deliberately narrower: it refuses a hand-typed variant the endpoint would
 * accept, never the other way round. That is safe because the picker is the only way a value
 * reaches this field - nobody types an identifier here.
 */
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

/** Which kind of payload an operation carries. */
export const eventMessageRule = (event: MagEventType): MagEventMessageRule => {
  if (event === 'sendMessage') return 'text'
  if (event === 'playChannel') return 'channel'

  return 'none'
}

/**
 * What is wrong with a message for this operation, or null when nothing is.
 *
 * `required` and `channel` both mean "this field must be filled" - the server keys the first
 * as a missing message and the second as a message that is not a uuid - and the form words
 * them for its own field. `tooLong` is the column width: the server refuses anything past
 * `MESSAGE_MAX_LENGTH` after trimming.
 */
export type MagEventMessageError = 'required' | 'tooLong' | 'channel'

export const messageErrorFor = (
  event: MagEventType,
  message: string | null | undefined,
): MagEventMessageError | null => {
  // The four payload-less operations drop a message sent anyway, so nothing about one can be
  // wrong. Checking that first is what makes this rule the server's, not a stricter variant.
  if (eventMessageRule(event) === 'none') return null

  const trimmed = (message ?? '').trim()

  if (trimmed.length === 0) return 'required'
  // Before the shape check, exactly as the server orders its own two refusals.
  if (trimmed.length > MESSAGE_MAX_LENGTH) return 'tooLong'
  if (event === 'playChannel' && !UUID.test(trimmed)) return 'channel'

  return null
}
