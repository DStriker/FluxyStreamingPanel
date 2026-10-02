import { useEffect, useState } from 'react'
import { Navigate } from 'react-router-dom'
import { currentSession } from '../lib/api'
import { homeForRole } from '../lib/session'

/**
 * Keeps a signed-in visitor off the pages that exist only for somebody who is not.
 *
 * The three sign-in forms and the registration flow are guest pages, and showing them to an
 * account that already holds a session invites exactly the conflict they were built to
 * avoid: signing the same browser in a second time overwrites the cookies while the first
 * refresh family stays alive in the database, where nobody can end it anymore - the
 * browser that owned it has already forgotten it - and confirming a registration inside a
 * session does the same thing at the end of a form the visitor had no reason to fill in.
 * So the check runs on the way in, and a confirmed session is sent where a fresh sign-in
 * would send it: by role, never by entrance (`homeForRole` mirrors the server's own
 * `LandingPathOf`).
 *
 * The children render while the check is in flight, deliberately. A login form must not be
 * something the page waits for, and `fetch` has no timeout - a backend that never answers
 * would otherwise hold a visitor on a spinner with no form in sight, which is the failure
 * this repository has already learned to avoid once. The cost is that an authenticated
 * visitor sees the form for the length of one round trip before it replaces itself, and
 * that is the cheap direction to be wrong in: the form has no side effects until it is
 * submitted.
 *
 * `currentSession` answers `null` both when there is no session and when the server could
 * not be asked, and both mean the same thing to this page - show the form. A guard that
 * refused to render until the server confirmed the absence of a session would make the
 * sign-in page depend on the very connectivity the sign-in page is for.
 *
 * It lives in the router rather than inside the pages so the rule is stated once and the
 * pages stay the thin wrappers they are; nothing here needs to know it is being guarded.
 */
export default function GuestOnly({ children }) {
  const [landing, setLanding] = useState(null)

  useEffect(() => {
    let cancelled = false

    currentSession().then((session) => {
      if (cancelled || !session) return
      setLanding(homeForRole(session.role))
    })

    return () => {
      cancelled = true
    }
  }, [])

  return landing ? <Navigate to={landing} replace /> : children
}
