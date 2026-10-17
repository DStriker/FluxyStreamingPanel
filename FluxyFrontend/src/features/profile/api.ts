import { apiFetch, apiGet } from '../../lib/http'
import type { LoginGuardSettings, MessageResponse } from '../../types'
import type { GeoLookup } from './types/GeoLookup'
import type { ProfileResponse } from './types/ProfileResponse'
import type { CallOptions } from '../../lib/http'

/**
 * The signed-in account's own profile: the four changes it can make, the login guard, and
 * the two reads the profile page runs - its own facts and the network the request arrived
 * from.
 *
 * The one write endpoint behind the four changes is `PATCH /auth/profile`, with a body
 * naming exactly one of them; the private helper below is where its method, path and two
 * tokens are written once.
 */

/**
 * The actions a reCAPTCHA token has to have been minted for on the two profile-change
 * endpoints. Unlike the three sign-ins they are not split per role: a profile change has no
 * role to speak of, and any signed-in account may change its own.
 */
export const ProfileChangeCaptchaAction = 'profile_change'
export const ProfileConfirmCaptchaAction = 'profile_confirm'

/**
 * The three facts the profile page shows: username, email and role.
 *
 * It is not `currentSession`. That one reports the claims of the token, and the address is
 * deliberately not among them - a claim is a copy that outlives the change it describes -
 * so anything that has to show or verify the current address has to ask the row.
 */
export const getProfile = (signal?: AbortSignal): Promise<ProfileResponse> =>
  apiGet<ProfileResponse>('/auth/profile', signal)

/**
 * The one endpoint every profile edit goes through: `PATCH /auth/profile`.
 *
 * The backend collapsed the five `POST /auth/profile/*` paths into this single partial
 * update (REST: a profile is one resource, so it is edited by one method on one path).
 * The body names exactly one change — `username`, `email`, `newPassword`, `loginGuard`
 * or `timeZone` — and the server refuses a body naming none or two rather than picking
 * an order, because the staged-confirmation flow holds one pending change at a time.
 *
 * `body` is a record rather than a typed value shape because the five wrappers below
 * each build their own single-change body; the shared parts (the method, the path, the
 * two tokens) are what this helper exists to keep in one place.
 */
const patchProfile = ({
  body,
  csrfToken,
  captchaToken,
}: CallOptions & { body: Record<string, unknown> }): Promise<MessageResponse> =>
  apiFetch('/auth/profile', { method: 'PATCH', body, csrfToken, captchaToken })

/**
 * Starts changing one of the four sensitive fields, and answers with the outcome.
 *
 * A 2xx means one of two things and the `code` says which: `profile_updated` when this
 * installation has no mail server and the change is already on the account, and
 * `profile_change_submitted` when a code is on its way and the caller has to run it through
 * `confirmProfileChange`. Both are successes; only the second one continues.
 */
export const changeUsername = ({
  currentPassword,
  username,
  csrfToken,
  captchaToken,
}: CallOptions & { currentPassword: string; username: string }): Promise<MessageResponse> =>
  patchProfile({ body: { currentPassword, username }, csrfToken, captchaToken })

/** The code for an address change is mailed to the new one, never to the old one. */
export const changeEmail = ({
  currentPassword,
  email,
  csrfToken,
  captchaToken,
}: CallOptions & { currentPassword: string; email: string }): Promise<MessageResponse> =>
  patchProfile({ body: { currentPassword, email }, csrfToken, captchaToken })

/**
 * Starts changing the password. The new one replaces the old only once the mailed code is
 * entered, and doing so ends every session of the account - the browser that made the
 * change is signed straight back in, every other device has to sign in again.
 */
export const changePassword = ({
  currentPassword,
  newPassword,
  csrfToken,
  captchaToken,
}: CallOptions & { currentPassword: string; newPassword: string }): Promise<MessageResponse> =>
  patchProfile({ body: { currentPassword, newPassword }, csrfToken, captchaToken })

/**
 * Applies a pending profile change.
 *
 * No identifier travels with the code: the account comes from the session, so a code is
 * never a credential on its own and cannot be spent by whoever is holding it.
 */
export const confirmProfileChange = ({
  code,
  csrfToken,
  captchaToken,
}: CallOptions & { code: string }): Promise<MessageResponse> =>
  apiFetch('/auth/profile/confirm', {
    body: { code },
    csrfToken,
    captchaToken,
  })

/**
 * What the login guard form submits: the current password proving the session is the
 * owner's, plus the guard itself.
 *
 * The guard is `LoginGuardSettings` rather than a second spelling of the same six
 * fields, because the card edits that shape and the endpoint accepts it - one shape
 * with two spellings would drift the way every duplicated contract eventually does.
 */
export interface LoginGuardValues {
  currentPassword: string
  guard: LoginGuardSettings
}

/**
 * Starts changing the login guard: the two switches and the three allow lists.
 *
 * Like the other three changes, a 2xx is one of two things and the `code` says which:
 * `profile_updated` when the change is already on the account, and
 * `profile_change_submitted` when a code is on its way through `confirmProfileChange`.
 * A new guard touches no session either way - the refresh path rechecks each session
 * against the new lists on its next rotation.
 */
export const changeLoginGuard = ({
  currentPassword,
  guard,
  csrfToken,
  captchaToken,
}: CallOptions & { currentPassword: string; guard: LoginGuardSettings }): Promise<MessageResponse> =>
  patchProfile({
    body: {
      currentPassword,
      loginGuard: {
        geoProtectionEnabled: guard.geoProtectionEnabled,
        bindSessionToIp: guard.bindSessionToIp,
        allowedIps: guard.allowedIps,
        allowedCountry: guard.allowedCountry,
        allowedAutonomousSystemNumber: guard.allowedAutonomousSystemNumber,
      },
    },
    csrfToken,
    captchaToken,
  })

/**
 * What the GeoIP database knows about the network this browser arrived from.
 *
 * Exists for the "use my current network" button: a visitor cannot be expected to know
 * their own autonomous system number. Throws like every other call when the backend is
 * unreachable - the button then toasts the transport error rather than filling the form
 * with nothing.
 */
export const lookupGeo = (): Promise<GeoLookup> => apiGet<GeoLookup>('/auth/geo/lookup')

/**
 * Sets the time zone the profile is displayed in, or clears it with `null` so the browser's
 * own decides again.
 *
 * The one profile call without a captcha token: the endpoint expects no captcha (a
 * signed-in visitor saving a display preference), and a header the server does not read
 * would only suggest otherwise. The antiforgery token is still required - it is checked on
 * every write of this API, session or not.
 *
 * `null` is sent as an empty string, and that is the contract rather than a detail: on the
 * merged `PATCH` an absent `timeZone` means "leave it alone" while an explicit empty string
 * means "clear the preference", so a literal `null` in the body would be read as no change
 * at all and the button would appear to do nothing.
 */
export const updateTimezone = ({
  timeZone,
  csrfToken,
}: {
  timeZone: string | null
  csrfToken: string | null
}): Promise<MessageResponse> =>
  patchProfile({ body: { timeZone: timeZone ?? '' }, csrfToken, captchaToken: null })
