import { useQuery } from '@tanstack/react-query'
import {
  getTranscodingProfile,
  getTranscodingProfileOptions,
  getTranscodingProfiles,
} from '../api'
import type {
  TranscodingProfileSortField,
  TranscodingProfileSortOrder,
} from '../api'
import { detailId } from '../../../utils/detailId'

/**
 * The transcoding profiles feature's reads: one page of the table, one profile in full, and the
 * picker the four content forms draw.
 */

/**
 * One page of the profiles table.
 *
 * The key is the whole parameter set, so a search, an order or a page turn is a new entry rather
 * than an effect that had to notice one.
 */
export const useTranscodingProfiles = (params: {
  page: number
  pageSize: number
  search: string
  sortBy: TranscodingProfileSortField
  sortOrder: TranscodingProfileSortOrder
}) =>
  useQuery({
    queryKey: ['transcodingProfiles', params],
    queryFn: ({ signal }) => getTranscodingProfiles({ ...params, signal }),
  })

/**
 * One profile in full, for the edit form.
 *
 * Disabled while there is no identifier, which is what makes one component serve the add
 * address as well as the edit one. `detailId` states that invariant in code rather than in a
 * cast.
 */
export const useTranscodingProfile = (id: string | undefined) =>
  useQuery({
    queryKey: ['transcodingProfile', id],
    queryFn: ({ signal }) => getTranscodingProfile(detailId(id), signal),
    enabled: id !== undefined,
  })

/**
 * Every profile as a picker entry - what the four content forms offer.
 *
 * Its own key and its own endpoint: the picker is gated by the content-view grants, so a form
 * that may be opened at all can fill it, and the table's page query is neither needed nor
 * permitted there.
 */
export const useAllTranscodingProfiles = () =>
  useQuery({
    queryKey: ['transcodingProfileOptions'],
    queryFn: ({ signal }) => getTranscodingProfileOptions(signal),
  })
