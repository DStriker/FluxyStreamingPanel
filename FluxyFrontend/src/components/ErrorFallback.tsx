import { Button, Result } from 'antd'
import { useTranslation } from 'react-i18next'
import config, { routePath } from '../config'

/**
 * The screen a visitor is left with when a page throws instead of rendering.
 *
 * Two boundaries draw it, and that it can serve both is the design rather than a reuse
 * that happened: the `errorElement` every route in `src/router/routes.tsx` carries (so a
 * crash opens here, inside the theme and the language the visitor picked) and the class
 * boundary in `main.tsx` (the last resort for a failure above the router, where the
 * providers themselves are gone).
 *
 * Deliberately free of router hooks. The second of those two positions is *above*
 * `RouterProvider`, so a `useNavigate` here would throw inside the one component whose
 * whole job is to be renderable when something already has. The way back to the sign-in
 * form is therefore a plain anchor and the reset is a real reload - which for a route
 * error is also the honest repair: whatever state the failed tree left behind is gone
 * with it.
 */
export default function ErrorFallback() {
  const { t } = useTranslation()

  return (
    <div className="error-fallback">
      <Result
        status="error"
        title={t('errors.title')}
        subTitle={t('errors.description')}
        extra={[
          <Button key="reload" type="primary" onClick={() => window.location.reload()}>
            {t('errors.reload')}
          </Button>,
          // `href` rather than an onClick: a full document load is what puts the sign-in
          // form on screen, and it is also what a visitor with a session needs - the guest
          // guard there will send them on to their own area, which is exactly the fresh
          // start the button promises. `actions.login` is reused rather than a new key:
          // the button says what every other door to that page says.
          <Button key="signIn" href={routePath(config.CLIENT_LOGIN_ROUTE)}>
            {t('actions.login')}
          </Button>,
        ]}
      />
    </div>
  )
}
