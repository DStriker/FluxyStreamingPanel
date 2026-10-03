import { createContext, useContext } from 'react'

/**
 * The account the current area was opened for, or `null` outside one.
 *
 * It is provided by `RequireAuth`, which is the component that already had to ask
 * `/auth/me` before it could decide anything - so the header gets the username and the
 * role from a request that had to happen anyway, rather than making a second one.
 *
 * The value also carries `refresh`, which asks `/auth/me` again and republishes. It
 * exists for the one flow that changes the answer after it has been read: renaming the
 * account rewrites the `name` claim the header displays, so what is held here is true
 * until the moment it stops being true. Anything that renames the account has to call it,
 * or the old name stays up for as long as the area stays mounted.
 *
 * Deliberately not a provider with its own fetching state: a cached session is a stale
 * session, and the two places that need to know who is signed in (`GuestOnly` on the way
 * in, `RequireAuth` on the way in here) both want a fresh answer per navigation. This
 * holds the result of that answer for as long as the area is mounted.
 */
export const SessionContext = createContext(null)

export const useSession = () => useContext(SessionContext)
