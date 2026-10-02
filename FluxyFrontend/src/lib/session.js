import config, { routePath } from '../config'

/**
 * Landing paths per role, keyed by the role name as the backend reports it in `me`
 * (`UserRole` serialized by name: `Client`, `Reseller`, `Admin`).
 */
const HOME_BY_ROLE = {
  Admin: config.ADMIN_HOME_ROUTE,
  Reseller: config.RESELLER_HOME_ROUTE,
  Client: config.CLIENT_HOME_ROUTE,
}

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
export const homeForRole = (role) =>
  routePath(HOME_BY_ROLE[role] ?? config.CLIENT_HOME_ROUTE)
