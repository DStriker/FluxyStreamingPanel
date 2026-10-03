import { useEffect, useState } from 'react'
import { Navigate } from 'react-router-dom'
import { Skeleton } from 'antd'
import config, { routePath } from '../config'
import { currentSession } from '../lib/api'
import { sessionOpensArea } from '../lib/session'
import { SessionContext } from '../lib/sessionContext'
import NotFoundPage from '../pages/NotFoundPage'

/**
 * Lets only a signed-in visitor whose role matches through, and hands the rest a decision
 * of their own.
 *
 * Three outcomes, in this order:
 *
 * 1. **Still asking** - a skeleton. `GuestOnly` renders its children while it waits, and
 *    that is the right call there because a login form has no side effects until it is
 *    submitted. This guard has to render the *area*, and showing an admin sidebar to
 *    somebody whose role has not been confirmed yet is the expensive direction to be
 *    wrong in - the cheap one is a moment of skeleton. `fetch` has no timeout, so a
 *    backend that never answers leaves the skeleton up rather than showing the wrong
 *    room, which is the failure this guard exists to prevent.
 * 2. **No session** - to the sign-in form, with `replace`, so the back button does not
 *    return to a page that will only redirect again. `currentSession` answers `null` both
 *    for "no session" and for "the server could not be asked", and both mean the same
 *    thing here: no area.
 * 3. **A role that does not match** - 404, not a redirect. Each entrance is a door to one
 *    area and the server enforces exactly the same rule (`AdminOnly` and friends demand
 *    one role, not a minimum), so a client sitting on `/admin/dashboard` is not "in the
 *    wrong place, let me show them theirs" - there is nothing here for them to see. A
 *    redirect would also have to invent a destination the visitor did not ask for.
 *
 * With no `role` the guard only requires *some* session. That is what the catch-all route
 * uses: an unknown path from a signed-in visitor is a 404, and from a visitor with no
 * session it is still the sign-in form, which is where a typo'd URL lands.
 *
 * The session is published to `children` so everything below - the header, the not-found
 * page - reads the one answer this component already obtained instead of asking again.
 */
export default function RequireAuth({ role, children }) {
  // `undefined` means "not answered yet"; `null` is an answer - no session.
  const [session, setSession] = useState(undefined)

  useEffect(() => {
    let cancelled = false

    currentSession().then((answer) => {
      if (cancelled) return
      setSession(answer)
    })

    return () => {
      cancelled = true
    }
  }, [])

  if (session === undefined) return <Skeleton active paragraph={{ rows: 6 }} />
  if (!session) return <Navigate to={routePath(config.CLIENT_LOGIN_ROUTE)} replace />

  const allowed = sessionOpensArea(session, role)

  return (
    <SessionContext.Provider value={session}>
      {allowed ? children : <NotFoundPage />}
    </SessionContext.Provider>
  )
}
