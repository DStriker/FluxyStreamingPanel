/**
 * The installation-wide totals `GET /admin/dashboard` answers with.
 *
 * `networkInMbps` and `networkOutMbps` are sums over the servers that answered the probe;
 * the snapshot's `serversUnavailable` is what lets the page say the total is partial. No
 * combined total exists: the two directions are separate facts and adding them would be a
 * number neither server reported.
 */
export interface DashboardKpis {
  /** Live connection-log rows, including the unattributed tail (`server_id IS NULL`). */
  activeConnections: number
  /** Distinct owners of those live connections. */
  usersWithActiveConnections: number
  /** Streams whose stored status is `Running`. */
  streamsRunning: number
  /** Streams whose stored status is `Stopped`. */
  streamsStopped: number
  /** Inbound throughput summed over available servers, in Mbit/s. */
  networkInMbps: number
  /** Outbound throughput summed over available servers, in Mbit/s. */
  networkOutMbps: number
}
