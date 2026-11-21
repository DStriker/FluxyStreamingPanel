/**
 * The bounds and lengths the two settings forms mirror from the backend.
 *
 * Stated here rather than imported from the API contracts for the reason every other mirror in
 * this repo gives: a limit belongs to the value being accepted, and the column is what happens
 * to agree with it. Several of these describe a *sentinel* rather than a ceiling -
 * `publicApiFloodLimit = 0` is "unlimited", `backupRotationCount = 0` keeps every backup and
 * `maxLocalRecordings = 0` allows none - so none of the lower bounds is exclusive.
 */

/** Longest accepted service name and Enigma2 bouquet name, matching `varchar(100)`. */
export const SETTINGS_TEXT_MAX = 100

/** Longest accepted reCAPTCHA site or private key, matching `varchar(255)`. */
export const RECAPTCHA_KEY_MAX = 255

/** Lowest public-API flood limit; `0` means unlimited. */
export const FLOOD_LIMIT_MIN = 0
/** Highest public-API flood limit. */
export const FLOOD_LIMIT_MAX = 10000

/** Most addresses the public-API flood whitelist may hold. */
export const FLOOD_WHITELIST_MAX = 100

/** The retention window, in days, every one of the six log rows shares. */
export const RETENTION_MIN_DAYS = 1
export const RETENTION_MAX_DAYS = 3650

/** Lowest backup rotation count; `0` keeps every backup. */
export const BACKUP_ROTATION_MIN = 0
/** Highest backup rotation count. */
export const BACKUP_ROTATION_MAX = 1000

/** Longest accepted fallback address, matching `varchar(2048)`. */
export const FALLBACK_URL_MAX = 2048

/** The recording length the portal allows, in minutes. */
export const RECORDING_MINUTES_MIN = 1
export const RECORDING_MINUTES_MAX = 1440

/** Lowest local recording count; `0` allows none. */
export const MAX_LOCAL_RECORDINGS_MIN = 0
export const MAX_LOCAL_RECORDINGS_MAX = 1000

/**
 * The playback limit's window. The ceiling is the subscription's own connection maximum
 * (`10000`), so the two cannot drift apart - the MAG portal is playing a subscription.
 */
export const PLAYBACK_LIMIT_MIN = 1
export const PLAYBACK_LIMIT_MAX = 10000
