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
import {
  ALL_GROUPS_PAGE_SIZE,
  createUser,
  getProfile,
  getUser,
  getUserGroups,
  updateUser,
} from '../lib/api'
import { getCsrfToken } from '../lib/csrf'
import { ApiError, fieldErrors, messageForError, textForCode } from '../lib/http'
import { hinted } from '../lib/hinted'
import { canEdit } from '../lib/permissions'
import { areaForRole } from '../lib/session'
import { useSession } from '../lib/sessionContext'
import { combineStatus } from '../lib/userStatus'
import {
  EMAIL_MAX,
  GENERATED_PASSWORD_LENGTH,
  generatePassword,
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
import type {
  AdminUserDetail,
  LoginGuardSettings,
  UserGroup,
  UserPermission,
  UserStatus,
} from '../types'

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
  /**
   * Group the account joins, and therefore the level it holds.
   *
   * There is deliberately no `role` field: the level is not a property of the account at
   * all, it is read out of whichever group this names, so a form collecting both would be a
   * form that could contradict itself and the server would have to pick a winner. Empty on
   * the add form until the group list arrives - the base `Clients` group fills it then (see
   * the effect below) - and required before submit, because an account with no group is one
   * every role check has to guess about.
   */
  groupId: string
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

/** The three states, in the order the server declares them. */
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
 * Status `Registered` rather than the row a self-service registration would start with: an
 * account made by hand here has its password typed in below and exists to be signed in with
 * right away, so `Unregistered` - the state that waits for a mailed code nobody asked for on
 * this path - would leave it unable to do the one thing the administrator opened this form
 * to make.
 *
 * `groupId` starts empty rather than at a literal identifier. The base `Clients` group's id
 * *is* fixed on the backend (`BaseUserGroups.ClientsId`), but copying a GUID out of one
 * repository into a page that only wants it as a default would mean two places that have to
 * agree about a value neither of them owns - and the id is meaningless on an installation
 * where the row was seeded differently. So the effect below asks the server which groups
 * exist and fills this in with the base group of the lowest level, read from the very list
 * the picker draws.
 */
const CREATE_VALUES: UserFormValues = {
  username: '',
  email: '',
  password: '',
  groupId: '',
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
  'groupId',
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
  groupId: detail.groupId,
  // The row's own state, not the effective one: this field is what `PATCH` writes, and the
  // effective state is derived from it plus the group's - see the note under the status
  // select for how the two are shown side by side.
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
 * - **The group picker offers only groups the caller may write accounts of, and Save greys
 *   for the one the row is already in if they may not.** Which roles an operator may reach is
 *   now a per-role grant rather than one `editUsers` bit, so the destination of this save is
 *   a fact the server checks - the role being left *and* the one being arrived in. The held
 *   group is never dropped from the list, because it is what the row is in and a form that
 *   cannot describe the account it opened on is worse than one whose Save the page is about
 *   to disable.
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
  // The eye over the password, controlled from the first paint rather than switched to
  // controlled later - an uncontrolled antd input becoming controlled is a warning about
  // a state two places think they own. Opened by `handleGeneratePassword` below.
  const [passwordVisible, setPasswordVisible] = useState(false)
  // The groups the picker draws, read once on both addresses, plus the reason it could not
  // read them. Not an `alert` over the whole form: the rest of the account is editable
  // without knowing which groups exist, and a page that stopped working because one of its
  // lookups failed would be reporting a limitation as a breakdown. The message is the
  // server's own (`permission_denied` reads as a permission sentence, a dead connection as
  // a connection one), so it is worth showing rather than swallowing.
  const [groups, setGroups] = useState<UserGroup[]>([])
  const [groupsError, setGroupsError] = useState<string | null>(null)
  /**
   * Everything the signed-in operator's group grants, read once on this page too.
   *
   * It is what the two rules below ask: which groups the picker may offer, and whether Save
   * is worth drawing live. `viewUsers`/`editUsers` are gone - "may this operator touch
   * accounts" is now a claim about a *particular role*, so an administrator who may run the
   * clients still cannot move one into the resellers, and the account behind this address
   * may be in a group outside their grants entirely.
   *
   * Empty rather than "unknown" when the call fails, mirroring `AccountStanding.Permissions`
   * on the backend: an account the server cannot find grants nothing, and a form that went
   * ahead on the strength of a lookup it did not get would be claiming a permission nobody
   * has confirmed. The server refuses regardless - this only decides what is drawn.
   */
  const [granted, setGranted] = useState<UserPermission[]>([])

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

  useEffect(() => {
    // The operator's own grants, in an effect of its own rather than folded into either
    // fetch above: this is a third answer from a third place, and it must not take the
    // account or the group list down with it when it is the one that fails. It gates what
    // is drawn, never what is loaded - the account is read in full either way, because a
    // form that cannot open is a form that cannot explain why it is closed.
    let cancelled = false

    getProfile()
      .then((profile) => {
        if (!cancelled) setGranted(profile.permissions)
      })
      .catch(() => {})

    return () => {
      cancelled = true
    }
  }, [])

  useEffect(() => {
    // The picker's own options. One page of the ceiling rather than a second unpaged route
    // - see `getUserGroups` for why the account form and the accounts table ask the same
    // endpoint for the same thing.
    //
    // It fails on its own, without a retry button: the only reason this can fail that is
    // worth repeating is a dropped connection, and the one reason it will not heal on its
    // own - the caller's group holding no `viewUserGroups` - is not something another click
    // would change. Either way the message goes under the field it blocks, where it explains
    // exactly what could not be offered.
    let cancelled = false

    getUserGroups({ page: 1, pageSize: ALL_GROUPS_PAGE_SIZE })
      .then((answer) => {
        if (cancelled) return
        setGroups(answer.items)
        setGroupsError(null)

        // The add form's default, read from the list rather than written into this file:
        // the base group of the lowest level is where an account made by hand belongs until
        // the administrator says otherwise. Written only when nothing is chosen yet, so a
        // back button into a half-filled form keeps whatever was already picked - and never
        // in edit mode, where the account's own group is what the answer painted above.
        if (isEdit) return
        if (form.getFieldValue('groupId')) return

        const base = answer.items.find((group) => group.isBase && group.role === 'Client')
        if (base) form.setFieldsValue({ groupId: base.id })
      })
      .catch((error) => {
        if (cancelled) return
        setGroups([])
        setGroupsError(messageForError(error))
      })

    return () => {
      cancelled = true
    }
  }, [form, isEdit])

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

  /**
   * What the account is **actually** in right now, and the group it is heading for.
   *
   * Both halves are watched rather than read from `detail`, because the note this feeds has
   * to keep telling the truth while the visitor is still choosing: pick a blocked group and
   * the account becomes blocked on the spot, and the `409 user_blocked_by_group` an unblock
   * would come back with is worth seeing *before* the save rather than after it. The
   * arithmetic is `combineStatus`, the frontend's copy of the server's own rule - the
   * server's `effectiveStatus` on arrival is what the same two inputs must reproduce, and
   * every refusal about them still comes from the server.
   *
   * The fallbacks are the states of a form with nothing in it yet: a static render, a
   * spinner, or a group list that has not arrived - none of which the note is drawn in, and
   * all of which must produce a *value* rather than `undefined` because a comparison
   * against `undefined` would decide a branch instead of leaving it alone.
   */
  const ownStatus: UserStatus = Form.useWatch('status', form) ?? detail?.status ?? 'Registered'
  const chosenGroupId: string = Form.useWatch('groupId', form) ?? detail?.groupId ?? ''
  const chosenGroup = groups.find((group) => group.id === chosenGroupId)
  const effectiveStatus: UserStatus =
    chosenGroup !== undefined
      ? combineStatus(ownStatus, chosenGroup.status)
      : (detail?.effectiveStatus ?? ownStatus)

  /**
   * Which groups the picker may offer - and, by the same rule, which roles the account may
   * be moved *into*.
   *
   * The server enforces the same split (it checks the role being left and the role being
   * arrived in), so this is the difference between being told before the choice and being
   * told after it. Two rules keep the list honest:
   *
   * - Only a group whose role the operator may edit is offered.
   * - **The group the form is already holding is always kept**, even when its role sits
   *   outside those grants: in edit mode that is the row's own group, and dropping it would
   *   blank the one field that says what the account is in right now; on add it is the
   *   default the picker chose. It cannot be picked *into* again - once the selection moves
   *   somewhere allowed, the refused one is no longer offered.
   */
  const groupOptions = useMemo(() => {
    const editable = groups.filter((group) => canEdit(granted, group.role))
    const held = groups.filter(
      (group) => group.id === chosenGroupId && !editable.includes(group),
    )

    return [...held, ...editable].map((group) => ({ value: group.id, label: group.name }))
  }, [groups, granted, chosenGroupId])

  /**
   * Whether Save is worth drawing live - asked about the group the account is going to,
   * which is the fact the server checks (the role being left *and* the role being arrived
   * in), rather than about accounts in general.
   *
   * `undefined` means the destination is not known yet - the list has not arrived, or the
   * field is still empty - and then nothing is greyed: the `required` rule under the picker
   * is what refuses an empty destination, and greying over a lookup that has not finished
   * would be describing a permission nobody has been asked about.
   */
  const maySave = chosenGroup === undefined || canEdit(granted, chosenGroup.role)

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

  /**
   * Fills the password field and opens it at once.
   *
   * The value is meant to be handed to whoever will sign in with it, and a masked one
   * cannot be handed over - so the eye opens with the generation and the visitor closes
   * it afterwards if they want to. An explicit button rather than the browser's own
   * offer alone, because that offer is a heuristic of the visitor's browser: absent in
   * some, and it may file the value into the administrator's own vault instead of
   * saying it out loud. Both modes get it - an account is created with a password on
   * the add address just as often as it has one set on the edit address.
   */
  const handleGeneratePassword = () => {
    form.setFields([{ name: 'password', value: generatePassword(), errors: [] }])
    setPasswordVisible(true)
  }

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
                groupId: values.groupId,
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
                groupId: values.groupId,
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

  // Drawn once and wrapped only when it has a reason to be greyed: `hinted` around a live
  // button would put a tooltip on a control that needs no explanation, and the reason a
  // disabled button *is* disabled is the one thing the span exists to keep reachable - the
  // same rule the account table's own greyed actions follow.
  const saveButton = (
    <Button type="primary" htmlType="submit" loading={submitting} disabled={!maySave}>
      {isEdit ? t('actions.save') : t('users.create')}
    </Button>
  )

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
          // The `on` every guest form already gets from `AuthCard` - this page renders its
          // own `Form` and had none, which means antd 6 draws the `<form>` with no
          // `autocomplete` attribute at all. Together with the password input's
          // `new-password` below this is the pair a browser weighs before offering to
          // generate a strong password, and it is exactly what the registration form that
          // demonstrably triggers that offer states. The two identity fields opt out
          // individually instead: autofilling the administrator's own saved username or
          // address into somebody else's account is the browser answering the wrong
          // question, and a per-field `off` on a text input is honoured while the offer
          // on the password field survives.
          autoComplete="on"
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
            <Input.Password
              autoComplete="new-password"
              visibilityToggle={{
                visible: passwordVisible,
                onVisibleChange: setPasswordVisible,
              }}
            />
          </Form.Item>

          {/* The explicit way to get a strong password into the field, beside whatever the
              browser offers for it. `marginTop` pulls it close enough to read as belonging
              to the password rather than as a section of its own. */}
          <Form.Item style={{ marginTop: -12 }}>
            <Button disabled={submitting} onClick={handleGeneratePassword}>
              {t('users.generatePassword', { length: GENERATED_PASSWORD_LENGTH })}
            </Button>
          </Form.Item>

          <Form.Item
            name="groupId"
            label={t('users.columns.group')}
            // Required, unlike the three-valued selects below: there is no "no group" state
            // for an account to be in. The level, the effective status and every permission
            // the account may use are all read out of wherever this lands, so an empty one
            // is not a blank to be filled in later - it is a row nothing could answer for.
            rules={[{ required: true, message: t('users.groupRequired') }]}
            // What the choice *is*, rather than what it is called: the label above says
            // "Group" and the visitor is choosing between names, and the level is the fact
            // they are actually deciding. Drawn from the group on hand rather than from a
            // second request, so the sentence cannot disagree with the row it describes.
            extra={
              chosenGroup !== undefined
                ? t('users.groupLevel', { role: t(`profile.roles.${chosenGroup.role}`) })
                : undefined
            }
          >
            {/* No `allowClear`: an account with no group is one every role check has to
                guess about, so there is no state for the visitor to clear their way into.
                Search rather than a plain dropdown, because the whole list is here (one page
                of the ceiling) and a name is what somebody came to this field knowing. */}
            {/* rc-select's hidden combobox input claims `new-password` for itself
                (`autoComplete || 'new-password'` in its `SelectInput/Input.js`), so this
                form shows several inputs saying "new password" while holding exactly one
                password - and no prop can change that: the Select's own `autoComplete`
                reaches only the wrapper div, never the input (verified in the rendered
                markup). The two signals that actually decide whether the browser offers to
                generate a strong password are still both stated here - the form's `on`
                above and the password field's `new-password` below. Those combobox inputs
                are `type="text"` and never password-type fields, which is not what a
                password manager counts when it classifies a form. */}
            <Select
              showSearch
              options={groupOptions}
              optionFilterProp="label"
              placeholder={t('users.groupRequired')}
            />
          </Form.Item>

          {groupsError && (
            // Under the field it blocks and nowhere else: the rest of the form still works,
            // and an alert across the top of the card would be reporting one unavailable
            // lookup as a broken page. The sentence is the server's own, so a missing
            // permission reads as one and a dead connection as one.
            <Alert type="error" showIcon style={{ marginBottom: 16 }} message={groupsError} />
          )}

          <Form.Item name="status" label={t('users.columns.status')}>
            {/* Also always one of the three. Changing it here moves the row through the same
                state transition the table's block/unblock/confirm buttons use - the server
                stamps or clears `registeredAt`, revokes sessions, decides which state an
                unblock returns to - so this select asks for the destination and the service
                decides how to get there, exactly as the dedicated actions do. */}
            <Select options={statusOptions} />
          </Form.Item>

          {effectiveStatus !== ownStatus && (
            // The one thing on this form the row above it does not say: what the account is
            // *actually* in once the group is counted. It appears while the visitor is
            // still choosing rather than after the save, because the two refusals it
            // pre-empts (`409 user_blocked_by_group` on an unblock, and an account that
            // looks registered and cannot be signed in to) are both cheaper to read here.
            // Shown only when the group is what moves the outcome - when the two halves
            // agree there is nothing the status field is not already saying.
            <Alert
              type={effectiveStatus === 'Blocked' ? 'warning' : 'info'}
              showIcon
              style={{ marginBottom: 16 }}
              message={
                effectiveStatus === 'Blocked'
                  ? t('users.blockedByGroup', { group: chosenGroup?.name ?? detail?.groupName ?? '' })
                  : t('users.effectiveStatus', {
                      status: t(`users.statuses.${effectiveStatus}`),
                    })
              }
            />
          )}

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
              {maySave ? saveButton : hinted(t('users.noPermission'), saveButton)}
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
