import { useTranslation } from 'react-i18next'
import AuthForm from '../components/AuthForm'
import { submitAuth } from '../lib/api'

export default function AdminLoginPage() {
  const { t } = useTranslation()

  return (
    <AuthForm
      title={t('titles.adminLogin')}
      action="admin_login"
      submitText={t('actions.login')}
      onSubmit={({ values, csrfToken, captchaToken }) =>
        submitAuth({ action: 'admin-login', values, csrfToken, captchaToken })
      }
    />
  )
}
