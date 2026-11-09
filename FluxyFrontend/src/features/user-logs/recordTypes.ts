import type { UserLogRecordType } from './types'

/**
 * The ten record types, as a runtime list.
 *
 * It lives at the feature root rather than in `types/` for the reason `subscriptionOptions.ts`
 * does: `types/` and its barrel are type-only, so a value there would erase to nothing at the
 * call site that needs it. That call site is the address read-back - an element outside this
 * list is dropped rather than sent, because the endpoint refuses a bad element with a 400
 * under `recordTypes` - and the filter picker, which draws one option per entry.
 *
 * The order is the server's enum order, which is also the order a reader meets the events in
 * an account's life: registration, sign-in, resets, changes, sign-out.
 */
export const USER_LOG_RECORD_TYPES: readonly UserLogRecordType[] = [
  'registration_submit',
  'registration_confirm',
  'login',
  'password_reset_request',
  'password_reset_confirm',
  'username_change',
  'email_change',
  'password_change',
  'login_guard_change',
  'logout',
]
