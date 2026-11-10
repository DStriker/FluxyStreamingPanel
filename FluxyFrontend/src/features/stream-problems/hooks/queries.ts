import { useQuery } from '@tanstack/react-query'
import { getStreamProblems } from '../api'
import type { StreamProblemQuery } from '../api'

/**
 * The stream problem journal's one read: a page of it.
 *
 * The key **is** the parameters, so a filter change, a page turn or a different order is a new
 * query rather than an effect that had to notice one - the same rule every other feature's
 * reads follow here. The `AbortSignal` goes through to `apiGet`, so a key change or an unmount
 * aborts the request it replaced.
 */

/** Optional switches for the journal read. */
interface UseStreamProblemsOptions {
  /**
   * Whether to ask at all. Defaults to true.
   *
   * The one caller that passes false is the page's refusal state: a visitor without
   * `viewStreams` would get a 403 from this endpoint, so the screen says what the refusal is
   * instead of spending a request on a list it may not read.
   */
  enabled?: boolean
}

export const useStreamProblems = (
  params: StreamProblemQuery,
  { enabled = true }: UseStreamProblemsOptions = {},
) =>
  useQuery({
    queryKey: ['streamProblems', params],
    queryFn: ({ signal }) => getStreamProblems({ ...params, signal }),
    enabled,
  })
