import { useNavigate } from 'react-router-dom'
import { Button } from 'antd'
import { useTranslation } from 'react-i18next'
import AuthForm from '../components/AuthForm'
import config, { routePath } from '../config'
import { ClientLoginCaptchaAction, submitAuth, type SignInValues } from '../lib/api'
import { internalPath } from '../lib/url'

export default function ClientLoginPage() {
  const navigate = useNavigate()
  const { t } = useTranslation()

  return (
    <AuthForm<SignInValues>
      title={t('titles.clientLogin')}
      action={ClientLoginCaptchaAction}
      submitText={t('actions.login')}
      onSubmit={({ values, csrfToken, captchaToken }) =>
        submitAuth({ action: 'client-login', values, csrfToken, captchaToken })
      }
      // The server names the destination, in `redirect`, because only it knows what the
      // account's role is and the three areas do not share a layout. Following it rather
      // than hardcoding the path here is what keeps one rule - which page a role belongs on -
      // instead of three copies of it.
      onSuccess={(result) =>
        // `internalPath` first: the server's word is a candidate path, not an instruction.
        // Anything that is not a path this router could have - a scheme, a host,
        // `//elsewhere` - is dropped here and the role's own landing page takes over, so a
        // disagreed-about string can never send a visitor outside the application.
        navigate(internalPath(result?.redirect) ?? routePath(config.CLIENT_HOME_ROUTE), {
          replace: true,
        })
      }
      // Two cross-links, and they belong to different flows: this one is for somebody who
      // has an account and cannot get into it, the other for somebody who has none yet.
      // Both live in the footer because that is where the one between sign-in and
      // registration has always sat - the reset is a third door out of the same form, so it
      // goes where the visitor already looks for a way out.
      footer={
        <>
          <Button
            type="link"
            onClick={() => navigate(routePath(config.FORGOT_PASSWORD_ROUTE))}
          >
            {t('actions.forgotPassword')}
          </Button>
          <Button
            type="link"
            onClick={() => navigate(routePath(config.REGISTER_ROUTE))}
          >
            {t('actions.noAccount')}
          </Button>
        </>
      }
    />
  )
}