import { Card, Form, Image } from 'antd'
import LanguageSwitcher from './LanguageSwitcher'

/**
 * The frame every unauthenticated page shares: the language switcher, the logo, and a
 * card sized for a short form.
 *
 * It exists so that a second form - the confirmation code input - looks like the first one
 * without copying the layout. It owns no state and no rule; it only decides where a form
 * sits and hands its fields to the antd `Form` the caller brought.
 */
export default function AuthCard({
  title,
  form,
  onFinish,
  autoComplete = 'on',
  children,
  footer,
}) {
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