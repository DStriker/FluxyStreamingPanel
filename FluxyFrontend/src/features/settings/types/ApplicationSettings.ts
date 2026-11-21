import type { BackupPeriod } from './BackupPeriod'

/**
 * `GET /admin/settings` - the installation's own one-row configuration, as the screen reads it.
 *
 * **There is no `recaptchaSecretKey` here, and there must not be one.** The response carries
 * `recaptchaConfigured` instead: whether a private key is stored is a yes-or-no question the
 * screen can answer, while the key itself is write-only once stored - a response carrying it
 * would put the installation's reCAPTCHA protection into every proxy log and browser cache that
 * ever held this page. The write shape in `ApplicationSettingsValues` is where the secret lives,
 * and it is optional there for exactly that reason.
 *
 * `backupPeriod` travels as the owner's lowercase word rather than as the stored member, and the
 * four fallback addresses arrive as empty strings when unset - the shape the columns store.
 */
export interface ApplicationSettings {
  /** Name the panel presents itself with, used in the four email subjects. */
  serviceName: string
  /** IANA identifier of the installation's display time zone. */
  defaultTimeZone: string
  /** Name of the Enigma2 bouquet. */
  enigma2BouquetName: string
  /** Public reCAPTCHA site key; empty when reCAPTCHA is disabled. */
  recaptchaSiteKey: string
  /** Whether a private reCAPTCHA key is stored. The key itself is never answered. */
  recaptchaConfigured: boolean
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
