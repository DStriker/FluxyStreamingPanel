/**
 * The subscriptions feature's contracts, re-exported as types only so a consumer names the folder
 * rather than a file.
 *
 * **Nothing but types leaves this barrel.** The two runtime lists the screens loop over live in
 * `subscriptionOptions.ts` beside the feature's rules: a value exported here would be erased by a
 * type-only import, and a caller reading it through the barrel would get `undefined` at runtime -
 * silently, because `t()` and a `map` over nothing both compile.
 */
export type { Subscription } from './Subscription'
export type { SubscriptionList } from './SubscriptionList'
export type { SubscriptionDetail } from './SubscriptionDetail'
export type { SubscriptionDerived } from './SubscriptionDerived'
export type { SubscriptionStatus } from './SubscriptionStatus'
export type { SubscriptionOutputFormat } from './SubscriptionOutputFormat'
export type { TariffPlanDurationUnit } from './TariffPlanDurationUnit'
