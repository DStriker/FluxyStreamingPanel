import { apiFetch, apiGet } from '../../utils/http'
import type { MessageResponse } from '../../types'
import type {
  ApplicationSettings,
  ApplicationSettingsValues,
  MagSettings,
  MagSettingsValues,
  PublicSettings,
} from './types'

/**
 * The installation's two one-row resources and the guest read beside them.
 *
 * Neither resource has an identifier, a create or a delete: each table holds one row, created
 * by the migration and kept by the seeder. That is why there is no `POST`, no `{id}` and no
 * `DELETE` here - the collection answers `405` to every method it does not own, and a screen
 * that offered one would be offering a request the server refuses.
 *
 * **The reads are behind `viewSettings`, both writes behind `editSettings`**, and the guest
 * read is anonymous - the one value a sign-in form needs before anybody has signed in.
 */

/** The installation settings as the admin screen reads them: `GET /admin/settings`. */
export const getSettings = (signal?: AbortSignal): Promise<ApplicationSettings> =>
  apiGet<ApplicationSettings>('/admin/settings', signal)

/**
 * Writes the installation settings: `PATCH /admin/settings` - a full write.
 *
 * `recaptchaSecretKey` is the one optional field: absent or empty keeps the stored secret,
 * because the read never handed the form a copy to send back. A refused field answers
 * `400 validation_failed` keyed by its camelCase name, which is the same name the `Form.Item`s
 * carry.
 */
export const updateSettings = ({
  values,
  csrfToken,
}: {
  values: ApplicationSettingsValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/admin/settings', { method: 'PATCH', body: values, csrfToken })

/** The MAG portal settings: `GET /admin/mag-settings`. */
export const getMagSettings = (signal?: AbortSignal): Promise<MagSettings> =>
  apiGet<MagSettings>('/admin/mag-settings', signal)

/**
 * Writes the MAG portal settings: `PATCH /admin/mag-settings`.
 *
 * All eleven fields are required and none is optional, so the body is the read shape exactly.
 * Both model lists are validated against the STB catalogue - a name outside it refuses the
 * whole list rather than being dropped.
 */
export const updateMagSettings = ({
  values,
  csrfToken,
}: {
  values: MagSettingsValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/admin/mag-settings', { method: 'PATCH', body: values, csrfToken })

/**
 * The one anonymous read: `GET /auth/public-settings`.
 *
 * The endpoint is answered `Cache-Control: no-store` because an operator can change the key and
 * a shared cache holding the old answer would hand a stale key to every visitor behind it. The
 * browser side of that rule is `publicSettings.ts`, which memoises the answer for the tab's
 * lifetime rather than asking on every submit - and is reset when a settings write succeeds.
 *
 * Deliberately called through the memo, not directly by a form: see `publicSettings.ts` for why
 * the guest forms must never block a sign-in on this request.
 */
export const getPublicSettings = (signal?: AbortSignal): Promise<PublicSettings> =>
  apiGet<PublicSettings>('/auth/public-settings', signal)
