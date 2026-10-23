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
import { CONTENT_TYPE_PARAMS } from '../../features/content-categories/api'
import type { AudioStreamValues } from '../../features/audio-streams/api'
import { ApiError, fieldErrors, messageForError, successTextFor } from '../../utils/http'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'
import SourceListEditor from '../../components/SourceListEditor'
import { useContentCategories } from '../../features/content-categories/hooks/queries'
import {
  useCreateAudioStream,
  useUpdateAudioStream,
} from '../../features/audio-streams/hooks/mutations'
import { useAudioStreamDetail } from '../../features/audio-streams/hooks/queries'
import { useUnsavedChangesWarning } from '../../hooks/useUnsavedChangesWarning'
import type { AudioStreamStatus, AudioStreamType } from '../../features/audio-streams/types'

/**
 * The column widths the other side of these fields already fixed, written here rather than
 * imported from the backend's configuration class: the two projects cannot see each other,
 * and a limit that only lives in a comment would be a limit the form quietly forgot the
 * moment somebody changed one of them.
 */
const NAME_MAX = 200
const LOGO_URL_MAX = 500
const ENIGMA2_SID_MAX = 200
const RESTREAM_USER_AGENT_MAX = 500
const RESTREAM_PROXY_MAX = 500
const SOURCE_URL_MAX = 2000

/** The two kinds, in the order the backend declares them. */
const TYPES: AudioStreamType[] = ['OnDemand', 'Redirect']

/** The two states, in the order the picker offers them: active first, because that is the default. */
const STATUSES: AudioStreamStatus[] = ['Running', 'Stopped']

/**
 * The add form's starting content, and the shape the edit form is painted over once its
 * stream arrives.
 *
 * `OnDemand` rather than the least surprising kind: it is the kind that has a name and an
 * address list - the two things this form is built to collect - so a visitor who opens it
 * and types gets a working stream without first discovering that the kind picker decides
 * how it is played.
 *
 * `Running` rather than the server's own create default of `Stopped`: a stream an operator
 * has just described is one they mean to relay, and the form is allowed to say so. The API
 * keeps `Stopped` for a caller that sends no status at all, which is a contract older than
 * this picker; the form always sends the value it holds.
 *
 * The list starts as **one empty field** rather than an empty array, because that is what
 * the editor draws: a list with no rows in it would be a control with nothing on it to press
 * the buttons of. Blank rows are dropped on the way out (see `toBody`), so the one thing
 * standing between that and a valid request is the list's own rule, which says at least one
 * of them has to be filled in.
 */
const CREATE_VALUES: AudioStreamValues = {
  name: '',
  type: 'OnDemand',
  status: 'Running',
  categoryId: '',
  logoUrl: '',
  enigma2Sid: '',
  restreamUserAgent: '',
  restreamProxy: '',
  restreamCookies: '',
  restreamHeaders: '',
  sources: [''],
}

/**
 * Every field this form draws, so a refusal that landed under one of them on the previous
 * attempt can be cleared before the next attempt is judged. Without it a server refusal of
 * `name` would sit under the input after the visitor had already fixed it, and the field
 * would look broken while passing every rule the form has for it.
 *
 * Annotated rather than `as const`, for the reason `UserFormPage` annotates the same list:
 * `Form.setFields` takes a closed union of this form's own keys, and a bare tuple of
 * strings widens to `string` on the way through `map`.
 */
const FIELD_NAMES: readonly (keyof AudioStreamValues)[] = [
  'name',
  'type',
  'status',
  'categoryId',
  'logoUrl',
  'enigma2Sid',
  'restreamUserAgent',
  'restreamProxy',
  'restreamCookies',
  'restreamHeaders',
  'sources',
]

/** One list of one thing a stream relays: trims what is there and drops what nobody typed. */
const cleanList = (list: string[] | undefined): string[] =>
  (list ?? []).map((entry) => entry.trim()).filter((entry) => entry !== '')

/**
 * What the form holds, spelled as what the two endpoints take - which is the same shape on
 * both sides, which is the whole reason this is one function rather than two.
 *
 * The `??` on every field is not optimism about the form: `onFinish` hands over the whole
 * store, and a field the visitor never opened simply is not in it. A body that asked the
 * server about `undefined` would be a request neither endpoint can describe, and the
 * difference between "absent" and "empty" is exactly the difference PATCH uses to mean
 * "keep" and "clear" - so absent is never allowed to reach the wire.
 */
const toBody = (values: AudioStreamValues): AudioStreamValues => ({
  name: values.name ?? '',
  type: values.type ?? CREATE_VALUES.type,
  status: values.status ?? CREATE_VALUES.status,
  categoryId: values.categoryId ?? '',
  logoUrl: values.logoUrl ?? '',
  enigma2Sid: values.enigma2Sid ?? '',
  restreamUserAgent: values.restreamUserAgent ?? '',
  restreamProxy: values.restreamProxy ?? '',
  restreamCookies: values.restreamCookies ?? '',
  restreamHeaders: values.restreamHeaders ?? '',
  sources: cleanList(values.sources),
})

/** The stored stream spelled as form content, drawn over `CREATE_VALUES` when the answer arrives. */
const detailToFormValues = (detail: {
  name: string
  type: AudioStreamType
  status: AudioStreamStatus
  categoryId: string
  logoUrl: string | null
  enigma2Sid: string | null
  restreamUserAgent: string | null
  restreamProxy: string | null
  restreamCookies: string | null
  restreamHeaders: string | null
  sources: string[]
}): AudioStreamValues => ({
  name: detail.name,
  type: detail.type,
  status: detail.status,
  categoryId: detail.categoryId,
  logoUrl: detail.logoUrl ?? '',
  enigma2Sid: detail.enigma2Sid ?? '',
  restreamUserAgent: detail.restreamUserAgent ?? '',
  restreamProxy: detail.restreamProxy ?? '',
  restreamCookies: detail.restreamCookies ?? '',
  restreamHeaders: detail.restreamHeaders ?? '',
  // The list arrives in relay order and is sent back in that same order, which is the
  // contract on both sides. An empty list still becomes one blank row for the editor: a
  // stored stream always has at least one source, so this only ever happens to a stream
  // that could not be saved.
  sources: detail.sources.length > 0 ? detail.sources : [''],
})

interface AudioStreamFormPageProps {
  /**
   * The menu key this route carries, accepted and deliberately unread - the same as on
   * `VideoStreamFormPage` and for the same reason: one component serves two addresses, so
   * the heading is chosen by whether there is an id rather than read from a key that names
   * only the first of them. The locale files say the same above `audioStreams.addTitle`.
   */
  sectionKey: string
}

/**
 * The administrator's add and edit form for an audio stream - one component behind two
 * addresses.
 *
 * `audio-streams/add` is a menu item and `audio-streams/{id}` is not: the sidebar offers
 * the two things an administrator can decide to do (add a stream, look at the streams),
 * while the second address is only ever reached by choosing a row, and a third entry saying
 * "edit" would be a page that is empty until you tell it *which*. React-router ranks the
 * static segment above the dynamic one, so `audio-streams/add` is always this form in its
 * add mode no matter which of the two lazy types is mounted over the module.
 *
 * The stream is fetched rather than passed, exactly like the video form: a form painted from
 * whatever the previous screen happened to hold would edit the wrong row after a reload,
 * and while that fetch is in flight the page is a spinner - never empty fields against
 * nothing, which is what a static render of this page lands in and what `probe:render`
 * asserts.
 *
 * The rules on the page are the server's:
 *
 * - **Both kinds relay from a list of addresses.** There is no kind here whose payload
 *   another kind can switch off, so the form always draws the one ordered list and a bulk
 *   type change clears nothing. That is the whole difference from a video stream, and it is
 *   why this form has no capabilities module: there is no conditional field to describe.
 * - **An empty list is refused**, not stored - a stream with no source could never play, so
 *   the editor's own rule ("at least one entry has to be filled in") is the same rule the
 *   service states.
 * - **`status` is a field of the form.** An operator decides when adding a stream whether it
 *   should be active straight away (the add form starts at `Running`), and can enable or
 *   disable an existing one from the edit form. It is the same row fact the start and stop
 *   buttons write; the form is simply another way of stating it. `activeSource` stays a
 *   label with no `name`: it is written by no one in this interface at all, only reported by
 *   the relaying process, so nothing here can collect it - which is the difference between
 *   "shown" and "collectable".
 *
 * Success goes back to the list, which is where the new row is visible among the others.
 */
export default function AudioStreamFormPage(_props: AudioStreamFormPageProps) {
  const { id } = useParams()
  const isEdit = id !== undefined

  const navigate = useNavigate()
  const { t } = useTranslation()
  const { message } = App.useApp()
  const session = useSession()
  const [form] = Form.useForm<AudioStreamValues>()

  /**
   * The stream being edited, fetched through a query rather than painted from whatever the
   * previous screen happened to hold. `enabled` only when the address carries an id, so on
   * the add address nothing fetches and nothing is pending - which is also why `loading`
   * below is gated on `isEdit`: a disabled query is `pending` forever, and the add form is
   * exactly the address that must not wait for anything.
   */
  const detailQuery = useAudioStreamDetail(id)
  const detail = detailQuery.data ?? null
  const loading = isEdit && (detailQuery.isPending || detailQuery.isFetching)
  const loadError = isEdit && detailQuery.isError ? messageForError(detailQuery.error) : null
  /**
   * The two writes this form makes, from `mutations.ts` - one hook per endpoint, each
   * minting its antiforgery token at call time, never remembered: the server binds a token
   * to the identity that was current when it was minted, and one from before a refresh is
   * refused as `csrf_invalid`.
   *
   * The submit button spins on whichever half is running, and which half that is is not
   * known until submit time - the same address adds and edits.
   */
  const create = useCreateAudioStream()
  const update = useUpdateAudioStream()
  const submitting = create.isPending || update.isPending

  // Whether anything on this form was edited by hand - what the unsaved-changes guard asks
  // about before letting the page be left. Only hand edits count: `onValuesChange` is not
  // reached by the `setFieldsValue` calls in code below (painting the stream, defaulting the
  // category) or by the effect that writes a value *because* a hand edit made it untrue -
  // which is what keeps a form that was merely *loaded* from asking about edits nobody
  // made.
  const [dirty, setDirty] = useState(false)
  const allowNavigation = useUnsavedChangesWarning(dirty)

  /**
   * The categories the picker draws, in a query of its own from the table's.
   *
   * The kind of content a stream is filed under is fixed here rather than chosen: an audio
   * stream belongs to an audio-stream category, and offering the other three kinds would be
   * a picker that can be answered with a row the server will not accept. An alert rather
   * than a failure across the whole form, for the reason the account form treats its own
   * group lookup the same way - the rest of the fields are editable without knowing which
   * categories exist, and the sentence is the server's own, so a missing permission reads
   * as one and a dead connection as one.
   */
  const categoriesQuery = useContentCategories(CONTENT_TYPE_PARAMS.AudioStream)
  const categories = categoriesQuery.data?.items ?? []
  const categoriesError = categoriesQuery.isError ? messageForError(categoriesQuery.error) : null

  // The area this page hangs from, for both buttons below. `null` only under the probes,
  // which render no provider: `RequireAuth` publishes one for every visitor this page can
  // have, and without it the navigation is inert rather than wrong.
  const area = session ? areaForRole(session.role) : ''
  const listPath = `${area}/audio-streams`

  useEffect(() => {
    if (!detail) return

    // Painted rather than passed as `initialValues`: the form is already mounted by the
    // time the answer lands (the spinner above is what drew in the meantime), and
    // `initialValues` only ever applies to fields nothing has set yet.
    form.setFieldsValue(detailToFormValues(detail))
  }, [detail, form])

  useEffect(() => {
    // The add form's default category, chosen from the list rather than written into this
    // file: a stream must be filed under *a* category, and the first one the operator
    // arranged is where one made by hand belongs until they say otherwise. Written only
    // when nothing is chosen yet, so a back button into a half-filled form keeps whatever
    // was already picked - and never in edit mode, where the stream's own category is what
    // the answer above painted.
    if (isEdit) return
    if (categories.length === 0) return
    if (form.getFieldValue('categoryId')) return

    const first = categories[0]
    if (first) form.setFieldsValue({ categoryId: first.id })
  }, [form, isEdit, categories])

  /**
   * The kind as it stands, with the fallbacks a form with nothing in it yet has: a static
   * render, a spinner, or a query that has not answered. `Form.useWatch` answers
   * `undefined` until its own effect runs, and a comparison against `undefined` would
   * decide a branch instead of leaving it alone - so this one always carries a value, and
   * the payload section below is drawn from it from the very first paint.
   */
  const chosenType: AudioStreamType =
    Form.useWatch('type', form) ?? detail?.type ?? CREATE_VALUES.type

  const categoryOptions = useMemo(
    () => categories.map((category) => ({ value: category.id, label: category.name })),
    [categories],
  )

  const typeOptions = useMemo(
    () => TYPES.map((type) => ({ value: type, label: t(`audioStreams.types.${type}`) })),
    [t],
  )

  const statusOptions = useMemo(
    () => STATUSES.map((status) => ({ value: status, label: t(`audioStreams.statuses.${status}`) })),
    [t],
  )

  const handleFinish = async (values: AudioStreamValues) => {
    // Every previous refusal goes before the new attempt is judged: a field the visitor has
    // already fixed would otherwise keep the red the old answer left under it, and would
    // look broken while passing every rule the form has for it.
    form.setFields(FIELD_NAMES.map((name) => ({ name, errors: [] })))

    try {
      const body = toBody(values)

      const result =
        isEdit && detail
          ? await update.mutateAsync({ id: detail.id, values: body })
          : await create.mutateAsync({ values: body })

      // The server's own sentence where this build translates the code, its English
      // `message` where it does not - the rule every other page follows.
      message.success(successTextFor(result))
      // Saved, so this is the one navigation out of here that must not be questioned: the
      // form is still marked dirty in this same event, and the guard would ask about edits
      // that have just been stored.
      allowNavigation()
      navigate(listPath)
    } catch (error) {
      const fields = fieldErrors(error instanceof ApiError ? error.errors : null)
      if (fields.length > 0)
        form.setFields(
          fields.map((field) => ({
            // The server keys refusals by the camelCase JSON name, which for this form is
            // the same set of strings the `Form.Item`s collect - so the cast bridges two
            // type systems, not two spellings.
            name: field.name as keyof AudioStreamValues,
            errors: field.errors,
          })),
        )
      message.error(messageForError(error))
    }
  }

  const cardTitle = isEdit ? t('audioStreams.editTitle') : t('audioStreams.addTitle')

  if (loadError) {
    // Edit mode only: nothing fetches on add, so this state cannot be reached there. The
    // title stays because the address is still an edit address - the stream is simply not
    // on this server anymore (or never was, for a hand-typed URL).
    return (
      <Card title={cardTitle}>
        <Alert
          type="error"
          showIcon
          message={loadError}
          action={
            <Space>
              <Button size="small" loading={loading} onClick={() => detailQuery.refetch()}>
                {t('actions.retry')}
              </Button>
              <Button size="small" onClick={() => navigate(listPath)}>
                {t('audioStreams.backToList')}
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
        <Form<AudioStreamValues>
          form={form}
          layout="vertical"
          initialValues={CREATE_VALUES}
          onFinish={handleFinish}
          onValuesChange={() => setDirty(true)}
          style={{ maxWidth: 720 }}
        >
          {detail && (
            <Form.Item label={t('audioStreamsForm.idLabel')}>
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
            label={t('audioStreamsForm.nameLabel')}
            rules={[
              { required: true, message: t('audioStreamsForm.nameRequired') },
              { max: NAME_MAX, message: t('audioStreamsForm.maxLength', { max: NAME_MAX }) },
            ]}
          >
            <Input placeholder={t('audioStreamsForm.namePlaceholder')} />
          </Form.Item>

          <Form.Item
            name="categoryId"
            label={t('audioStreams.columns.category')}
            // Required, and there is no "no category" state for a stream to be in: the
            // uniqueness of its name and the whole grouping of the table are read out of
            // wherever this lands. It comes before the kind because it is the broader fact:
            // the category says where the stream belongs, the kind says what it does once
            // it is there.
            rules={[{ required: true, message: t('audioStreamsForm.categoryRequired') }]}
          >
            <Select
              showSearch
              options={categoryOptions}
              optionFilterProp="label"
              placeholder={t('audioStreamsForm.categoryRequired')}
            />
          </Form.Item>

          {categoriesError && (
            // Under the field it blocks and nowhere else: the rest of the form still works,
            // and an alert across the top of the card would be reporting one unavailable
            // lookup as a broken page.
            <Alert type="error" showIcon style={{ marginBottom: 16 }} message={categoriesError} />
          )}

          <Form.Item
            name="status"
            label={t('audioStreams.columns.status')}
            // Always one of the two states, and the add form starts at `Running`. The value
            // travels in the same body as everything else; the start and stop buttons on the
            // table write this same fact afterwards.
            rules={[{ required: true, message: t('audioStreamsForm.statusRequired') }]}
            extra={t('audioStreamsForm.statusHint')}
          >
            <Select options={statusOptions} />
          </Form.Item>

          <Form.Item
            name="type"
            label={t('audioStreams.columns.type')}
            // Always one of the two. The kind is not a preference - it decides how the
            // relaying process plays the stream - so there is no state for the visitor to
            // clear their way into.
            rules={[{ required: true, message: t('audioStreamsForm.typeRequired') }]}
            // What the choice *is* rather than what it is called: the label above says
            // "Type" and the visitor is choosing between names, and the payload below is
            // the fact they are actually deciding. Drawn live rather than from a lookup,
            // so the sentence cannot disagree with the controls it introduces.
            extra={t(`audioStreamsForm.hints.${chosenType}`)}
          >
            <Select options={typeOptions} />
          </Form.Item>

          <Divider titlePlacement="start">{t('audioStreamsForm.payloadDivider')}</Divider>

          {/* The one list both kinds carry. There is no branch here: an audio stream has no
              kind whose payload another kind can switch off, so the editor is drawn once and
              the rule under it is the server's own - a stream with no source could never
              play, so at least one of them has to be filled in. */}
          <Form.Item label={t('audioStreamsForm.sourcesLabel')} required>
            <SourceListEditor
              name={['sources']}
              max={SOURCE_URL_MAX}
              placeholder={t('audioStreamsForm.sourcesPlaceholder')}
              labels={{
                up: t('audioStreamsForm.moveUp'),
                down: t('audioStreamsForm.moveDown'),
                remove: t('audioStreamsForm.removeSource'),
                add: t('audioStreamsForm.addSource'),
                required: t('audioStreamsForm.sourcesRequired'),
              }}
            />
          </Form.Item>

          <Divider titlePlacement="start">{t('audioStreamsForm.identityDivider')}</Divider>

          <Form.Item
            name="logoUrl"
            label={t('audioStreamsForm.logoUrlLabel')}
            // Length only, deliberately: the server accepts any text that fits the column,
            // and a `type: 'url'` rule here would refuse addresses it happily stores -
            // an internal host, a scheme the validator has never heard of. The rule a form
            // invents on top of the server's is a rule that can only ever disagree with it.
            rules={[{ max: LOGO_URL_MAX, message: t('audioStreamsForm.maxLength', { max: LOGO_URL_MAX }) }]}
          >
            <Input placeholder={t('audioStreamsForm.urlPlaceholder')} />
          </Form.Item>

          <Form.Item
            name="enigma2Sid"
            label={t('audioStreamsForm.enigma2SidLabel')}
            rules={[{ max: ENIGMA2_SID_MAX, message: t('audioStreamsForm.maxLength', { max: ENIGMA2_SID_MAX }) }]}
          >
            <Input placeholder={t('audioStreamsForm.sidPlaceholder')} />
          </Form.Item>

          <Divider titlePlacement="start">{t('audioStreamsForm.restreamDivider')}</Divider>

          {/* The four settings every audio kind carries, so there is no greyed control and
              no sentence about a kind that cannot hold them - the difference from the video
              form, where the same four had to be switched off for some kinds. */}
          <Form.Item
            name="restreamUserAgent"
            label={t('audioStreamsForm.userAgentLabel')}
            rules={[
              { max: RESTREAM_USER_AGENT_MAX, message: t('audioStreamsForm.maxLength', { max: RESTREAM_USER_AGENT_MAX }) },
            ]}
          >
            <Input placeholder={t('audioStreamsForm.userAgentPlaceholder')} />
          </Form.Item>

          <Form.Item
            name="restreamProxy"
            label={t('audioStreamsForm.proxyLabel')}
            rules={[
              { max: RESTREAM_PROXY_MAX, message: t('audioStreamsForm.maxLength', { max: RESTREAM_PROXY_MAX }) },
            ]}
          >
            <Input placeholder={t('audioStreamsForm.proxyPlaceholder')} />
          </Form.Item>

          {/* Cookies and headers are free text with no length bound on either side, because
              what they hold is a request line the relay copies verbatim - a maxlength here
              would be a number this form invented. */}
          <Form.Item name="restreamCookies" label={t('audioStreamsForm.cookiesLabel')}>
            <Input.TextArea
              rows={3}
              placeholder={t('audioStreamsForm.cookiesPlaceholder')}
            />
          </Form.Item>

          <Form.Item name="restreamHeaders" label={t('audioStreamsForm.headersLabel')}>
            <Input.TextArea
              rows={3}
              placeholder={t('audioStreamsForm.headersPlaceholder')}
            />
          </Form.Item>

          {/* The one fact about this row that nothing here writes. A label with no `name`,
              so nothing is collected: `activeSource` belongs to no writer in this interface
              at all. It is shown because a form that opened on a stream and said nothing
              about its current source would hide the fact most likely to matter before a
              save. `status`, which used to sit beside it as a read-only tag, is now the
              editable field above. */}
          {detail && (
            <Form.Item label={t('audioStreams.columns.activeSource')}>
              {detail.activeSource ? (
                <Typography.Text copyable={{ text: detail.activeSource }}>
                  {detail.activeSource}
                </Typography.Text>
              ) : (
                <span>—</span>
              )}
            </Form.Item>
          )}

          <Form.Item style={{ marginTop: 24 }}>
            <Space>
              <Button type="primary" htmlType="submit" loading={submitting}>
                {isEdit ? t('actions.save') : t('audioStreams.create')}
              </Button>
              <Button disabled={submitting} onClick={() => navigate(listPath)}>
                {t('audioStreams.backToList')}
              </Button>
            </Space>
          </Form.Item>
        </Form>
      )}
    </Card>
  )
}
