import { apiFetch, apiGet } from '../../utils/http'
import type { MessageResponse } from '../../types'
import type { EpgSource, EpgSourceChannelList, EpgSourceList } from './types'

/**
 * The programme guide sources: the table, one source in full, the channels that use one, and
 * the form's two writes plus the delete.
 *
 * The raw guide and the programmes parsed out of it have no endpoint here: they are written
 * and deleted by the updater, which does not exist yet. What this module covers is the
 * operator's side of the domain - where a schedule comes from and how much of it is kept.
 */

/**
 * The columns the table may be ordered by, as the query-string spelling.
 *
 * These are the endpoint's own sort fields. `daysKeep` and `lastEpgUpdatedAt` are sent in
 * that spelling and the server lowercases them.
 */
export type EpgSourceSortField = 'name' | 'url' | 'daysKeep' | 'lastEpgUpdatedAt'

export type EpgSourceSortOrder = 'asc' | 'desc'

/** The columns the channels table may be ordered by, as the query-string spelling. */
export type EpgSourceChannelSortField = 'streamName' | 'channelKey'

/** The page size the pager offers, which is also the server's default. */
export const EPG_SOURCE_PAGE_SIZE = 20

/**
 * How many sources a picker asks for in one request.
 *
 * A guide source list is short - one entry per schedule an installation reads - and the
 * picker on the stream form needs all of them to be useful. The server's ceiling is 1000;
 * asking for it is the same thing the group picker does.
 */
export const ALL_EPG_SOURCES_PAGE_SIZE = 1000

/** The server's range for a retention, mirrored so the form can refuse before the wire. */
export const EPG_DAYS_KEEP_MIN = 1
export const EPG_DAYS_KEEP_MAX = 365
export const EPG_DAYS_KEEP_DEFAULT = 7

/** The name and address columns, mirrored for the form's own length rules. */
export const EPG_NAME_MAX = 200
export const EPG_URL_MAX = 2048

/** Everything one form can decide about a source - the body of both writes. */
export interface EpgSourceValues {
  /** Display name, unique across the installation. */
  name: string
  /** Absolute http(s) address the guide is fetched from. */
  url: string
  /** Days of programmes to keep, or null to keep the default on create / the row's own. */
  daysKeep: number | null
}

/**
 * The body both writes send: the two texts trimmed, and the retention as text so the server
 * parses it where a refusal can name the field. A blank retention means "not named" - the
 * create falls back to the default and the change keeps what the row holds.
 */
const toBody = (values: EpgSourceValues) => ({
  name: (values.name ?? '').trim(),
  url: (values.url ?? '').trim(),
  daysKeep: values.daysKeep === null ? '' : String(values.daysKeep),
})

/** One page of the table: `GET /admin/epg-sources`. */
export const getEpgSources = ({
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
  sortBy: EpgSourceSortField
  sortOrder: EpgSourceSortOrder
  signal?: AbortSignal
}): Promise<EpgSourceList> => {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
    sortBy,
    sortOrder,
  })

  if (search) query.set('search', search)

  return apiGet<EpgSourceList>(`/admin/epg-sources?${query.toString()}`, signal)
}

/** One source in full, for the edit form: `GET /admin/epg-sources/{id}`. */
export const getEpgSource = (id: string, signal?: AbortSignal): Promise<EpgSource> =>
  apiGet<EpgSource>(`/admin/epg-sources/${encodeURIComponent(id)}`, signal)

/**
 * The streams that read their guide from a source:
 * `GET /admin/epg-sources/{id}/channels`.
 */
export const getEpgSourceChannels = ({
  id,
  page,
  pageSize,
  search,
  sortBy,
  sortOrder,
  signal,
}: {
  id: string
  page: number
  pageSize: number
  search?: string
  sortBy: EpgSourceChannelSortField
  sortOrder: EpgSourceSortOrder
  signal?: AbortSignal
}): Promise<EpgSourceChannelList> => {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
    sortBy,
    sortOrder,
  })

  if (search) query.set('search', search)

  return apiGet<EpgSourceChannelList>(
    `/admin/epg-sources/${encodeURIComponent(id)}/channels?${query.toString()}`,
    signal,
  )
}

/** Creates a source: `POST /admin/epg-sources`, answered with `201` and a `Location`. */
export const createEpgSource = ({
  values,
  csrfToken,
}: {
  values: EpgSourceValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/admin/epg-sources', {
    method: 'POST',
    body: toBody(values),
    csrfToken,
  })

/**
 * Changes a source: `PATCH /admin/epg-sources/{id}`.
 *
 * The whole form travels rather than a diff, for the reason the other forms do: one screen
 * holds one source, so what the operator sees and what the row becomes are the same
 * statement.
 */
export const updateEpgSource = ({
  id,
  values,
  csrfToken,
}: {
  id: string
  values: EpgSourceValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/epg-sources/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body: toBody(values),
    csrfToken,
  })

/**
 * Deletes a source: `DELETE /admin/epg-sources/{id}`.
 *
 * A source a stream still reads its guide from is refused with `epg_source_in_use`; the
 * programmes filed under it go with it.
 */
export const deleteEpgSource = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/epg-sources/${encodeURIComponent(id)}`, {
    method: 'DELETE',
    csrfToken,
  })
