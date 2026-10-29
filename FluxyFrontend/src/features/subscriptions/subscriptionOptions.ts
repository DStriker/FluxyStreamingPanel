import type {
  SubscriptionOutputFormat,
  SubscriptionStatus,
} from './types'

/**
 * The two runtime lists the subscription screens loop over.
 *
 * They live here rather than in `types/` because that folder and its barrel are **type-only**: a
 * value exported from it would be erased by a type-only import, and a caller that read the array
 * through the barrel would get `undefined` at runtime. Beside the feature's rules and its API is
 * where the rest of this repository keeps the values a screen enumerates (`loginGuardPolicy.ts`,
 * `permissions.ts`), so the same rule applies here.
 */

/**
 * The four states, in the order the server's precedence reads them.
 *
 * A value rather than a type, so the filter and the tag colour table can loop over it. The order
 * is the precedence itself - expired first - because that is the order a reader who is looking for
 * what has gone wrong scans.
 */
export const SUBSCRIPTION_STATUSES: readonly SubscriptionStatus[] = [
  'expired',
  'blocked',
  'trial',
  'active',
]

/** The two formats, in the order the server names them. */
export const SUBSCRIPTION_OUTPUT_FORMATS: readonly SubscriptionOutputFormat[] = ['mpegTs', 'hls']
