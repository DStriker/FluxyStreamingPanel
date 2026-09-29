import { useEffect, useState } from 'react'
import { App, Button, Card, Form, Input, Image } from 'antd'
import { useTranslation } from 'react-i18next'
import LanguageSwitcher from './LanguageSwitcher'
import { getCsrfToken } from '../lib/csrf'
import { getCaptchaToken } from '../lib/recaptcha'

export default function AuthForm({
  title,
  action,
  register = false,
  submitText,
  onSubmit,
  footer,
}) {
  const { t } = useTranslation()
  const [form] = Form.useForm()
  const { message } = App.useApp()
  const [submitting, setSubmitting] = useState(false)

  useEffect(() => {
    getCsrfToken()
  }, [])

  const handleFinish = async (values) => {
    setSubmitting(true)
    try {
      const [csrfToken, captchaToken] = await Promise.all([
        getCsrfToken({ refresh: true }),
        getCaptchaToken(action),
      ])
      await onSubmit({ values, csrfToken, captchaToken })
      message.success(t('messages.sent'))
    } catch (err) {
      message.error(err?.message || t('messages.sendFailed'))
    } finally {
      setSubmitting(false)
    }
  }

  const passwordRules = [
    { required: true, message: t('validation.passwordRequired') },
    { min: 6, message: t('validation.passwordMin') },
  ]

  return (
    <div className="auth-page">
      <LanguageSwitcher />
      <Card className="auth-logo" cover={
        <Image src="/logo.jpg" alt="logo" width={200} height={60}/>
      }/>
      <br/>
      <Card className="auth-card" title={title} variant="outlined">
        <Form
          form={form}
          className="auth-form"
          layout="vertical"
          onFinish={handleFinish}
          requiredMark
          autoComplete="on"
        >
          <Form.Item
            name="username"
            label={t('fields.username')}
            rules={[
              { required: true, message: t('validation.usernameRequired') },
              { min: 3, message: t('validation.usernameMin') },
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

          {footer && <div className="auth-form__footer">{footer}</div>}
        </Form>
      </Card>
    </div>
  )
}
