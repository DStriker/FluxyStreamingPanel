import type { DashboardKpis } from './DashboardKpis'
import type { DashboardServer } from './DashboardServer'

/**
 * The whole answer of `GET /admin/dashboard`.
 *
 * `servers` holds one entry per configured server, disabled ones included, ordered by name.
 * `serversUnavailable` counts the entries whose `available` is false, so the network KPI
 * can label its total as partial without re-counting the list on the page.
 *
 * `generatedAt` is the moment the server built the snapshot, as an ISO 8601 string with an
 * offset. The page's "Updated at" line is driven by the query's own `dataUpdatedAt` instead
 * - that is when this browser received the answer, which is what the visitor is looking at.
 */
export interface DashboardSnapshot {
  kpis: DashboardKpis
  servers: DashboardServer[]
  serversUnavailable: number
  generatedAt: string
}
