/**
 * The state a subscription is in, as the server computes it.
 *
 * The value is the lower-case member name the endpoint sends and the list filter accepts, so a
 * tag and a filter read the same spelling. It is *computed* - expired beats blocked beats trial
 * beats active - and never a field a client writes.
 */
export type SubscriptionStatus = 'active' | 'trial' | 'blocked' | 'expired'
