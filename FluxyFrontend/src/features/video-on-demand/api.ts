import { apiFetch, apiGet } from '../../utils/http'
import { apiUrl } from '../../utils/url'
import type { BulkOperationResponse, MessageResponse } from '../../types'
import type {
  VideoOnDemandDetail,
  VideoOnDemandImportResponse,
  VideoOnDemandList,
  VideoOnDemandStatus,
  VideoOnDemandTranscodeAvailability,
  VideoOnDemandType,
} from './types'

/**
 * The on-demand catalogue: one page of it, one video in full, the form's two writes, the
 * bulk run, the playlist import, the delivery URL and the transcoding surface.
 */

/**
 * The columns the table may be ordered by, as the query-string spelling.
 *
 * These are the endpoint's own sort fields. `releaseDate` is sent in that spelling and the
 * server lowercases it; `type` is a field the endpoint honours though no header offers it.
 */
export type VideoOnDemandSortField =
  | 'name'
  | 'category'
  | 'status'
  | 'type'
  | 'releaseDate'
  | 'rating'

export type VideoOnDemandSortOrder = 'asc' | 'desc'

/** The page size the pager offers, which is also the server's default. */
export const VIDEO_ON_DEMAND_PAGE_SIZE = 20

/**
 * The page size the "every on-demand video" picker asks for, and the server's ceiling.
 *
 * The stream form's VOD field is a picker over the whole catalogue rather than a page of it,
 * for the reason `ALL_SERIES_PAGE_SIZE` gives from the other catalogue: a pager inside a form
 * field would ask the operator to walk it before they could answer one question.
 */
export const ALL_VIDEO_ON_DEMAND_PAGE_SIZE = 1000

/** The largest playlist a browser will send before the server sees it. */
export const VIDEO_ON_DEMAND_IMPORT_MAX_FILE_BYTES = 10 * 1024 * 1024

/** The most entries one import accepts, as the endpoint's own limit. */
export const VIDEO_ON_DEMAND_IMPORT_MAX_ENTRIES = 5000

/** One page of the catalogue: `GET /admin/video-on-demand`. */
export const getVideoOnDemand = ({
  page,
  pageSize,
  search,
  categoryId,
  status,
  type,
  sortBy,
  sortOrder,
  signal,
}: {
  page: number
  pageSize: number
  search?: string
  categoryId?: string | null
  status?: VideoOnDemandStatus | null
  type?: VideoOnDemandType | null
  sortBy: VideoOnDemandSortField
  sortOrder: VideoOnDemandSortOrder
  signal?: AbortSignal
}): Promise<VideoOnDemandList> => {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
    sortBy,
    sortOrder,
  })

  if (search) query.set('search', search)
  if (categoryId) query.set('categoryId', categoryId)
  if (status) query.set('status', status)
  if (type) query.set('type', type)

  return apiGet<VideoOnDemandList>(`/admin/video-on-demand?${query.toString()}`, signal)
}

/** One video in full, for the edit form: `GET /admin/video-on-demand/{id}`. */
export const getVideoOnDemandDetail = (
  id: string,
  signal?: AbortSignal,
): Promise<VideoOnDemandDetail> =>
  apiGet<VideoOnDemandDetail>(`/admin/video-on-demand/${encodeURIComponent(id)}`, signal)

/**
 * Everything one form can decide about a video - the body of both the create and the change
 * endpoint.
 *
 * `rating` is a number in the form and in the create body, but the change endpoint takes it
 * as text so that an empty string can clear it while an absent value keeps it; the update
 * call converts it. `releaseDate` is a `yyyy-MM-dd` string on both ends, and an empty string
 * means "none" on create and "clear" on update.
 */
export interface VideoOnDemandValues {
  /** Display name, unique within its category and compared case sensitively. */
  name: string
  /** How the video is delivered. */
  type: VideoOnDemandType
  /** Whether the video is published or disabled. */
  status: VideoOnDemandStatus
  /** Identifier of the category the video is filed under, as text. */
  categoryId: string
  /** URL or file path the video is played from. */
  source: string
  /** Subtitles address, or an empty string. */
  subtitleUrl: string
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
  /** Upper case ISO 3166-1 alpha-2 country code, or an empty string. */
  countryCode: string
  /**
   * Bouquets the video belongs to, as identifiers.
   *
   * A **set** rather than an order: a bouquet the video joins takes it at the end of that
   * bouquet's list, and its position in a bouquet it already belonged to is the bouquet
   * screen's to decide. Always sent, so an empty array means "in no bouquet".
   */
  bouquetIds: string[]
}

/** Creates a video: `POST /admin/video-on-demand`, answered with `201` and a `Location`. */
export const createVideoOnDemand = ({
  values,
  csrfToken,
}: {
  values: VideoOnDemandValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/admin/video-on-demand', {
    method: 'POST',
    body: values,
    csrfToken,
  })

/**
 * Changes a video: `PATCH /admin/video-on-demand/{id}`.
 *
 * The whole form travels rather than a diff, for the reason the stream form does: one screen
 * holds one video, so what the operator sees and what the row becomes are the same
 * statement. An empty string clears a text field; `rating` travels as text for the same
 * reason, because the server reads `null` as "keep" and `""` as "clear".
 */
export const updateVideoOnDemand = ({
  id,
  values,
  csrfToken,
}: {
  id: string
  values: VideoOnDemandValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/video-on-demand/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body: {
      ...values,
      rating: values.rating === null ? '' : String(values.rating),
    },
    csrfToken,
  })

/**
 * Publishes or hides one video: `PATCH /admin/video-on-demand/{id}`.
 *
 * Only the status travels. The change endpoint reads an absent field as "keep", so this is
 * the same write the edit form makes without carrying the whole row back to the server.
 */
export const setVideoOnDemandStatus = ({
  id,
  status,
  csrfToken,
}: {
  id: string
  status: VideoOnDemandStatus
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/video-on-demand/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body: { status },
    csrfToken,
  })

/** Deletes a video: `DELETE /admin/video-on-demand/{id}`. */
export const deleteVideoOnDemand = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/video-on-demand/${encodeURIComponent(id)}`, {
    method: 'DELETE',
    csrfToken,
  })

/** The operations one bulk request may ask for, spelled the way the endpoint parses them. */
export type BulkVideoOnDemandAction =
  | 'change-status'
  | 'move-category'
  | 'change-type'
  | 'delete'

/** The body of `POST /admin/video-on-demand/bulk`. */
export interface BulkVideoOnDemandPayload {
  action: BulkVideoOnDemandAction
  ids: string[]
  categoryId?: string
  status?: VideoOnDemandStatus
  type?: VideoOnDemandType
}

/** Applies one operation to a selection: `POST /admin/video-on-demand/bulk`. */
export const bulkVideoOnDemand = ({
  payload,
  csrfToken,
}: {
  payload: BulkVideoOnDemandPayload
  csrfToken: string | null
}): Promise<BulkOperationResponse> =>
  apiFetch('/admin/video-on-demand/bulk', {
    method: 'POST',
    body: payload,
    csrfToken,
  })

/**
 * The settings one playlist import applies to every video it creates.
 *
 * There is no name and no source: each `#EXTINF` entry names its own video and carries its
 * own address. Both delivery kinds accept one address, so both are offered.
 */
export interface VideoOnDemandImportValues {
  /** How every imported video is delivered. */
  type: VideoOnDemandType
  /** Whether every imported video opens published or disabled. */
  status: VideoOnDemandStatus
  /** Identifier of the category the videos are filed under. */
  categoryId: string
  /**
   * Bouquets every stream this import creates joins, as identifiers.
   *
   * Applied to the rows the import **creates**, not to the ones it skips or appends to: those
   * already exist, and an import is not an edit of a package somebody else arranged. Sent as
   * `bouquetIds`, once per bouquet - the one field a multipart form repeats for a list.
   */
  bouquetIds: string[]}

/**
 * Imports videos from an uploaded M3U playlist:
 * `POST /admin/video-on-demand/import`, multipart/form-data.
 *
 * The body is `FormData` rather than JSON, which is why `http.ts` leaves the content type
 * unset for it - the browser writes the multipart boundary itself.
 */
export const importVideoOnDemand = ({
  values,
  file,
  csrfToken,
}: {
  values: VideoOnDemandImportValues
  file: File
  csrfToken: string | null
}): Promise<VideoOnDemandImportResponse> => {
  const body = new FormData()

  body.set('type', values.type)
  body.set('status', values.status)
  body.set('categoryId', values.categoryId)

  // Once per bouquet: a multipart form has no array, so a repeated field is the list.
  for (const bouquetId of values.bouquetIds ?? []) {
    body.append('bouquetIds', bouquetId)
  }
  body.set('playlist', file)

  return apiFetch('/admin/video-on-demand/import', {
    method: 'POST',
    body,
    csrfToken,
  })
}

/**
 * Whether this installation can transcode, and the formats it offers:
 * `GET /admin/video-on-demand/transcode-formats`.
 *
 * A read behind the view permission, so no antiforgery token. The table asks it once and
 * draws the action disabled with the reason while `available` is false.
 */
export const getVideoOnDemandTranscodeFormats = (
  signal?: AbortSignal,
): Promise<VideoOnDemandTranscodeAvailability> =>
  apiGet<VideoOnDemandTranscodeAvailability>(
    '/admin/video-on-demand/transcode-formats',
    signal,
  )

/**
 * Asks for one video to be converted to one format:
 * `POST /admin/video-on-demand/{id}/transcodes`.
 *
 * This pass has no encoder, so the server answers `503 transcode_not_configured`. The call
 * exists so the menu is wired to the endpoint that will answer when one arrives, and it is
 * drawn only while `available` is true.
 */
export const requestVideoOnDemandTranscode = ({
  id,
  format,
  csrfToken,
}: {
  id: string
  format: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/video-on-demand/${encodeURIComponent(id)}/transcodes`, {
    method: 'POST',
    body: { format },
    csrfToken,
  })

/**
 * The address the player and a browser use to fetch the video:
 * `GET /admin/video-on-demand/{id}/play`.
 *
 * A `Relay` item is served by this installation (a local file with range support, or a
 * remote address streamed through this process); a `Redirect` item answers 302 and the
 * browser follows it. One URL covers both because the server owns the difference.
 */
export const playVideoOnDemandUrl = (id: string): string =>
  apiUrl(`/admin/video-on-demand/${encodeURIComponent(id)}/play`)
