import { apiFetch, apiGet } from '../../utils/http'
import type { BulkOperationResponse, MessageResponse } from '../../types'
import type {
  AudioStreamDetail,
  AudioStreamImportResponse,
  AudioStreamList,
  AudioStreamStatus,
  AudioStreamType,
} from './types'

/**
 * The audio streams table: one page of it, one stream in full, the body both form
 * submissions send, the three transitions that start, stop and restart a broadcast, and the
 * delete that takes the sources with it.
 *
 * `status` travels in both form bodies: the add form starts a new stream active, and the
 * edit form can enable or disable an existing one. The start, stop and restart actions
 * remain the transitions on the table.
 */

/**
 * The column the audio streams table may be ordered by, as the query-string spelling.
 *
 * These four *are* the endpoint's sort fields, so the column keys of the table are these
 * values and no mapping table exists to get wrong - the same trick the accounts table uses
 * with `lastSeenAt`. `id` and `actions` deliberately have no member: the server has no
 * order for either, and a header that offered one would answer `validation_failed`.
 */
export type AudioStreamSortField = 'name' | 'category' | 'status' | 'type'

export type AudioStreamSortOrder = 'asc' | 'desc'

/**
 * The page size the audio streams pager offers, which is also the server's default.
 *
 * Kept in step by hand because the two are one contract written twice - and because a page
 * size the pager draws from has to be known before the first answer arrives, or the pager
 * would show one row per page until it did. The server's ceiling is 1000; the shared pager
 * offers 500 and 1000 as its two largest choices, so a "select everything" selection can
 * cover more than a hundred rows in one bulk request.
 */
export const AUDIO_STREAMS_PAGE_SIZE = 20

/**
 * What one uploaded playlist may hold, mirroring `AudioStreamImportLimits` on the server.
 *
 * The size is checked in the browser before the request so a mistaken upload is refused
 * without sending ten megabytes over the wire; the server checks it again, and its 413 is
 * the answer that actually counts. The entry ceiling is shown in the field's hint for the
 * same reason - it is a fact about the endpoint the operator should read before choosing a
 * file, not after being refused one.
 */
export const AUDIO_STREAM_IMPORT_MAX_FILE_BYTES = 10 * 1024 * 1024
export const AUDIO_STREAM_IMPORT_MAX_ENTRIES = 5000

/** One page of the audio streams table: `GET /admin/audio-streams`. */
export const getAudioStreams = ({
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
  status?: AudioStreamStatus | null
  type?: AudioStreamType | null
  sortBy: AudioStreamSortField
  sortOrder: AudioStreamSortOrder
  signal?: AbortSignal
}): Promise<AudioStreamList> => {
  // Built with `URLSearchParams` for the reason the session history's own query is: a term
  // containing `+` or `&` is searched for rather than reinterpreted, and the three optional
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

  return apiGet<AudioStreamList>(`/admin/audio-streams?${query.toString()}`, signal)
}

/**
 * One stream in full, for the edit form: `GET /admin/audio-streams/{id}`.
 *
 * A plain GET behind the admin session, so no antiforgery token - nothing is changed. The
 * answer is `404 audio_stream_not_found` for an id nobody answers to, which is what lets the
 * form draw a refusal rather than an empty page after a reload.
 */
export const getAudioStream = (id: string, signal?: AbortSignal): Promise<AudioStreamDetail> =>
  apiGet<AudioStreamDetail>(`/admin/audio-streams/${encodeURIComponent(id)}`, signal)

/**
 * Everything one form can decide about an audio stream - the body of both the create and the
 * change endpoint, which is why it is one shape rather than two.
 *
 * Every text field is a string, possibly empty, and never `undefined`: an empty string
 * means "no value" to the server on both ends (on create an id of `""` is *none*, on a
 * change it *clears*), so one rule covers both requests and there is no third state to
 * remember. The source list always travels, because both audio kinds relay from one - there
 * is no payload a kind can switch off, which is the whole difference from a video stream.
 *
 * `status` is the one field the start and stop buttons also write, and it belongs in the
 * body because the add form chooses whether the stream opens active and the edit form can
 * enable or disable it later.
 */
export interface AudioStreamValues {
  /** Display name, unique within its category and compared case sensitively. */
  name: string
  /** The kind of stream, which decides how it is played. */
  type: AudioStreamType
  /** Whether the stream is active or disabled, as the enum member. */
  status: AudioStreamStatus
  /** Identifier of the category the stream is filed under, as text. */
  categoryId: string
  /** Address the icon is fetched from. */
  logoUrl: string
  /** Enigma2 service reference as text. */
  enigma2Sid: string
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
  /**
   * Bouquets the stream belongs to, as identifiers.
   *
   * A **set** rather than an order: a bouquet the stream joins takes it at the end of that
   * bouquet's list, and its position in a bouquet it already belonged to is the bouquet
   * screen's to decide. Always sent, so an empty array means "in no bouquet".
   */
  bouquetIds: string[]
}

/**
 * Creates an audio stream: `POST /admin/audio-streams`, answered with `201
 * audio_stream_created` and a `Location` naming it.
 *
 * A write, so the antiforgery token is minted at call time and never cached. There is no
 * captcha on this endpoint on purpose: the caller already holds an admin session *and*
 * `editAudioStreams`, which together are a stronger gate than a score from reCAPTCHA (the
 * backend's recorded departure for the whole admin area).
 *
 * A name another stream of the same category holds is `409 audio_stream_already_exists`
 * with the field named in `errors`, so the form shows it under the input. The same name
 * over a *different* category is a different stream and is accepted - two sections of two
 * different screens may each be called "News".
 */
export const createAudioStream = ({
  values,
  csrfToken,
}: {
  values: AudioStreamValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/admin/audio-streams', { method: 'POST', body: values, csrfToken })

/**
 * Changes an audio stream: `PATCH /admin/audio-streams/{id}`, answered with
 * `200 audio_stream_updated`.
 *
 * The whole form travels rather than a diff of it, for the reason the group form sends its
 * whole permission set: this is one screen holding one stream, so "what the operator sees"
 * and "what the row becomes" are the same statement, and a diff would be two sources for
 * one fact. An empty string clears a field - `null` would mean "leave it alone", and the
 * form never means that.
 *
 * The source list travels the same way: a list with entries replaces the stored one in the
 * order it is named, and an empty one is refused - both kinds relay from a list, so a
 * stream with none could never play.
 */
export const updateAudioStream = ({
  id,
  values,
  csrfToken,
}: {
  id: string
  values: AudioStreamValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/audio-streams/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body: values,
    csrfToken,
  })

/**
 * Marks an audio stream as broadcasting: `POST /admin/audio-streams/{id}/start`, answered
 * with `200 audio_stream_started`.
 *
 * An operation on a row rather than a field write, which is why it is its own path: the
 * transition's whole meaning - "this stream should now relay" - would otherwise have to
 * live inside a caller's payload, and a `PATCH {status}` would be a field an edit form
 * could set. Starting a stream that is already running is a success rather than a
 * complaint, so a double click is not a failure.
 *
 * The request carries no body of its own - the path is what names the stream.
 */
export const startAudioStream = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/audio-streams/${encodeURIComponent(id)}/start`, {
    method: 'POST',
    csrfToken,
  })

/** Marks an audio stream as no longer broadcasting. The mirror of `startAudioStream`. */
export const stopAudioStream = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/audio-streams/${encodeURIComponent(id)}/stop`, {
    method: 'POST',
    csrfToken,
  })

/**
 * Marks an audio stream as broadcasting again: `POST /admin/audio-streams/{id}/restart`,
 * answered with `200 audio_stream_restarted`.
 *
 * A third transition rather than a stop followed by a start: the row is one request, the
 * answer is one code, and a client that stopped first and failed to start second would leave
 * a stream it was told nothing about. Like the two beside it, it writes `status` and nothing
 * else - the relaying process reads the same row.
 */
export const restartAudioStream = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/audio-streams/${encodeURIComponent(id)}/restart`, {
    method: 'POST',
    csrfToken,
  })

/**
 * The operations one bulk request may ask for, spelled the way the endpoint parses them.
 *
 * The list matches `BulkAudioStreamAction` on the server. `restart` sits beside start and
 * stop because the row action and the bulk menu name the same transition; the other four are
 * edits, and each one carries its own value in `BulkAudioStreamsPayload`.
 *
 * There is no delay, no archive and no file list here, and that is not an omission: an audio
 * stream has none of those fields, so there is nothing for such an operation to write.
 */
export type BulkAudioStreamAction =
  | 'start'
  | 'stop'
  | 'restart'
  | 'delete'
  | 'move-category'
  | 'change-type'
  | 'change-restream'

/**
 * The only kinds a bulk type change may choose.
 *
 * Both of them - the audio stream has only two kinds, and a bulk change can keep either
 * whole. That is the difference from a video stream, where the menu had to leave three kinds
 * out because their payload could not be invented.
 */
export type BulkAudioStreamType = AudioStreamType

/**
 * The body of `POST /admin/audio-streams/bulk`.
 *
 * One value beside the action rather than a patch of twenty rows: a bulk edit changes the one
 * thing the operator chose and leaves every other field of every row alone. The server judges
 * the value against each row and refuses only the rows it cannot carry - a category move
 * that collides with a name already used there refuses that row alone.
 *
 * `restream*` is a whole-list replacement, not a diff: a blank string clears its setting, so
 * "the restream settings are these now" and "clear them" are the same request shape.
 */
export interface BulkAudioStreamsPayload {
  action: BulkAudioStreamAction
  ids: string[]
  categoryId?: string
  type?: BulkAudioStreamType
  restreamUserAgent?: string
  restreamProxy?: string
  restreamCookies?: string
  restreamHeaders?: string
}

/**
 * Applies one operation to a selection: `POST /admin/audio-streams/bulk`.
 *
 * **Always a `200`, and the per-row answers live in `items`** - the same decision the
 * accounts, the groups and the video streams bulk endpoints make. A run that changed four
 * rows and refused one is one request that was carried out, and folding the worst row's
 * status into the response would make it indistinguishable from a run that changed nothing.
 *
 * A write, so the antiforgery token is minted at call time and never cached. No captcha for
 * the reason every other admin write gives: the session, the `editAudioStreams` permission
 * and a fresh antiforgery pair are the gate.
 */
export const bulkAudioStreams = ({
  payload,
  csrfToken,
}: {
  payload: BulkAudioStreamsPayload
  csrfToken: string | null
}): Promise<BulkOperationResponse> =>
  apiFetch('/admin/audio-streams/bulk', {
    method: 'POST',
    body: payload,
    csrfToken,
  })

/**
 * Deletes an audio stream together with its sources:
 * `DELETE /admin/audio-streams/{id}`, answered with `200 audio_stream_deleted`.
 *
 * The only action on the table that asks first, because it is the only one that cannot be
 * undone. The dialog names the stream: this is the one row whose name the visitor is
 * reading when they press it.
 */
export const deleteAudioStream = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/audio-streams/${encodeURIComponent(id)}`, { method: 'DELETE', csrfToken })

/**
 * The kinds an imported stream may be: the two an audio stream has.
 *
 * Both relay from a list of addresses, which is exactly what a playlist supplies, so the
 * picker offers both and the server accepts exactly these.
 */
export type AudioStreamImportType = AudioStreamType

/**
 * The settings one import applies to every stream it creates.
 *
 * There is no name, no sources list and no identifier pair: each `#EXTINF` entry names its
 * own stream and carries its own address, so those are read from the file rather than typed
 * here. What remains is the frame every created row shares - where it is filed, whether it
 * starts active, and the restreaming settings every kind carries.
 */
export interface AudioStreamImportValues {
  /** The kind every imported stream is created as. */
  type: AudioStreamImportType
  /** Whether the created streams start active or disabled. */
  status: AudioStreamStatus
  /** Identifier of the category the streams are filed under. */
  categoryId: string
  /** User agent every imported relay carries. */
  restreamUserAgent: string
  /** Proxy every imported relay goes out through. */
  restreamProxy: string
  /** Cookies every imported relay carries. */
  restreamCookies: string
  /** Extra headers every imported relay carries, as typed. */
  restreamHeaders: string
  /** Append the entry's source to an existing stream's source list. */
  appendSourceToExisting: boolean
  /** Overwrite an existing stream's status, type and restream settings. */
  overwriteExistingSettings: boolean
  /**
   * Bouquets every stream this import creates joins, as identifiers.
   *
   * Applied to the rows the import **creates**, not to the ones it skips or appends to: those
   * already exist, and an import is not an edit of a package somebody else arranged. Sent as
   * `bouquetIds`, once per bouquet - the one field a multipart form repeats for a list.
   */
  bouquetIds: string[]}

/**
 * Imports audio streams from an uploaded M3U playlist:
 * `POST /admin/audio-streams/import`, multipart/form-data.
 *
 * One `#EXTINF` entry and the address under it become one stream. Existing names are skipped
 * unless the operator opts in - `appendSourceToExisting` appends the entry's source to an
 * existing stream's source list, and `overwriteExistingSettings` overwrites its status, type
 * and restream settings. Both may be used together.
 *
 * The body is `FormData` rather than JSON, which is why `http.ts` leaves the content type
 * unset for it - the browser writes the multipart boundary itself.
 */
export const importAudioStreams = ({
  values,
  file,
  csrfToken,
}: {
  values: AudioStreamImportValues
  file: File
  csrfToken: string | null
}): Promise<AudioStreamImportResponse> => {
  const body = new FormData()

  body.set('type', values.type)
  body.set('status', values.status)
  body.set('categoryId', values.categoryId)

  // Once per bouquet: a multipart form has no array, so a repeated field is the list.
  for (const bouquetId of values.bouquetIds ?? []) {
    body.append('bouquetIds', bouquetId)
  }
  body.set('restreamUserAgent', values.restreamUserAgent)
  body.set('restreamProxy', values.restreamProxy)
  body.set('restreamCookies', values.restreamCookies)
  body.set('restreamHeaders', values.restreamHeaders)
  body.set('appendSourceToExisting', String(values.appendSourceToExisting))
  body.set('overwriteExistingSettings', String(values.overwriteExistingSettings))
  body.set('playlist', file)

  return apiFetch('/admin/audio-streams/import', {
    method: 'POST',
    body,
    csrfToken,
  })
}
