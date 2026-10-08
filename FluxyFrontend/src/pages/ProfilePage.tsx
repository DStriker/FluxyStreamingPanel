import { useCallback, useEffect, useRef, useState } from 'react'
import {
  Alert,
  App,
  Button,
  Card,
  Descriptions,
  Form,
  Input,
  Segmented,
  Space,
  Spin,
  Typography,
} from 'antd'
import { useTranslation } from 'react-i18next'
import { getCsrfToken } from '../lib/csrf'
import { getCaptchaToken } from '../lib/recaptcha'
import {
  ProfileChangeCaptchaAction,
  ProfileConfirmCaptchaAction,
  changeEmail,
  changeLoginGuard,
  changePassword,
  changeUsername,
  confirmProfileChange,
  getProfile,
  updateTimezone,
} from '../lib/api'
import { ApiError, fieldErrors, messageForError, textForCode } from '../lib/http'
import { useSession } from '../lib/sessionContext'
import TimeZoneCard from '../components/TimeZoneCard'
import LoginGuardCard from '../components/LoginGuardCard'
import {
  CODE_LENGTH,
  PASSWORD_MAX,
  PASSWORD_MIN,
  USERNAME_MAX,
  USERNAME_MIN,
  meetsPasswordComplexity,
} from '../lib/policy'
import type { ProfileResponse } from '../types'

/** The codes that decide what happens next. */
const Submitted = 'profile_change_submitted'
const Confirmed = 'profile_change_confirmed'

const KINDS = ['username', 'email', 'password'] as const

/** Which of the three changes is being made - the switch, and the branch in `handleFinish`. */
type Kind = (typeof KINDS)[number]

/**
 * Everything that can wait for a code: the three changes above, plus the login guard.
 * The guard is not a fourth `Kind` because it is not edited by that form - it has its
 * own card - but it is confirmed by the same step, with the code mailed to the same
 * address the account holds.
 */
type PendingKind = Kind | 'guard'

/**
 * What the change form collects: the current password, which is always rendered, plus the
 * one field the selected `kind` adds.
 *
 * The three named fields are never all present - exactly one of them is on the form for any
 * given submit - so the type states what the form *can* hold rather than what it did. That
 * is safe because every read below is inside the `kind ===` branch that rendered it: the
 * guard and the field come from the same condition, and the one unguarded read
 * (`currentPassword`) is the one always rendered.
 */
interface ProfileFormValues {
  currentPassword: string
  username: string
  email: string
  newPassword: string
}

/** The change waiting for its code, and where the code was sent. */
interface PendingChange {
  kind: PendingKind
  where: string
}

/**
 * Changing the username, the email address or the password of the account behind this
 * session.
 *
 * One form with a switch rather than three forms, because the three are the same act - prove
 * you own this account, then say what it should become - and only the last field differs.
 * Three separate forms would carry three copies of the current-password field and of every
 * rule attached to it, and one of them would eventually drift.
 *
 * The confirmation step is inline rather than a second route, for the reason registration is
 * built that way: what changes is the form, not the place, and a step that moved address
 * would break a back button and a reload for no gain.
 *
 * Nothing here decides whether a change needs a code. That is the server's answer to the one
 * question this page cannot ask - whether this installation has a mail server at all - and
 * the page reads it off the response: `profile_change_submitted` means a code is on its way,
 * `profile_updated` means the change is already on the account.
 */
export default function ProfilePage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const session = useSession()

  const [form] = Form.useForm()
  const [codeForm] = Form.useForm()

  const [profile, setProfile] = useState<ProfileResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [kind, setKind] = useState<Kind>('username')
  const [submitting, setSubmitting] = useState(false)
  const [confirming, setConfirming] = useState(false)
  // The change waiting for its code, and where the code was sent - both are needed to tell
  // the visitor what to look for, and the address differs by kind: an address change is
  // confirmed at the address it is moving *to*.
  const [pending, setPending] = useState<PendingChange | null>(null)
  // The time zone saves on selection, so it needs a draft of its own: the card is
  // controlled by the value the server last confirmed, and without a draft in between the
  // selector would show the old zone until the re-read landed - or, if the re-read were
  // skipped, never at all. `undefined` means "no pick of my own yet, show what is stored";
  // `null` is a real pick ("automatic"), which is why the two are different values and the
  // draft cannot simply start at `null`.
  const [timeZoneDraft, setTimeZoneDraft] = useState<string | null | undefined>(undefined)
  const [savingTimeZone, setSavingTimeZone] = useState(false)

  /**
   * Reads the card's data, and decides for itself whether it is still allowed to show it.
   *
   * Two counters, because two different questions need answering. `generation` decides
   * *which* answer may land: `load` runs from the mount and from both submit handlers, so
   * an earlier request still in flight when a later one starts must not arrive afterwards
   * and overwrite fresher data - which is exactly what a rename produces, the pre-rename
   * read answering last. The effect below also bumps it on unmount, so nothing can be
   * written to a page that is gone.
   *
   * `everLoaded` decides whether the spinner belongs at all. It used to be `loading` on
   * its own, which meant every successful change re-ran the first-load branch: the card,
   * the form and both form instances unmounted and came back over data the visitor
   * already had. The spinner is now the *first* load's and nothing else's; a refresh
   * updates in place, which is what a reader of `setProfile` expects anyway.
   */
  const generation = useRef(0)
  const everLoaded = useRef(false)

  const load = useCallback(async () => {
    const mine = ++generation.current
    if (!everLoaded.current) setLoading(true)
    try {
      const answer = await getProfile()
      if (mine !== generation.current) return
      setProfile(answer)
      setLoadError(null)
      everLoaded.current = true
    } catch (err) {
      // A blocked or deleted account answers 403 with `account_not_active`, and the
      // interesting part of that is the sentence rather than the status code. An expired
      // token never reaches here: the transport refreshes and retries it first.
      if (mine !== generation.current) return
      setProfile(null)
      setLoadError(messageForError(err))
      everLoaded.current = true
    } finally {
      if (mine === generation.current) setLoading(false)
    }
  }, [])

  useEffect(() => {
    load()
    // Leaving the page invalidates whatever this one has in flight, so the guard inside
    // `load` refuses an answer that would otherwise arrive after the page unmounted.
    return () => {
      generation.current += 1
    }
  }, [load])

  /**
   * Puts a new login name in front of the visitor where they can see it.
   *
   * The username is the one value of the session that this page is allowed to change, and
   * the header's copy of it was read from `/auth/me` before the change went through - it
   * belongs to `RequireAuth`, not to this page, so nothing here can overwrite it directly.
   * `refresh` re-asks; reloading the page would do the same thing and additionally take
   * the success message off the screen before it could be read, which makes a correct
   * change look as though nothing happened.
   *
   * The other two kinds need no such thing: the address is deliberately not a claim of the
   * token, and a password change ends the *other* sessions of the account while leaving
   * this one exactly as it was.
   */
  const publishRename = async (renamed: boolean) => {
    if (renamed) await session?.refresh?.()
  }

  /**
   * Saves the picked time zone, without a code and without a password.
   *
   * The two outcomes follow the shape the rest of the page already uses, because they are
   * the same two outcomes: on success the row is re-read - which is what moves the selector
   * onto the stored value and drops the draft - and on failure the draft is dropped without
   * the re-read, so the selector falls back to what is actually stored instead of keeping
   * offering a zone the server refused as though it had been accepted.
   *
   * Picking what is already stored sends nothing at all. The permit window is per client,
   * not per account, and a selector opened by accident should not spend one.
   */
  const handleTimeZoneChange = async (next: string | null) => {
    const stored = profile?.timeZone ?? null
    setTimeZoneDraft(next)

    if (next === stored) return

    setSavingTimeZone(true)
    try {
      const csrfToken = await getCsrfToken()
      const result = await updateTimezone({ timeZone: next, csrfToken })

      message.success(textForCode(result.code) ?? t('profile.timezoneSaved'))
      await load()
      setTimeZoneDraft(undefined)
    } catch (err) {
      message.error(messageForError(err))
      setTimeZoneDraft(undefined)
    } finally {
      setSavingTimeZone(false)
    }
  }

  const handleFinish = async (values: ProfileFormValues) => {
    setSubmitting(true)
    form.setFields(
      ['currentPassword', 'username', 'email', 'newPassword'].map((name) => ({
        name,
        errors: [],
      })),
    )

    try {
      const [csrfToken, captchaToken] = await Promise.all([
        getCsrfToken(),
        getCaptchaToken(ProfileChangeCaptchaAction),
      ])
      const args = { currentPassword: values.currentPassword, csrfToken, captchaToken }

      const result =
        kind === 'username'
          ? await changeUsername({ ...args, username: values.username })
          : kind === 'email'
            ? await changeEmail({ ...args, email: values.email })
            : await changePassword({ ...args, newPassword: values.newPassword })

      if (result?.code === Submitted) {
        message.success(textForCode(result.code) ?? t('profile.codeSent'))
        setPending({
          kind,
          where: kind === 'email' ? values.email : (profile?.email ?? ''),
        })
        codeForm.resetFields()
        return
      }

      // No mail server on this installation, so there was nothing to confirm and the change
      // is already on the account. The form is cleared and the card re-read, which is the
      // only visible difference - the visitor never sees a second step at all. The rename
      // still has to reach the header, and this is the path that needs it most: there is
      // no confirmation step afterwards to notice the difference in.
      message.success(textForCode(result?.code) ?? t('messages.sent'))
      form.resetFields()
      await load()
      await publishRename(kind === 'username')
    } catch (err) {
      const fields = fieldErrors(err instanceof ApiError ? err.errors : null)
      if (fields.length > 0) form.setFields(fields)
      message.error(messageForError(err))
    } finally {
      setSubmitting(false)
    }
  }

  /**
   * A guard code is on its way to the account's address: swap the page for the
   * confirmation step, the way a username, email or password change does. The address
   * is the one the account holds - a guard change proves nothing about a new address,
   * so there is no "moving to" here.
   */
  const handleGuardSubmitted = () => {
    setPending({ kind: 'guard', where: profile?.email ?? '' })
    codeForm.resetFields()
  }

  /** The guard is already on the account: re-read it onto the card. */
  const handleGuardApplied = async () => {
    await load()
  }

  const handleConfirm = async (values: { code: string }) => {    const wasKind = pending?.kind
    setConfirming(true)
    codeForm.setFields([{ name: 'code', errors: [] }])

    try {
      const [csrfToken, captchaToken] = await Promise.all([
        getCsrfToken(),
        getCaptchaToken(ProfileConfirmCaptchaAction),
      ])
      const result = await confirmProfileChange({
        code: values.code,
        csrfToken,
        captchaToken,
      })

      if (result?.code !== Confirmed) return

      message.success(textForCode(result.code) ?? t('profile.changed'))
      setPending(null)
      codeForm.resetFields()
      form.resetFields()
      await load()
      await publishRename(wasKind === 'username')
    } catch (err) {
      const fields = fieldErrors(err instanceof ApiError ? err.errors : null)
      if (fields.length > 0) codeForm.setFields(fields)
      message.error(messageForError(err))
    } finally {
      setConfirming(false)
    }
  }

  if (loading) {
    return (
      <Card>
        <div style={{ textAlign: 'center', padding: 48 }}>
          <Spin />
        </div>
      </Card>
    )
  }

  if (loadError) {
    return (
      <Card>
        <Alert type="error" showIcon message={loadError} />
      </Card>
    )
  }

  if (pending) {
    return (
      <Card title={t('profile.confirmTitle')}>
        <Space direction="vertical" size={16} style={{ width: '100%' }}>
          <Alert
            type="info"
            showIcon
            message={t('profile.codeSentTo', { where: pending.where })}
            description={t('profile.codeSentHint')}
          />

          <Form form={codeForm} layout="vertical" onFinish={handleConfirm}>
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

            <Space>
              <Button type="primary" htmlType="submit" loading={confirming}>
                {t('actions.confirm')}
              </Button>
              <Button
                onClick={() => setPending(null)}
                loading={confirming}
                title={t('profile.cancelHint')}
              >
                {t('actions.cancel')}
              </Button>
            </Space>
          </Form>
        </Space>
      </Card>
    )
  }

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Card title={t('profile.currentTitle')}>
        <Descriptions column={1} size="small">
          <Descriptions.Item label={t('fields.username')}>
            {profile?.username ?? ''}
          </Descriptions.Item>
          <Descriptions.Item label={t('fields.email')}>{profile?.email ?? ''}</Descriptions.Item>
          <Descriptions.Item label={t('fields.role')}>
            {profile?.role ? t(`profile.roles.${profile.role}`) : ''}
          </Descriptions.Item>
        </Descriptions>
      </Card>

      <TimeZoneCard
        value={timeZoneDraft === undefined ? (profile?.timeZone ?? null) : timeZoneDraft}
        saving={savingTimeZone}
        onChange={handleTimeZoneChange}
      />

      {profile && (
        <LoginGuardCard
          value={{
            geoProtectionEnabled: profile.geoProtectionEnabled,
            bindSessionToIp: profile.bindSessionToIp,
            allowedIps: profile.allowedIps,
            allowedCountry: profile.allowedCountry,
            allowedAutonomousSystemNumber: profile.allowedAutonomousSystemNumber,
          }}
          onSubmit={changeLoginGuard}
          onSubmitted={handleGuardSubmitted}
          onApplied={handleGuardApplied}
        />
      )}

      <Card title={t('profile.changeTitle')}>
        <Space direction="vertical" size={16} style={{ width: '100%' }}>
          <Segmented
            value={kind}
            options={KINDS.map((value) => ({ value, label: t(`profile.kinds.${value}`) }))}
            onChange={(value) => {
              setKind(value)
              // The previous kind's fields are not on the form any more, and a red error
              // under a field that has been switched away is a complaint about nothing.
              form.resetFields()
            }}
          />

          {/* The same `on` the guest forms get from `AuthCard` (see UserFormPage): this
              form holds the change-password fields, and without it antd renders the
              `<form>` with no `autocomplete` attribute for the browser to weigh before
              offering to generate a strong one. */}
          <Form form={form} layout="vertical" onFinish={handleFinish} autoComplete="on">
            <Form.Item
              name="currentPassword"
              label={t('fields.currentPassword')}
              rules={[{ required: true, message: t('validation.currentPasswordRequired') }]}
            >
              <Input.Password autoComplete="current-password" />
            </Form.Item>

            {kind === 'username' && (
              <Form.Item
                name="username"
                label={t('fields.newUsername')}
                rules={[
                  { required: true, message: t('validation.usernameRequired') },
                  { min: USERNAME_MIN, max: USERNAME_MAX, message: t('validation.usernameLength') },
                ]}
              >
                <Input autoComplete="username" placeholder={t('fields.usernamePlaceholder')} />
              </Form.Item>
            )}

            {kind === 'email' && (
              <Form.Item
                name="email"
                label={t('fields.newEmail')}
                rules={[
                  { required: true, message: t('validation.emailRequired') },
                  { type: 'email', message: t('validation.emailInvalid') },
                ]}
              >
                <Input autoComplete="email" placeholder={t('fields.emailPlaceholder')} />
              </Form.Item>
            )}

            {kind === 'password' && (
              <>
                <Form.Item
                  name="newPassword"
                  label={t('fields.newPassword')}
                  rules={[
                    { required: true, message: t('validation.passwordRequired') },
                    { min: PASSWORD_MIN, max: PASSWORD_MAX, message: t('validation.passwordLength') },
                    {
                      validator: (_, value) =>
                        !value || meetsPasswordComplexity(value)
                          ? Promise.resolve()
                          : Promise.reject(new Error(t('validation.passwordComplexity'))),
                    },
                  ]}
                >
                  <Input.Password autoComplete="new-password" />
                </Form.Item>

                <Form.Item
                  name="confirmPassword"
                  label={t('fields.confirmPassword')}
                  dependencies={['newPassword']}
                  rules={[
                    { required: true, message: t('validation.confirmRequired') },
                    ({ getFieldValue }) => ({
                      validator(_, value) {
                        if (!value || getFieldValue('newPassword') === value) {
                          return Promise.resolve()
                        }
                        return Promise.reject(new Error(t('validation.passwordMismatch')))
                      },
                    }),
                  ]}
                >
                  <Input.Password autoComplete="new-password" />
                </Form.Item>
              </>
            )}

            <Form.Item>
              <Button type="primary" htmlType="submit" loading={submitting}>
                {t('actions.change')}
              </Button>
            </Form.Item>
          </Form>

          <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
            {t('profile.changeHint')}
          </Typography.Paragraph>
        </Space>
      </Card>
    </Space>
  )
}
