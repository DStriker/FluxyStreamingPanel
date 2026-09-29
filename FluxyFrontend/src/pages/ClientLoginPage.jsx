import { useNavigate } from 'react-router-dom'
import { Button } from 'antd'
import { useTranslation } from 'react-i18next'
import AuthForm from '../components/AuthForm'
import config, { routePath } from '../config'
import { submitAuth } from '../lib/api'

export default function ClientLoginPage() {
  const navigate = useNavigate()
  const { t } = useTranslation()

  return (
    <AuthForm
      title={t('titles.clientLogin')}
      action="client_login"
      submitText={t('actions.login')}
      onSubmit={({ values, csrfToken, captchaToken }) =>
        submitAuth({ action: 'client-login', values, csrfToken, captchaToken })
      }
      footer={
        <Button
          type="link"
          onClick={() => navigate(routePath(config.REGISTER_ROUTE))}
        >
          {t('actions.noAccount')}
        </Button>
      }
    />
  )
}
