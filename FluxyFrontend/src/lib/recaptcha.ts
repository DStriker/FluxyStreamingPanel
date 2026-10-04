import config from '../config'

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

const loadScript = (siteKey: string): Promise<Grecaptcha> => {
  const win = window as RecaptchaWindow
  if (win.grecaptcha) return Promise.resolve(win.grecaptcha)
  if (loading) return loading

  loading = new Promise<Grecaptcha>((resolve, reject) => {
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
    document.head.appendChild(script)
  })

  return loading
}

/**
 * A token for `action`, or `null` when this build cannot produce one - in development,
 * or when no site key is configured. `null` is an answer the caller passes on, not a
 * failure: the server skips the check when its own key is empty, and the two empties are
 * meant to be the same machine.
 */
export async function getCaptchaToken(
  action: string,
  siteKey: string = config.RECAPTCHA_SITE_KEY,
): Promise<string | null> {
  if (import.meta.env.DEV) {
    return null
  }
  if (!siteKey) {
    console.warn('[recaptcha] Site key is not configured')
    return null
  }

  const grecaptcha = await loadScript(siteKey)
  await new Promise<void>((resolve) => grecaptcha.ready(() => resolve()))
  return grecaptcha.execute(siteKey, { action })
}
