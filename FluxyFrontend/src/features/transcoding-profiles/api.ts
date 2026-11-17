import { apiFetch, apiGet } from '../../utils/http'
import type { MessageResponse } from '../../types'
import type {
  TranscodingProfile,
  TranscodingProfileList,
  TranscodingProfileOption,
} from './types'

/**
 * The transcoding profiles: one page of the table, one profile in full, the picker the four
 * content forms draw, and the three writes.
 *
 * The name is the only sortable column, because every other field is an optional ffmpeg option
 * whose order a reader does not compare down a table. The options read is a separate endpoint
 * with a separate grant: choosing a profile while editing a stream is part of editing the
 * stream, and an operator who may run the streams has no business browsing the profiles table.
 */

/** The column the table may be ordered by, as the query-string spelling. */
export type TranscodingProfileSortField = 'name'

export type TranscodingProfileSortOrder = 'asc' | 'desc'

/** The page size the pager opens with, which is also the server's default. */
export const TRANSCODING_PROFILES_PAGE_SIZE = 20

/** The name limit, mirrored from the server so the form can refuse before the wire. */
export const TRANSCODING_PROFILE_NAME_MAX = 100

/** The aspect-ratio limit, mirrored from the server. */
export const TRANSCODING_PROFILE_ASPECT_RATIO_MAX = 32

/** The watermark address limit, mirrored from the server. */
export const TRANSCODING_PROFILE_LOGO_URL_MAX = 2048

/** The CRF window ffmpeg accepts. */
export const TRANSCODING_PROFILE_CRF_MIN = 0
export const TRANSCODING_PROFILE_CRF_MAX = 51

/** Everything the profile form collects - the body of both writes. */
export interface TranscodingProfileValues {
  /** Display name. */
  name: string
  /** Video encoder wire name. */
  videoCodec: string
  /** Audio encoder wire name. */
  audioCodec: string
  /** Preset wire name. */
  preset: string
  /** H.264 profile wire name, or null. */
  videoProfile: string | null
  /** Average video bitrate, or null. */
  videoBitrateAvg: number | null
  /** Average audio bitrate, or null. */
  audioBitrateAvg: number | null
  /** Lower bitrate tolerance, or null. */
  minBitrateTolerance: number | null
  /** Upper bitrate tolerance, or null. */
  maxBitrateTolerance: number | null
  /** Decoder buffer size, or null. */
  bufferSize: number | null
  /** Constant rate factor, or null. */
  crf: number | null
  /** Output width, or null. */
  width: number | null
  /** Output height, or null. */
  height: number | null
  /** Aspect ratio override, or null. */
  aspectRatio: string | null
  /** Output frame rate, or null. */
  framerate: number | null
  /** Audio sample rate, or null. */
  audioSampleRate: number | null
  /** Audio channel count, or null. */
  audioChannels: number | null
  /** Encoder thread count, or null. */
  threads: number | null
  /** Watermark address, or null. */
  logoUrl: string | null
  /** Redaction left edge, or null. */
  redactionX: number | null
  /** Redaction top edge, or null. */
  redactionY: number | null
  /** Redaction width, or null. */
  redactionWidth: number | null
  /** Redaction height, or null. */
  redactionHeight: number | null
}

/**
 * The body both writes send.
 *
 * `undefined` becomes `null` explicitly rather than being dropped by JSON: the form's controls
 * are optional, and an option the operator cleared must arrive as `null` so the column clears.
 * The four non-null columns - the name, the two codecs and the preset - always carry a value,
 * because their controls are required.
 */
const toBody = (values: TranscodingProfileValues) => ({
  name: (values.name ?? '').trim(),
  videoCodec: values.videoCodec,
  audioCodec: values.audioCodec,
  preset: values.preset,
  videoProfile: values.videoProfile ?? null,
  videoBitrateAvg: values.videoBitrateAvg ?? null,
  audioBitrateAvg: values.audioBitrateAvg ?? null,
  minBitrateTolerance: values.minBitrateTolerance ?? null,
  maxBitrateTolerance: values.maxBitrateTolerance ?? null,
  bufferSize: values.bufferSize ?? null,
  crf: values.crf ?? null,
  width: values.width ?? null,
  height: values.height ?? null,
  aspectRatio: values.aspectRatio?.trim() || null,
  framerate: values.framerate ?? null,
  audioSampleRate: values.audioSampleRate ?? null,
  audioChannels: values.audioChannels ?? null,
  threads: values.threads ?? null,
  logoUrl: values.logoUrl?.trim() || null,
  redactionX: values.redactionX ?? null,
  redactionY: values.redactionY ?? null,
  redactionWidth: values.redactionWidth ?? null,
  redactionHeight: values.redactionHeight ?? null,
})

/**
 * One page of the table: `GET /admin/transcoding-profiles`.
 *
 * A plain read behind the admin session, so no antiforgery token - nothing is changed. The
 * page, the filter and the order are the server's because the pager draws a `total`.
 */
export const getTranscodingProfiles = ({
  page,
  pageSize,
  search,
  sortBy,
  sortOrder,
  signal,
}: {
  page: number
  pageSize: number
  search?: string
  sortBy: TranscodingProfileSortField
  sortOrder: TranscodingProfileSortOrder
  signal?: AbortSignal
}): Promise<TranscodingProfileList> => {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
    sortBy,
    sortOrder,
  })

  if (search) query.set('search', search)

  return apiGet<TranscodingProfileList>(`/admin/transcoding-profiles?${query.toString()}`, signal)
}

/** One profile in full, for the edit form: `GET /admin/transcoding-profiles/{id}`. */
export const getTranscodingProfile = (
  id: string,
  signal?: AbortSignal,
): Promise<TranscodingProfile> =>
  apiGet<TranscodingProfile>(`/admin/transcoding-profiles/${encodeURIComponent(id)}`, signal)

/**
 * Every profile as a picker entry: `GET /admin/transcoding-profiles/options`.
 *
 * Gated by any of the four content-view grants rather than by the profile pair, because the
 * four content forms are the only callers - see the endpoint's own remarks.
 */
export const getTranscodingProfileOptions = (
  signal?: AbortSignal,
): Promise<TranscodingProfileOption[]> =>
  apiGet<TranscodingProfileOption[]>('/admin/transcoding-profiles/options', signal)

/**
 * Creates a profile: `POST /admin/transcoding-profiles`, answered with `201` and a `Location`.
 *
 * A name another profile holds is `409 transcoding_profile_already_exists` with the field named
 * in `errors`, so the form shows it under the input rather than as a toast.
 */
export const createTranscodingProfile = ({
  values,
  csrfToken,
}: {
  values: TranscodingProfileValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/admin/transcoding-profiles', {
    method: 'POST',
    body: toBody(values),
    csrfToken,
  })

/**
 * Changes a profile: `PATCH /admin/transcoding-profiles/{id}`, answered with `200`.
 *
 * The whole form travels rather than a diff, because the form holds one profile and an option
 * the operator cleared must clear the column - a field left out of the body would keep it.
 */
export const updateTranscodingProfile = ({
  id,
  values,
  csrfToken,
}: {
  id: string
  values: TranscodingProfileValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/transcoding-profiles/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body: toBody(values),
    csrfToken,
  })

/**
 * Deletes a profile: `DELETE /admin/transcoding-profiles/{id}`, answered with `200`.
 *
 * A profile a stream, a video or a series still points at answers
 * `409 transcoding_profile_in_use` and survives - the row is a reference the operator has to
 * clear first, not a mistake to retry.
 */
export const deleteTranscodingProfile = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/transcoding-profiles/${encodeURIComponent(id)}`, { method: 'DELETE', csrfToken })
