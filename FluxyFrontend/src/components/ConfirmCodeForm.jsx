import { useEffect, useState } from 'react'
import { App, Button, Form, Input } from 'antd'
import { useTranslation } from 'react-i18next'
import AuthCard from './AuthCard'
import { getCsrfToken } from '../lib/csrf'
import { getCaptchaToken } from '../lib/recaptcha'
import { ApiError, fieldErrors, messageForError } from '../lib/http'
import { ConfirmCaptchaAction } from '../lib/api'
import { CODE_LENGTH } from '../lib/policy'

/**
 * Second step of registration: the code that was mailed out.
 *
 * It is a separate form rather than a hidden field on the first one because the two have
 * nothing in common but the page they sit on - the first asks for three values, this asks
 * for an address and six digits - and because the endpoint, the captcha action and the
 * rate limit are all different.
 */
export default function ConfirmCodeForm({ email: initialEmail, onSubmit, onBack }) {
  const { t } = useTranslation()
  const [form] = Form.useForm()
  const { message } = App.useApp()
  const [submitting, setSubmitting] = useState(false)

  useEffect(() => {
    getCsrfToken()
  }, [])

  const handleFinish = async (values) => {
    setSubmitting(true)
    form.setFields([
      { name: 'email', errors: [] },
      { name: 'code', errors: [] },
    ])

    try {
      const [csrfToken, captchaToken] = await Promise.all([
        getCsrfToken({ refresh: true }),
        // A fixed action per endpoint, not a free choice: the token the browser asks for
        // has to be the one the server expects for this route.
        getCaptchaToken(ConfirmCaptchaAction),
      ])
      await onSubmit({ ...values, csrfToken, captchaToken })
    } catch (err) {
      const fields = fieldErrors(err instanceof ApiError ? err.errors : null)
      if (fields.length > 0) {
        form.setFields(fields)
      }
      message.error(messageForError(err))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <AuthCard title={t('titles.confirmRegistration')} form={form} onFinish={handleFinish}>
      <Form.Item name="email" label={t('fields.email')} initialValue={initialEmail}
        rules={[
          { required: true, message: t('validation.emailRequired') },
          { type: 'email', message: t('validation.emailInvalid') },
        ]}
      >
        <Input autoComplete="email" placeholder={t('fields.emailPlaceholder')} />
      </Form.Item>

      <Form.Item
        name="code"
        label={t('fields.code')}
        className="auth-form__otp"
        rules={[
          { required: true, message: t('validation.codeRequired') },
          { len: CODE_LENGTH, message: t('validation.codeLength') },
          {
            validator: (_, value) =>
              !value || /^\d+$/.test(value)
                ? Promise.resolve()
                : Promise.reject(new Error(t('validation.codeLength'))),
          },
        ]}
      >
        <Input.OTP
          length={CODE_LENGTH}
          formatter={(value) => value.replace(/\D/g, '')}
          autoComplete="one-time-code"
          inputMode="numeric"
        />
      </Form.Item>

      <Form.Item className="auth-form__submit">
        <Button type="primary" htmlType="submit" block loading={submitting}>
          {t('actions.confirm')}
        </Button>
      </Form.Item>

      <div className="auth-form__footer">
        <Button type="link" onClick={onBack} disabled={submitting}>
          {t('actions.back')}
        </Button>
      </div>
    </AuthCard>
  )
}