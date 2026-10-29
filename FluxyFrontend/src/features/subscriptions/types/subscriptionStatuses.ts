import type { SubscriptionStatus } from './SubscriptionStatus'

/**
 * The four states, in the order the server's precedence reads them.
 *
 * A value rather than a type, so the filter and the tag colour table can loop over it. The order
 * is the precedence itself - expired first - because that is the order a reader who is looking
 * for what has gone wrong scans.
 */
export const SUBSCRIPTION_STATUSES: readonly SubscriptionStatus[] = [
  'expired',
  'blocked',
  'trial',
  'active',
]
