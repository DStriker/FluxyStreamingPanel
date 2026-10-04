import { useCallback, useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { Navigate } from 'react-router-dom'
import { Skeleton } from 'antd'
import config, { routePath } from '../config'
import { currentSession } from '../lib/api'
import { sessionOpensArea } from '../lib/session'
import { SessionContext } from '../lib/sessionContext'
import NotFoundPage from '../pages/NotFoundPage'
import type { Role, Session } from '../types'

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
export default function RequireAuth({
  role,
  children,
}: {
  /** The one role this area belongs to; absent means "any signed-in visitor". */
  role?: Role
  children: ReactNode
}) {
  // Three states in one slot, which is the whole point of the annotation: `undefined` means
  // "not answered yet" (render the skeleton), `null` is an answer - no session - and
  // anything else is the session itself. Inferred from `useState(undefined)` the state would
  // be typed `null` and the two answers below could not both be represented.
  const [session, setSession] = useState<Session | null | undefined>(undefined)

  /**
   * Asks again who is signed in and republishes the answer.
   *
   * One flow needs it, and it is the reason this is a function and not only an effect:
   * changing the username changes the `name` claim the header is displaying, while the
   * value held here was obtained before the change went through. Reloading the page would
   * also make the guard re-run - and it takes the success message off the screen before
   * the visitor has had time to read it, which is how a correct change looks like nothing
   * happened. Having the header ask `/auth/me` itself instead would put a second answer to
   * a question this component already owns, with two chances for them to disagree.
   *
   * The old value stays on screen until the new one arrives, so a refresh never flashes a
   * skeleton over a signed-in area. An answer of `null` means the session ended and the
   * redirect below takes over, which is the same outcome the initial ask would produce.
   */
  const refresh = useCallback(async () => {
    const answer = await currentSession()
    setSession(answer)
    return answer
  }, [])

  useEffect(() => {
    refresh()
  }, [refresh])

  if (session === undefined) return <Skeleton active paragraph={{ rows: 6 }} />
  if (!session) return <Navigate to={routePath(config.CLIENT_LOGIN_ROUTE)} replace />

  const allowed = sessionOpensArea(session, role)

  return (
    <SessionContext.Provider value={{ ...session, refresh }}>
      {allowed ? children : <NotFoundPage />}
    </SessionContext.Provider>
  )
}
