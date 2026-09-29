import config from '../config'

const SCRIPT_ID = 'google-recaptcha-v3'
let loading = null

const loadScript = (siteKey) => {
  if (window.grecaptcha) return Promise.resolve(window.grecaptcha)
  if (loading) return loading

  loading = new Promise((resolve, reject) => {
    const script = document.createElement('script')
    script.id = SCRIPT_ID
    script.src = `https://www.google.com/recaptcha/api.js?render=${encodeURIComponent(siteKey)}`
    script.async = true
    script.defer = true
    script.onload = () => {
      if (window.grecaptcha) resolve(window.grecaptcha)
      else reject(new Error('reCAPTCHA isn\'t available'))
    }
    script.onerror = () => reject(new Error('reCAPTCHA wasn\'t loaded'))
    document.head.appendChild(script)
  })

  return loading
}

export async function getCaptchaToken(action, siteKey = config.RECAPTCHA_SITE_KEY) {
  if (import.meta.env.DEV) {
    return null
  }
  if (!siteKey) {
    console.warn('[recaptcha] Site key is not configured')
    return null
  }

  const grecaptcha = await loadScript(siteKey)
  await new Promise((resolve) => grecaptcha.ready(resolve))
  return grecaptcha.execute(siteKey, { action })
}
