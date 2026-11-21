import { describe, expect, it, vi } from 'vitest'

/**
 * The pure half of the rotation guard in `src/utils/recaptcha.ts`.
 *
 * `api.js?render=<key>` fixes the key for that page load, so the loader cannot be asked for a
 * second key. What can be asserted without a DOM is the decision `getCaptchaToken` takes before
 * it touches `grecaptcha`: reuse the loaded API for the same key, refuse a changed one.
 *
 * The settings import is stubbed because the decision needs no request.
 */

vi.mock('../../src/features/settings/publicSettings', () => ({
  publicRecaptchaSiteKey: vi.fn(),
}))

const { loadedScriptUse } = await import('../../src/utils/recaptcha')

describe('loadedScriptUse', () => {
  it('loads when nothing has been loaded yet', () => {
    expect(loadedScriptUse(null, 'first-key')).toBe('load')
  })

  it('reuses a script loaded for the same key', () => {
    expect(loadedScriptUse('first-key', 'first-key')).toBe('reuse')
  })

  it('refuses to hand a changed key to the script already loaded', () => {
    // This is the branch `getCaptchaToken` turns into `null` plus one warning: the loaded
    // `grecaptcha.execute('second-key', ...)` would reject, and every later submit with it.
    expect(loadedScriptUse('first-key', 'second-key')).toBe('key-changed')
  })
})
