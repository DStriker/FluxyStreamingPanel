import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import {
  Alert,
  App,
  Button,
  Card,
  Checkbox,
  Form,
  Input,
  Select,
  Space,
  Spin,
  Typography,
} from 'antd'
import { useTranslation } from 'react-i18next'
import { createUserGroup, getUserGroup, updateUserGroup } from '../lib/api'
import { getCsrfToken } from '../lib/csrf'
import { ApiError, fieldErrors, messageForError, textForCode } from '../lib/http'
import { permissionsForRole } from '../lib/permissions'
import { areaForRole } from '../lib/session'
import { useSession } from '../lib/sessionContext'
import type { Role, UserGroupDetail, UserPermission, UserStatus } from '../types'

/**
 * What the form collects.
 *
 * Four fields and no id: the identifier is shown as static text and travels in the path of
 * a `PATCH`, never as a property of the body - a form that submitted its own id as data
 * would be one edit away from writing it.
 *
 * `permissions` is a list rather than a bitmask number, because the server answers with the
 * individual keys and a client that had to decode `15` would be a client that has to know
 * which bit the system set next, which is the one thing a permission catalog exists to keep
 * stable.
 */
interface GroupFormValues {
  name: string
  role: Role
  status: UserStatus
  permissions: UserPermission[]
}

interface UserGroupFormPageProps {
  /**
   * The menu key every section route carries, passed here for the reason it is passed to
   * every other section page and deliberately unread: the heading comes from whether there
   * is an id (`userGroups.addTitle`/`userGroups.editTitle`), so a key naming only the first
   * of the two addresses would title the second a lie. The locale files say the same above
   * `userGroups.addTitle`.
   */
  sectionKey: string
}

/** The three levels and three states, in the order the server declares them. */
const ROLES: Role[] = ['Client', 'Reseller', 'Admin']
const STATUSES: UserStatus[] = ['Unregistered', 'Registered', 'Blocked']

const FIELD_NAMES: readonly (keyof GroupFormValues)[] = ['name', 'role', 'status', 'permissions']

/**
 * The add form's starting content, and the shape the edit form is painted over once its
 * group arrives.
 *
 * **The level starts at `Client`, the least privileged of the three**, and that is a choice
 * rather than an oversight: a default is a decision about a group nobody has described yet,
 * and the two directions fail differently. Starting at `Client` hands over nothing until
 * somebody says so - a group created without looking would be a group that grants only what
 * every account grants - while starting at `Admin` would hand the four permissions this
 * whole page exists to manage to whoever pressed the button first. It also matches what the
 * add form for an *account* does: its default group is the base `Clients` group, and both
 * defaults are the same answer for the same reason.
 *
 * Status `Registered` for the reason the account form's is: an administrator making a group
 * here is about to use it, and a state that waits for a step nobody has to take would only
 * be in the way. `Client` groups carry no permissions at all, so the permission section
 * below opens with its explanation rather than with four boxes that would all be refused.
 */
const CREATE_VALUES: GroupFormValues = {
  name: '',
  role: 'Client',
  status: 'Registered',
  permissions: [],
}

/** What the edit form paints from a fetched group. */
const detailToFormValues = (detail: UserGroupDetail): GroupFormValues => ({
  name: detail.name,
  role: detail.role,
  status: detail.status,
  // The set as the server holds it, not as the intersection with the role. The two agree
  // for every row the server wrote (a grant its role does not own is dropped on the way
  // in), and this form draws whatever is actually stored rather than what it believes
  // ought to be there.
  permissions: detail.permissions,
})

/**
 * The group form - one component behind `user-groups/add` and `user-groups/{id}`, both
 * `extra` routes with no menu item beside them (the route table says why they have none).
 *
 * Shaped after `UserFormPage` because it is the same kind of page: an id drawn as static
 * text, a spinner until the answer arrives, a save that goes back to the list, and a refusal
 * landed on the field it is about rather than in a toast with no field in it.
 *
 * Three rules the server owns, and none of them are this form's to invent:
 *
 * - **A base group accepts `name` and nothing else.** Its level, state and permissions are
 *   what the installation was set up with, and anything more is `409 user_group_immutable`.
 *   So those three controls are drawn - a control that vanishes teaches nothing about why
 *   it was there - and disabled, with one sentence at the top saying why.
 * - **A permission the level does not own cannot be granted.** `permissionsForRole` says
 *   which those are before anything is sent (see `src/lib/permissions.ts`), so a group of
 *   clients is offered no checkboxes at all rather than four that would all be thrown away.
 *   The set is still pruned when the level changes and filtered again at submit, because a
 *   stale tick left in the form's memory is a value the visitor never saw.
 * - **`permissions: []` clears and an absent `permissions` does not.** So this form always
 *   sends the set it holds rather than omitting it when nothing is ticked - otherwise
 *   unticking the last box would answer with a request that changed nothing while looking
 *   like it had worked.
 */
export default function UserGroupFormPage(_props: UserGroupFormPageProps) {
  const { id } = useParams()
  const isEdit = id !== undefined

  const navigate = useNavigate()
  const { t } = useTranslation()
  const { message } = App.useApp()
  const session = useSession()
  const [form] = Form.useForm<GroupFormValues>()

  const [detail, setDetail] = useState<UserGroupDetail | null>(null)
  // True before the first render in edit mode: the form must not draw its fields against a
  // group that has not arrived, and `renderToStaticMarkup` never runs the effect that would
  // clear this - which is what makes the spinner below the state every probe sees.
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
  const listPath = `${area}/user-groups`

  useEffect(() => {
    // Add mode has nothing to fetch, and this guard is what keeps the static render of *that*
    // address from ever writing into a form that is already complete.
    if (!isEdit || !id) return

    let cancelled = false

    setLoading(true)

    getUserGroup(id)
      .then((answer) => {
        if (cancelled) return
        setDetail(answer)
        setLoadError(null)
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
  }, [id, isEdit, form, attempt])

  /**
   * The level currently chosen, and everything that level is entitled to grant.
   *
   * The watch has a floor rather than a nullable answer: `Form.useWatch` is `undefined`
   * until its own effect runs (and always under a static render), and `permissionsForRole`
   * of an undefined level would be a call nothing documents. The floor is the same value
   * `initialValues` starts the add form at, so the two never disagree about what an empty
   * form means.
   */
  const watchedRole: Role = Form.useWatch('role', form) ?? CREATE_VALUES.role
  const offered = useMemo(() => permissionsForRole(watchedRole), [watchedRole])
  const offeredSet = useMemo(() => new Set(offered), [offered])

  /**
   * Drops the grants the chosen level is not entitled to, the moment it changes.
   *
   * Without this the value would quietly outlive the choice: antd's checkbox group only
   * *draws* the options it was given, so moving a group from `Admin` to `Client` would
   * untick every box on screen while the form still held all four - and moving it back
   * would bring the ticks back as if somebody had put them there. The prune watches the
   * level rather than the click so it also covers a level painted from the server, and
   * `handleFinish` filters once more on the way out for the case this effect cannot see.
   */
  useEffect(() => {
    const current = form.getFieldValue('permissions') as UserPermission[] | undefined
    if (!current) return

    const kept = current.filter((permission) => offeredSet.has(permission))
    if (kept.length !== current.length) form.setFieldValue('permissions', kept)
  }, [form, offeredSet])

  const roleOptions = useMemo(
    () => ROLES.map((role) => ({ value: role, label: t(`profile.roles.${role}`) })),
    [t],
  )

  const statusOptions = useMemo(
    () => STATUSES.map((status) => ({ value: status, label: t(`users.statuses.${status}`) })),
    [t],
  )

  const permissionOptions = useMemo(
    () => offered.map((permission) => ({ value: permission, label: t(`permissions.${permission}`) })),
    [offered, t],
  )

  /**
   * The ticked set, watched so the two bulk buttons below can say whether they have
   * anything to do.
   *
   * The same floor as the level above: `undefined` until antd's own effect runs (and always
   * under a static render), so the answer is an empty list rather than a nullable one.
   */
  const watchedPermissions: UserPermission[] = Form.useWatch('permissions', form) ?? []

  /**
   * Whether every permission this level may grant is already ticked, and whether any is.
   *
   * Both are counted against `offered` rather than against the catalog: the buttons act on
   * the boxes on screen, and a group of clients has no boxes however many permissions the
   * installation holds. The `offered.length > 0 &&` guard is the load-bearing half - `every`
   * over an empty list is `true`, so without it a level that grants nothing would report
   * "already all selected" and grey out the button meant to select them.
   */
  const allOfferedTicked =
    offered.length > 0 && offered.every((permission) => watchedPermissions.includes(permission))
  const someTicked = watchedPermissions.length > 0

  /**
   * The two bulk moves, each writing the whole set in one call.
   *
   * `selectAll` writes `offered` and not the whole catalog, because a permission this level
   * does not own would be dropped by the prune above on the very next render - and a button
   * whose effect is partly undone a frame later is a button that lies about what it did.
   * `clearAll` writes `[]` rather than filtering the watched list, because "none" has no
   * exceptions and the shorter write is the one that cannot disagree with itself.
   */
  const selectAllPermissions = () => form.setFieldsValue({ permissions: [...offered] })
  const clearPermissions = () => form.setFieldsValue({ permissions: [] })

  const handleFinish = async (values: GroupFormValues) => {
    setSubmitting(true)
    form.setFields(FIELD_NAMES.map((name) => ({ name, errors: [] })))

    try {
      // Minted here rather than remembered - the server binds a token to the identity that
      // was current when it was minted, so a token from before a refresh would come back
      // `csrf_invalid`. Every submit in this application mints at call time for that reason.
      const csrfToken = await getCsrfToken()
      const name = values.name.trim()
      const permissions = (values.permissions ?? []).filter((permission) =>
        offeredSet.has(permission),
      )

      const result =
        isEdit && detail
          ? await updateUserGroup({
              id: detail.id,
              csrfToken,
              // A base group is renamed and nothing else: sending the other three would be
              // sending a request the server answers `409 user_group_immutable` for, and the
              // controls are disabled above precisely so this body says only what is true.
              patch: detail.isBase
                ? { name }
                : { name, role: values.role, status: values.status, permissions },
            })
          : await createUserGroup({
              values: { name, role: values.role, status: values.status, permissions },
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
            name: field.name as keyof GroupFormValues,
            errors: field.errors,
          })),
        )
      message.error(messageForError(error))
    } finally {
      setSubmitting(false)
    }
  }

  const cardTitle = isEdit ? t('userGroups.editTitle') : t('userGroups.addTitle')

  if (loadError) {
    // Edit mode only: nothing fetches on add, so this state cannot be reached there. The
    // title stays because the address is still an edit address - the group is simply not on
    // this server anymore (or never was, for a hand-typed URL).
    return (
      <Card title={cardTitle}>
        <Alert
          type="error"
          showIcon
          message={loadError}
          action={
            <Space>
              {/* Re-ask rather than only offering a way out: the refusal may be a dropped
                  connection as easily as a missing group, and a missing group reads the same
                  either way. */}
              <Button
                size="small"
                loading={loading}
                onClick={() => setAttempt((value) => value + 1)}
              >
                {t('actions.retry')}
              </Button>
              <Button size="small" onClick={() => navigate(listPath)}>
                {t('userGroups.backToList')}
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
        <Form<GroupFormValues>
          form={form}
          layout="vertical"
          initialValues={CREATE_VALUES}
          onFinish={handleFinish}
          style={{ maxWidth: 640 }}
        >
          {detail?.isBase && (
            // The one sentence the three disabled controls need between them, rather than
            // three identical tooltips: it is a fact about the row and not about any one
            // field, and it says which of the four are still editable instead of leaving the
            // visitor to work it out by clicking.
            <Alert
              type="info"
              showIcon
              style={{ marginBottom: 16 }}
              message={t('userGroups.baseLocked')}
            />
          )}

          {detail && (
            <Form.Item label={t('userGroups.idLabel')}>
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
            name="name"
            label={t('userGroups.columns.name')}
            rules={[{ required: true, message: t('userGroups.nameRequired') }]}
          >
            {/* The one field a base group may still change, so it is never disabled - the
                Alert above says as much. */}
            <Input maxLength={100} autoComplete="off" />
          </Form.Item>

          <Form.Item name="role" label={t('userGroups.columns.role')}>
            {/* Always one of the three, like every other select here: there is no "no level"
                state for a group to be in, so a `required` rule would be catching nothing.
                What the level decides is stated under the permissions below, where it
                matters. */}
            <Select options={roleOptions} disabled={detail?.isBase} />
          </Form.Item>

          <Form.Item name="status" label={t('userGroups.columns.status')}>
            {/* The group's own state, and the one that reaches every member: a blocked group
                blocks the accounts in it whatever their own rows say, which is the rule the
                account form draws as its effective-status note. Here the note would repeat
                the field beside it, so the field says it on its own. */}
            <Select options={statusOptions} disabled={detail?.isBase} />
          </Form.Item>

          {offered.length > 0 ? (
            <Form.Item
              name="permissions"
              label={t('userGroups.permissionsTitle')}
              extra={
                <Space direction="vertical" size={4}>
                  <span>{t('userGroups.permissionsHint')}</span>
                  <Space>
                    {/* The two bulk moves sit with the hint rather than in a row of their
                        own, so the section reads as one thing: the boxes, what they mean,
                        and the two ways to settle them all at once. Four permissions is
                        four clicks and nobody minds; forty is forty, and a page that made
                        an administrator tick them one by one would be a page that gets
                        half of them right. */}
                    <Button
                      size="small"
                      disabled={detail?.isBase || allOfferedTicked}
                      onClick={selectAllPermissions}
                    >
                      {t('userGroups.permissionsSelectAll')}
                    </Button>
                    <Button
                      size="small"
                      disabled={detail?.isBase || !someTicked}
                      onClick={clearPermissions}
                    >
                      {t('userGroups.permissionsClear')}
                    </Button>
                  </Space>
                </Space>
              }
            >
              <Checkbox.Group disabled={detail?.isBase} options={permissionOptions} />
            </Form.Item>
          ) : (
            <Form.Item label={t('userGroups.permissionsTitle')}>
              {/* No `name` and no control: this level has nothing to grant, so there is no
                  value to collect and no checkbox for one to be unticked from. The note
                  answers the question the empty space would otherwise raise - "where did the
                  checkboxes go" - rather than leaving the section blank. */}
              <Typography.Text type="secondary">{t('userGroups.permissionsNone')}</Typography.Text>
            </Form.Item>
          )}

          {detail && (
            <Form.Item label={t('userGroups.membersLabel')}>
              {/* Also layout only. It is here rather than inside the delete dialog because
                  the dialog would have to fetch it and this page already has - and because
                  the number is what tells an administrator whether the `409
                  user_group_in_use` a deletion would meet is still a theoretical one. */}
              {t('userGroups.members', { count: detail.members })}
            </Form.Item>
          )}

          <Form.Item style={{ marginTop: 24 }}>
            <Space>
              <Button type="primary" htmlType="submit" loading={submitting}>
                {isEdit ? t('actions.save') : t('userGroups.create')}
              </Button>
              <Button disabled={submitting} onClick={() => navigate(listPath)}>
                {t('userGroups.backToList')}
              </Button>
            </Space>
          </Form.Item>
        </Form>
      )}
    </Card>
  )
}
