/**
 * `GET /auth/public-settings` - the one value a guest form needs before anybody has signed in.
 *
 * An empty `recaptchaSiteKey` means reCAPTCHA is disabled, and that is the whole contract: no
 * secret, no score, no action list. The read is anonymous and answered `Cache-Control:
 * no-store`, because an operator can change the key and a cached answer would hand the old one
 * to every visitor behind a shared cache.
 */
export interface PublicSettings {
  /** Public reCAPTCHA site key, or an empty string when reCAPTCHA is disabled. */
  recaptchaSiteKey: string
}
