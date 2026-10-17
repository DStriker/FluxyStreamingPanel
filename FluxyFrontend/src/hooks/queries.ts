import { useQuery } from '@tanstack/react-query'
import {
  getActiveSessions,
  getAdminSessionHistory,
  getContentCategories,
  getProfile,
  getSessionHistory,
  getUser,
  getUserGroup,
  getUserGroups,
  getUsers,
  getVideoStream,
  getVideoStreams,
  ALL_GROUPS_PAGE_SIZE,
  ALL_USERS_PAGE_SIZE,
} from '../lib/api'
import type {
  AdminUserSortField,
  AdminUserSortOrder,
  ContentTypeParam,
  SessionSortField,
  SessionSortOrder,
  UserGroupSortField,
  UserGroupSortOrder,
  VideoStreamSortField,
  VideoStreamSortOrder,
} from '../lib/api'
import type { Role, UserStatus, VideoStreamStatus, VideoStreamType } from '../types'

/**
 * The queries the application's pages run, one hook per endpoint, with the query keys in
 * this one file.
 *
 * Every new read of the API goes through here rather than through a `useEffect` in a page:
 * the key *is* the parameters (`['users', { page, pageSize, search, ... }]`), so a change
 * to any of them is a new query rather than a state update an effect had to notice, and a
 * mutation's answer to "what is now stale" is `refetch()` or `invalidateQueries` rather
 * than an `attempt` counter bumped in fifteen places.
 *
 * `staleTime` is 0 globally (see `src/lib/queryClient.ts` for why the cache is kept short
 * on purpose), and each `queryFn` passes React Query's `AbortSignal` through to the API -
 * an unmounted page's request now ends instead of riding on to completion under a
 * `cancelled` flag.
 *
 * Session fetching (`RequireAuth`, `GuestOnly`) deliberately stays outside this file: a
 * cached session is a stale session, and the guard asks `/auth/me` on every navigation.
 */

/** The three facts the profile page shows, shared by every page that needs the zone or the grants. */
export const useProfile = () =>
  useQuery({
    queryKey: ['profile'],
    queryFn: ({ signal }) => getProfile(signal),
  })

/** One page of this account's visit history, keyed by everything the server filters by. */
export const useSessionHistory = (params: {
  page: number
  pageSize: number
  search?: string
  sortBy?: SessionSortField
  sortOrder?: SessionSortOrder
}) =>
  useQuery({
    queryKey: ['sessions', 'history', params],
    queryFn: ({ signal }) => getSessionHistory({ ...params, signal }),
  })

/** Every session this account currently holds. */
export const useActiveSessions = () =>
  useQuery({
    queryKey: ['sessions', 'active'],
    queryFn: ({ signal }) => getActiveSessions(signal),
  })

/** One page of the installation's visit history, keyed like the account's own. */
export const useAdminSessionHistory = (params: {
  page: number
  pageSize: number
  search?: string
  userId?: string | null
  sortBy?: SessionSortField
  sortOrder?: SessionSortOrder
}) =>
  useQuery({
    queryKey: ['adminSessions', params],
    queryFn: ({ signal }) => getAdminSessionHistory({ ...params, signal }),
  })

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

/**
 * The id a detail query was enabled for, or a thrown error for the state `enabled` makes
 * unreachable.
 *
 * `getUser(id as string, signal)` was a cast, and a cast is a claim the compiler cannot
 * check - it held only because `enabled: id !== undefined` kept the query from running on
 * an add address. This states the invariant in code instead: a query that somehow runs
 * without an id reports the mistake rather than requesting `/users/undefined`, which the
 * server would answer with a 404 that reads like the row is gone.
 */
const detailId = (id: string | undefined): string => {
  if (id === undefined) throw new Error('a detail query ran without an id')
  return id
}

/** One account in full, for the edit form. Not fetched on the add address, whose `enabled` is false. */
export const useUserDetail = (id: string | undefined) =>
  useQuery({
    queryKey: ['users', 'detail', id],
    queryFn: ({ signal }) => getUser(detailId(id), signal),
    enabled: id !== undefined,
  })

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

/** The whole group list for a picker, for the reason `useAllUsers` gives above. */
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

/**
 * Every category of one kind of content, in the order the operator arranged them in.
 *
 * The key *is* the type, so the four section pages - each asking about a different kind -
 * never share a cache entry, and a drop that rewrites this list by hand still lands under
 * the same key the next read answers from.
 */
export const useContentCategories = (type: ContentTypeParam) =>
  useQuery({
    queryKey: ['contentCategories', type],
    queryFn: ({ signal }) => getContentCategories({ type, signal }),
  })

/** One page of the streams table, keyed by everything the server filters, sorts and pages by. */
export const useVideoStreams = (params: {
  page: number
  pageSize: number
  search?: string
  categoryId?: string | null
  status?: VideoStreamStatus | null
  type?: VideoStreamType | null
  sortBy: VideoStreamSortField
  sortOrder: VideoStreamSortOrder
}) =>
  useQuery({
    queryKey: ['videoStreams', params],
    queryFn: ({ signal }) => getVideoStreams({ ...params, signal }),
  })

/**
 * One stream in full, for the edit form. Not fetched on the add address, whose `enabled`
 * is false - a create has nothing to load, and asking would draw a spinner over a form
 * that is ready.
 */
export const useVideoStreamDetail = (id: string | undefined) =>
  useQuery({
    queryKey: ['videoStreams', 'detail', id],
    queryFn: ({ signal }) => getVideoStream(detailId(id), signal),
    enabled: id !== undefined,
  })
