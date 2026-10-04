import { Card, Form, Image } from 'antd'
import type { FormInstance } from 'antd'
import type { ReactNode } from 'react'
import LanguageSwitcher from './LanguageSwitcher'

/**
 * The frame every unauthenticated page shares: the language switcher, the logo, and a
 * card sized for a short form.
 *
 * It exists so that a second form - the confirmation code input - looks like the first one
 * without copying the layout. It owns no state and no rule; it only decides where a form
 * sits and hands its fields to the antd `Form` the caller brought.
 *
 * Generic in the form's own value type, which it takes from the `form` instance rather
 * than declaring one: the values belong to the caller's form, and a card that fixed them
 * would have to know what each page collects. Written this way a page using
 * `Form.useForm<LoginValues>()` gets its `onFinish` typed from the same argument it passes
 * here, and a page that never says keeps antd's own default.
 */
interface AuthCardProps<Values> {
  /** The heading inside the card, already translated by the caller. */
  title: ReactNode
  /** The instance the caller created - `AuthCard` renders `<Form form={...}>` around it. */
  form?: FormInstance<Values>
  /** What the caller does with the values the form produced. */
  onFinish?: (values: Values) => void
  /** antd's `Form` name for the `autocomplete` attribute; sign-in pages want `on`. */
  autoComplete?: 'on' | 'off'
  children?: ReactNode
  /** The cross-link between the two flows - see the comment inside the form below. */
  footer?: ReactNode
}

export default function AuthCard<Values>({
  title,
  form,
  onFinish,
  autoComplete = 'on',
  children,
  footer,
}: AuthCardProps<Values>) {
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
          onFinish={onFinish}
          requiredMark
          autoComplete={autoComplete}
        >
          {children}
          {/* Last child of the form, where the cross-link between the two flows has always
              sat: "no account? sign up" under the login forms, "have an account? sign in"
              under registration. */}
          {footer && <div className="auth-form__footer">{footer}</div>}
        </Form>
      </Card>
    </div>
  )
}