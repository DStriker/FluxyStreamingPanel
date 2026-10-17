import { useQuery } from '@tanstack/react-query'
import { ALL_USERS_PAGE_SIZE, getUser, getUsers } from '../../lib/api'
import type { AdminUserSortField, AdminUserSortOrder } from '../../lib/api'
import { detailId } from '../../lib/detailId'
import type { UserStatus } from '../../types'

/**
 * The users feature's reads: the paged accounts table, the whole-list picker, and the
 * detail read the edit form draws.
 *
 * One hook per endpoint with its key in this file beside it - the key *is* the
 * parameters, so a filter change is a new query rather than a state update an effect had
 * to notice - and every `queryFn` passes React Query's `AbortSignal` through to the API.
 */

/** One page of accounts, keyed by everything the server filters, sorts and pages by. */
export const useUsers = (params: {
  page: number
  pageSize: number
  search?: string
  groupId?: string | null
  status?: UserStatus | null
  sortBy?: AdminUserSortField
  sortOrder?: AdminUserSortOrder
}) =>
  useQuery({
    queryKey: ['users', params],
    queryFn: ({ signal }) => getUsers({ ...params, signal }),
  })

/**
 * The whole account list for a picker - one page of the server's ceiling, which is also
 * the answer the accounts table's own list needs. A key of its own rather than a slice of
 * the table's key: the two ask the same endpoint different questions (everything vs one
 * filtered page), and sharing a key would make one's cache answer the other's query.
 */
export const useAllUsers = () =>
  useQuery({
    queryKey: ['users', 'picker'],
    queryFn: ({ signal }) =>
      getUsers({ page: 1, pageSize: ALL_USERS_PAGE_SIZE, sortBy: 'username', sortOrder: 'asc', signal }),
  })

/** One account in full, for the edit form. Not fetched on the add address, whose `enabled` is false. */
export const useUserDetail = (id: string | undefined) =>
  useQuery({
    queryKey: ['users', 'detail', id],
    queryFn: ({ signal }) => getUser(detailId(id), signal),
    enabled: id !== undefined,
  })
