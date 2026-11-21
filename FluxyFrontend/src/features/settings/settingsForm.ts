import { isIpOrCidr } from '../../utils/ip'
import type {
  ApplicationSettings,
  ApplicationSettingsValues,
  BackupPeriod,
  MagContainer,
  MagSettings,
  MagSettingsValues,
  MagTheme,
} from './types'

/**
 * The one place a form value becomes a wire body for each settings resource.
 *
 * Extracted rather than written in the pages for the reason `subscriptionBody.ts` exists: the
 * mappings are the half a render cannot reach - every render lands before a submit - and a unit
 * test can pin them. They carry three decisions that are not obvious from either shape:
 *
 * - **The reCAPTCHA secret is only sent when it is not blank.** An empty string and an absent
 *   key both mean "keep the stored one" on this endpoint; this module sends nothing for blank
 *   so the request says that plainly rather than relying on the server's trim.
 * - **A whitelist entry is trimmed, and blank entries are dropped.** The server normalizes every
 *   entry itself, but a body carrying an accidental empty tag would be a validation refusal for
 *   a value the operator cannot see.
 * - **A fallback URL and every text field are sent as written.** Empty is a legitimate value on
 *   all of them (it means "none"), so this module does not turn `''` into `null` anywhere.
 */

/**
 * The fields every settings save sends, in the contract's own order.
 *
 * Exported so the page can clear a previous refusal from exactly the fields it drew, and so a
 * unit test can assert the body's key set rather than only a handful of members.
 */
export const SETTINGS_FIELDS: readonly (keyof ApplicationSettingsValues)[] = [
  'serviceName',
  'defaultTimeZone',
  'enigma2BouquetName',
  'recaptchaSiteKey',
  'enableTrials',
  'publicApiFloodLimit',
  'publicApiFloodWhitelist',
  'requireUserAgent',
  'retentionUserLogs',
  'retentionCreditLogs',
  'retentionConnectionLogs',
  'retentionApiRequestFailures',
  'retentionStreamProblems',
  'retentionMagEvents',
  'backupEnabled',
  'backupPeriod',
  'backupRotationCount',
  'fallbackStreamDownUrl',
  'fallbackUserBannedUrl',
  'fallbackUserExpiredUrl',
  'fallbackMaxConnectionsUrl',
]

/** Every field the MAG settings form draws, so a refusal can be cleared from all of them. */
export const MAG_SETTINGS_FIELDS: readonly (keyof MagSettingsValues)[] = [
  'showAllCategories',
  'strictSecurity',
  'showChannelLogos',
  'showPreviewLogos',
  'defaultContainer',
  'defaultTheme',
  'recordingMaxLengthMinutes',
  'maxLocalRecordings',
  'playbackLimit',
  'allowedStbTypes',
  'allowedRecordingStbTypes',
]

/** Whether a value is one of the four backup periods, for a read-off-the-wire guard. */
export const isBackupPeriod = (value: unknown): value is BackupPeriod =>
  value === 'hourly' || value === 'daily' || value === 'weekly' || value === 'monthly'

/** Whether a value is one of the two canonical containers. */
export const isMagContainer = (value: unknown): value is MagContainer =>
  value === 'mpeg-ts' || value === 'hls'

/** Whether a value is one of the five owner spellings. */
export const isMagTheme = (value: unknown): value is MagTheme =>
  value === 'default' ||
  value === 'digital' ||
  value === 'emerald' ||
  value === 'cappucino' ||
  value === 'ocean blue'

/**
 * The whitelist as the server should receive it: trimmed, blanks dropped.
 *
 * De-duplication is deliberately **not** done here. The server de-duplicates in first-occurrence
 * order; doing it in the browser too would be a second rule to keep in step, and the one place
 * the order is visible is the field the operator is editing.
 */
export const normalizeWhitelist = (entries: readonly string[] | undefined): string[] =>
  (entries ?? []).map((entry) => entry.trim()).filter((entry) => entry.length > 0)

/**
 * Whether every whitelist entry is an address or a CIDR range.
 *
 * The shape check is `isIpOrCidr`, the same predicate the login guard's allow list uses - so the
 * two lists on this installation cannot disagree about what "an address" means.
 */
export const isValidWhitelist = (entries: readonly string[] | undefined): boolean =>
  normalizeWhitelist(entries).every((entry) => isIpOrCidr(entry))

/**
 * Whether a fallback address is acceptable: blank, or an absolute `http`/`https` URL.
 *
 * The server's rule exactly - empty allowed, otherwise absolute and one of the two schemes, at
 * most 2048 characters. `URL` is the parse rather than a regular expression: it accepts what the
 * server's `Uri.TryCreate(..., UriKind.Absolute)` accepts and refuses the relative string an
 * operator who forgot the scheme would type.
 */
export const isValidFallbackUrl = (value: string | undefined): boolean => {
  const text = (value ?? '').trim()

  if (text === '') return true
  if (text.length > 2048) return false

  try {
    const parsed = new URL(text)

    return parsed.protocol === 'http:' || parsed.protocol === 'https:'
  } catch {
    return false
  }
}

/**
 * The form's opening content for the application settings: everything the read answered, plus
 * an empty secret.
 *
 * The secret is empty because the read never carries one - the empty field is the whole
 * "leave it alone" signal, and seeding it with anything else would be inventing a value the
 * server never sent.
 */
export const settingsToFormValues = (
  settings: ApplicationSettings,
): ApplicationSettingsValues => ({
  serviceName: settings.serviceName,
  defaultTimeZone: settings.defaultTimeZone,
  enigma2BouquetName: settings.enigma2BouquetName,
  recaptchaSiteKey: settings.recaptchaSiteKey,
  recaptchaSecretKey: '',
  enableTrials: settings.enableTrials,
  publicApiFloodLimit: settings.publicApiFloodLimit,
  publicApiFloodWhitelist: [...settings.publicApiFloodWhitelist],
  requireUserAgent: settings.requireUserAgent,
  retentionUserLogs: settings.retentionUserLogs,
  retentionCreditLogs: settings.retentionCreditLogs,
  retentionConnectionLogs: settings.retentionConnectionLogs,
  retentionApiRequestFailures: settings.retentionApiRequestFailures,
  retentionStreamProblems: settings.retentionStreamProblems,
  retentionMagEvents: settings.retentionMagEvents,
  backupEnabled: settings.backupEnabled,
  backupPeriod: settings.backupPeriod,
  backupRotationCount: settings.backupRotationCount,
  fallbackStreamDownUrl: settings.fallbackStreamDownUrl,
  fallbackUserBannedUrl: settings.fallbackUserBannedUrl,
  fallbackUserExpiredUrl: settings.fallbackUserExpiredUrl,
  fallbackMaxConnectionsUrl: settings.fallbackMaxConnectionsUrl,
})

/** The form's opening content for the MAG settings: the read shape, lists copied. */
export const magSettingsToFormValues = (settings: MagSettings): MagSettingsValues => ({
  showAllCategories: settings.showAllCategories,
  strictSecurity: settings.strictSecurity,
  showChannelLogos: settings.showChannelLogos,
  showPreviewLogos: settings.showPreviewLogos,
  defaultContainer: settings.defaultContainer,
  defaultTheme: settings.defaultTheme,
  recordingMaxLengthMinutes: settings.recordingMaxLengthMinutes,
  maxLocalRecordings: settings.maxLocalRecordings,
  playbackLimit: settings.playbackLimit,
  allowedStbTypes: [...settings.allowedStbTypes],
  allowedRecordingStbTypes: [...settings.allowedRecordingStbTypes],
})

/**
 * The body `PATCH /admin/settings` sends.
 *
 * The secret is dropped when blank - see the module note - and every other value travels as the
 * form holds it. A field the form did not draw is not silently invented: the type requires all
 * of them, so a missing one is a compile error rather than a "keep" the server would refuse.
 */
export const toSettingsBody = (
  values: ApplicationSettingsValues,
): ApplicationSettingsValues => {
  const secret = (values.recaptchaSecretKey ?? '').trim()

  const body: ApplicationSettingsValues = {
    serviceName: values.serviceName,
    defaultTimeZone: values.defaultTimeZone,
    enigma2BouquetName: values.enigma2BouquetName,
    recaptchaSiteKey: values.recaptchaSiteKey,
    enableTrials: values.enableTrials,
    publicApiFloodLimit: values.publicApiFloodLimit,
    publicApiFloodWhitelist: normalizeWhitelist(values.publicApiFloodWhitelist),
    requireUserAgent: values.requireUserAgent,
    retentionUserLogs: values.retentionUserLogs,
    retentionCreditLogs: values.retentionCreditLogs,
    retentionConnectionLogs: values.retentionConnectionLogs,
    retentionApiRequestFailures: values.retentionApiRequestFailures,
    retentionStreamProblems: values.retentionStreamProblems,
    retentionMagEvents: values.retentionMagEvents,
    backupEnabled: values.backupEnabled,
    backupPeriod: values.backupPeriod,
    backupRotationCount: values.backupRotationCount,
    fallbackStreamDownUrl: values.fallbackStreamDownUrl,
    fallbackUserBannedUrl: values.fallbackUserBannedUrl,
    fallbackUserExpiredUrl: values.fallbackUserExpiredUrl,
    fallbackMaxConnectionsUrl: values.fallbackMaxConnectionsUrl,
  }

  if (secret !== '') body.recaptchaSecretKey = secret

  return body
}

/**
 * The body `PATCH /admin/mag-settings` sends: all eleven fields, unchanged.
 *
 * Copied rather than passed through so a caller cannot keep a reference into the form store and
 * mutate the request it already sent. The two lists are copied for the same reason.
 */
export const toMagSettingsBody = (values: MagSettingsValues): MagSettingsValues => ({
  showAllCategories: values.showAllCategories,
  strictSecurity: values.strictSecurity,
  showChannelLogos: values.showChannelLogos,
  showPreviewLogos: values.showPreviewLogos,
  defaultContainer: values.defaultContainer,
  defaultTheme: values.defaultTheme,
  recordingMaxLengthMinutes: values.recordingMaxLengthMinutes,
  maxLocalRecordings: values.maxLocalRecordings,
  playbackLimit: values.playbackLimit,
  allowedStbTypes: [...values.allowedStbTypes],
  allowedRecordingStbTypes: [...values.allowedRecordingStbTypes],
})
