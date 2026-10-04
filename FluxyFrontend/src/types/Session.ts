import type { Role } from './Role'

/**
 * What `GET /auth/me` answers - the frontend's copy of `SessionResponse`.
 *
 * It exists because the tokens are `HttpOnly`: the page cannot read who it is signed in
 * as, so it asks. `username` and `userId` are nullable because the record allows it, and
 * `code` is the server's own "why this session is not usable" marker, which the guards
 * never read - they read the refusal itself.
 */
export interface Session {
  /** Identifier of the account, as a string so the page need not parse a GUID. */
  userId?: string | null
  /** Login name of the account, or `null` when the answer carried none. */
  username?: string | null
  /** Role the account holds. */
  role: Role
  /** Set by the server on a refusal; `null` on a successful answer. */
  code?: string | null
}
