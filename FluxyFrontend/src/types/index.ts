/**
 * The cross-domain vocabulary: one type per file, named after what it holds, so the file
 * name *is* the import and a reader looking for where `Session` is defined finds it without
 * a search. A domain's own response contracts live in `features/<domain>/types/` instead,
 * beside the `api.ts` that returns them; what remains here is what more than one feature or
 * `src/lib` names — the role and status enums, the permission catalog, the session and its
 * context value, the bulk and message responses, the login guard settings and the CSRF
 * answer.
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
export type { UserStatus } from './UserStatus'
export type { Session } from './Session'
export type { SessionContextValue } from './SessionContextValue'
export type { BulkItemResponse } from './BulkItemResponse'
export type { BulkOperationResponse } from './BulkOperationResponse'
export type { UserPermission } from './UserPermission'
export type { FieldErrors } from './FieldErrors'
export type { MessageResponse } from './MessageResponse'
export type { LoginGuardSettings } from './LoginGuardSettings'
export type { CsrfTokenResponse } from './CsrfTokenResponse'
