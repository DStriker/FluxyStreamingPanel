/**
 * One format a subscription may be delivered in, spelled as the server names it.
 *
 * The two members are the two shapes the relay speaks, and a subscription with neither is
 * refused: a package that cannot be played is not a package.
 */
export type SubscriptionOutputFormat = 'mpegTs' | 'hls'
