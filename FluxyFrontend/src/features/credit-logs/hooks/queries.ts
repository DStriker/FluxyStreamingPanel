import { useQuery } from '@tanstack/react-query'
import { getCreditLogs } from '../api'
import type { CreditLogQuery } from '../api'

/**
 * The credit log's one read: a page of it.
 *
 * The key **is** the parameters, so a filter change, a page turn or a different order is a
 * new query rather than an effect that had to notice one - the same rule every other
 * feature's reads follow here.
 */
export const useCreditLogs = (params: CreditLogQuery) =>
  useQuery({
    queryKey: ['creditLogs', params],
    queryFn: ({ signal }) => getCreditLogs({ ...params, signal }),
  })
