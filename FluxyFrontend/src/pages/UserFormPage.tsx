import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import {
  Alert,
  App,
  Button,
  Card,
  Divider,
  Form,
  Input,
  Select,
  Space,
  Spin,
  Typography,
} from 'antd'
import { useTranslation } from 'react-i18next'
import { createUser, getUser, updateUser } from '../lib/api'
import { getCsrfToken } from '../lib/csrf'
import { ApiError, fieldErrors, messageForError, textForCode } from '../lib/http'
import { areaForRole } from '../lib/session'
import { useSession } from '../lib/sessionContext'
import {
  EMAIL_MAX,
  meetsPasswordComplexity,
  PASSWORD_MAX,
  PASSWORD_MIN,
  USERNAME_MAX,
  USERNAME_MIN,
} from '../lib/policy'
import { AutoTimeZone, knownZones } from '../lib/timeZones'
import LoginGuardFields from '../components/LoginGuardFields'
import { formValuesToGuard, guardToFormValues } from '../lib/loginGuardForm'
import type { GuardFieldValues } from '../lib/loginGuardForm'
import type { AdminUserDetail, LoginGuardSettings, Role, UserStatus } from '../types'

/**
 * What the form collects.
 *
 * The five guard fields are `GuardFieldValues`, spelled by the same `LoginGuardFields` the
 * profile's own card renders, because two flows editing the same five values under the same
 * rules would drift apart the moment their copies did. Everything above them is the account
 * itself.
 *
 * `timeZone` is a `string` rather than `string | null` because a `Select` cannot hold
 * `null`: "follow the browser" travels as the sentinel `AutoTimeZone` through the form and
 * becomes `null` (on create) or `''` (on PATCH, where the empty string means "clear it") on
 * the way out - see `handleFinish`.
 */
interface UserFormValues extends GuardFieldValues {
  username: string
  email: string
  /** Blank at the start of every attempt: see the note on the PATCH body in `handleFinish`. */
  password: string
  role: Role
  status: UserStatus
  timeZone: string
}

interface UserFormPageProps {
  /**
   * The menu key every section route carries, passed here for the reason it is passed to
   * every other section page and deliberately unread: one component serves two addresses,
   * so the heading is chosen by whether there is an id rather than read from a key that
   * names only the first of them. The locale files say the same above `users.addTitle`.
   */
  sectionKey: string
}

/** The three roles and three states, in the order the server declares them. */
const ROLES: Role[] = ['Client', 'Reseller', 'Admin']
const STATUSES: UserStatus[] = ['Unregistered', 'Registered', 'Blocked']

/** An account with no guard yet - what the add form starts from. */
const EMPTY_GUARD: LoginGuardSettings = {
  geoProtectionEnabled: false,
  bindSessionToIp: false,
  allowedIps: [],
  allowedCountry: null,
  allowedAutonomousSystemNumber: null,
}

/**
 * The add form's starting content, and the shape the edit form is painted over once its
 * account arrives.
 *
 * Role `Client` and status `Registered` rather than the row a self-service registration
 * would start with: an account made by hand here has its password typed in below and exists
 * to be signed in with right away, so `Unregistered` - the state that waits for a mailed
 * code nobody asked for on this path - would leave it unable to do the one thing the
 * administrator opened this form to make.
 */
const CREATE_VALUES: UserFormValues = {
  username: '',
  email: '',
  password: '',
  role: 'Client',
  status: 'Registered',
  timeZone: AutoTimeZone,
  ...guardToFormValues(EMPTY_GUARD),
}

/**
 * Every field this form draws, so a refusal that landed under one of them on the previous
 * attempt can be cleared before the next attempt is judged. Without it a server refusal of
 * `email` would sit under the input after the visitor had already fixed it, and the field
 * would look broken while passing every rule the form has for it.
 */
// Annotated rather than `as const`: `Form.setFields` takes `NamePath<UserFormValues>`, which
// is a closed union of this form's own keys, and a bare tuple of strings widens to `string`
// on the way through `map` - enough to reach the call as something the form has never heard
// of. Keyed to the values interface instead, the same names the `Form.Item`s collect.
const FIELD_NAMES: readonly (keyof UserFormValues)[] = [
  'username',
  'email',
  'password',
  'role',
  'status',
  'timeZone',
  'geoProtectionEnabled',
  'bindSessionToIp',
  'allowedIps',
  'allowedCountry',
  'allowedAutonomousSystemNumber',
]

/** The stored account spelled as form content, drawn over `CREATE_VALUES` when the answer arrives. */
const detailToFormValues = (detail: AdminUserDetail): UserFormValues => ({
  username: detail.username,
  email: detail.email,
  // Always blank: there is no password to show back, and the field below reads a blank
  // password as "keep the one that is there" - which is what the PATCH body says too.
  password: '',
  role: detail.role,
  status: detail.status,
  timeZone: detail.timeZone ?? AutoTimeZone,
  ...guardToFormValues(detail.loginGuard),
})

/**
 * The administrator's add and edit form - one component behind two addresses.
 *
 * `users/add` is a menu item and `users/{id}` is not: the sidebar offers the two things an
 * administrator can decide to do (add an account, look at the accounts), while this second
 * address is only ever reached by choosing a row, and a third entry saying "edit" would be
 * a page that is empty until you tell it *which*. React-router ranks the static segment
 * above the dynamic one, so `users/add` is always this form in its add mode no matter which
 * of the two lazy types is mounted over the module (see `routes.tsx` for why there are two).
 *
 * The account is fetched rather than passed: the visitor arrives by a URL, possibly by one
 * they have bookmarked, and a form painted from whatever the previous screen happened to
 * hold would edit the wrong row after a reload. While that fetch is in flight the page is a
 * spinner - never empty fields against nothing, which is what a static render of this page
 * lands in and what `probe:render` asserts.
 *
 * Three rules the form keeps that are the server's, not its own:
 *
 * - The id is **static text**, not a field: it is not editable and it never travels in the
 *   body (the path carries it), so it is a `Form.Item` with no `name` - layout only, which
 *   is exactly the difference between "shown" and "collectable".
 * - A blank password in edit mode means **keep the current one**, which is why the field is
 *   required only on add and why `password` is sent as `null` when it is still blank.
 * - The guard below is edited **without the caller-address anti-lock rule**: that rule stops
 *   an administrator from locking *themselves* out of *their own* account on the profile
 *   page, and here the caller's network is the wrong network for an account that is not
 *   theirs - so there is deliberately no "use my current network" button (see the `fillCurrent`
 *   prop of `LoginGuardFields`).
 *
 * Success goes back to the list rather than staying here: the save-and-close shape matches
 * the button beside it, and the list is where the change is visible among the other rows.
 */
export default function UserFormPage(_props: UserFormPageProps) {
  const { id } = useParams()
  const isEdit = id !== undefined

  const navigate = useNavigate()
  const { t } = useTranslation()
  const { message } = App.useApp()
  const session = useSession()
  const [form] = Form.useForm<UserFormValues>()

  const [detail, setDetail] = useState<AdminUserDetail | null>(null)
  // True before the first render in edit mode: the form must not draw its fields against an
  // account that has not arrived, and `renderToStaticMarkup` never runs the effect that
  // would clear this - which is what makes the spinner below the state every probe sees.
  const [loading, setLoading] = useState(isEdit)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  // Bumped by the retry button rather than by anything the page navigated to, so a dropped
  // connection can be asked about again without leaving the address the visitor chose.
  const [attempt, setAttempt] = useState(0)

  // The area this page hangs from - both buttons below go back into it. `null` only under
  // the probes, which render no provider: `RequireAuth` publishes one for every visitor
  // this page can have, and without it the navigation is inert rather than wrong.
  const area = session ? areaForRole(session.role) : ''
  const listPath = `${area}/users`

  useEffect(() => {
    if (!id) return

    let cancelled = false
    setLoading(true)

    getUser(id)
      .then((answer) => {
        if (cancelled) return
        setDetail(answer)
        setLoadError(null)
        // Painted rather than passed as `initialValues`: the form is already mounted by the
        // time the answer lands (the spinner above is what drew in the meantime), and
        // `initialValues` only ever applies to fields nothing has set yet.
        form.setFieldsValue(detailToFormValues(answer))
      })
      .catch((error) => {
        if (cancelled) return
        setDetail(null)
        setLoadError(messageForError(error))
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [id, form, attempt])

  /**
   * Whether the guard's allow lists are enforced right now - the value `LoginGuardFields`
   * disables them by while the switch is off. The watch is taken here rather than inside
   * that component so it can be given its floor: `Form.useWatch` answers `undefined` until
   * its own effect runs (and always under a static render), so the stored value - or `false`
   * on the add form, where nothing is stored yet - is what keeps the lists greyed out or not
   * from the very first paint.
   */
  const protectionEnabled: boolean =
    Form.useWatch('geoProtectionEnabled', form) ??
    (detail?.loginGuard.geoProtectionEnabled ?? false)

  const roleOptions = useMemo(
    () => ROLES.map((role) => ({ value: role, label: t(`profile.roles.${role}`) })),
    [t],
  )

  const statusOptions = useMemo(
    () => STATUSES.map((status) => ({ value: status, label: t(`users.statuses.${status}`) })),
    [t],
  )

  const zones = useMemo(knownZones, [])

  const timeZoneOptions = useMemo(
    () => [
      { value: AutoTimeZone, label: t('profile.timezoneAuto') },
      ...zones.map((zone) => ({ value: zone, label: zone })),
    ],
    [zones, t],
  )

  // The registration form's rules, because the server applies the same numbers to an
  // administrator's input as to a self-service one - see `RegistrationPolicy` on the backend.
  // The password's length and complexity live in one validator rather than in two rules
  // because of the blank field: an empty password on the edit form means "keep the one that
  // is there", and one validator states that short-circuit where a `min` rule would leave it
  // to how a validator library happens to treat an empty string.
  const passwordRules = [
    ...(isEdit ? [] : [{ required: true, message: t('validation.passwordRequired') }]),
    {
      validator: (_: unknown, value: string) => {
        if (!value) return Promise.resolve()
        if (value.length < PASSWORD_MIN || value.length > PASSWORD_MAX) {
          return Promise.reject(new Error(t('validation.passwordLength')))
        }
        return meetsPasswordComplexity(value)
          ? Promise.resolve()
          : Promise.reject(new Error(t('validation.passwordComplexity')))
      },
    },
  ]

  const handleFinish = async (values: UserFormValues) => {
    setSubmitting(true)
    form.setFields(FIELD_NAMES.map((name) => ({ name, errors: [] })))

    try {
      // Minted here rather than remembered - the server binds a token to the identity that
      // was current when it was minted, so a token from before a refresh would come back
      // `csrf_invalid`. Every submit in this application mints at call time for that reason.
      const csrfToken = await getCsrfToken()
      const loginGuard = formValuesToGuard(values)
      const timeZone = values.timeZone === AutoTimeZone ? '' : values.timeZone

      const result =
        isEdit && detail
          ? await updateUser({
              id: detail.id,
              patch: {
                username: values.username,
                email: values.email,
                // Blank stays blank: on this endpoint `null` is "keep the current password",
                // and the field only ever sends something the visitor typed into it.
                password: values.password || null,
                role: values.role,
                status: values.status,
                // `''` clears the zone so the account goes back to following its own
                // browser; `null` would mean "leave it alone", and this form always says
                // what it wants rather than what to skip.
                timeZone,
                loginGuard,
              },
              csrfToken,
            })
          : await createUser({
              values: {
                username: values.username,
                email: values.email,
                password: values.password,
                role: values.role,
                status: values.status,
                // On create the same "automatic" travels as `null`: there is no stored zone
                // to preserve, so "keep" would be an answer to a question never asked.
                timeZone: values.timeZone === AutoTimeZone ? null : values.timeZone,
                loginGuard,
              },
              csrfToken,
            })

      // The server's own sentence where this build translates the code, its English `message`
      // where it does not - the rule every other page follows.
      message.success(textForCode(result.code) ?? result.message)
      navigate(listPath)
    } catch (error) {
      const fields = fieldErrors(error instanceof ApiError ? error.errors : null)
      if (fields.length > 0)
        form.setFields(
          fields.map((field) => ({
            // The server keys refusals by the camelCase JSON name, which for this form is the
            // same set of strings the `Form.Item`s collect - so the cast bridges two type
            // systems, not two spellings. `fieldErrors` is declared over `unknown` for the
            // pages whose forms are untyped; this one is typed and says which keys it has.
            name: field.name as keyof UserFormValues,
            errors: field.errors,
          })),
        )
      message.error(messageForError(error))
    } finally {
      setSubmitting(false)
    }
  }

  const cardTitle = isEdit ? t('users.editTitle') : t('users.addTitle')

  if (loadError) {
    // Edit mode only: nothing fetches on add, so this state cannot be reached there. The
    // title stays because the address is still an edit address - the account is simply not
    // on this server anymore (or never was, for a hand-typed URL).
    return (
      <Card title={cardTitle}>
        <Alert
          type="error"
          showIcon
          message={loadError}
          action={
            <Space>
              {/* Re-ask rather than only offering a way out: the refusal may be a dropped
                  connection as easily as a missing account, and a missing account reads the
                  same either way. */}
              <Button size="small" loading={loading} onClick={() => setAttempt((value) => value + 1)}>
                {t('actions.retry')}
              </Button>
              <Button size="small" onClick={() => navigate(listPath)}>
                {t('users.backToList')}
              </Button>
            </Space>
          }
        />
      </Card>
    )
  }

  return (
    <Card title={cardTitle}>
      {loading ? (
        <div style={{ textAlign: 'center', padding: 48 }}>
          <Spin />
        </div>
      ) : (
        <Form<UserFormValues>
          form={form}
          layout="vertical"
          initialValues={CREATE_VALUES}
          onFinish={handleFinish}
          style={{ maxWidth: 640 }}
        >
          {detail && (
            <Form.Item label={t('users.idLabel')}>
              {/* No `name`: a Form.Item without one is layout only, so nothing here is
                  collected and nothing can be submitted - which is the requirement. It is
                  `copyable` because the identifier's only use outside this form is pasted
                  somewhere (a ticket, a query), and a GUID read off the screen is a GUID
                  mistyped. */}
              <Typography.Text code copyable={{ text: detail.id }}>
                {detail.id}
              </Typography.Text>
            </Form.Item>
          )}

          <Form.Item
            name="username"
            label={t('fields.username')}
            rules={[
              { required: true, message: t('validation.usernameRequired') },
              { min: USERNAME_MIN, max: USERNAME_MAX, message: t('validation.usernameLength') },
            ]}
          >
            <Input autoComplete="off" placeholder={t('fields.usernamePlaceholder')} />
          </Form.Item>

          <Form.Item
            name="email"
            label={t('fields.email')}
            rules={[
              { required: true, message: t('validation.emailRequired') },
              { type: 'email', message: t('validation.emailInvalid') },
              // The one bound the sign-in flow's own rules carry for the same reason: an
              // address past `EMAIL_MAX` passes every rule above and is refused only by the
              // server, where the reason lands in a toast instead of under the field.
              { max: EMAIL_MAX, message: t('validation.emailMaxLength', { max: EMAIL_MAX }) },
            ]}
          >
            <Input autoComplete="off" placeholder={t('fields.emailPlaceholder')} />
          </Form.Item>

          <Form.Item
            name="password"
            label={t('fields.password')}
            // `new-password` on both addresses deliberately: this is somebody else's
            // password being set, and the browser offering to fill the administrator's own
            // credentials into it would be autofill answering the wrong question.
            extra={isEdit ? t('users.passwordKeep') : undefined}
            rules={passwordRules}
          >
            <Input.Password autoComplete="new-password" />
          </Form.Item>

          <Form.Item name="role" label={t('users.columns.role')}>
            {/* No `allowClear` and no required rule: an account with no level is one every
                role check has to guess about, and this select can only ever hold one of the
                three - so there is no state for the rule to catch. The server still has the
                last word on a change it refuses (`cannot_demote_self` for an administrator
                demoting this account, which is this account if the id above is their own). */}
            <Select options={roleOptions} />
          </Form.Item>

          <Form.Item name="status" label={t('users.columns.status')}>
            {/* Also always one of the three. Changing it here moves the row through the same
                state transition the table's block/unblock/confirm buttons use - the server
                stamps or clears `registeredAt`, revokes sessions, decides which state an
                unblock returns to - so this select asks for the destination and the service
                decides how to get there, exactly as the dedicated actions do. */}
            <Select options={statusOptions} />
          </Form.Item>

          <Form.Item name="timeZone" label={t('profile.timezoneTitle')}>
            {/* The profile's own list, down to the sentinel: "automatic" here means this
                account's browser decides what zone its dates are shown in - the same
                `profile.timezoneAuto` the account's own profile page would pick. */}
            <Select
              showSearch
              options={timeZoneOptions}
              optionFilterProp="label"
              aria-label={t('profile.timezoneTitle')}
            />
          </Form.Item>

          {/* `titlePlacement` rather than antd 5's `orientation="left"`: in antd 6 the text's
              position moved to its own prop, and `orientation` now names the line itself. */}
          <Divider titlePlacement="start">{t('profile.guardTitle')}</Divider>

          {/* The five guard fields, shared with the profile's card. No `fillCurrent`: the
              button it would draw fills the lists from the *caller's* network, which on
              somebody else's account is the wrong network to allow. */}
          <LoginGuardFields protectionEnabled={protectionEnabled} />

          <Form.Item style={{ marginTop: 24 }}>
            <Space>
              <Button type="primary" htmlType="submit" loading={submitting}>
                {isEdit ? t('actions.save') : t('users.create')}
              </Button>
              <Button disabled={submitting} onClick={() => navigate(listPath)}>
                {t('users.backToList')}
              </Button>
            </Space>
          </Form.Item>
        </Form>
      )}
    </Card>
  )
}
