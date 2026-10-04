import { useNavigate } from 'react-router-dom'
import { Button } from 'antd'
import { useTranslation } from 'react-i18next'
import AuthForm from '../components/AuthForm'
import config, { routePath } from '../config'
import { submitAuth, type SignInValues } from '../lib/api'

export default function ResellerLoginPage() {
  const navigate = useNavigate()
  const { t } = useTranslation()

  return (
    <AuthForm<SignInValues>
      title={t('titles.resellerLogin')}
      action="reseller_login"
      submitText={t('actions.login')}
      onSubmit={({ values, csrfToken, captchaToken }) =>
        submitAuth({ action: 'reseller-login', values, csrfToken, captchaToken })
      }
      // The server names the destination, in `redirect` - see ClientLoginPage.
      onSuccess={(result) =>
        navigate(result?.redirect ?? routePath(config.RESELLER_HOME_ROUTE), { replace: true })
      }
      // The reseller entrance has never offered a cross-link - `git log -S footer` over this
      // file returns nothing - so the reset link is the first thing its footer carries. It
      // is not an oversight being repaired: the reset is a door for somebody who has an
      // account and cannot open it, which applies to a reseller exactly as it does to a
      // client, and until now they had nowhere to go but to ask.
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