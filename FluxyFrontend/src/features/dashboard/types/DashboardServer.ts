import type { ServerStatus } from './ServerStatus'

/**
 * One server row of `GET /admin/dashboard`: its configured identity, the connection
 * aggregates attributed to it, and the telemetry the probe reported.
 *
 * `available` is the distinction the screen draws. `false` means the probe could not reach
 * this server, and every numeric telemetry field is `0` because that is what the server
 * answered with - not because the machine was idle. The card's unavailable badge is what
 * separates "unreachable" from "legitimately zero"; the numbers themselves are always
 * numbers, never dashes.
 */
export interface DashboardServer {
  /** Identifier of the server, as text. */
  id: string
  /** Display name, unique across the installation. */
  name: string
  /** State the operator configured. */
  status: ServerStatus
  /** Whether the probe reached this server. `false` is "unreachable", not "idle". */
  available: boolean
  /** Live connection-log rows attributed to this server. */
  activeConnections: number
  /** Distinct owners whose preferred server is this one. */
  preferredServerUsers: number
  /** Distinct owners among this server's live connections. */
  usersWithActiveConnections: number
  /** Uptime reported by the probe, in seconds. */
  uptimeSeconds: number
  /** CPU load reported by the probe, in percent. */
  cpuPercent: number
  /** RAM load reported by the probe, in percent. */
  ramPercent: number
  /** Disk load reported by the probe, in percent. */
  diskPercent: number
  /** Inbound throughput reported by the probe, in Mbit/s. */
  networkInMbps: number
  /** Outbound throughput reported by the probe, in Mbit/s. */
  networkOutMbps: number
}
