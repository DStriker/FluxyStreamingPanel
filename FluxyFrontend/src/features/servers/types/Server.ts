import type { ServerStatus } from './ServerStatus'

/**
 * One server, as `GET /admin/servers` answers with it in both the list and the detail.
 *
 * `status` is the backend enum's member name rather than a lower-case word, because that is
 * what the controller writes. `latency` and `guaranteedSpeedKbps` carry their sentinels:
 * `0` means "not measured" and "unknown" respectively, and `clientCount`'s `0` means
 * unlimited - all three are real states rather than missing values.
 *
 * `isMain` travels because the table draws the main row's delete button disabled; the
 * client never compares an identifier against a constant of the API.
 */
export interface Server {
  /** Identifier of the server, as text. */
  id: string
  /** Display name, unique across the installation. */
  name: string
  /** Address the server is reached at. */
  ip: string
  /** Port the server listens on. */
  port: number
  /** Client ceiling, or `0` for unlimited. */
  clientCount: number
  /** Name of the operating system the machine runs. */
  osName: string
  /** Name of the network interface the server is reached through. */
  networkInterface: string
  /** Measured round-trip latency in seconds, or `0` when not measured. */
  latency: number
  /** State of the server, as the name of the backend's enum member. */
  status: ServerStatus
  /** Guaranteed throughput in kbps, or `0` when unknown. */
  guaranteedSpeedKbps: number
  /** Whether the server only serves timeshifted content. */
  timeshiftOnly: boolean
  /** Host name of the machine, or null when it has none. */
  hostname: string | null
  /** Address of the VPN the server is reached through, or null. */
  vpnIp: string | null
  /** ISO 3166-1 alpha-2 country codes the server serves, upper case; empty for none. */
  countryCodes: string[]
  /** Whether this is the main server, which cannot be deleted. */
  isMain: boolean
  /** When the row was written, as an ISO timestamp. */
  createdAt: string
}
