import type { SubscriptionOutputFormat } from './types'

/**
 * The rules a subscription must satisfy, mirrored from the server so a form refuses before the
 * wire instead of explaining a refusal after one.
 *
 * The server is still the authority - every endpoint checks again - but a form that let an
 * operator save a subscription with no service, or with a playlist id of the wrong shape, and only
 * told them afterwards would be a form that reported failure for choices it never questioned. The
 * numbers live here as constants rather than as literals at the call sites for the same reason
 * `tariffPlanRules.ts` mirrors its limits.
 */

/** Length of a playlist id the server mints (`SubscriptionLimits.PlaylistIdLength`). */
export const PLAYLIST_ID_LENGTH = 32

/** Longest single entry of either whitelist (`SubscriptionLimits.WhitelistEntryMaxLength`). */
export const WHITELIST_ENTRY_MAX_LENGTH = 200

/** Most entries one whitelist may hold (`SubscriptionLimits.WhitelistMaxEntries`). */
export const WHITELIST_MAX_ENTRIES = 20

/** Fewest simultaneous connections a subscription may allow. */
export const SUBSCRIPTION_CONNECTIONS_MIN = 1

/** Most simultaneous connections a subscription may allow. */
export const SUBSCRIPTION_CONNECTIONS_MAX = 10000

/** Whether a playlist id is the 32-character lower-case hex string the server mints. */
export const isPlaylistId = (value: string): boolean => /^[0-9a-f]{32}$/.test(value)

/**
 * Whether a MAC is six pairs of hex digits with `:` or `-` between them.
 *
 * The server accepts either separator and either case and stores the canonical uppercase
 * colon-separated form, so this is a shape check rather than a spelling: refusing a lowercase MAC
 * would refuse the value every device label prints.
 */
export const isMac = (value: string): boolean => /^[0-9a-f]{2}([:-][0-9a-f]{2}){5}$/i.test(value)

/**
 * Whether at least one service is attached once the selected plan's support is applied.
 *
 * What the server does is *clear* the services the plan cannot deliver and refuse only if nothing
 * survives. This answers the same question so the form can refuse before the wire, and it takes
 * the effective support flags rather than the row's own - a plan is the source of truth for what a
 * subscription may hold.
 */
export const hasService = (values: {
  playlistId: string | null
  magMac: string | null
  enigma2Mac: string | null
  supportsPlaylist: boolean
  supportsMag: boolean
  supportsEnigma2: boolean
}): boolean =>
  (values.supportsPlaylist && values.playlistId !== null) ||
  (values.supportsMag && values.magMac !== null) ||
  (values.supportsEnigma2 && values.enigma2Mac !== null)

/**
 * Whether a set of output formats names at least one this system delivers.
 *
 * An empty set is a subscription that cannot be played, and the server refuses it under
 * `outputFormats`.
 */
export const hasOutputFormat = (formats: readonly SubscriptionOutputFormat[]): boolean =>
  formats.length > 0

/** Whether a connection ceiling is inside its range. A null ceiling is unlimited. */
export const isUsableConnections = (connections: number | null): boolean =>
  connections === null ||
  (Number.isInteger(connections) &&
    connections >= SUBSCRIPTION_CONNECTIONS_MIN &&
    connections <= SUBSCRIPTION_CONNECTIONS_MAX)
