import { describe, expect, it } from 'vitest'
import {
  SETTINGS_FIELDS,
  isValidFallbackUrl,
  isValidWhitelist,
  normalizeWhitelist,
  settingsToFormValues,
  toMagSettingsBody,
  toSettingsBody,
} from '../../../src/features/settings/settingsForm'
import { STB_TYPES } from '../../../src/features/settings/options'
import type {
  ApplicationSettings,
  ApplicationSettingsValues,
  MagSettings,
} from '../../../src/features/settings/types'

/**
 * The two body mappings and the three validators, asserted as functions.
 *
 * The bodies are the half of each form a render cannot reach - every render lands before a
 * submit - and the reCAPTCHA secret is the one field whose absence is a statement ("keep the
 * stored key") rather than a missing value. That is the bug this file exists to stand against:
 * a body sending `recaptchaSecretKey: ''` would be read as "keep" by the server's trim, but a
 * body sending a *stale* key would silently overwrite the stored one.
 */

const settings = (overrides: Partial<ApplicationSettings> = {}): ApplicationSettings => ({
  serviceName: 'Fluxy Streaming panel',
  defaultTimeZone: 'Europe/Berlin',
  enigma2BouquetName: 'Fluxy IPTV',
  recaptchaSiteKey: '',
  recaptchaConfigured: false,
  enableTrials: true,
  publicApiFloodLimit: 20,
  publicApiFloodWhitelist: [],
  requireUserAgent: false,
  retentionUserLogs: 30,
  retentionCreditLogs: 30,
  retentionConnectionLogs: 30,
  retentionApiRequestFailures: 30,
  retentionStreamProblems: 30,
  retentionMagEvents: 30,
  backupEnabled: false,
  backupPeriod: 'daily',
  backupRotationCount: 10,
  fallbackStreamDownUrl: '',
  fallbackUserBannedUrl: '',
  fallbackUserExpiredUrl: '',
  fallbackMaxConnectionsUrl: '',
  ...overrides,
})

const formValues = (overrides: Partial<ApplicationSettingsValues> = {}) => ({
  ...settingsToFormValues(settings()),
  ...overrides,
})

describe('settingsToFormValues', () => {
  it('copies every value the read answered and opens the secret empty', () => {
    const values = formValues({ serviceName: 'Panel', enableTrials: false })

    expect(values.serviceName).toBe('Panel')
    expect(values.enableTrials).toBe(false)
    expect(values.recaptchaSecretKey).toBe('')
    expect(values.retentionMagEvents).toBe(30)
    expect(values.backupPeriod).toBe('daily')
  })

  it('copies the whitelist rather than sharing the array', () => {
    const source = settings({ publicApiFloodWhitelist: ['10.0.0.0/8'] })
    const values = settingsToFormValues(source)

    values.publicApiFloodWhitelist.push('192.168.0.0/16')
    expect(source.publicApiFloodWhitelist).toEqual(['10.0.0.0/8'])
  })
})

describe('toSettingsBody', () => {
  it('sends every field of the contract, and the secret only when one was typed', () => {
    const body = toSettingsBody(formValues({ recaptchaSecretKey: '  secret  ' }))

    expect(Object.keys(body).sort()).toEqual(
      [...SETTINGS_FIELDS, 'recaptchaSecretKey'].sort(),
    )
    expect(body.recaptchaSecretKey).toBe('secret')
    expect(body.serviceName).toBe('Fluxy Streaming panel')
  })

  it('drops a blank secret, which is the endpoint\'s "keep the stored key"', () => {
    expect(toSettingsBody(formValues({ recaptchaSecretKey: '' }))).not.toHaveProperty(
      'recaptchaSecretKey',
    )
    expect(toSettingsBody(formValues({ recaptchaSecretKey: '   ' }))).not.toHaveProperty(
      'recaptchaSecretKey',
    )
  })

  it('trims the whitelist and drops blank tags before they reach the wire', () => {
    const body = toSettingsBody(
      formValues({ publicApiFloodWhitelist: [' 10.0.0.0/8 ', '', '192.168.1.1'] }),
    )

    expect(body.publicApiFloodWhitelist).toEqual(['10.0.0.0/8', '192.168.1.1'])
  })

  it('keeps an empty fallback address empty rather than turning it into null', () => {
    const body = toSettingsBody(formValues())

    expect(body.fallbackStreamDownUrl).toBe('')
    expect(body.fallbackUserBannedUrl).toBe('')
    expect(body.fallbackUserExpiredUrl).toBe('')
    expect(body.fallbackMaxConnectionsUrl).toBe('')
  })

  it('never sends recaptchaConfigured, which is not a write field', () => {
    expect(toSettingsBody(formValues())).not.toHaveProperty('recaptchaConfigured')
  })
})

describe('toMagSettingsBody', () => {
  const mag = (overrides: Partial<MagSettings> = {}): MagSettings => ({
    showAllCategories: true,
    strictSecurity: true,
    showChannelLogos: true,
    showPreviewLogos: true,
    defaultContainer: 'mpeg-ts',
    defaultTheme: 'cappucino',
    recordingMaxLengthMinutes: 180,
    maxLocalRecordings: 10,
    playbackLimit: 3,
    allowedStbTypes: ['MAG255', 'MAG256'],
    allowedRecordingStbTypes: ['MAG255'],
    ...overrides,
  })

  it('sends all eleven fields unchanged', () => {
    const body = toMagSettingsBody(mag())

    expect(Object.keys(body)).toHaveLength(11)
    expect(body.defaultTheme).toBe('cappucino')
    expect(body.defaultContainer).toBe('mpeg-ts')
    expect(body.allowedRecordingStbTypes).toEqual(['MAG255'])
  })

  it('copies the two model lists so a later form edit cannot rewrite the request', () => {
    const values = mag()
    const body = toMagSettingsBody(values)

    body.allowedStbTypes.push('WR320')
    expect(values.allowedStbTypes).toEqual(['MAG255', 'MAG256'])
  })
})

describe('the STB catalogue', () => {
  it('is the backend\'s 31 entries, verbatim and in its order', () => {
    // The endpoint refuses the whole list for a name outside the catalogue, so this list is the
    // select's whole vocabulary. `WR320` is last and `Aura HD9` is the ninth entry; the middle
    // entry is `MAG245D`, the only name with a suffix letter.
    expect(STB_TYPES).toHaveLength(31)
    expect(STB_TYPES[0]).toBe('Aura HD')
    expect(STB_TYPES[8]).toBe('Aura HD9')
    expect(STB_TYPES[11]).toBe('MAG245D')
    expect(STB_TYPES[30]).toBe('WR320')
    expect(new Set(STB_TYPES).size).toBe(31)
  })
})

describe('normalizeWhitelist', () => {
  it('trims each entry and drops the blanks', () => {
    expect(normalizeWhitelist([' 10.0.0.1 ', ' ', ''])).toEqual(['10.0.0.1'])
    expect(normalizeWhitelist(undefined)).toEqual([])
  })
})

describe('isValidWhitelist', () => {
  it('accepts an empty list and real addresses or ranges', () => {
    expect(isValidWhitelist([])).toBe(true)
    expect(isValidWhitelist(undefined)).toBe(true)
    expect(isValidWhitelist(['192.168.1.1', '10.0.0.0/8', '2001:db8::/32'])).toBe(true)
  })

  it('refuses a typo the form can catch before the wire', () => {
    expect(isValidWhitelist(['not-an-address'])).toBe(false)
    expect(isValidWhitelist(['999.1.1.1'])).toBe(false)
    expect(isValidWhitelist(['10.0.0.0/33'])).toBe(false)
  })
})

describe('isValidFallbackUrl', () => {
  it('accepts blank, which means "no fallback"', () => {
    expect(isValidFallbackUrl('')).toBe(true)
    expect(isValidFallbackUrl(undefined)).toBe(true)
    expect(isValidFallbackUrl('   ')).toBe(true)
  })

  it('accepts an absolute http or https address', () => {
    expect(isValidFallbackUrl('https://example.com/notice.mp4')).toBe(true)
    expect(isValidFallbackUrl('http://example.com')).toBe(true)
  })

  it('refuses a relative address and a scheme the server does not accept', () => {
    // The likely typo: an operator who forgot the scheme. `new URL` refuses it, which is the
    // same answer `Uri.TryCreate(..., UriKind.Absolute)` gives.
    expect(isValidFallbackUrl('example.com/notice.mp4')).toBe(false)
    expect(isValidFallbackUrl('//example.com/notice.mp4')).toBe(false)
    expect(isValidFallbackUrl('ftp://example.com/notice.mp4')).toBe(false)
  })

  it('refuses an address past the column length', () => {
    expect(isValidFallbackUrl(`https://example.com/${'a'.repeat(2048)}`)).toBe(false)
  })
})
