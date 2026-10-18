import { describe, expect, it } from 'vitest'
import { areaForRole, homeForRole, loginRouteForPath, sessionOpensArea } from '../../src/lib/session'
import type { Role, Session } from '../../src/types'

/**
 * The landing rules, asserted as functions rather than through a render.
 *
 * `probe:session` already checks the same agreement against a live server - the path
 * `homeForRole` names must equal the `redirect` a sign-in is answered with - but that
 * needs a backend and credentials. These cases are the half that can run anywhere: the
 * unknown-role fallback, the area/page split, and the entrance a path belongs to.
 */

/** A session of some role; the guards read the role and nothing else. */
const session = (role: Role): Session => ({ role })

describe('homeForRole', () => {
  it('names the landing page of each role inside its own area', () => {
    expect(homeForRole('Admin')).toBe('/admin/dashboard')
    expect(homeForRole('Reseller')).toBe('/reseller/dashboard')
    expect(homeForRole('Client')).toBe('/client/index')
  })

  it('falls back to a non-guest route for a role this build does not know', () => {
    // A role the server adds later. The fallback must not be a sign-in page: a session
    // that demonstrably exists sent back to a guest route would pass straight through
    // `GuestOnly`, which is a loop rather than a fallback.
    const unknown = 'Wizard' as Role

    expect(homeForRole(unknown)).toBe('/client/index')
    expect(homeForRole(unknown)).not.toBe('/login')
  })
})

describe('areaForRole', () => {
  it('answers the area prefix, not the landing page inside it', () => {
    // The split that once put the profile at `/admin/dashboard/profile`: sections hang
    // off the area, and the landing path is a page within it.
    expect(areaForRole('Admin')).toBe('/admin')
    expect(areaForRole('Reseller')).toBe('/reseller')
    expect(areaForRole('Client')).toBe('/client')
  })
})

describe('loginRouteForPath', () => {
  it('names the entrance of the area a path sits in', () => {
    expect(loginRouteForPath('/admin/users')).toBe('/admin/login')
    expect(loginRouteForPath('/reseller/dashboard')).toBe('/reseller/login')
    // The client area's entrance is the bare `/login` - the public form, which is also
    // where a path naming no area falls back.
    expect(loginRouteForPath('/client/index')).toBe('/login')
  })

  it('falls back to the public client form for a path that names no area', () => {
    expect(loginRouteForPath('/')).toBe('/login')
    expect(loginRouteForPath('/unknown/deep')).toBe('/login')
  })
})

describe('sessionOpensArea', () => {
  it('refuses every area when there is no session', () => {
    expect(sessionOpensArea(null)).toBe(false)
    expect(sessionOpensArea(undefined, 'Admin')).toBe(false)
    expect(sessionOpensArea(null, undefined)).toBe(false)
  })

  it('without a role asks only for some session - the catch-all rule', () => {
    expect(sessionOpensArea(session('Client'))).toBe(true)
    expect(sessionOpensArea(session('Admin'), null)).toBe(true)
  })

  it('matches the role exactly rather than by rank', () => {
    expect(sessionOpensArea(session('Admin'), 'Admin')).toBe(true)
    expect(sessionOpensArea(session('Client'), 'Client')).toBe(true)
    // The roles are ordered on the backend (`Client < Reseller < Admin`), but the areas
    // are doors, not levels: an admin session does not open the client area and a
    // client session does not open the admin one.
    expect(sessionOpensArea(session('Client'), 'Admin')).toBe(false)
    expect(sessionOpensArea(session('Admin'), 'Client')).toBe(false)
    expect(sessionOpensArea(session('Reseller'), 'Admin')).toBe(false)
  })
})
