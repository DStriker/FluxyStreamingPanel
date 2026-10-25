import { apiFetch, apiGet } from '../../utils/http'
import type { BulkOperationResponse, MessageResponse } from '../../types'
import type {
  EpisodeDetail,
  EpisodeList,
  EpisodeStatus,
  EpisodeTranscodeAvailability,
  EpisodeType,
  SeasonCreated,
  SeriesDetail,
  SeriesList,
  SeriesStatus,
} from './types'

/**
 * The series catalogue: its table, one series and its episodes, the form writes, the bulk
 * runs, the season creation and the transcoding surface.
 */

/** The columns the series table may be ordered by, as the query-string spelling. */
export type SeriesSortField =
  | 'name'
  | 'category'
  | 'status'
  | 'releaseDate'
  | 'rating'
  | 'seasons'
  | 'episodes'

/** The columns the episode table may be ordered by, as the query-string spelling. */
export type EpisodeSortField =
  | 'season'
  | 'episode'
  | 'name'
  | 'status'
  | 'type'
  | 'releaseDate'
  | 'rating'

export type SeriesSortOrder = 'asc' | 'desc'
export type EpisodeSortOrder = 'asc' | 'desc'

/** The page size the pager offers, which is also the server's default. */
export const SERIES_PAGE_SIZE = 20

/** The page size the episode pager offers. */
export const EPISODES_PAGE_SIZE = 20

/** One page of the catalogue: `GET /admin/series`. */
export const getSeries = ({
  page,
  pageSize,
  search,
  categoryId,
  status,
  sortBy,
  sortOrder,
  signal,
}: {
  page: number
  pageSize: number
  search?: string
  categoryId?: string | null
  status?: SeriesStatus | null
  sortBy: SeriesSortField
  sortOrder: SeriesSortOrder
  signal?: AbortSignal
}): Promise<SeriesList> => {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
    sortBy,
    sortOrder,
  })

  if (search) query.set('search', search)
  if (categoryId) query.set('categoryId', categoryId)
  if (status) query.set('status', status)

  return apiGet<SeriesList>(`/admin/series?${query.toString()}`, signal)
}

/** One series in full, for the edit form and the episode page: `GET /admin/series/{id}`. */
export const getSeriesDetail = (
  id: string,
  signal?: AbortSignal,
): Promise<SeriesDetail> =>
  apiGet<SeriesDetail>(`/admin/series/${encodeURIComponent(id)}`, signal)

/** Everything one form can decide about a series. */
export interface SeriesValues {
  /** Display name, unique within its category. */
  name: string
  /** Whether the series is published or disabled. */
  status: SeriesStatus
  /** Identifier of the category the series is filed under, as text. */
  categoryId: string
  /** IMDb identifier, or an empty string. */
  imdbId: string
  /** Portrait cover address, or an empty string. */
  posterUrl: string
  /** Wide background picture address, or an empty string. */
  backdropUrl: string
  /** Synopsis, or an empty string. */
  plot: string
  /** Cast, in billing order. */
  cast: string[]
  /** Director, or an empty string. */
  director: string
  /** Genres, in the order entered. */
  genres: string[]
  /** Release date as `yyyy-MM-dd`, or an empty string. */
  releaseDate: string
  /** Rating from 0 to 10, or null. */
  rating: number | null
}

/** Creates a series: `POST /admin/series`, answered with `201` and a `Location`. */
export const createSeries = ({
  values,
  csrfToken,
}: {
  values: SeriesValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/admin/series', {
    method: 'POST',
    body: values,
    csrfToken,
  })

/**
 * Changes a series: `PATCH /admin/series/{id}`.
 *
 * The whole form travels rather than a diff. An empty string clears a text field; `rating`
 * travels as text because the server reads `null` as "keep" and `""` as "clear".
 */
export const updateSeries = ({
  id,
  values,
  csrfToken,
}: {
  id: string
  values: SeriesValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/series/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body: {
      ...values,
      rating: values.rating === null ? '' : String(values.rating),
    },
    csrfToken,
  })

/** Publishes or hides one series: `PATCH /admin/series/{id}`. */
export const setSeriesStatus = ({
  id,
  status,
  csrfToken,
}: {
  id: string
  status: SeriesStatus
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/series/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body: { status },
    csrfToken,
  })

/** Deletes a series and, by cascade, its episodes: `DELETE /admin/series/{id}`. */
export const deleteSeries = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/series/${encodeURIComponent(id)}`, {
    method: 'DELETE',
    csrfToken,
  })

/** The operations one series bulk request may ask for. */
export type BulkSeriesAction = 'change-status' | 'move-category' | 'delete'

/** The body of `POST /admin/series/bulk`. */
export interface BulkSeriesPayload {
  action: BulkSeriesAction
  ids: string[]
  categoryId?: string
  status?: SeriesStatus
}

/** Applies one operation to a series selection: `POST /admin/series/bulk`. */
export const bulkSeries = ({
  payload,
  csrfToken,
}: {
  payload: BulkSeriesPayload
  csrfToken: string | null
}): Promise<BulkOperationResponse> =>
  apiFetch('/admin/series/bulk', {
    method: 'POST',
    body: payload,
    csrfToken,
  })

/** One page of the episode table of one series. */
export const getEpisodes = ({
  seriesId,
  page,
  pageSize,
  search,
  seasonNumber,
  status,
  type,
  sortBy,
  sortOrder,
  signal,
}: {
  seriesId: string
  page: number
  pageSize: number
  search?: string
  seasonNumber?: number | null
  status?: EpisodeStatus | null
  type?: EpisodeType | null
  sortBy: EpisodeSortField
  sortOrder: EpisodeSortOrder
  signal?: AbortSignal
}): Promise<EpisodeList> => {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
    sortBy,
    sortOrder,
  })

  if (search) query.set('search', search)
  if (seasonNumber) query.set('seasonNumber', String(seasonNumber))
  if (status) query.set('status', status)
  if (type) query.set('type', type)

  return apiGet<EpisodeList>(
    `/admin/series/${encodeURIComponent(seriesId)}/episodes?${query.toString()}`,
    signal,
  )
}

/** One episode in full: `GET /admin/series/{id}/episodes/{episodeId}`. */
export const getEpisodeDetail = (
  seriesId: string,
  episodeId: string,
  signal?: AbortSignal,
): Promise<EpisodeDetail> =>
  apiGet<EpisodeDetail>(
    `/admin/series/${encodeURIComponent(seriesId)}/episodes/${encodeURIComponent(episodeId)}`,
    signal,
  )

/** Everything one form can decide about an episode. */
export interface EpisodeValues {
  /** Season the episode belongs to, counted from one. */
  seasonNumber: number
  /** Number of the episode inside its season, counted from one. */
  episodeNumber: number
  /** Display name, or an empty string. */
  name: string
  /** URL or file path the episode is played from. */
  source: string
  /** Whether the episode is published or disabled. */
  status: EpisodeStatus
  /** How the episode is delivered. */
  type: EpisodeType
  /** IMDb identifier, or an empty string. */
  imdbId: string
  /** Episode image address, or an empty string. */
  imageUrl: string
  /** Synopsis, or an empty string. */
  plot: string
  /** Release date as `yyyy-MM-dd`, or an empty string. */
  releaseDate: string
  /** Rating from 0 to 10, or null. */
  rating: number | null
}

/** Creates an episode: `POST /admin/series/{id}/episodes`. */
export const createEpisode = ({
  seriesId,
  values,
  csrfToken,
}: {
  seriesId: string
  values: EpisodeValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/series/${encodeURIComponent(seriesId)}/episodes`, {
    method: 'POST',
    body: values,
    csrfToken,
  })

/** Changes an episode: `PATCH /admin/series/{id}/episodes/{episodeId}`. */
export const updateEpisode = ({
  seriesId,
  episodeId,
  values,
  csrfToken,
}: {
  seriesId: string
  episodeId: string
  values: EpisodeValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(
    `/admin/series/${encodeURIComponent(seriesId)}/episodes/${encodeURIComponent(episodeId)}`,
    {
      method: 'PATCH',
      body: {
        ...values,
        rating: values.rating === null ? '' : String(values.rating),
      },
      csrfToken,
    },
  )

/** Publishes or hides one episode: `PATCH .../episodes/{episodeId}`. */
export const setEpisodeStatus = ({
  seriesId,
  episodeId,
  status,
  csrfToken,
}: {
  seriesId: string
  episodeId: string
  status: EpisodeStatus
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(
    `/admin/series/${encodeURIComponent(seriesId)}/episodes/${encodeURIComponent(episodeId)}`,
    {
      method: 'PATCH',
      body: { status },
      csrfToken,
    },
  )

/** Deletes an episode: `DELETE .../episodes/{episodeId}`. */
export const deleteEpisode = ({
  seriesId,
  episodeId,
  csrfToken,
}: {
  seriesId: string
  episodeId: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(
    `/admin/series/${encodeURIComponent(seriesId)}/episodes/${encodeURIComponent(episodeId)}`,
    {
      method: 'DELETE',
      csrfToken,
    },
  )

/** The operations one episode bulk request may ask for. */
export type BulkEpisodeAction = 'change-status' | 'change-type' | 'delete'

/** The body of `POST /admin/series/{id}/episodes/bulk`. */
export interface BulkEpisodePayload {
  action: BulkEpisodeAction
  ids: string[]
  status?: EpisodeStatus
  type?: EpisodeType
}

/** Applies one operation to an episode selection. */
export const bulkEpisodes = ({
  seriesId,
  payload,
  csrfToken,
}: {
  seriesId: string
  payload: BulkEpisodePayload
  csrfToken: string | null
}): Promise<BulkOperationResponse> =>
  apiFetch(`/admin/series/${encodeURIComponent(seriesId)}/episodes/bulk`, {
    method: 'POST',
    body: payload,
    csrfToken,
  })

/** The settings one season creation applies to every episode it creates. */
export interface SeasonValues {
  /** Season number every created episode carries. */
  seasonNumber: number
  /** Folder the episode files are read from. */
  folderPath: string
  /** Whether the generated name carries the season number. */
  addSeasonNumberToName: boolean
  /** Whether the generated name carries the episode number. */
  addEpisodeNumberToName: boolean
  /** State every created episode starts with. */
  status: EpisodeStatus
  /** Delivery kind every created episode starts with. */
  type: EpisodeType
}

/** Creates a whole season from a folder: `POST /admin/series/{id}/seasons`. */
export const createSeason = ({
  seriesId,
  values,
  csrfToken,
}: {
  seriesId: string
  values: SeasonValues
  csrfToken: string | null
}): Promise<SeasonCreated> =>
  apiFetch(`/admin/series/${encodeURIComponent(seriesId)}/seasons`, {
    method: 'POST',
    body: values,
    csrfToken,
  })

/** Whether this installation can transcode episodes: `GET /admin/series/transcode-formats`. */
export const getEpisodeTranscodeFormats = (
  signal?: AbortSignal,
): Promise<EpisodeTranscodeAvailability> =>
  apiGet<EpisodeTranscodeAvailability>('/admin/series/transcode-formats', signal)

/** Asks for one episode to be converted; refused while no encoder is configured. */
export const requestEpisodeTranscode = ({
  seriesId,
  episodeId,
  format,
  csrfToken,
}: {
  seriesId: string
  episodeId: string
  format: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(
    `/admin/series/${encodeURIComponent(seriesId)}/episodes/${encodeURIComponent(episodeId)}/transcodes`,
    {
      method: 'POST',
      body: { format },
      csrfToken,
    },
  )
