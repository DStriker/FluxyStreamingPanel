import type { ActiveSession } from './ActiveSession'

/**
 * `GET /auth/active-sessions` - every session the account currently holds, newest first.
 *
 * No paging and no `total`, unlike `SessionHistoryResponse`: an account holds a handful of
 * sessions at most, so a pager would have nothing to turn - and the "end all but this one"
 * button revokes by exclusion, which needs the whole list to exist in one answer.
 */
export interface ActiveSessionList {
  /** The account's live sessions, newest first. */
  items: ActiveSession[]
}
