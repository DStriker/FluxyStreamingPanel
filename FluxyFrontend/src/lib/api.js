import i18n from '../i18n'
import { csrfHeader } from './csrf'

export async function submitAuth({ action, values, csrfToken, captchaToken }) {
  const payload = {
    username: values.username,
    password: values.password,
    captchaAction: action,
    captchaToken,
  }
  if (values.email) payload.email = values.email

  let res
  try {
    res = await fetch(`/api/auth/${action}`, {
      method: 'POST',
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        'X-Recaptcha-Token': captchaToken ?? '',
        ...csrfHeader(csrfToken),
      },
      body: JSON.stringify(payload),
    })
  } catch {
    throw new Error(i18n.t('messages.serverUnavailable'))
  }

  if (!res.ok) {
    const data = await res.json().catch(() => null)
    throw new Error(data?.message || i18n.t('messages.serverError', { status: res.status }))
  }

  return res.json().catch(() => ({}))
}
