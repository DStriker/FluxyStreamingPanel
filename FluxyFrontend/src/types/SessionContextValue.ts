import type { Session } from './Session'

/**
 * What `SessionContext` publishes to the area behind the guards.
 *
 * `refresh` is what the profile page calls after renaming the account: the header shows
 * the `username` claim this component obtained before the change, and a claim that was
 * read before the rename is stale from the moment the rename succeeds.
 */
export interface SessionContextValue extends Session {
  /** Asks `/auth/me` again and republishes the answer. Resolves `null` when there is none. */
  refresh: () => Promise<Session | null>
}
