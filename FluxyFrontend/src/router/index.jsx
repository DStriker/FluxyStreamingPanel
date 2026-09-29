import { createBrowserRouter, Navigate } from 'react-router-dom'
import config, { routePath } from '../config'
import RegisterPage from '../pages/RegisterPage'
import ClientLoginPage from '../pages/ClientLoginPage'
import ResellerLoginPage from '../pages/ResellerLoginPage'
import AdminLoginPage from '../pages/AdminLoginPage'

export const router = createBrowserRouter([
  {
    path: '/',
    element: <Navigate to={routePath(config.CLIENT_LOGIN_ROUTE)} replace />,
  },
  {
    path: routePath(config.REGISTER_ROUTE),
    element: <RegisterPage />,
  },
  {
    path: routePath(config.CLIENT_LOGIN_ROUTE),
    element: <ClientLoginPage />,
  },
  {
    path: routePath(config.RESELLER_LOGIN_ROUTE),
    element: <ResellerLoginPage />,
  },
  {
    path: routePath(config.ADMIN_LOGIN_ROUTE),
    element: <AdminLoginPage />,
  },
])
