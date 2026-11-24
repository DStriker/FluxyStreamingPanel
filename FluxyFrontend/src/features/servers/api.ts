import { apiFetch, apiGet } from '../../utils/http'
import type { MessageResponse } from '../../types'
import type { Server, ServerList, ServerOption, ServerStatus } from './types'
import { mbpsToKbps } from './serverFields'

/**
 * The servers: one page of the table, one server in full, and the three writes.
 *
 * The API keeps the throughput in kbps - the unit is in the field's own name and the server
 * never converts - while the form shows Mbps. The conversion lives here, next to the body
 * that needs it, so no screen has to remember which unit it is holding.
 *
 * `latency` is the one asymmetry between the two writes: it is not a create field at all, so
 * a new row always starts at `0` ("not measured") and only the full-write `PATCH` sets it.
 */

/** The columns the table may be ordered by, as the query-string spelling. */
export type ServerSortField =
  | 'name'
  | 'status'
  | 'latency'
  | 'hostname'
  | 'ip'
  | 'clientCount'
  | 'createdAt'

export type ServerSortOrder = 'asc' | 'desc'

/** The page size the pager opens with, which is also the server's default. */
export const SERVERS_PAGE_SIZE = 20

/** The name limit, mirrored from the server so the form can refuse before the wire. */
export const SERVER_NAME_MAX = 200

/** The address limit, mirrored from the server. */
export const SERVER_IP_MAX = 45

/** The port window the server accepts. */
export const SERVER_PORT_MIN = 1
export const SERVER_PORT_MAX = 65535

/** The client ceiling's window; `0` means unlimited. */
export const SERVER_CLIENT_COUNT_MIN = 0
export const SERVER_CLIENT_COUNT_MAX = 10000

/** The two text limits, mirrored from the server. */
export const SERVER_OS_NAME_MAX = 100
export const SERVER_NETWORK_INTERFACE_MAX = 50

/** The host name limit, mirrored from the server. */
export const SERVER_HOSTNAME_MAX = 253

/** The guaranteed throughput's ceiling, in kbps, as the form's Mbps counterpart. */
export const SERVER_GUARANTEED_SPEED_MAX_KBPS = 1_000_000
export const SERVER_GUARANTEED_SPEED_MAX_MBPS = SERVER_GUARANTEED_SPEED_MAX_KBPS / 1000

/**
 * Everything the server form collects.
 *
 * `guaranteedMbps` is the form's own unit and is converted on the way out; every other field
 * carries the wire's name so the form and the contract read the same way.
 */
export interface ServerValues {
  /** Display name, unique across the installation. */
  name: string
  /** Address the server is reached at. */
  ip: string
  /** Port the server listens on, or null while the add form is empty. */
  port: number | null
  /** State of the server. */
  status: ServerStatus
  /** Name of the operating system the machine runs. */
  osName: string
  /** Name of the network interface the server is reached through. */
  networkInterface: string
  /** Client ceiling; `0` means unlimited. */
  clientCount: number
  /** Guaranteed throughput in Mbps; `0` means unknown. */
  guaranteedMbps: number
  /** Measured latency in seconds; `0` means not measured. Edit only. */
  latency: number
  /** Whether the server only serves timeshifted content. */
  timeshiftOnly: boolean
  /** Host name of the machine, or null. */
  hostname: string
  /** Address of the VPN the server is reached through, or null. */
  vpnIp: string
  /** ISO 3166-1 alpha-2 country codes the server serves. */
  countryCodes: string[]
}

/** A blank text the operator cleared arrives as `null`, which is what the column stores. */
const orNull = (value: string): string | null => {
  const trimmed = (value ?? '').trim()
  return trimmed === '' ? null : trimmed
}

/**
 * The create body: the five required fields plus the optional ones, and no `latency`.
 *
 * Latency is deliberately absent rather than sent as `0`: the request contract has no such
 * property, so a create always starts at "not measured" and the operator records a reading
 * afterwards through the edit form.
 */
const createBody = (values: ServerValues) => ({
  name: (values.name ?? '').trim(),
  ip: (values.ip ?? '').trim(),
  port: values.port ?? 0,
  osName: (values.osName ?? '').trim(),
  networkInterface: (values.networkInterface ?? '').trim(),
  clientCount: values.clientCount,
  status: values.status,
  guaranteedSpeedKbps: mbpsToKbps(values.guaranteedMbps ?? 0),
  timeshiftOnly: values.timeshiftOnly,
  hostname: orNull(values.hostname),
  vpnIp: orNull(values.vpnIp),
  countryCodes: values.countryCodes,
})

/**
 * The change body: the whole row, including `latency`.
 *
 * A full write rather than a diff, because the form holds one server and a text the operator
 * cleared must clear the column - a field left out of the body would keep the stored value.
 */
const updateBody = (values: ServerValues) => ({
  ...createBody(values),
  latency: values.latency,
})

/**
 * One page of the table: `GET /admin/servers`.
 *
 * A plain read behind the admin session, so no antiforgery token. The page, the search and
 * the order are the server's because the pager draws a `total`.
 */
export const getServers = ({
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
  sortBy: ServerSortField
  sortOrder: ServerSortOrder
  signal?: AbortSignal
}): Promise<ServerList> => {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
    sortBy,
    sortOrder,
  })

  if (search) query.set('search', search)

  return apiGet<ServerList>(`/admin/servers?${query.toString()}`, signal)
}

/** One server in full, for the edit form: `GET /admin/servers/{id}`. */
export const getServer = (id: string, signal?: AbortSignal): Promise<Server> =>
  apiGet<Server>(`/admin/servers/${encodeURIComponent(id)}`, signal)

/**
 * Every server as a picker entry: `GET /admin/servers/options`.
 *
 * Gated by an "any of" policy over the subscription and stream read grants rather than by the
 * server pair, because the two forms that draw this picker are the only callers - choosing a
 * server while editing either of them is part of editing it, and reading a name to choose it is
 * not browsing the servers table.
 */
export const getServerOptions = (signal?: AbortSignal): Promise<ServerOption[]> =>
  apiGet<ServerOption[]>('/admin/servers/options', signal)

/**
 * Creates a server: `POST /admin/servers`, answered with `201` and a `Location`.
 *
 * A name another row holds is `409 server_already_exists`, and a refused field is
 * `400 validation_failed` keyed by its camelCase name.
 */
export const createServer = ({
  values,
  csrfToken,
}: {
  values: ServerValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/admin/servers', {
    method: 'POST',
    body: createBody(values),
    csrfToken,
  })

/**
 * Changes a server: `PATCH /admin/servers/{id}`, answered with `200`.
 *
 * The whole row travels, latency included - this is the only write that carries it.
 */
export const updateServer = ({
  id,
  values,
  csrfToken,
}: {
  id: string
  values: ServerValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/servers/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body: updateBody(values),
    csrfToken,
  })

/**
 * Deletes a server: `DELETE /admin/servers/{id}`, answered with `200`.
 *
 * The main row answers `409 server_immutable` and survives; its delete button is disabled
 * with that reason, so the refusal is described before the click rather than after it.
 */
export const deleteServer = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/servers/${encodeURIComponent(id)}`, { method: 'DELETE', csrfToken })
