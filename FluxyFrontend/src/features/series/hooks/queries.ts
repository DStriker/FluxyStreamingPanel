import { useQuery } from '@tanstack/react-query'
import {
  ALL_SERIES_PAGE_SIZE,
  getEpisodeDetail,
  getEpisodeTranscodeFormats,
  getEpisodes,
  getSeries,
  getSeriesDetail,
} from '../api'
import type {
  EpisodeSortField,
  EpisodeSortOrder,
  SeriesSortField,
  SeriesSortOrder,
} from '../api'
import type { EpisodeStatus, EpisodeType, SeriesStatus } from '../types'
import { detailId } from '../../../utils/detailId'

/**
 * The series feature's reads: the paged catalogue, one series, one series' episodes, one
 * episode, and the transcoding availability the episode table asks once.
 */

export const useSeries = (params: {
  page: number
  pageSize: number
  search?: string
  categoryId?: string | null
  status?: SeriesStatus | null
  sortBy: SeriesSortField
  sortOrder: SeriesSortOrder
}) =>
  useQuery({
    queryKey: ['series', params],
    queryFn: ({ signal }) => getSeries({ ...params, signal }),
  })

/**
 * The whole catalogue for a picker, for the reason `useAllEpgSources` gives: the stream form
 * needs every series to choose from, and it asks by name rather than by paging through a list
 * inside a form field.
 *
 * A separate key from the paged table's - `['series', 'picker']` against `['series', params]`
 * - so opening the stream form never disturbs the page the operator was reading, and the
 * two reads cannot answer each other's question.
 */
export const useAllSeries = () =>
  useQuery({
    queryKey: ['series', 'picker'],
    queryFn: ({ signal }) =>
      getSeries({
        page: 1,
        pageSize: ALL_SERIES_PAGE_SIZE,
        sortBy: 'name',
        sortOrder: 'asc',
        signal,
      }),
  })

export const useSeriesDetail = (id: string | undefined) =>
  useQuery({
    queryKey: ['series', 'detail', id],
    queryFn: ({ signal }) => getSeriesDetail(detailId(id), signal),
    enabled: id !== undefined,
  })

export const useEpisodes = (
  seriesId: string | undefined,
  params: {
    page: number
    pageSize: number
    search?: string
    seasonNumber?: number | null
    status?: EpisodeStatus | null
    type?: EpisodeType | null
    sortBy: EpisodeSortField
    sortOrder: EpisodeSortOrder
  },
) =>
  useQuery({
    queryKey: ['series', seriesId, 'episodes', params],
    queryFn: ({ signal }) =>
      getEpisodes({ seriesId: detailId(seriesId), ...params, signal }),
    enabled: seriesId !== undefined,
  })

export const useEpisodeDetail = (
  seriesId: string | undefined,
  episodeId: string | undefined,
) =>
  useQuery({
    queryKey: ['series', seriesId, 'episodes', 'detail', episodeId],
    queryFn: ({ signal }) =>
      getEpisodeDetail(detailId(seriesId), detailId(episodeId), signal),
    enabled: seriesId !== undefined && episodeId !== undefined,
  })

export const useEpisodeTranscodeFormats = () =>
  useQuery({
    queryKey: ['series', 'transcodeFormats'],
    queryFn: ({ signal }) => getEpisodeTranscodeFormats(signal),
  })
