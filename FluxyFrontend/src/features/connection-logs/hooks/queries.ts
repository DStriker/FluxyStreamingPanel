import { useQuery } from '@tanstack/react-query'
import { getConnectionLogs } from '../api'
import type { ConnectionLogQuery } from '../api'

/**
 * The connection log's one read: a page of it.
 *
 * The key **is** the parameters, so a filter change, a page turn or a different medium is a new
 * query rather than an effect that had to notice one - the same rule every other feature's
 * reads follow here.
 */
export const useConnectionLogs = (params: ConnectionLogQuery) =>
  useQuery({
    queryKey: ['connectionLogs', params],
    queryFn: ({ signal }) => getConnectionLogs({ ...params, signal }),
  })
