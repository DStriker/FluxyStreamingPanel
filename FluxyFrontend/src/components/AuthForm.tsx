import { useEffect, useState } from 'react'
import type { ReactNode } from 'react'
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
import type { MessageResponse } from '../types'

/**
 * Field names the API may answer with, matching the camelCase JSON property names it uses
 * for them. Kept in step with `formItems` below so a field the server can reject always has
 * a rendered input to carry the reason.
 */
const SERVER_FIELDS = ['username', 'email', 'password']

/** What a page's `onSubmit` is handed, in the same shape `api.ts` expects. */
interface SubmitArgs<Values> {
  values: Values
  csrfToken: string | null
  captchaToken: string | null
}

interface AuthFormProps<Values> {
  /** The heading inside the card, already translated. */
  title: ReactNode
  /**
   * The captcha action for this endpoint - `client_login`, `register`, ... The server fixes
   * one per endpoint, so this is a name the caller has to get right rather than a setting.
   */
  action: string
  /** When the two differ. They usually do not, which is why it defaults to `action`. */
  captchaAction?: string
  /** Adds the two fields registration needs and the complexity rule that belongs to it. */
  register?: boolean
  /** The button's label, already translated. */
  submitText: ReactNode
  /** Text for an endpoint that answers a 2xx without naming its own outcome. */
  successText?: ReactNode
  /** Performs the call. Returns the response so the caller can read its `code`. */
  onSubmit: (args: SubmitArgs<Values>) => Promise<MessageResponse>
  /** Called after a successful submit, with the response and the values it was built from. */
  onSuccess?: (result: MessageResponse, values: Values) => void
  /** The cross-link between the two flows - see `AuthCard`. */
  footer?: ReactNode
}

/**
 * The shared credentials form.
 *
 * Generic in the values it collects, and with no default: a page that forgets the argument
 * gets `unknown` where its `values` should be, which is a compile error rather than a
 * `string` that turns out to be `undefined`. The type argument states what the page's form
 * actually holds - `SignInValues` for an entrance, `RegistrationValues` for a flow that
 * also takes an address - and from there everything downstream is checked: the call into
 * `api.ts`, and any field the page reads off `values` in `onSuccess`.
 */
export default function AuthForm<Values>({
  title,
  action,
  captchaAction = action,
  register = false,
  submitText,
  successText,
  onSubmit,
  onSuccess,
  footer,
}: AuthFormProps<Values>) {
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

  const handleFinish = async (values: Values) => {
    setSubmitting(true)
    // A rejection from the previous attempt has to go before the next submit, or the
    // fields stay red even after the visitor corrected them. Only the errors are cleared -
    // what the visitor typed is still what they want.
    clearServerErrors()
    try {
      const [csrfToken, captchaToken] = await Promise.all([
        getCsrfToken(),
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

  // The complexity rule belongs to registration and only to registration. A sign-in is not
  // choosing a password - it is presenting one the account already has - and an account
  // created before a rule existed, or by someone who set a password in another tool, would
  // be locked out of itself by a form that refuses to submit. It would also tell an
  // attacker something for free: "this password is worth guessing" is a different response
  // from "this password is wrong", and only one of them is what the server will say.
  //
  // The length bounds stay on both. They bound the request rather than judge the password,
  // and the server applies exactly the same numbers - see LoginRequest.
  const passwordRules = [
    { required: true, message: t('validation.passwordRequired') },
    { min: PASSWORD_MIN, max: PASSWORD_MAX, message: t('validation.passwordLength') },
    ...(register
      ? [
          {
            validator: (_: unknown, value: string) =>
              !value || meetsPasswordComplexity(value)
                ? Promise.resolve()
                : Promise.reject(new Error(t('validation.passwordComplexity'))),
          },
        ]
      : []),
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