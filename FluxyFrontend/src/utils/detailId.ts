/**
 * The id a detail query was enabled for, or a thrown error for the state `enabled` makes
 * unreachable.
 *
 * `getUser(id as string, signal)` was a cast, and a cast is a claim the compiler cannot
 * check - it held only because `enabled: id !== undefined` kept the query from running on
 * an add address. This states the invariant in code instead: a query that somehow runs
 * without an id reports the mistake rather than requesting `/users/undefined`, which the
 * server would answer with a 404 that reads like the row is gone.
 *
 * The three detail reads - an account, a group, a stream - share it because they live in
 * three different features, and three copies of an invariant are three chances for one of
 * them to disagree.
 */
export const detailId = (id: string | undefined): string => {
  if (id === undefined) throw new Error('a detail query ran without an id')
  return id
}
