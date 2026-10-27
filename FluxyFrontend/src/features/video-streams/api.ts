import { apiFetch, apiGet } from '../../utils/http'
import type { BulkOperationResponse, MessageResponse } from '../../types'
import type {
  VideoStreamDetail,
  VideoStreamImportResponse,
  VideoStreamList,
  VideoStreamStatus,
  VideoStreamType,
} from './types'

/**
 * The streams table: one page of it, one stream in full, the body both form submissions send,
 * the two transitions that start and stop a broadcast, and the delete that takes sources and
 * files with it.
 *
 * `status` travels in both form bodies: the add form starts a new stream active, and the edit
 * form can enable or disable an existing one. The start and stop actions remain the two
 * transitions on the table.
 */

/**
 * The column the streams table may be ordered by, as the query-string spelling.
 *
 * These four *are* the endpoint's sort fields, so the column keys of the table are these
 * values and no mapping table exists to get wrong - the same trick the accounts table uses
 * with `lastSeenAt`. `id` and `actions` deliberately have no member: the server has no
 * order for either, and a header that offered one would answer `validation_failed`.
 */
export type VideoStreamSortField = 'name' | 'category' | 'status' | 'type'

export type VideoStreamSortOrder = 'asc' | 'desc'

/**
 * The page size the streams pager offers, which is also the server's default.
 *
 * Kept in step by hand because the two are one contract written twice - and because a page
 * size the pager draws from has to be known before the first answer arrives, or the pager
 * would show one row per page until it did. The server's ceiling is 1000; the shared pager
 * offers 500 and 1000 as its two largest choices, so a "select everything" selection can
 * cover more than a hundred rows in one bulk request.
 */
export const VIDEO_STREAMS_PAGE_SIZE = 20

/**
 * What one uploaded playlist may hold, mirroring `VideoStreamImportLimits` on the server.
 *
 * The size is checked in the browser before the request so a mistaken upload is refused
 * without sending ten megabytes over the wire; the server checks it again, and its 413 is
 * the answer that actually counts. The entry ceiling is shown in the field's hint for the
 * same reason - it is a fact about the endpoint the operator should read before choosing a
 * file, not after being refused one.
 */
export const VIDEO_STREAM_IMPORT_MAX_FILE_BYTES = 10 * 1024 * 1024
export const VIDEO_STREAM_IMPORT_MAX_ENTRIES = 5000

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
 * `status` is the one field the start and stop buttons also write, and it belongs in the
 * body because the add form chooses whether the stream opens active and the edit form can
 * enable or disable it later. There is no position here: the order a stream is shown in
 * belongs to the list that shows it, not to the stream row itself.
 */
export interface VideoStreamValues {
  /** Display name, unique within its category and compared case sensitively. */
  name: string
  /** The kind of stream, which decides which payload below is used. */
  type: VideoStreamType
  /** Whether the stream is active or disabled, as the enum member. */
  status: VideoStreamStatus
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
  /**
   * Bouquets the stream belongs to, as identifiers.
   *
   * A **set** rather than an order: the picker says "these are the packages this stream is
   * in" and nothing about where it sits inside one. A bouquet the stream joins is appended to
   * the end of that bouquet's list, and its position in a bouquet it already belonged to is
   * the bouquet screen's to decide. Always sent - the form never means "leave the field
   * alone" - so an empty array is the statement "in no bouquet".
   */
  bouquetIds: string[]
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
 * There is no `sortOrder` in the body: the order a stream is shown in belongs to the list
 * that shows it rather than to the row, so the request has no position to name.
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
 * `status` is sent like every other field: the form holds the state the operator chose, and
 * leaving the field out would keep whatever the row already holds rather than saving the
 * screen.
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
 * Marks a stream as broadcasting again: `POST /admin/video-streams/{id}/restart`, answered
 * with `200 video_stream_restarted`.
 *
 * A third transition rather than a stop followed by a start: the row is one request, the
 * answer is one code, and a client that stopped first and failed to start second would leave
 * a stream it was told nothing about. Like the two beside it, it writes `status` and nothing
 * else - the relaying process reads the same row.
 */
export const restartVideoStream = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/video-streams/${encodeURIComponent(id)}/restart`, {
    method: 'POST',
    csrfToken,
  })

/**
 * The operations one bulk request may ask for, spelled the way the endpoint parses them.
 *
 * The list matches `BulkVideoStreamAction` on the server. `restart` sits beside start and
 * stop because the row action and the bulk menu name the same transition; the other six are
 * edits, and each one carries its own value in `BulkVideoStreamsPayload`.
 */
export type BulkVideoStreamAction =
  | 'start'
  | 'stop'
  | 'restart'
  | 'delete'
  | 'move-category'
  | 'change-type'
  | 'change-delay'
  | 'change-archive'
  | 'change-restream'

/**
 * The only kinds a bulk type change may choose.
 *
 * A bulk change cannot invent the payload the other three kinds need - a series reference, an
 * on-demand reference or a file list - so offering them would be a menu entry whose every row
 * is refused. The single-row form still offers all six, because it can collect the payload.
 */
export type BulkVideoStreamType = Extract<
  VideoStreamType,
  'Permanent' | 'OnDemand' | 'Redirect'
>

/**
 * The body of `POST /admin/video-streams/bulk`.
 *
 * One value beside the action rather than a patch of twenty rows: a bulk edit changes the one
 * thing the operator chose and leaves every other field of every row alone. The server judges
 * the value against each row's own kind and refuses only the rows it cannot carry - a
 * category move that collides with a name already used there refuses that row alone, and a
 * delay change on a non-permanent stream refuses that row with `validation_failed`.
 *
 * `restream*` is a whole-list replacement, not a diff: a blank string clears its setting, so
 * "the restream settings are these now" and "clear them" are the same request shape.
 */
export interface BulkVideoStreamsPayload {
  action: BulkVideoStreamAction
  ids: string[]
  categoryId?: string
  type?: BulkVideoStreamType
  delayMinutes?: number
  archiveDays?: number
  restreamUserAgent?: string
  restreamProxy?: string
  restreamCookies?: string
  restreamHeaders?: string
}

/**
 * Applies one operation to a selection: `POST /admin/video-streams/bulk`.
 *
 * **Always a `200`, and the per-row answers live in `items`** - the same decision the accounts
 * and groups bulk endpoints make. A run that changed four rows and refused one is one request
 * that was carried out, and folding the worst row's status into the response would make it
 * indistinguishable from a run that changed nothing.
 *
 * A write, so the antiforgery token is minted at call time and never cached. No captcha for
 * the reason every other admin write gives: the session, the `editStreams` permission and a
 * fresh antiforgery pair are the gate.
 */
export const bulkVideoStreams = ({
  payload,
  csrfToken,
}: {
  payload: BulkVideoStreamsPayload
  csrfToken: string | null
}): Promise<BulkOperationResponse> =>
  apiFetch('/admin/video-streams/bulk', {
    method: 'POST',
    body: payload,
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

/**
 * The kinds an imported stream may be: the three that take their addresses from a list.
 *
 * The other three need a reference or a file list that a playlist cannot supply, so offering
 * them on the import form would be a choice whose every entry the service has to invent a
 * payload for. The server accepts exactly these three from the multipart field.
 */
export type VideoStreamImportType = Extract<
  VideoStreamType,
  'Permanent' | 'OnDemand' | 'Redirect'
>

/**
 * The settings one import applies to every stream it creates.
 *
 * There is no name, no sources list and no identifier pair: each `#EXTINF` entry names its
 * own stream and carries its own address, so those are read from the file rather than typed
 * here. What remains is the frame every created row shares - where it is filed, whether it
 * starts active, and the settings a kind may carry.
 */
export interface VideoStreamImportValues {
  /** The kind every imported stream is created as. */
  type: VideoStreamImportType
  /** Whether the created streams start active or disabled. */
  status: VideoStreamStatus
  /** Identifier of the category the streams are filed under. */
  categoryId: string
  /** How far behind live the relay runs, in minutes. */
  delayMinutes: number
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
  /** Append the entry's source to an existing stream's source list. */
  appendSourceToExisting: boolean
  /** Overwrite an existing stream's status, type, delay, archive and restream settings. */
  overwriteExistingSettings: boolean
}

/**
 * Imports streams from an uploaded M3U playlist:
 * `POST /admin/video-streams/import`, multipart/form-data.
 *
 * One `#EXTINF` entry and the address under it become one stream. Existing names are skipped
 * unless the operator opts in - `appendSourceToExisting` appends the entry's source to an
 * existing stream's source list, and `overwriteExistingSettings` overwrites its status, type,
 * delay, archive length and restream settings. Both may be used together.
 *
 * The body is `FormData` rather than JSON, which is why `http.ts` leaves the content type
 * unset for it - the browser writes the multipart boundary itself.
 */
export const importVideoStreams = ({
  values,
  file,
  csrfToken,
}: {
  values: VideoStreamImportValues
  file: File
  csrfToken: string | null
}): Promise<VideoStreamImportResponse> => {
  const body = new FormData()

  body.set('type', values.type)
  body.set('status', values.status)
  body.set('categoryId', values.categoryId)
  body.set('delayMinutes', String(values.delayMinutes))
  body.set('archiveDays', String(values.archiveDays))
  body.set('restreamUserAgent', values.restreamUserAgent)
  body.set('restreamProxy', values.restreamProxy)
  body.set('restreamCookies', values.restreamCookies)
  body.set('restreamHeaders', values.restreamHeaders)
  body.set('appendSourceToExisting', String(values.appendSourceToExisting))
  body.set('overwriteExistingSettings', String(values.overwriteExistingSettings))
  body.set('playlist', file)

  return apiFetch('/admin/video-streams/import', {
    method: 'POST',
    body,
    csrfToken,
  })
}
