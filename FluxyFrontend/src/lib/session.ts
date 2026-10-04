import config, { areaOf, routePath } from '../config'
import type { Role, Session } from '../types'

/**
 * Landing paths per role, keyed by the role name as the backend reports it in `me`
 * (`UserRole` serialized by name: `Client`, `Reseller`, `Admin`).
 *
 * Typed `Record<Role, string>` so that all three are present or the file does not compile.
 * The input to the lookup is whatever the server called a role, and a role this build does
 * not know falls through to the client area - which is what the runtime `??` below is for;
 * the type states the protocol, not a guarantee about the next deployment.
 */
const HOME_BY_ROLE: Record<Role, string> = {
  Admin: config.ADMIN_HOME_ROUTE,
  Reseller: config.RESELLER_HOME_ROUTE,
  Client: config.CLIENT_HOME_ROUTE,
}

/** The configured landing path for a role - `admin/dashboard` - with the client's as the fallback. */
const landingRouteFor = (role: Role): string => HOME_BY_ROLE[role] ?? config.CLIENT_HOME_ROUTE

/**
 * Where a session of this role belongs - the destination a successful sign-in names.
 *
 * The server owns this rule for its own responses: `AuthenticationOptions.LandingPathOf`
 * answers every sign-in, refresh and confirmation with `redirect`. `/auth/me` reports the
 * role and nothing else, so a client that has to act on an *existing* session - the guest
 * pages, before showing a form to somebody who already has one - applies the same rule
 * here. By role and never by entrance: an admin at `/login` belongs on the admin area and a
 * client at `/admin/login` on the client one, because that is where signing that account in
 * would land it.
 *
 * The path comes from this application's own route table rather than from a string the
 * server sent. That is not distrust, it is a safety property: the destination is then
 * guaranteed to be a route this router has, and a path the two sides disagreed about could
 * otherwise bounce a signed-in visitor between the catch-all route and the guard forever.
 *
 * An unknown or missing role falls back to the client area - a non-guest route, for the
 * same reason. Sending a session that demonstrably exists back to a guest page would put it
 * straight through the guard that just asked, which is a loop and not a fallback.
 */
export const homeForRole = (role: Role): string => routePath(landingRouteFor(role))

/**
 * The address of the role's **area** - `/admin`, `/reseller`, `/client` - where
 * `homeForRole` is the page a sign-in opens inside it.
 *
 * Two answers cut from the same configured string, and the split is the whole point of
 * this function existing. Sections hang off the area, so `profile` is `/admin/profile`;
 * the landing path hangs off the area too but ends in a page, and it is the server that
 * names it. Using `homeForRole` as the base for both is exactly how the section links came
 * out as `/admin/dashboard/profile`. Everything that builds an address inside an area -
 * the router's children, the sidebar's keys, the header's shortcut - takes this one.
 */
export const areaForRole = (role: Role): string => routePath(areaOf(landingRouteFor(role)))

/**
 * Whether a session may open the area guarded for `role`.
 *
 * `role` absent means the guard asks only for *some* session, which is what the catch-all
 * route needs; present, it is an exact comparison rather than an ordered one. `UserRole`
 * is ordered on the backend (`Client < Reseller < Admin`) but the *areas* are not levels
 * of the same thing - each sign-in is a door to one room, and the server enforces exactly
 * this rule: `RoleRequirement` demands a single role rather than `AtLeast`. Reading this
 * predicate as "at least" would let a client into the admin panel on the strength of a
 * ranking the authorization handlers never applied.
 *
 * It is a named export rather than an inline `session.role === role` for one reason: it is
 * the whole of the refusal, and a predicate can be asserted on. A comparison written
 * inside a component cannot be - `renderToStaticMarkup` never runs an effect, so a guard
 * never reaches its decision while being rendered, and a check that cannot execute proves
 * nothing.
 *
 * Written as an early return rather than the original `Boolean(session) && ...`, because
 * `Boolean()` narrows nothing: TypeScript would still call `session.role` a read of a
 * possibly-null value on the second half. `!session` narrows the rest of the function.
 */
export const sessionOpensArea = (
  session: Session | null | undefined,
  role?: Role | null,
): boolean => {
  if (!session) return false
  return role === undefined || role === null || session.role === role
}
