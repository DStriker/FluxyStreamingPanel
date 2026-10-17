import { useQuery } from '@tanstack/react-query'
import { ALL_GROUPS_PAGE_SIZE, getUserGroup, getUserGroups } from '../../../lib/api'
import type { UserGroupSortField, UserGroupSortOrder } from '../../../lib/api'
import { detailId } from '../../../lib/detailId'
import type { Role, UserStatus } from '../../../types'

/**
 * The user-groups feature's reads: the paged groups table, the whole-list picker the
 * accounts pages fill their filter from, and the detail read the edit form draws.
 *
 * One hook per endpoint with its key in this file beside it, every `queryFn` passing
 * React Query's `AbortSignal` through to the API.
 */

/** One page of groups, keyed by everything the server filters, sorts and pages by. */
export const useUserGroups = (params: {
  page: number
  pageSize: number
  search?: string
  role?: Role | null
  status?: UserStatus | null
  sortBy?: UserGroupSortField
  sortOrder?: UserGroupSortOrder
}) =>
  useQuery({
    queryKey: ['userGroups', params],
    queryFn: ({ signal }) => getUserGroups({ ...params, signal }),
  })

/** The whole group list for a picker, for the reason `useAllUsers` gives in the users feature. */
export const useAllUserGroups = () =>
  useQuery({
    queryKey: ['userGroups', 'picker'],
    queryFn: ({ signal }) => getUserGroups({ page: 1, pageSize: ALL_GROUPS_PAGE_SIZE, signal }),
  })

/** One group in full, for the edit form. Not fetched on the add address. */
export const useUserGroupDetail = (id: string | undefined) =>
  useQuery({
    queryKey: ['userGroups', 'detail', id],
    queryFn: ({ signal }) => getUserGroup(detailId(id), signal),
    enabled: id !== undefined,
  })
