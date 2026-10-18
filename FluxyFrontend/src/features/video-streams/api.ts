import { apiFetch, apiGet } from '../../lib/http'
import type { MessageResponse } from '../../types'
import type { VideoStreamDetail, VideoStreamList, VideoStreamStatus, VideoStreamType } from './types'

/**
 * The streams table: one page of it, one stream in full, the body both form submissions send,
 * the two transitions that start and stop a broadcast, and the delete that takes sources and
 * files with it.
 *
 * `status` is deliberately absent from the form's body - the two transitions write it, so
 * "this stream is broadcasting" stays a fact about a process rather than a field an edit can
 * set.
 */

/**
 * The column the streams table may be ordered by, as the query-string spelling.
 *
 * These five *are* the endpoint's sort fields, so the column keys of the table are these
 * values and no mapping table exists to get wrong - the same trick the accounts table uses
 * with `lastSeenAt`. `id` and `actions` deliberately have no member: the server has no
 * order for either, and a header that offered one would answer `validation_failed`.
 *
 * `sortOrder` rather than `sortorder`: the server lower-cases before matching, so the
 * conventional camelCase spelling and the enum member's own name are the same request.
 */
export type VideoStreamSortField = 'name' | 'category' | 'status' | 'type' | 'sortOrder'

export type VideoStreamSortOrder = 'asc' | 'desc'

/**
 * The page size the streams pager offers, which is also the server's default.
 *
 * Kept in step by hand because the two are one contract written twice - and because a page
 * size the pager draws from has to be known before the first answer arrives, or the pager
 * would show one row per page until it did. The server's ceiling is 100, which is why the
 * picker below offers no more than that: asking for 500 comes back `validation_failed`.
 */
export const VIDEO_STREAMS_PAGE_SIZE = 20

/** One page of the streams table: `GET /admin/video-streams`. */
export const getVideoStreams = ({
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
  status?: VideoStreamStatus | null
  type?: VideoStreamType | null
  sortBy: VideoStreamSortField
  sortOrder: VideoStreamSortOrder
  signal?: AbortSignal
}): Promise<VideoStreamList> => {
  // Built with `URLSearchParams` for the reason the session history's own query is: a term
  // containing `+` or `&` is searched for rather than reinterpreted, and the four optional
  // filters are simply left off instead of being sent as the string "null".
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

  return apiGet<VideoStreamList>(`/admin/video-streams?${query.toString()}`, signal)
}

/**
 * One stream in full, for the edit form: `GET /admin/video-streams/{id}`.
 *
 * A plain GET behind the admin session, so no antiforgery token - nothing is changed. The
 * answer is `404 video_stream_not_found` for an id nobody answers to, which is what lets the
 * form draw a refusal rather than an empty page after a reload.
 */
export const getVideoStream = (id: string, signal?: AbortSignal): Promise<VideoStreamDetail> =>
  apiGet<VideoStreamDetail>(`/admin/video-streams/${encodeURIComponent(id)}`, signal)

/**
 * Everything one form can decide about a stream - the body of both the create and the
 * change endpoint, which is why it is one shape rather than two.
 *
 * Every text field is a string, possibly empty, and never `undefined`: an empty string
 * means "no value" to the server on both ends (on create an id of `""` is *none*, on a
 * change it *clears*), so one rule covers both requests and there is no third state to
 * remember. The two lists always travel, empty when the chosen kind of stream does not use
 * them - the server drops what the type cannot carry rather than refusing it, which is
 * what makes a form that has just changed type safe to save.
 *
 * There is deliberately no `status` here. It belongs to the start and stop actions, and a
 * body that could set it would make "this stream is broadcasting" a claim an edit form
 * makes rather than a fact about a process. `sortOrder` is absent for the same reason the
 * add-category form has no position: where a new stream goes in its category is the
 * service's fact to read, and a caller naming one would be one stale screen away from
 * overwriting a neighbour an operator has just moved.
 */
export interface VideoStreamValues {
  /** Display name, unique within its category and compared case sensitively. */
  name: string
  /** The kind of stream, which decides which payload below is used. */
  type: VideoStreamType
  /** Identifier of the category the stream is filed under, as text. */
  categoryId: string
  /** Address the icon is fetched from. */
  logoUrl: string
  /** Enigma2 service reference as text. */
  enigma2Sid: string
  /** How far behind live the relay runs, in minutes. */
  delayMinutes: number
  /** Series reference, for `OnlineSeries` and nothing else. */
  seriesId: string
  /** On-demand reference, for `VodPlaylist` and nothing else. */
  vodId: string
  /** Programme guide reference. */
  epgSourceId: string
  /** Key the channel is filed under inside that guide. */
  epgChannelKey: string
  /** Archive length in days, refused above zero on anything but `Permanent`. */
  archiveDays: number
  /** User agent the relayed requests carry. */
  restreamUserAgent: string
  /** Proxy the relay goes out through. */
  restreamProxy: string
  /** Cookies the relayed requests carry. */
  restreamCookies: string
  /** Extra headers the relayed requests carry, as typed. */
  restreamHeaders: string
  /** Sources to relay from, in order, first as the primary. */
  sources: string[]
  /** Files to play, in order. */
  files: string[]
}

/**
 * Creates a stream: `POST /admin/video-streams`, answered with `201
 * video_stream_created` and a `Location` naming it.
 *
 * A write, so the antiforgery token is minted at call time and never cached. There is no
 * captcha on this endpoint on purpose: the caller already holds an admin session *and*
 * `editStreams`, which together are a stronger gate than a score from reCAPTCHA (the
 * backend's recorded departure for the whole admin area).
 *
 * A name another stream of the same category holds is `409 video_stream_already_exists`
 * with the field named in `errors`, so the form shows it under the input. The same name
 * over a *different* category is a different stream and is accepted - two sections of two
 * different screens may each be called "News".
 *
 * There is no `sortOrder` in the body: a new stream joins the end of its category's list,
 * and where "the end" is (the highest position of that category, plus one) is the server's
 * fact to read.
 */
export const createVideoStream = ({
  values,
  csrfToken,
}: {
  values: VideoStreamValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/admin/video-streams', { method: 'POST', body: values, csrfToken })

/**
 * Changes a stream: `PATCH /admin/video-streams/{id}`, answered with
 * `200 video_stream_updated`.
 *
 * The whole form travels rather than a diff of it, for the reason the group form sends its
 * whole permission set: this is one screen holding one stream, so "what the operator sees"
 * and "what the row becomes" are the same statement, and a diff would be two sources for
 * one fact. An empty string clears a field - `null` would mean "leave it alone", and the
 * form never means that.
 *
 * `status` is absent and the server refuses it: only start and stop write it.
 */
export const updateVideoStream = ({
  id,
  values,
  csrfToken,
}: {
  id: string
  values: VideoStreamValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/video-streams/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body: values,
    csrfToken,
  })

/**
 * Marks a stream as broadcasting: `POST /admin/video-streams/{id}/start`, answered with
 * `200 video_stream_started`.
 *
 * An operation on a row rather than a field write, which is why it is its own path: the
 * transition's whole meaning - "this stream should now relay" - would otherwise have to
 * live inside a caller's payload, and a `PATCH {status}` would be a field an edit form
 * could set. Starting a stream that is already running is a success rather than a
 * complaint, so a double click is not a failure.
 *
 * The request carries no body of its own - the path is what names the stream.
 */
export const startVideoStream = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/video-streams/${encodeURIComponent(id)}/start`, {
    method: 'POST',
    csrfToken,
  })

/** Marks a stream as no longer broadcasting. The mirror of `startVideoStream`. */
export const stopVideoStream = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/video-streams/${encodeURIComponent(id)}/stop`, {
    method: 'POST',
    csrfToken,
  })

/**
 * Deletes a stream together with its sources and its files:
 * `DELETE /admin/video-streams/{id}`, answered with `200 video_stream_deleted`.
 *
 * The only action on the table that asks first, because it is the only one that cannot be
 * undone. The dialog names the stream: this is the one row whose name the visitor is
 * reading when they press it, and a body warning about categories would be about a thing
 * this page has not introduced - the category's *own* delete is the one that refuses while
 * streams still file under it.
 */
export const deleteVideoStream = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/video-streams/${encodeURIComponent(id)}`, { method: 'DELETE', csrfToken })
