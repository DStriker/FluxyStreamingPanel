import { Button, Result } from 'antd'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import config, { routePath } from '../config'
import { homeForRole } from '../lib/session'
import { useSession } from '../lib/sessionContext'

/**
 * A path this application does not have, or one belonging to another role's area.
 *
 * Both visitors a session can have end up here, and both get the same page rather than a
 * redirect: the role mismatch is the answer to "may I see this?" (no) and the catch-all
 * is the answer to "does this exist?" (no). Neither is improved by showing a sign-in form
 * to somebody who already holds a session, nor by teleporting them to a page they did not
 * ask for.
 *
 * The way out goes to *this* visitor's own area - by role, through the same rule the
 * server applies - so an admin who reached a client URL gets back to the admin panel and
 * not to a form that would ask them to sign in twice.
 */
export default function NotFoundPage() {
  const navigate = useNavigate()
  const { t } = useTranslation()
  const session = useSession()

  const home = session
    ? homeForRole(session.role)
    : routePath(config.CLIENT_LOGIN_ROUTE)

  return (
    <Result
      status="404"
      title="404"
      subTitle={t('messages.notFound')}
      extra={
        <Button type="primary" onClick={() => navigate(home, { replace: true })}>
          {t('actions.backHome')}
        </Button>
      }
    />
  )
}
