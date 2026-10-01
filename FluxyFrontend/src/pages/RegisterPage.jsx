import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { App, Button } from 'antd'
import { useTranslation } from 'react-i18next'
import AuthForm from '../components/AuthForm'
import ConfirmCodeForm from '../components/ConfirmCodeForm'
import config, { routePath } from '../config'
import { confirmRegistration, submitRegistration } from '../lib/api'
import { ApiError } from '../lib/http'
import {
  forgetPendingEmail,
  readPendingEmail,
  rememberPendingEmail,
} from '../lib/pendingRegistration'

/** The codes that mean the confirmation step has nothing left to confirm. */
const CodeExpired = 'code_expired'

/**
 * Registration in two steps on one page: the account details, then the code that was
 * mailed out.
 *
 * The page owns which step is showing and the address carried between them, and stays
 * otherwise thin - each step is a component with its own form, endpoint and rules. The
 * address is remembered in `sessionStorage` so a reload between the steps resumes rather
 * than throwing the visitor back to the beginning.
 */
export default function RegisterPage() {
  const navigate = useNavigate()
  const { t } = useTranslation()
  const { message } = App.useApp()

  const [email, setEmail] = useState(readPendingEmail)
  const [step, setStep] = useState(() => (readPendingEmail() ? 'code' : 'details'))

  const backToDetails = () => {
    forgetPendingEmail()
    setEmail('')
    setStep('details')
  }

  const handleRegistered = (_result, values) => {
    rememberPendingEmail(values.email)
    setEmail(values.email)
    setStep('code')
  }

  const handleConfirmed = () => {
    forgetPendingEmail()
    message.success(t('messages.api.registration_confirmed'))
    navigate(routePath(config.CLIENT_LOGIN_ROUTE), { replace: true })
  }

  if (step === 'code') {
    return (
      <ConfirmCodeForm
        email={email}
        onBack={backToDetails}
        onSubmit={async ({ email: address, code, csrfToken, captchaToken }) => {
          try {
            await confirmRegistration({ email: address, code, csrfToken, captchaToken })
            handleConfirmed()
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
      title={t('titles.register')}
      action="register"
      register
      submitText={t('actions.register')}
      onSubmit={({ values, csrfToken, captchaToken }) =>
        submitRegistration({ values, csrfToken, captchaToken })
      }
      onSuccess={handleRegistered}
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