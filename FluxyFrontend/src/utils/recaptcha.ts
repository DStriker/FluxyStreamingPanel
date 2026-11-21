import { publicRecaptchaSiteKey } from '../features/settings/publicSettings'

/**
 * The part of Google's reCAPTCHA v3 API this application calls, declared here rather than
 * from `@types/grecaptcha`, which would add a dependency for three lines. The official
 * loader puts `grecaptcha` on `window`, so the module reads it through the cast below.
 */
interface Grecaptcha {
  /** Runs `callback` once the API is usable - including when it already was. */
  ready(callback: () => void): void
  /** Mints a token for `action`. The action is fixed per endpoint on the server side. */
  execute(siteKey: string, options: { action: string }): Promise<string>
}

type RecaptchaWindow = Window & { grecaptcha?: Grecaptcha }

const SCRIPT_ID = 'google-recaptcha-v3'
let loading: Promise<Grecaptcha> | null = null

/**
 * The key this module loaded `api.js` with, or `null` while it has not loaded one.
 *
 * `render` is a **load-time** parameter: Google's loader bakes the key into the `grecaptcha`
 * it installs, and there is no supported way to re-point an already loaded API at another
 * key. Appending a second `api.js?render=<newKey>` tag to re-load it is at best unreliable
 * (the first script has already defined `grecaptcha`, and Google documents `render` as fixed
 * for that load), so a key changed at runtime - which the settings screen now allows - is not
 * picked up until the page is reloaded. `getCaptchaToken` therefore refuses to hand a
 * mismatched key to the loaded API and degrades to the no-token path instead.
 */
let loadedForKey: string | null = null

/** Whether a warning about the rotation limitation has already been logged in this tab. */
let warnedKeyChange = false

/** What to do with the API the page already carries. */
export type LoadedScriptUse = 'load' | 'reuse' | 'key-changed'

/**
 * The pure decision behind the rotation guard: `load` when no script was loaded by this
 * module, `reuse` when it was loaded for the same key, and `key-changed` otherwise.
 */
export const loadedScriptUse = (
  loadedFor: string | null,
  siteKey: string,
): LoadedScriptUse => {
  if (loadedFor === null) return 'load'
  return loadedFor === siteKey ? 'reuse' : 'key-changed'
}

const loadScript = (siteKey: string): Promise<Grecaptcha> => {
  const win = window as RecaptchaWindow
  if (win.grecaptcha) {
    // A script this module did not load - adopt the key on first sight, so a later rotation is
    // still noticed; there is no better answer for an API someone else installed.
    loadedForKey ??= siteKey
    return Promise.resolve(win.grecaptcha)
  }
  if (loading) return loading

  const attempt = new Promise<Grecaptcha>((resolve, reject) => {
    const script = document.createElement('script')
    script.id = SCRIPT_ID
    script.src = `https://www.google.com/recaptcha/api.js?render=${encodeURIComponent(siteKey)}`
    script.async = true
    script.defer = true
    script.onload = () => {
      if (win.grecaptcha) resolve(win.grecaptcha)
      else reject(new Error('reCAPTCHA isn\'t available'))
    }
    script.onerror = () => reject(new Error('reCAPTCHA wasn\'t loaded'))
    // Recorded before the append, so an in-flight load counts as "loaded for this key" too:
    // a call that arrives with a different one while this is still on the wire would otherwise
    // reuse this promise and then execute against a key the script was not rendered for.
    loadedForKey = siteKey
    document.head.appendChild(script)
  })

  // A failure must not become the cached answer. `loading` is module state, so a rejected
  // promise left standing here makes every later `getCaptchaToken` in the tab reject
  // instantly - and with a site key configured that is *every* submit failing until a
  // reload, which is a permanent outage caused by one transient one. Dropping the binding
  // is what turns "loaded once, failed once" into "try again on the next submit", and
  // taking the broken element with it lets the retry append a fresh script rather than
  // find the id already taken. The identity check keeps a slower rejection from clearing
  // a newer attempt.
  const cached = attempt.catch((err: unknown) => {
    if (loading === cached) {
      loading = null
      loadedForKey = null
      document.getElementById(SCRIPT_ID)?.remove()
    }
    throw err
  })

  loading = cached
  return cached
}

/**
 * A token for `action`, or `null` when this build cannot produce one - in development,
 * or when no site key is configured. `null` is an answer the caller passes on, not a
 * failure: the server skips the check when its own key is empty, and the two empties are
 * meant to be the same machine.
 *
 * **The key is resolved at call time from the installation's own settings.** There is no
 * build-time variable, so changing the key needs no rebuild or deployment: the anonymous
 * `GET /auth/public-settings` returns the public half of the row the backend keeps it in, and
 * `publicSettings.ts` memoises that read for the tab and resets it when the settings screen
 * saves, so the next submit asks again. A key changed that way still cannot re-point an
 * `api.js` already loaded in this tab; see `loadedForKey` above for why a reload is needed.
 *
 * The resolve is deliberately **not allowed to block a sign-in**: a failed read answers an
 * empty key, and an empty key is the same "no token" path this function has always had. The
 * explicit `siteKey` parameter stays for a caller that already holds one (the probes and any
 * future surface that mints against a second property).
 */
export async function getCaptchaToken(
  action: string,
  siteKey?: string,
): Promise<string | null> {
  if (import.meta.env.DEV) {
    return null
  }

  const resolved = siteKey ?? (await publicRecaptchaSiteKey())

  if (!resolved) {
    console.warn('[recaptcha] Site key is not configured')
    return null
  }

  if (loadedScriptUse(loadedForKey, resolved) === 'key-changed') {
    // Fails closed rather than letting `grecaptcha.execute` reject with a key the loaded API
    // was not rendered for - that exception is what used to make every later submit fail too.
    if (!warnedKeyChange) {
      warnedKeyChange = true
      console.warn(
        '[recaptcha] The site key changed after the reCAPTCHA script loaded; reload the page to use the new key. Submitting without a captcha token until then.',
      )
    }
    return null
  }

  const grecaptcha = await loadScript(resolved)
  await new Promise<void>((resolve) => grecaptcha.ready(() => resolve()))
  return grecaptcha.execute(resolved, { action })
}
