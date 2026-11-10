import type { ApiRequestFailureReason } from './types'

/**
 * The six refusal reasons, as a runtime list.
 *
 * It lives at the feature root rather than in `types/` for the reason `USER_LOG_RECORD_TYPES`
 * does: `types/` and its barrel are type-only, so a value there would erase to nothing at the
 * call site that needs it. That call site is the address read-back - an element outside this
 * list is dropped rather than sent, because the endpoint refuses a bad element with a 400
 * under `reasons` - and the filter picker, which draws one option per entry.
 *
 * The order is the server's enum order, which is also append-only schema: a reason added to
 * the vocabulary takes the next value and this list grows one entry, never renumbers.
 */
export const API_REQUEST_FAILURE_REASONS: readonly ApiRequestFailureReason[] = [
  'unknown_id',
  'geo_ip_violation',
  'auth_failed',
  'too_many_connections',
  'subscription_expired',
  'user_blocked',
]
