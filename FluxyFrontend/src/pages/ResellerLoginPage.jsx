import { useTranslation } from 'react-i18next'
import AuthForm from '../components/AuthForm'
import { submitAuth } from '../lib/api'

export default function ResellerLoginPage() {
  const { t } = useTranslation()

  return (
    <AuthForm
      title={t('titles.resellerLogin')}
      action="reseller_login"
      submitText={t('actions.login')}
      onSubmit={({ values, csrfToken, captchaToken }) =>
        submitAuth({ action: 'reseller-login', values, csrfToken, captchaToken })
      }
    />
  )
}
