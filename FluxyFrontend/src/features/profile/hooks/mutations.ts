import {
  changeEmail,
  changeLoginGuard,
  changePassword,
  changeUsername,
  confirmProfileChange,
  updateTimezone,
} from '../api'
import { useWrite } from '../../../hooks/useWrite'

/**
 * The profile feature's writes: the six changes the profile page starts.
 *
 * Built on `useWrite` (`src/hooks/useWrite.ts`), so the antiforgery token is minted at
 * call time and the captcha token - which belongs to an action the form knows - stays
 * with the caller. Five of the six join the confirmation step; the time zone is the one
 * that saves straight away.
 */

/** Starts a login-name change; `profile_change_submitted` means a code is on its way. */
export const useChangeUsername = () => useWrite(changeUsername)

/** Starts an address change; the code goes to the *new* address. */
export const useChangeEmail = () => useWrite(changeEmail)

/** Starts a password change; confirming it ends every other session of the account. */
export const useChangePassword = () => useWrite(changePassword)

/** Applies a pending change with the code that was mailed out. */
export const useConfirmProfileChange = () => useWrite(confirmProfileChange)

/** Starts a login-guard change: the two switches and the three allow lists. */
export const useChangeLoginGuard = () => useWrite(changeLoginGuard)

/** Saves the display time zone - the one profile write with no captcha of its own. */
export const useUpdateTimezone = () => useWrite(updateTimezone)
