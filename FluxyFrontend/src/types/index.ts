/**
 * One type per file, named after what it holds: `Role` in `Role.ts`, `Session` in
 * `Session.ts`, and so on. The file name *is* the import, so a reader looking for where
 * `MessageResponse` is defined finds it without a search.
 *
 * This barrel exists so callers do not have to know the layout: import from `../types`
 * (or `./types`) and name the type. `export type` rather than `export` because every
 * re-export here is a type - under `isolatedModules` a value export of a type is an error
 * rather than a slow build.
 *
 * Nothing in this folder imports anything outside it except another type in it. The
 * moment a type grows behaviour it belongs in `lib/`, not here: this is the vocabulary,
 * not the code.
 */

export type { Role } from './Role'
export type { Session } from './Session'
export type { SessionContextValue } from './SessionContextValue'
export type { SessionVisit } from './SessionVisit'
export type { SessionHistoryResponse } from './SessionHistoryResponse'
export type { FieldErrors } from './FieldErrors'
export type { MessageResponse } from './MessageResponse'
export type { ProfileResponse } from './ProfileResponse'
export type { LoginGuardSettings } from './LoginGuardSettings'
export type { GeoLookup } from './GeoLookup'
export type { PasswordStatusResponse } from './PasswordStatusResponse'
export type { CsrfTokenResponse } from './CsrfTokenResponse'
