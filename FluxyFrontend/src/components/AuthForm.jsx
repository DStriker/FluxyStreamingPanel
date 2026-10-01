import { useEffect, useState } from 'react'
import { App, Button, Form, Input } from 'antd'
import { useTranslation } from 'react-i18next'
import AuthCard from './AuthCard'
import { getCsrfToken } from '../lib/csrf'
import { getCaptchaToken } from '../lib/recaptcha'
import { ApiError, fieldErrors, messageForError, textForCode } from '../lib/http'
import {
  PASSWORD_MAX,
  PASSWORD_MIN,
  USERNAME_MAX,
  USERNAME_MIN,
  meetsPasswordComplexity,
} from '../lib/policy'

/**
 * Field names the API may answer with, matching the camelCase JSON property names it uses
 * for them. Kept in step with `formItems` below so a field the server can reject always has
 * a rendered input to carry the reason.
 */
const SERVER_FIELDS = ['username', 'email', 'password']

export default function AuthForm({
  title,
  action,
  captchaAction = action,
  register = false,
  submitText,
  successText,
  onSubmit,
  onSuccess,
  footer,
}) {
  const { t } = useTranslation()
  const [form] = Form.useForm()
  const { message } = App.useApp()
  const [submitting, setSubmitting] = useState(false)

  useEffect(() => {
    getCsrfToken()
  }, [])

  // The fields the server can name in `errors`. Clearing anything more would wipe a rule
  // the visitor has not satisfied yet, which is a different complaint than a stale one.
  const clearServerErrors = () => {
    form.setFields(SERVER_FIELDS.map((name) => ({ name, errors: [] })))
  }

  const handleFinish = async (values) => {
    setSubmitting(true)
    // A rejection from the previous attempt has to go before the next submit, or the
    // fields stay red even after the visitor corrected them. Only the errors are cleared -
    // what the visitor typed is still what they want.
    clearServerErrors()
    try {
      const [csrfToken, captchaToken] = await Promise.all([
        getCsrfToken({ refresh: true }),
        getCaptchaToken(captchaAction),
      ])
      const result = await onSubmit({ values, csrfToken, captchaToken })
      // The server names its own outcome, so the wording follows the response rather than
      // asserting a generic success for any 2xx. `successText` covers an endpoint that
      // answers without a code.
      message.success(textForCode(result?.code) ?? successText ?? t('messages.sent'))
      onSuccess?.(result, values)
    } catch (err) {
      // The server rejected named fields. Putting the reasons under the inputs that caused
      // them is the whole difference between "which field?" and a single toast.
      const fields = fieldErrors(err instanceof ApiError ? err.errors : null)
      if (fields.length > 0) {
        form.setFields(fields)
      }
      message.error(messageForError(err))
    } finally {
      setSubmitting(false)
    }
  }

  const passwordRules = [
    { required: true, message: t('validation.passwordRequired') },
    { min: PASSWORD_MIN, max: PASSWORD_MAX, message: t('validation.passwordLength') },
    {
      validator: (_, value) =>
        !value || meetsPasswordComplexity(value)
          ? Promise.resolve()
          : Promise.reject(new Error(t('validation.passwordComplexity'))),
    },
  ]

  return (
    <AuthCard
      title={title}
      form={form}
      onFinish={handleFinish}
      footer={footer}
    >
      <Form.Item
        name="username"
        label={t('fields.username')}
        rules={[
          { required: true, message: t('validation.usernameRequired') },
          { min: USERNAME_MIN, max: USERNAME_MAX, message: t('validation.usernameLength') },
        ]}
      >
        <Input autoComplete="username" placeholder={t('fields.usernamePlaceholder')} />
      </Form.Item>

      {register && (
        <Form.Item
          name="email"
          label={t('fields.email')}
          rules={[
            { required: true, message: t('validation.emailRequired') },
            { type: 'email', message: t('validation.emailInvalid') },
          ]}
        >
          <Input autoComplete="email" placeholder={t('fields.emailPlaceholder')} />
        </Form.Item>
      )}

      <Form.Item name="password" label={t('fields.password')} rules={passwordRules}>
        <Input.Password
          autoComplete={register ? 'new-password' : 'current-password'}
        />
      </Form.Item>

      {register && (
        <Form.Item
          name="confirmPassword"
          label={t('fields.confirmPassword')}
          dependencies={['password']}
          rules={[
            { required: true, message: t('validation.confirmRequired') },
            ({ getFieldValue }) => ({
              validator(_, value) {
                if (!value || getFieldValue('password') === value) {
                  return Promise.resolve()
                }
                return Promise.reject(new Error(t('validation.passwordMismatch')))
              },
            }),
          ]}
        >
          <Input.Password autoComplete="new-password" />
        </Form.Item>
      )}

      <Form.Item className="auth-form__submit">
        <Button type="primary" htmlType="submit" block loading={submitting}>
          {submitText}
        </Button>
      </Form.Item>
    </AuthCard>
  )
}