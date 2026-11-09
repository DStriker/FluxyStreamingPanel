import { useQuery } from '@tanstack/react-query'
import { getUserLogs } from '../api'
import type { UserLogQuery } from '../api'

/**
 * The user audit log's one read: a page of it.
 *
 * The key **is** the parameters, so a filter change, a page turn or a different order is a
 * new query rather than an effect that had to notice one - the same rule every other
 * feature's reads follow here. The `AbortSignal` goes through to `apiGet`, so a key change
 * or an unmount aborts the request it replaced.
 */

/** Optional switches for the log read. */
interface UseUserLogsOptions {
  /**
   * Whether to ask at all. Defaults to true.
   *
   * The one caller that passes false is the page's refusal state: a visitor without
   * `viewUserLogs` would get a 403 from this endpoint, so the screen says what the refusal is
   * instead of spending a request on a list it may not read.
   */
  enabled?: boolean
}

export const useUserLogs = (params: UserLogQuery, { enabled = true }: UseUserLogsOptions = {}) =>
  useQuery({
    queryKey: ['userLogs', params],
    queryFn: ({ signal }) => getUserLogs({ ...params, signal }),
    enabled,
  })
