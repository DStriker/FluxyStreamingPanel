import { afterEach, describe, expect, it, vi } from 'vitest'
import config from '../config'
import { apiUrl, internalPath } from './url'

/**
 * The two rules every request and every server-named destination passes through. The
 * composition of a URL is asserted against the configured base rather than a literal,
 * because the base is an environment decision: what must hold here is that the two halves
 * join without a doubled separator, whatever the base is.
 */

afterEach(() => {
  vi.unstubAllEnvs()
  vi.resetModules()
})

describe('apiUrl', () => {
  it('joins the configured base and the path', () => {
    expect(apiUrl('/auth/csrf')).toBe(`${config.API_BASE_URL}/auth/csrf`)
  })

  it('never leaves a trailing separator on the base for the join to double', () => {
    // The trimming in `config` is what this pins: `https://api.example.com/` must not
    // reach the page as `https://api.example.com//auth/csrf`.
    expect(config.API_BASE_URL.endsWith('/')).toBe(false)
  })

  it('honours a configured base - the split-deployment case', async () => {
    // Re-import behind `resetModules` so `config` reads the stubbed value; the module
    // reads `import.meta.env` once, when it is first evaluated, which is the behaviour
    // this test needs to exercise rather than bypass.
    vi.stubEnv('VITE_API_BASE_URL', 'https://api.example.com/')
    vi.resetModules()

    const { apiUrl: freshApiUrl } = await import('./url')

    expect(freshApiUrl('/auth/csrf')).toBe('https://api.example.com/auth/csrf')
  })
})

describe('internalPath', () => {
  it('accepts a site path and answers it unchanged', () => {
    expect(internalPath('/admin/dashboard')).toBe('/admin/dashboard')
    expect(internalPath('/client/index?tab=1')).toBe('/client/index?tab=1')
    expect(internalPath('/admin/')).toBe('/admin/')
  })

  it('refuses everything that is not a string', () => {
    expect(internalPath(null)).toBeNull()
    expect(internalPath(undefined)).toBeNull()
    expect(internalPath(42)).toBeNull()
    expect(internalPath({ path: '/admin' })).toBeNull()
  })

  it('refuses a value that is not this site path', () => {
    expect(internalPath('')).toBeNull()
    expect(internalPath('admin/dashboard')).toBeNull()
    expect(internalPath('https://evil.example/admin')).toBeNull()
    // Protocol-relative: a different origin wearing the same first character.
    expect(internalPath('//evil.example/admin')).toBeNull()
  })

  it('refuses backslashes, which some clients read as separators', () => {
    expect(internalPath('/admin\\dashboard')).toBeNull()
    expect(internalPath('\\\\evil.example\\share')).toBeNull()
  })

  it('refuses control characters, which would be carried into the address bar', () => {
    expect(internalPath('/admin\n/dashboard')).toBeNull()
    expect(internalPath('/admin\t/dashboard')).toBeNull()
    expect(internalPath('/admin\u0000')).toBeNull()
  })
})
