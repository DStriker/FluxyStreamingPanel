/**
 * What every call carries beside its values: the antiforgery token, and outside
 * development the captcha one.
 *
 * Both are `string | null` rather than `string` because that is what minting them answers
 * when it cannot - `getCsrfToken` is silent when the backend is absent, `getCaptchaToken`
 * is silent in development and whenever no site key is configured. Required rather than
 * optional because every call site already supplies them: a forgotten `csrfToken` compiles
 * just as happily as a wrong one and fails later, at the server, as `csrf_invalid`.
 */
export interface CallOptions {
  csrfToken: string | null
  captchaToken: string | null
}
