import { useQuery } from '@tanstack/react-query'
import { getDashboard } from '../api'

/**
 * The admin dashboard's one read.
 *
 * The key carries no parameters because the endpoint takes none - the whole screen is one
 * snapshot. No cache overrides are set: the shared client's `staleTime: 0`, `retry: false`
 * and focus/reconnect rules are the decision (see `src/utils/queryClient.ts`), and the
 * page's 30-second timer is the only thing that re-reads it on purpose.
 */
export const useDashboard = () =>
  useQuery({
    queryKey: ['dashboard'],
    queryFn: ({ signal }) => getDashboard({ signal }),
  })
