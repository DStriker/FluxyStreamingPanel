/**
 * The address of a registration that is still waiting for its code.
 *
 * Registration is deliberately two steps on one page, which means the address only exists
 * in component state - and a reload between the two steps would otherwise throw it away
 * and send the visitor back to the first form. `sessionStorage` survives a reload and dies
 * with the tab, which is exactly the lifetime of "a confirmation in progress".
 *
 * It is a convenience, never a source of truth: the server keys the confirmation by the
 * address alone, so a lost or stale value costs a re-typed email and nothing else.
 */

const KEY = 'fluxy-pending-registration'

export const readPendingEmail = () => {
  try {
    return sessionStorage.getItem(KEY) ?? ''
  } catch {
    // Private browsing and a full quota both make sessionStorage throw. Not worth
    // failing a render over - the form simply starts empty.
    return ''
  }
}

export const rememberPendingEmail = (email) => {
  try {
    sessionStorage.setItem(KEY, email)
  } catch {
    // See above: the worst case is being asked to type the address again.
  }
}

/**
 * Forgets the pending registration.
 *
 * Called once the account is confirmed and once the code has expired - in both cases the
 * stored address would only produce a second, guaranteed failure.
 */
export const forgetPendingEmail = () => {
  try {
    sessionStorage.removeItem(KEY)
  } catch {
    // Nothing to clean up if it was never stored.
  }
}