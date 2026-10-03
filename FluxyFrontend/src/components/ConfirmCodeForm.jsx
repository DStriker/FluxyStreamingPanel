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
 *
 * The same form carries the second step of a password reset, which is why the identifier
 * is a prop rather than a field written into the markup: the two steps ask for different
 * things (an address the visitor just typed, a login name carried over from step one) and
 * only the code they both confirm is the same. Registration passes `email` and the default
 * applies, so nothing about it changed; the reset passes `identifier` and gets its own field
 * under the same rules of shape.
 */
export default function ConfirmCodeForm({
  email: initialEmail,
  identifier,
  title,
  captchaAction = ConfirmCaptchaAction,
  onSubmit,
  onBack,
}) {
  const { t } = useTranslation()
  const [form] = Form.useForm()
  const { message } = App.useApp()
  const [submitting, setSubmitting] = useState(false)

  // What the code is being confirmed for. Everything the form knows about it is read from
  // here, so the two shapes below differ in one place instead of in five.
  const field = identifier ?? {
    name: 'email',
    label: t('fields.email'),
    initialValue: initialEmail,
    autoComplete: 'email',
    placeholder: t('fields.emailPlaceholder'),
    rules: [
      { required: true, message: t('validation.emailRequired') },
      { type: 'email', message: t('validation.emailInvalid') },
    ],
  }

  useEffect(() => {
    getCsrfToken()
  }, [])

  const handleFinish = async (values) => {
    setSubmitting(true)
    form.setFields([
      { name: field.name, errors: [] },
      { name: 'code', errors: [] },
    ])

    try {
      const [csrfToken, captchaToken] = await Promise.all([
        getCsrfToken(),
        // A fixed action per endpoint, not a free choice: the token the browser asks for
        // has to be the one the server expects for this route. The two steps of a flow
        // that ask for a code under one session do not share theirs either.
        getCaptchaToken(captchaAction),
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
    <AuthCard title={title ?? t('titles.confirmRegistration')} form={form} onFinish={handleFinish}>
      <Form.Item name={field.name} label={field.label} initialValue={field.initialValue}
        rules={field.rules}
      >
        <Input
          autoComplete={field.autoComplete}
          placeholder={field.placeholder}
          type={field.type}
        />
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