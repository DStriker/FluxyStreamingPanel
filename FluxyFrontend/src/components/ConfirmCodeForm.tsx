import { useState } from 'react'
import type { ReactNode } from 'react'
import { App, Button, Form, Input } from 'antd'
import type { FormRule, InputProps } from 'antd'
import { useTranslation } from 'react-i18next'
import AuthCard from './AuthCard'
import { getCsrfToken } from '../lib/csrf'
import { getCaptchaToken } from '../lib/recaptcha'
import { ApiError, fieldErrors, messageForError } from '../lib/http'
import { ConfirmCaptchaAction } from '../lib/api'
import type { CallOptions } from '../lib/api'
import { CODE_LENGTH, EMAIL_MAX } from '../lib/policy'

/** What the registration confirm step submits: the address it was mailed to, and the code. */
export type RegistrationConfirmValues = { email: string; code: string }

/** What the password-reset confirm step submits: the login name, and the code. */
export type ResetConfirmValues = { username: string; code: string }

/**
 * The identifier field, as a bundle of the `Form.Item` and `Input` props it needs.
 *
 * Only the second step's field is described this way, because there are exactly two of
 * them and they differ in every part but the code beside them: the address the visitor just
 * typed, or the login name carried over from step one. Stating the bundle once keeps the
 * two shapes differing in one place instead of in five, which is the reason `identifier` is
 * a prop at all.
 */
interface ConfirmIdentifier {
  /** The item's name, which is also the JSON property the server may reject. */
  name: string
  /** Label above the input, already translated. */
  label: ReactNode
  /** The value the field starts with - the address from step one, when there is one. */
  initialValue?: string
  autoComplete: string
  placeholder: string
  rules: FormRule[]
  /** `Input`'s `type`; the address field sets it, the login-name field leaves it out. */
  type?: InputProps['type']
}

interface ConfirmCodeFormProps<Values extends { code: string }> {
  /** The address the code was mailed to. Ignored when `identifier` names its own field. */
  email?: string
  /** The field the code belongs to, when it is not the address - see the note above. */
  identifier?: ConfirmIdentifier
  /** The heading inside the card, already translated. */
  title?: ReactNode
  /** The captcha action for this endpoint. One per endpoint, fixed by the server. */
  captchaAction?: string
  /** Performs the call with the form's values and both tokens, in one object. */
  onSubmit: (args: Values & CallOptions) => Promise<unknown>
  /** Goes back a step - the visitor's own change of mind, and an expired code. */
  onBack: () => void
}

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
 *
 * It is generic in what the form collects, and with no default: the identifier field is
 * decided by the page, so only the page can say whether `values` carries an `email` or a
 * `username`, and a type argument it forgets to pass lands as `unknown` rather than as a
 * `string` that is not there. Both shapes are named above, so a call site reads as a fact.
 */
export default function ConfirmCodeForm<Values extends { code: string }>({
  email: initialEmail,
  identifier,
  title,
  captchaAction = ConfirmCaptchaAction,
  onSubmit,
  onBack,
}: ConfirmCodeFormProps<Values>) {
  const { t } = useTranslation()
  const [form] = Form.useForm()
  const { message } = App.useApp()
  const [submitting, setSubmitting] = useState(false)

  // What the code is being confirmed for. Everything the form knows about it is read from
  // here, so the two shapes below differ in one place instead of in five.
  const field: ConfirmIdentifier = identifier ?? {
    name: 'email',
    label: t('fields.email'),
    initialValue: initialEmail,
    autoComplete: 'email',
    placeholder: t('fields.emailPlaceholder'),
    rules: [
      { required: true, message: t('validation.emailRequired') },
      { type: 'email', message: t('validation.emailInvalid') },
      { max: EMAIL_MAX, message: t('validation.emailMaxLength', { max: EMAIL_MAX }) },
    ],
  }

  // No `getCsrfToken()` on mount here either, and for the same reason as `AuthForm`: the
  // submit below mints a fresh one immediately before sending, so a token requested while
  // the form was loading would only ever be the one that gets thrown away.

  const handleFinish = async (values: Values) => {
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