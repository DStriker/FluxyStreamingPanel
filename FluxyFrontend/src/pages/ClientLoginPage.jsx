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
      // The server names the destination, in `redirect`, because only it knows what the
      // account's role is and the three areas do not share a layout. Following it rather
      // than hardcoding the path here is what keeps one rule - which page a role belongs on -
      // instead of three copies of it.
      onSuccess={(result) =>
        navigate(result?.redirect ?? routePath(config.CLIENT_HOME_ROUTE), { replace: true })
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