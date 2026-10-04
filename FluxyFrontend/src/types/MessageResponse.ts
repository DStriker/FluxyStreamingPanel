import type { FieldErrors } from './FieldErrors'

/**
 * The body every endpoint of this API answers with, success or failure - the frontend's
 * copy of `MessageResponse`.
 *
 * `code` is what callers branch on and what the locale files translate under
 * `messages.api.<code>`; `message` is the English fallback for a code this build has not
 * translated yet. An unknown code is legitimate (the backend adds them without telling
 * anybody), so `code` is `string` and not a union of the codes known today.
 */
export interface MessageResponse {
  /** Machine readable outcome, snake_case. */
  code: string
  /** English fallback text for `code`. */
  message: string
  /** Fields the server rejected, or `null` when none were. */
  errors: FieldErrors
  /** Path the server says this visitor belongs on, or `null`. Never an absolute URL. */
  redirect: string | null
}
