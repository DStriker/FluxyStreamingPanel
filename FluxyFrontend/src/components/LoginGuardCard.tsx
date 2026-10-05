import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { App, Button, Card, Form, Input, InputNumber, Select, Switch, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { getCsrfToken } from '../lib/csrf'
import { getCaptchaToken } from '../lib/recaptcha'
import { ApiError, fieldErrors, messageForError, textForCode } from '../lib/http'
import { ProfileChangeCaptchaAction, lookupGeo } from '../lib/api'
import type { CallOptions, LoginGuardValues } from '../lib/api'
import { countryOptions, isCountryCode } from '../lib/countries'
import { isIpOrCidr } from '../lib/ip'
import { LOGIN_GUARD_MAX_IPS } from '../lib/policy'
import type { LoginGuardSettings } from '../types'

/** The code that means a confirmation code is on its way to the account's address. */
const Submitted = 'profile_change_submitted'

/**
 * What the guard form collects. The guard fields spell exactly what the server may
 * reject - `allowedIps`, not `ips` - because a rejection the form has no input for
 * arrives as a bare toast instead of a reason under the input.
 */
interface LoginGuardFormValues {
  currentPassword: string
  geoProtectionEnabled: boolean
  bindSessionToIp: boolean
  allowedIps: string[]
  allowedCountry: string
  allowedAutonomousSystemNumber: number | null
}

interface LoginGuardCardProps {
  /** The guard stored on the account - the draft starts here and returns here. */
  value: LoginGuardSettings
  /**
   * Performs the call with the form's values and both tokens, in one object. Answered
   * with the outcome; the card branches on its code the way the change form does.
   */
  onSubmit: (args: LoginGuardValues & CallOptions) => Promise<{ code: string }>
  /** A code is on its way - the page swaps the form for the confirmation step. */
  onSubmitted: () => void
  /** The change is already on the account - the page re-reads it. */
  onApplied: () => Promise<void>
}

/**
 * The form's spelling of the guard: `null` where the contract has `null`, and the empty
 * string where a field has nothing in it yet - neither an `Input` nor a `Select` can hold
 * `null`, so the translation happens on the way in and back on the way out, in exactly
 * these two places.
 *
 * The country is upper-cased on the way in for the same reason the selector below can
 * only draw what its options list: a stored `ru` has no option, and a `Select` asked for
 * a value with no option shows *nothing selected* - which reads as "no country is set"
 * over an account that has one, until a save writes `null` over it.
 */
const toFormValues = (value: LoginGuardSettings): LoginGuardFormValues => ({
  currentPassword: '',
  geoProtectionEnabled: value.geoProtectionEnabled,
  bindSessionToIp: value.bindSessionToIp,
  allowedIps: value.allowedIps,
  allowedCountry: (value.allowedCountry ?? '').toUpperCase(),
  allowedAutonomousSystemNumber: value.allowedAutonomousSystemNumber,
})

/**
 * The stored guard spelled as content rather than as an object - which is the whole point
 * of it, and the bug it exists to fix.
 *
 * The page builds the object inline, so it is a new object on every render, and a language
 * switch re-renders the page. The sync effect used to follow that identity, so it read
 * "something re-rendered" as "the server answered something new" and painted the stored
 * guard over the visitor's half-typed draft every time they changed the language. Content
 * is the only thing that distinguishes the two: nothing about the guard can change without
 * one of these five fields changing with it.
 */
const fingerprint = (guard: LoginGuardSettings): string =>
  [
    guard.geoProtectionEnabled ? '1' : '0',
    guard.bindSessionToIp ? '1' : '0',
    guard.allowedIps.join(','),
    guard.allowedCountry ?? '',
    guard.allowedAutonomousSystemNumber ?? '',
  ].join('|')

/**
 * The card that guards the sign-ins of the account: which networks may open a session,
 * and whether a session may move between addresses.
 *
 * Its own card rather than a fourth kind of the change form, because nothing about it
 * fits that form: the change form edits one value, this edits five, and the switch at the
 * top decides whether the four allow lists are read at all - while they are off they are
 * disabled rather than hidden, because a field that is not rendered is a field whose
 * value `onFinish` never hands over, and a save through it would empty the very lists it
 * was meant to keep. Splitting it out is also what lets it be rendered on its own, which
 * is how the probe asserts it draws at all: the profile page itself is a spinner until
 * the server answers, and an effect never runs under `renderToStaticMarkup`.
 *
 * Like `ConfirmCodeForm`, the card owns its form, its tokens and its field errors, and
 * the page owns what happens next: `onSubmitted` swaps in the confirmation step and
 * `onApplied` re-reads the account. A reason the card cannot place - a refusal about
 * nothing on the form - still reaches the visitor as a toast rather than as silence.
 */
export default function LoginGuardCard({ value, onSubmit, onSubmitted, onApplied }: LoginGuardCardProps) {
  const { t, i18n } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm()
  const [submitting, setSubmitting] = useState(false)
  const [lookingUp, setLookingUp] = useState(false)

  const storedGuard = fingerprint(value)
  /** What the draft currently shows, so a re-render with the same guard paints nothing. */
  const paintedRef = useRef<string | null>(null)

  /**
   * Puts a stored guard into the draft *and* records it as what the draft now shows. The
   * two happen together or not at all: a fingerprint the store does not actually hold
   * would tell the effect below that there is nothing to paint while the form still shows
   * something else.
   */
  const paint = useCallback(
    (guard: LoginGuardSettings) => {
      paintedRef.current = fingerprint(guard)
      form.setFieldsValue(toFormValues(guard))
    },
    [form],
  )

  // The draft follows the stored guard: after a save the page re-reads the account and
  // the card picks the confirmed values up, dropping whatever the visitor had typed but
  // not yet saved. Without this the card would keep offering the pre-save draft as
  // though the server had never answered. `initialValues` below paints the first draft;
  // this paints every one after it, because the form reads those only on mount.
  //
  // `storedGuard` - the content - is what it follows, and `value` is only the vehicle it
  // arrives in, which is why the effect may still re-run on every render without doing
  // anything: see `fingerprint` above for the reset this replaced.
  useEffect(() => {
    if (paintedRef.current === storedGuard) return
    paint(value)
  }, [paint, value, storedGuard])

  // The switch decides whether the four allow lists are read at all, so while it is off
  // they are disabled rather than editable. `useWatch` answers `undefined` until its own
  // effect runs - and always under `renderToStaticMarkup`, where effects never run - so
  // the stored value is the floor beneath it.
  const geoProtectionEnabled: boolean =
    Form.useWatch('geoProtectionEnabled', form) ?? value.geoProtectionEnabled

  /**
   * The country names for the language in use - the codes underneath never change, only
   * how they are written. Memoised on the language alone: this list is built once per
   * language, and the form re-renders on every keystroke of every other field.
   */
  const countryList = useMemo(() => countryOptions(i18n.language), [i18n.language])

  const handleFinish = async (values: LoginGuardFormValues) => {
    setSubmitting(true)
    form.setFields(
      [
        'currentPassword',
        'geoProtectionEnabled',
        'bindSessionToIp',
        'allowedIps',
        'allowedCountry',
        'allowedAutonomousSystemNumber',
      ].map((name) => ({ name, errors: [] as string[] })),
    )

    try {
      const [csrfToken, captchaToken] = await Promise.all([
        getCsrfToken(),
        getCaptchaToken(ProfileChangeCaptchaAction),
      ])
      const result = await onSubmit({
        currentPassword: values.currentPassword,
        guard: {
          geoProtectionEnabled: values.geoProtectionEnabled,
          bindSessionToIp: values.bindSessionToIp,
          allowedIps: (values.allowedIps ?? []).map((entry) => entry.trim()).filter((entry) => entry !== ''),
          allowedCountry: values.allowedCountry?.trim() ? values.allowedCountry.trim().toUpperCase() : null,
          allowedAutonomousSystemNumber: values.allowedAutonomousSystemNumber ?? null,
        },
        csrfToken,
        captchaToken,
      })

      if (result?.code === Submitted) {
        message.success(textForCode(result.code) ?? t('profile.codeSent'))
        onSubmitted()
        return
      }

      // No mail server on this installation, so there was nothing to confirm and the
      // guard is already on the account - the same second outcome the change form reads
      // off the same code.
      message.success(textForCode(result?.code) ?? t('profile.changed'))
      // Reset clears the validation state the submit just went through; the paint that
      // follows is what puts the *stored* guard back into the draft, because `resetFields`
      // alone would leave `initialValues` there - what the page held on mount, which is
      // stale the moment the account changed underneath it.
      form.resetFields()
      paint(value)
      await onApplied()
    } catch (err) {
      const fields = fieldErrors(err instanceof ApiError ? err.errors : null)
      if (fields.length > 0) form.setFields(fields)
      message.error(messageForError(err))
    } finally {
      setSubmitting(false)
    }
  }

  /**
   * Fills the lists from the network the visitor arrived on: the address is appended
   * when there is room for it, the single-valued country and provider replace whatever
   * is there - "use mine" means adopt it, not merge with it.
   */
  const handleFillCurrent = async () => {
    setLookingUp(true)
    try {
      const where = await lookupGeo()
      const current = form.getFieldsValue(true) as Partial<LoginGuardFormValues>
      const ips = [...(current.allowedIps ?? [])]
      if (where.ip && !ips.includes(where.ip) && ips.length < LOGIN_GUARD_MAX_IPS) {
        ips.push(where.ip)
      }
      if (!where.ip && !where.countryCode && where.autonomousSystemNumber == null) {
        message.warning(t('profile.guardCurrentUnknown'))
        return
      }
      form.setFieldsValue({
        allowedIps: ips,
        // Only a country this selector can actually draw: a code outside the list would
        // be held by the form and shown as nothing selected, which reads as "country
        // cleared" while the value underneath it is still a country.
        allowedCountry:
          where.countryCode && isCountryCode(where.countryCode)
            ? where.countryCode
            : (current.allowedCountry ?? ''),
        allowedAutonomousSystemNumber:
          where.autonomousSystemNumber ?? current.allowedAutonomousSystemNumber ?? null,
      })
      message.success(t('profile.guardCurrentFilled'))
    } catch (err) {
      message.error(messageForError(err))
    } finally {
      setLookingUp(false)
    }
  }

  return (
    <Card title={t('profile.guardTitle')}>
      <Form
        form={form}
        layout="vertical"
        onFinish={handleFinish}
        initialValues={toFormValues(value)}
      >
        <Form.Item
          name="geoProtectionEnabled"
          label={t('profile.guardProtection')}
          valuePropName="checked"
        >
          <Switch />
        </Form.Item>

        <Typography.Paragraph type="secondary" style={{ marginTop: -8 }}>
          {t('profile.guardProtectionHint')}
        </Typography.Paragraph>

        <Form.Item>
          {/* Off with the lists it fills: a button that writes into three greyed-out
              inputs reports success over changes the visitor cannot see. */}
          <Button
            onClick={handleFillCurrent}
            loading={lookingUp}
            disabled={submitting || !geoProtectionEnabled}
          >
            {t('profile.guardUseCurrent')}
          </Button>
        </Form.Item>

        <Form.Item
          name="allowedIps"
          label={t('profile.guardIps', { max: LOGIN_GUARD_MAX_IPS })}
          rules={[
            {
              // Named rather than generic: the message carries the entry that broke the
              // rule, and a tags input has no other way to say which of its tags it was.
              validator: (_: unknown, ips: string[]) => {
                const invalid = (ips ?? []).find((entry) => !isIpOrCidr(entry.trim()))
                return invalid
                  ? Promise.reject(new Error(t('profile.guardIpsInvalid', { entry: invalid })))
                  : Promise.resolve()
              },
            },
          ]}
        >
          <Select
            mode="tags"
            tokenSeparators={[',', ' ', ';']}
            maxCount={LOGIN_GUARD_MAX_IPS}
            placeholder={t('profile.guardIpsPlaceholder')}
            aria-label={t('profile.guardIps', { max: LOGIN_GUARD_MAX_IPS })}
            disabled={!geoProtectionEnabled}
          />
        </Form.Item>

        <Form.Item
          name="allowedCountry"
          label={t('profile.guardCountry')}
        >
          {/* A selector rather than two typed letters, so the visitor picks a country
              the server can act on instead of guessing a code - and clears it to "no
              restriction", which is what an empty value means on the contract. No rule:
              a `Select` offers no value that is not already one of the options. */}
          <Select
            showSearch
            allowClear
            options={countryList}
            // Names, not codes: searching a code only was what the field did by hand,
            // and a name is what a visitor knows how to type.
            optionFilterProp="label"
            placeholder={t('profile.guardCountryPlaceholder')}
            aria-label={t('profile.guardCountry')}
            style={{ width: '100%', maxWidth: 320 }}
            disabled={!geoProtectionEnabled}
          />
        </Form.Item>

        <Form.Item
          name="allowedAutonomousSystemNumber"
          label={t('profile.guardAsn')}
        >
          <InputNumber
            min={1}
            precision={0}
            placeholder="AS12345"
            style={{ width: '100%', maxWidth: 220 }}
            disabled={!geoProtectionEnabled}
          />
        </Form.Item>

        <Form.Item
          name="bindSessionToIp"
          label={t('profile.guardBind')}
          valuePropName="checked"
        >
          <Switch />
        </Form.Item>

        <Typography.Paragraph type="secondary" style={{ marginTop: -8 }}>
          {t('profile.guardBindHint')}
        </Typography.Paragraph>

        <Form.Item
          name="currentPassword"
          label={t('fields.currentPassword')}
          rules={[{ required: true, message: t('validation.currentPasswordRequired') }]}
        >
          <Input.Password autoComplete="current-password" />
        </Form.Item>

        <Form.Item>
          <Button type="primary" htmlType="submit" loading={submitting}>
            {t('actions.save')}
          </Button>
        </Form.Item>
      </Form>
    </Card>
  )
}
