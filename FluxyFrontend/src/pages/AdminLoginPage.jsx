import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import AuthForm from '../components/AuthForm'
import config, { routePath } from '../config'
import { submitAuth } from '../lib/api'

export default function AdminLoginPage() {
  const navigate = useNavigate()
  const { t } = useTranslation()

  return (
    <AuthForm
      title={t('titles.adminLogin')}
      action="admin_login"
      submitText={t('actions.login')}
      onSubmit={({ values, csrfToken, captchaToken }) =>
        submitAuth({ action: 'admin-login', values, csrfToken, captchaToken })
      }
      // The server names the destination, in `redirect` - see ClientLoginPage.
      onSuccess={(result) =>
        navigate(result?.redirect ?? routePath(config.ADMIN_HOME_ROUTE), { replace: true })
      }
    />
  )
}