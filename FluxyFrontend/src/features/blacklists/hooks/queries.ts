import { useQuery } from '@tanstack/react-query'
import { getBlacklistEntries } from '../api'
import type { BlacklistSortField, BlacklistSortOrder } from '../api'
import type { BlacklistType } from '../types'

/**
 * The blacklists feature's reads: one page of one list.
 *
 * One hook, because one endpoint serves all three lists - the type is part of the key, so
 * moving between the three screens is a new query rather than a filter applied to a cached one.
 */
export const useBlacklistEntries = (params: {
  type: BlacklistType
  page: number
  pageSize: number
  search?: string
  sortBy: BlacklistSortField
  sortOrder: BlacklistSortOrder
}) =>
  useQuery({
    queryKey: ['blacklists', params],
    queryFn: ({ signal }) => getBlacklistEntries({ ...params, signal }),
  })
