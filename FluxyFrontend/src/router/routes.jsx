import { Navigate } from 'react-router-dom'
import config, { areaOf, insideArea, routePath } from '../config'
import GuestOnly from '../components/GuestOnly'
import RequireAuth from '../components/RequireAuth'
import AccountLayout from '../components/AccountLayout'
import RegisterPage from '../pages/RegisterPage'
import ClientLoginPage from '../pages/ClientLoginPage'
import ResellerLoginPage from '../pages/ResellerLoginPage'
import AdminLoginPage from '../pages/AdminLoginPage'
import ForgotPasswordPage from '../pages/ForgotPasswordPage'
import NotFoundPage from '../pages/NotFoundPage'
import SectionPage from '../pages/SectionPage'
import { flatItems } from '../lib/navigation'

/**
 * The three sign-in areas, each of which has to answer the path the backend names in its
 * `redirect` field - a page inside the area, and therefore a child of it.
 *
 * The paths are configured rather than written here because they have to agree with the
 * `Auth` section of the backend's appsettings.json in two directions: the server sends the
 * path back on a successful sign-in, and this router has to have a matching route or the
 * visitor lands on a 404 after a successful login. One source for the strings, on each
 * side, is what keeps that from drifting.
 *
 * The five guest routes - the three entrances, registration and the password reset - are
 * wrapped in
 * `GuestOnly`, because a visitor who already holds a session has no business being offered
 * a form for creating or re-entering one: the pages would have them stack a second session
 * over a first that nothing can end anymore. The guard sends such a visitor to their own
 * area, which is where a successful sign-in would have taken them; see the component for
 * why the check sits here rather than inside the page.
 *
 * Each area is a *parent* route whose element is `RequireAuth` wrapping `AccountLayout`,
 * with the section pages as its children. Two things follow from that shape and neither is
 * incidental:
 *
 * - The role is checked before the layout renders, not inside a page. `/admin/profile`
 *   matches the admin route whatever the role of the visitor is, so a check on the page
 *   would have already shown them the admin shell. `RequireAuth` refuses with a 404
 *   instead, which is what "this address is not yours" means.
 * - The layout is mounted once per area, so moving between two sections replaces only the
 *   outlet. The sidebar's scroll position, its open categories and its collapsed state all
 *   survive a click; a layout wrapped around each page individually would reset them.
 *
 * The area's own path is the role's **prefix and nothing more** - `/client`, `/admin` -
 * while the landing path the server names in `redirect` is a page *inside* that prefix:
 * `/client/index`, `/admin/dashboard`. So `profile` hangs off the prefix and comes out as
 * `/admin/profile`, never `/admin/dashboard/profile`; the landing path is one child among
 * the rest, at whatever `insideArea` says. `sectionRoutes` reads the same table
 * `AccountSidebar` renders, which is why no menu item can point at a route that does not
 * exist.
 *
 * The catch-all is the only route here that does not know which role it belongs to, so it
 * uses `RequireAuth` without one: a signed-in visitor who asked for a page this
 * application does not have gets a 404, and a visitor with no session still gets the
 * sign-in form, which is where a typo'd URL always landed before.
 *
 * ## Why this is a data module and not the router
 *
 * `createBrowserRouter` builds a browser history on the spot, and building one reads
 * `document`. `npm run probe:render` runs in Node, so importing a file that constructs the
 * router would fail before a single check executed - which is exactly what happened when
 * the table and the construction were the same file. Splitting them is what lets the probe
 * assert what cannot be seen by rendering: that `homeForRole(role)` lands inside the route
 * guarded for that role (else a sign-in bounces between two guards), that every menu item
 * has a route while every route has a menu item, and that react-router itself resolves each
 * of those addresses to that area rather than to the catch-all.
 */
const sectionRoutes = (role, homeRoute) =>
  flatItems(role).map((item) => {
    const Page = item.page ?? SectionPage
    const element = <Page sectionKey={item.labelKey} />
    // The item with no path of its own is this role's home - the page a sign-in opens,
    // whose address the server names in `redirect`. It is a child of the area like every
    // other section, at whatever the landing path says sits inside that area (`dashboard`
    // for `admin/dashboard`, `index` for `client/index`), and when the landing path *is*
    // the area there is nothing left to give it and the home becomes the index route.
    const path = item.path || insideArea(homeRoute)
    return path === '' ? { index: true, element } : { path, element }
  })

const area = (role, homeRoute, titleKey) => ({
  path: routePath(areaOf(homeRoute)),
  element: (
    <RequireAuth role={role}>
      <AccountLayout titleKey={titleKey} />
    </RequireAuth>
  ),
  children: sectionRoutes(role, homeRoute),
})

export const routes = [
  {
    path: '/',
    element: <Navigate to={routePath(config.CLIENT_LOGIN_ROUTE)} replace />,
  },
  {
    path: routePath(config.REGISTER_ROUTE),
    element: (
      <GuestOnly>
        <RegisterPage />
      </GuestOnly>
    ),
  },
  {
    path: routePath(config.FORGOT_PASSWORD_ROUTE),
    element: (
      <GuestOnly>
        <ForgotPasswordPage />
      </GuestOnly>
    ),
  },
  {
    path: routePath(config.CLIENT_LOGIN_ROUTE),
    element: (
      <GuestOnly>
        <ClientLoginPage />
      </GuestOnly>
    ),
  },
  {
    path: routePath(config.RESELLER_LOGIN_ROUTE),
    element: (
      <GuestOnly>
        <ResellerLoginPage />
      </GuestOnly>
    ),
  },
  {
    path: routePath(config.ADMIN_LOGIN_ROUTE),
    element: (
      <GuestOnly>
        <AdminLoginPage />
      </GuestOnly>
    ),
  },
  area('Client', config.CLIENT_HOME_ROUTE, 'titles.clientArea'),
  area('Reseller', config.RESELLER_HOME_ROUTE, 'titles.resellerArea'),
  area('Admin', config.ADMIN_HOME_ROUTE, 'titles.adminArea'),
  {
    path: '*',
    element: (
      <RequireAuth>
        <NotFoundPage />
      </RequireAuth>
    ),
  },
]
