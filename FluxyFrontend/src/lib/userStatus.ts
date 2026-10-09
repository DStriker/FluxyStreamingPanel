import type { UserStatus } from '../types'

/**
 * The frontend's copy of `UserStatusComposition.Combine`
 * (`Fluxy.Core/Models/Users/UserStatusComposition.cs`): an account's **effective** state is
 * its own row combined with its group's, the most restrictive of the two winning.
 *
 * It exists for one job - drawing the note beside the status field of the account form -
 * and it exists at all because that note has to keep telling the truth while the visitor is
 * still choosing. Pick a blocked group and the account becomes blocked *right there*,
 * without a round trip; leave it and the note goes with it. Reading the stored
 * `effectiveStatus` instead would describe the row as it arrived and say nothing about what
 * the form is about to do, which is the one thing worth saying.
 *
 * Three facts are mirrored rather than inferred, because each is one only the rule states:
 *
 * - **`UserStatus` is not an ordered scale**, so the enum's numeric values are the wrong
 *   order for this: `Unregistered = 0` and `Registered = 1`, yet an unregistered account is
 *   *more* restricted than a registered one. Ranking by the stored number would answer
 *   `Registered` for a row waiting for its email, which is the one case this rule exists to
 *   catch.
 * - **The tie goes to neither** - equal states combine to that state, so two `Registered`
 *   halves are `Registered` and the comparison never has to break one.
 * - **The server stays the authority.** `GET /admin/users` sends the combined value as
 *   `status` and `GET /admin/users/{id}` sends both halves, so every *read* of an account
 *   anywhere in this application is the server's answer; this is the same arithmetic applied
 *   to values the form is holding, and a refusal about them (`409 user_blocked_by_group`)
 *   still comes from the server.
 *
 * The restriction order, worst first: `Blocked` > `Unregistered` > `Registered`.
 */
const RESTRICTIVENESS: Readonly<Record<UserStatus, number>> = {
  Registered: 0,
  Unregistered: 1,
  Blocked: 2,
}

/**
 * The state an account is actually in, given its own row and its group's.
 *
 * @param own State stored on the account's own row.
 * @param group State of the group that account belongs to.
 * @returns The more restrictive of the two.
 */
export const combineStatus = (own: UserStatus, group: UserStatus): UserStatus =>
  RESTRICTIVENESS[own] >= RESTRICTIVENESS[group] ? own : group
