import { apiGet } from '../../utils/http'
import type { DashboardSnapshot } from './types'

/**
 * The admin dashboard's one read: `GET /admin/dashboard`.
 *
 * The endpoint composes several aggregates and a live probe on the server and caches the
 * answer for ten seconds there; this side reads it as one object and adds nothing. The
 * contract is in `types/DashboardSnapshot.ts`, which is also the shape the backend was
 * written against.
 */
export const getDashboard = ({ signal }: { signal?: AbortSignal }): Promise<DashboardSnapshot> =>
  apiGet<DashboardSnapshot>('/admin/dashboard', signal)
