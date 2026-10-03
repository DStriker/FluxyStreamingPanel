/**
 * The login name of a password reset that is still waiting for its code.
 *
 * The reset form is two steps on one page for the same reason registration is, and the same
 * reason registration mirrors its address into `sessionStorage`: a reload between the two
 * steps would otherwise send the visitor back to the first form. Here it costs more than a
 * re-typed address, because asking for a new code *replaces* the pending one - so a visitor
 * who reloaded to fix a typo'd layout would invalidate the code already in their inbox.
 *
 * A convenience, never a source of truth: the server keys the confirmation by login name, so
 * a lost or stale value costs one trip back to the first step and nothing else. Cleared once
 * the reset goes through and once the code expires, when the stored name could only produce
 * a second, guaranteed failure.
 */

const KEY = 'fluxy-pending-reset'

export const readPendingReset = () => {
  try {
    return sessionStorage.getItem(KEY) ?? ''
  } catch {
    // Private browsing and a full quota both make sessionStorage throw. Not worth failing a
    // render over - the form simply starts at the first step.
    return ''
  }
}

export const rememberPendingReset = (username) => {
  try {
    sessionStorage.setItem(KEY, username)
  } catch {
    // See above: the worst case is being asked to type the name again.
  }
}

export const forgetPendingReset = () => {
  try {
    sessionStorage.removeItem(KEY)
  } catch {
    // Nothing to clean up if it was never stored.
  }
}
