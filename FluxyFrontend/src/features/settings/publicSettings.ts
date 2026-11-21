import { getPublicSettings } from './api'
import type { PublicSettings } from './types'

/**
 * The tab's one answer to "what is the public reCAPTCHA site key", asked at most once.
 *
 * Three call sites mint a captcha token on ordinary submits - the four guest forms through
 * `AuthForm` and `ConfirmCodeForm`, and the two profile-change flows through `ProfilePage` and
 * `LoginGuardCard` - and every one of them would otherwise spend a request on this read before
 * its own. The memo is therefore module state, not a query: it is not a list that may change
 * under the visitor, it is one configuration value that changes when an operator saves the
 * settings screen - and that save is what resets it.
 *
 * **The in-flight promise is the point, not an optimisation.** Two forms mounted at once would
 * each start a read without it, and the answer is identical; keeping the promise means the
 * second caller awaits the first request rather than a second one.
 *
 * **A failure answers "no key", never a rejection.** The whole calling path is a sign-in: an
 * installation whose settings endpoint is unreachable, or whose proxy answers an HTML page,
 * must degrade to the behaviour an empty site key has today (no token, the server skips its
 * check) rather than blocking the form. So the memo stores `''` on failure and does not cache
 * the failure as a rejection - the next submit asks again, which is the same
 * "loaded once, failed once" rule `recaptcha.ts` states for its script loader.
 */

/** The memoised key, or null while no read has settled. */
let memo: string | null = null

/** The read currently in flight, shared by every caller that arrives while it is. */
let inFlight: Promise<string> | null = null

/**
 * Forgets the memoised key, so the next caller re-reads it.
 *
 * Called by the settings mutation's `onSuccess`: the operator may have just changed the key,
 * and a tab that kept the old one would mint tokens against a site the new secret does not
 * recognise. Also exported for tests and probes, which need a fresh module state without a
 * reload.
 *
 * **Reset clears this memo, not the reCAPTCHA script.** `api.js` was loaded with the old key
 * as its `render` parameter and cannot be re-pointed, so `utils/recaptcha.ts` refuses the new
 * key in this tab and asks for a reload rather than minting an invalid token; see the note at
 * its `loadedForKey`.
 */
export const resetPublicSettings = (): void => {
  memo = null
  inFlight = null
}

/**
 * The public settings, asked once per tab unless `resetPublicSettings` ran.
 *
 * Never rejects; see the note above. An empty string means reCAPTCHA is disabled.
 */
export const readPublicSettings = (): Promise<PublicSettings> => {
  if (memo !== null) return Promise.resolve({ recaptchaSiteKey: memo })
  if (inFlight) return inFlight.then((siteKey) => ({ recaptchaSiteKey: siteKey }))

  const attempt = getPublicSettings().then(
    (answer) => {
      // `?? ''` rather than trusting the type: this body is read off the wire, and the whole
      // memo's contract is a string. A missing field is "disabled", not a reason to throw in a
      // sign-in handler.
      const siteKey = typeof answer?.recaptchaSiteKey === 'string' ? answer.recaptchaSiteKey : ''

      // The same identity check the failure path uses: `resetPublicSettings` may have run while
      // this read was in flight, and its answer is then older than the memo's contract allows.
      // A newer attempt owns `inFlight` from that moment on, and this one must not overwrite it.
      if (inFlight === attempt) {
        memo = siteKey
        inFlight = null
      }

      return siteKey
    },
    () => {
      // Not memoised as the settled answer, deliberately: a transient failure must not become
      // "reCAPTCHA is off for this tab". The empty string is returned to the caller that
      // needed it now, and the next submit asks again.
      if (inFlight === attempt) inFlight = null

      return ''
    },
  )

  inFlight = attempt

  return attempt.then((siteKey) => ({ recaptchaSiteKey: siteKey }))
}

/** The memoised site key, or the empty string - the shape `getCaptchaToken` wants. */
export const publicRecaptchaSiteKey = async (): Promise<string> =>
  (await readPublicSettings()).recaptchaSiteKey
