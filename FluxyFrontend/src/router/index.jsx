import { createBrowserRouter, Navigate } from 'react-router-dom'
import config, { routePath } from '../config'
import GuestOnly from '../components/GuestOnly'
import RegisterPage from '../pages/RegisterPage'
import ClientLoginPage from '../pages/ClientLoginPage'
import ResellerLoginPage from '../pages/ResellerLoginPage'
import AdminLoginPage from '../pages/AdminLoginPage'
import AreaPlaceholderPage from '../pages/AreaPlaceholderPage'

/**
 * The three sign-in areas, each answering to the path the backend names in its `redirect`
 * field.
 *
 * The paths are configured rather than written here because they have to agree with the
 * `Auth` section of the backend's appsettings.json in two directions: the server sends the
 * path back on a successful sign-in, and this router has to have a matching route or the
 * visitor lands on a 404 after a successful login. One source for the strings, on each
 * side, is what keeps that from drifting.
 *
 * The four guest routes - the three entrances and registration - are wrapped in
 * `GuestOnly`, because a visitor who already holds a session has no business being offered
 * a form for creating or re-entering one: the pages would have them stack a second session
 * over a first that nothing can end anymore. The guard sends such a visitor to their own
 * area, which is where a successful sign-in would have taken them; see the component for
 * why the check sits here rather than inside each page.
 */
export const router = createBrowserRouter([
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
  {
    path: routePath(config.CLIENT_HOME_ROUTE),
    element: <AreaPlaceholderPage titleKey="titles.clientLogin" />,
  },
  {
    path: routePath(config.RESELLER_HOME_ROUTE),
    element: <AreaPlaceholderPage titleKey="titles.resellerLogin" />,
  },
  {
    path: routePath(config.ADMIN_HOME_ROUTE),
    element: <AreaPlaceholderPage titleKey="titles.adminLogin" />,
  },
  {
    path: '*',
    element: <Navigate to={routePath(config.CLIENT_LOGIN_ROUTE)} replace />,
  },
])