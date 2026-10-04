import { useNavigate } from 'react-router-dom'
import { Button } from 'antd'
import { useTranslation } from 'react-i18next'
import AuthForm from '../components/AuthForm'
import config, { routePath } from '../config'
import { AdminLoginCaptchaAction, submitAuth, type SignInValues } from '../lib/api'
import { internalPath } from '../lib/url'

export default function AdminLoginPage() {
  const navigate = useNavigate()
  const { t } = useTranslation()

  return (
    <AuthForm<SignInValues>
      title={t('titles.adminLogin')}
      action={AdminLoginCaptchaAction}
      submitText={t('actions.login')}
      onSubmit={({ values, csrfToken, captchaToken }) =>
        submitAuth({ action: 'admin-login', values, csrfToken, captchaToken })
      }
      // The server names the destination, in `redirect` - see ClientLoginPage. The path
      // is still only a candidate: `internalPath` decides it is one this router has.
      onSuccess={(result) =>
        navigate(internalPath(result?.redirect) ?? routePath(config.ADMIN_HOME_ROUTE), {
          replace: true,
        })
      }
      // See ResellerLoginPage: the first cross-link this footer has ever carried, and the
      // reason it is the reset rather than registration is that an administrator who cannot
      // sign in has an account, not none.
      footer={
        <Button
          type="link"
          onClick={() => navigate(routePath(config.FORGOT_PASSWORD_ROUTE))}
        >
          {t('actions.forgotPassword')}
        </Button>
      }
    />
  )
}