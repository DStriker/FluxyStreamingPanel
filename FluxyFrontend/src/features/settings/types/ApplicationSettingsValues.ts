import type { BackupPeriod } from './BackupPeriod'

/**
 * The body `PATCH /admin/settings` sends: every field of the read shape except
 * `recaptchaConfigured`, plus the optional private key.
 *
 * A full write - a value left out is not "keep" here, it is the field the server requires - with
 * exactly one asymmetry: **an absent or empty-after-trim `recaptchaSecretKey` keeps the stored
 * secret**, and anything else replaces it. That is the only way a form can express "do not touch
 * the key I was never shown", and it is why this type is separate from `ApplicationSettings`
 * rather than the same shape with a flag.
 *
 * `backupPeriod` carries the wire union, so a selector cannot offer a word the server refuses.
 */
export interface ApplicationSettingsValues {
  /** Name the panel presents itself with. */
  serviceName: string
  /** IANA identifier of the installation's display time zone. */
  defaultTimeZone: string
  /** Name of the Enigma2 bouquet. */
  enigma2BouquetName: string
  /** Public reCAPTCHA site key; empty when reCAPTCHA is disabled. */
  recaptchaSiteKey: string
  /** Leave empty to keep the stored key. Never sent back from the read, which has no copy. */
  recaptchaSecretKey?: string
  /** Whether a subscription may be issued as a trial. */
  enableTrials: boolean
  /** Requests one public-API client may make in a window; `0` means unlimited. */
  publicApiFloodLimit: number
  /** Addresses exempt from the public-API flood limit, exact or CIDR. */
  publicApiFloodWhitelist: string[]
  /** Whether a public-API request has to present a user agent. */
  requireUserAgent: boolean
  /** Days a user log row is kept. */
  retentionUserLogs: number
  /** Days a credit log row is kept. */
  retentionCreditLogs: number
  /** Days a connection log row is kept. */
  retentionConnectionLogs: number
  /** Days a refused-request row is kept. */
  retentionApiRequestFailures: number
  /** Days a reported stream problem is kept. */
  retentionStreamProblems: number
  /** Days a MAG event row is kept. */
  retentionMagEvents: number
  /** Whether a scheduled backup would run. */
  backupEnabled: boolean
  /** How often the backup would run, as the owner's lowercase word. */
  backupPeriod: BackupPeriod
  /** How many backups are kept; `0` keeps every backup. */
  backupRotationCount: number
  /** Address shown when the stream is down; empty for none. */
  fallbackStreamDownUrl: string
  /** Address shown when the viewer is banned; empty for none. */
  fallbackUserBannedUrl: string
  /** Address shown when the viewer's subscription expired; empty for none. */
  fallbackUserExpiredUrl: string
  /** Address shown when the viewer's connection ceiling is reached; empty for none. */
  fallbackMaxConnectionsUrl: string
}
