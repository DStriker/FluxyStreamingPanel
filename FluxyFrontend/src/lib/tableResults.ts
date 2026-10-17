import { messageForError } from './http'

/**
 * What a table page reads out of its query: the rows, the sentence for a failed read, and
 * whether the table is busy.
 *
 * `isFetching` rather than `isPending` is the decision this exists to state once: a page turn
 * re-reads with the previous rows still on the table under the spinner, which is what the old
 * effect's `setLoading(true)` did, while `isPending` is true only before the first answer ever
 * arrives. A failure is the query's own `error`, resolved through `messageForError` so the
 * reason arrives translated; the retry button calls `refetch`.
 *
 * It reads the four fields it needs rather than a `UseQueryResult` - those fields are the
 * whole contract, and naming them keeps this a pure function that unit tests can call
 * without a query client.
 */
export interface TableResults<T> {
  data: T | undefined
  loadError: string | null
  loading: boolean
}

export function tableResults<T>(query: {
  data: T | undefined
  isError: boolean
  error: unknown
  isFetching: boolean
}): TableResults<T> {
  return {
    data: query.data,
    loadError: query.isError ? messageForError(query.error) : null,
    loading: query.isFetching,
  }
}
