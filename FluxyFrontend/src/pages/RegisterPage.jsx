import { useNavigate } from 'react-router-dom'
import { Button } from 'antd'
import { useTranslation } from 'react-i18next'
import AuthForm from '../components/AuthForm'
import config, { routePath } from '../config'
import { submitAuth } from '../lib/api'

export default function RegisterPage() {
  const navigate = useNavigate()
  const { t } = useTranslation()

  return (
    <AuthForm
      title={t('titles.register')}
      action="register"
      register
      submitText={t('actions.register')}
      onSubmit={({ values, csrfToken, captchaToken }) =>
        submitAuth({ action: 'register', values, csrfToken, captchaToken })
      }
      footer={
        <Button
          type="link"
          onClick={() => navigate(routePath(config.CLIENT_LOGIN_ROUTE))}
        >
          {t('actions.haveAccount')}
        </Button>
      }
    />
  )
}
