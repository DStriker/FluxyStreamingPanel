/**
 * The auth feature's response contracts — today only the password-reset status — re-exported
 * as types only so a consumer names the folder rather than a file. `export type` because
 * nothing here is a runtime value.
 */
export type { PasswordStatusResponse } from './PasswordStatusResponse'
