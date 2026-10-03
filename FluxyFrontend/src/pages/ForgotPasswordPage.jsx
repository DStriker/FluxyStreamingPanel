import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { App, Button, Spin, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import AuthCard from '../components/AuthCard'
import AuthForm from '../components/AuthForm'
import ConfirmCodeForm from '../components/ConfirmCodeForm'
import config, { routePath } from '../config'
import {
  PasswordResetConfirmCaptchaAction,
  confirmPasswordReset,
  passwordResetStatus,
  requestPasswordReset,
} from '../lib/api'
import { ApiError } from '../lib/http'
import {
  forgetPendingReset,
  readPendingReset,
  rememberPendingReset,
} from '../lib/pendingReset'

/** The codes that decide what this page is showing. */
const Submitted = 'password_reset_submitted'
const Confirmed = 'password_reset_confirmed'
const NotConfigured = 'password_reset_not_configured'
const CodeExpired = 'code_expired'

/**
 * Password reset for somebody who cannot sign in, in two steps on one page: the account and
 * its new password, then the code that was mailed out.
 *
 * It is a guest route for the same reason registration is - a visitor who already holds a
 * session is sent to their own area rather than being offered a form that would stack a
 * second session over a first that nothing can end anymore.
 *
 * The page asks the server whether this installation can send a code at all *before*
 * rendering anything to type into. Without a mail server there is no second step, and a
 * form whose confirmation could never arrive is worse than no form: the visitor would fill
 * it in, be told to check their inbox, and wait for a message the server has no way to
 * send. The answer is also why the form is not simply shown and left to fail with a 503.
 *
 * The reset does not ask who the visitor is beyond a username and an address that belong
 * together, and it does not need to: the code that comes back proves control of the mailbox,
 * which is the only proof available to somebody with no session.
 */
export default function ForgotPasswordPage() {
  const navigate = useNavigate()
  const { t } = useTranslation()
  const { message } = App.useApp()

  // `null` is "still asking", which is also the state a plain render produces - see the
  // render probe, which pins the spinner rather than a form appearing before the answer.
  const [configured, setConfigured] = useState(null)
  const [username, setUsername] = useState(readPendingReset)
  const [step, setStep] = useState(() => (readPendingReset() ? 'code' : 'details'))

  useEffect(() => {
    let alive = true
    passwordResetStatus().then((available) => {
      if (alive) setConfigured(available)
    })
    return () => {
      alive = false
    }
  }, [])

  const backToLogin = (
    <Button type="link" onClick={() => navigate(routePath(config.CLIENT_LOGIN_ROUTE))}>
      {t('actions.haveAccount')}
    </Button>
  )

  const backToDetails = () => {
    forgetPendingReset()
    setUsername('')
    setStep('details')
  }

  const handleRequested = (result, values) => {
    if (result?.code !== Submitted) return
    // Mirrored into `sessionStorage` for the reason registration mirrors its address: a
    // reload between the two steps would otherwise send the visitor back to the first form,
    // and asking again here replaces the code already sitting in their inbox.
    rememberPendingReset(values.username)
    setUsername(values.username)
    setStep('code')
  }

  if (configured === null) {
    return (
      <AuthCard title={t('titles.forgotPassword')} footer={backToLogin}>
        <div className="auth-form__submit" style={{ textAlign: 'center' }}>
          <Spin />
        </div>
      </AuthCard>
    )
  }

  if (configured === false) {
    return (
      <AuthCard title={t('titles.forgotPassword')} footer={backToLogin}>
        <Typography.Paragraph>{t('messages.passwordResetUnavailable')}</Typography.Paragraph>
      </AuthCard>
    )
  }

  if (step === 'code') {
    return (
      <ConfirmCodeForm
        title={t('titles.confirmPasswordReset')}
        identifier={{
          name: 'username',
          label: t('fields.username'),
          initialValue: username,
          autoComplete: 'username',
          placeholder: t('fields.usernamePlaceholder'),
          rules: [{ required: true, message: t('validation.usernameRequired') }],
        }}
        captchaAction={PasswordResetConfirmCaptchaAction}
        onBack={backToDetails}
        onSubmit={async ({ username: name, code, csrfToken, captchaToken }) => {
          try {
            const result = await confirmPasswordReset({
              username: name,
              code,
              csrfToken,
              captchaToken,
            })
            if (result?.code === Confirmed) {
              forgetPendingReset()
              // The toast is rendered by `AntApp`, which sits above the router, so it
              // survives the navigation instead of vanishing with the page that raised it.
              message.success(t('messages.api.password_reset_confirmed'))
              navigate(routePath(config.CLIENT_LOGIN_ROUTE), { replace: true })
            }
          } catch (err) {
            // An expired code cannot be revived by typing a better one, so the step is
            // dropped back to the first form - which is also what asks for a fresh code.
            if (err instanceof ApiError && err.code === CodeExpired) {
              backToDetails()
            }
            throw err
          }
        }}
      />
    )
  }

  return (
    <AuthForm
      title={t('titles.forgotPassword')}
      action="password_reset"
      register
      submitText={t('actions.resetPassword')}
      onSubmit={async (args) => {
        try {
          return await requestPasswordReset(args)
        } catch (err) {
          // The server refuses up front when there is no mail server to finish with - the
          // form stops being offered rather than being left to fail the same way twice.
          if (err instanceof ApiError && err.code === NotConfigured) {
            setConfigured(false)
          }
          throw err
        }
      }}
      onSuccess={handleRequested}
      footer={backToLogin}
    />
  )
}
