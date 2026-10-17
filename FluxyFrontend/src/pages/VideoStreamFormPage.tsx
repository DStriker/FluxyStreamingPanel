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
  InputNumber,
  Select,
  Space,
  Spin,
  Tag,
  Typography,
} from 'antd'
import { useTranslation } from 'react-i18next'
import { CONTENT_TYPE_PARAMS } from '../lib/api'
import type { VideoStreamValues } from '../lib/api'
import { ApiError, fieldErrors, messageForError, successTextFor } from '../lib/http'
import { areaForRole } from '../lib/session'
import { useSession } from '../lib/sessionContext'
import SourceListEditor from '../features/video-streams/SourceListEditor'
import { useContentCategories } from '../features/content-categories/queries'
import {
  useCreateVideoStream,
  useUpdateVideoStream,
} from '../features/video-streams/mutations'
import { useVideoStreamDetail } from '../features/video-streams/queries'
import { useUnsavedChangesWarning } from '../hooks/useUnsavedChangesWarning'
import type { VideoStreamType } from '../types'

/**
 * The column widths the other side of these fields already fixed, written here rather than
 * imported from the backend's configuration class: the two projects cannot see each other,
 * and a limit that only lives in a comment would be a limit the form quietly forgot the
 * moment somebody changed one of them.
 */
const NAME_MAX = 200
const LOGO_URL_MAX = 500
const ENIGMA2_SID_MAX = 200
const EPG_CHANNEL_KEY_MAX = 200
const RESTREAM_USER_AGENT_MAX = 500
const RESTREAM_PROXY_MAX = 500
const SOURCE_URL_MAX = 2000
const FILE_PATH_MAX = 1000

/**
 * The three kinds that relay from a list of addresses, named once because four places on
 * this page ask the same question: which payload to draw, which list rule applies, and
 * whether the archive length is a field at all.
 */
const URL_TYPES: VideoStreamType[] = ['Permanent', 'OnDemand', 'Redirect']

/** A uuid, checked case-insensitively because the server parses one either way. */
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

/** The six kinds, in the order the backend declares them. */
const TYPES: VideoStreamType[] = [
  'Permanent',
  'OnDemand',
  'Redirect',
  'OnlineSeries',
  'VodPlaylist',
  'FilePlaylist',
]

/**
 * The add form's starting content, and the shape the edit form is painted over once its
 * stream arrives.
 *
 * `Permanent` rather than the least surprising kind: it is the kind that has a name, an
 * address list and an archive - the three things this form is built to collect - so a
 * visitor who opens it and types gets a working stream without first discovering that the
 * kind picker decides which fields exist.
 *
 * The two lists start as **one empty field** rather than an empty array, because that is
 * what the editor draws: a list with no rows in it would be a control with nothing on it
 * to press the buttons of. Blank rows are dropped on the way out (see `toBody`), so the
 * one thing standing between that and a valid request is the list's own rule, which says
 * at least one of them has to be filled in.
 */
const CREATE_VALUES: VideoStreamValues = {
  name: '',
  type: 'Permanent',
  categoryId: '',
  logoUrl: '',
  enigma2Sid: '',
  delayMinutes: 0,
  seriesId: '',
  vodId: '',
  epgSourceId: '',
  epgChannelKey: '',
  archiveDays: 0,
  restreamUserAgent: '',
  restreamProxy: '',
  restreamCookies: '',
  restreamHeaders: '',
  sources: [''],
  files: [''],
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
const FIELD_NAMES: readonly (keyof VideoStreamValues)[] = [
  'name',
  'type',
  'categoryId',
  'logoUrl',
  'enigma2Sid',
  'delayMinutes',
  'seriesId',
  'vodId',
  'epgSourceId',
  'epgChannelKey',
  'archiveDays',
  'restreamUserAgent',
  'restreamProxy',
  'restreamCookies',
  'restreamHeaders',
  'sources',
  'files',
]

/** One list of one thing a stream relays: trims what is there and drops what nobody typed. */
const cleanList = (list: string[] | undefined): string[] =>
  (list ?? []).map((entry) => entry.trim()).filter((entry) => entry !== '')

/**
 * What the form holds, spelled as what the two endpoints take - which is the same shape on
 * both sides, which is the whole reason this is one function rather than two.
 *
 * The `??` on every field is not optimism about the form: `onFinish` hands over the whole
 * store, and a field the visitor never opened (they never switched to the series kind, so
 * `seriesId` never became a live field) simply is not in it. A body that asked the server
 * about `undefined` would be a request neither endpoint can describe, and the difference
 * between "absent" and "empty" is exactly the difference PATCH uses to mean "keep" and
 * "clear" - so absent is never allowed to reach the wire.
 */
const toBody = (values: VideoStreamValues): VideoStreamValues => ({
  name: values.name ?? '',
  type: values.type ?? CREATE_VALUES.type,
  categoryId: values.categoryId ?? '',
  logoUrl: values.logoUrl ?? '',
  enigma2Sid: values.enigma2Sid ?? '',
  delayMinutes: values.delayMinutes ?? 0,
  seriesId: values.seriesId ?? '',
  vodId: values.vodId ?? '',
  epgSourceId: values.epgSourceId ?? '',
  epgChannelKey: values.epgChannelKey ?? '',
  // Zeroed for a kind that keeps no archive. The field is greyed out while the kind is not
  // `Permanent`, so a number here would be a value nothing on screen claims - and the
  // server refuses it rather than dropping it, which is the one asymmetry between the two
  // halves of "the type owns the fields only it can use" (payloads are dropped, an archive
  // is refused). Refusing a body the form itself built would be a 400 with no mistake in
  // it, so the form never builds one.
  archiveDays: values.type === 'Permanent' ? (values.archiveDays ?? 0) : 0,
  restreamUserAgent: values.restreamUserAgent ?? '',
  restreamProxy: values.restreamProxy ?? '',
  restreamCookies: values.restreamCookies ?? '',
  restreamHeaders: values.restreamHeaders ?? '',
  sources: cleanList(values.sources),
  files: cleanList(values.files),
})

/** The stored stream spelled as form content, drawn over `CREATE_VALUES` when the answer arrives. */
const detailToFormValues = (detail: {
  name: string
  type: VideoStreamType
  categoryId: string
  logoUrl: string | null
  enigma2Sid: string | null
  delayMinutes: number
  seriesId: string | null
  vodId: string | null
  epgSourceId: string | null
  epgChannelKey: string | null
  archiveDays: number
  restreamUserAgent: string | null
  restreamProxy: string | null
  restreamCookies: string | null
  restreamHeaders: string | null
  sources: string[]
  files: string[]
}): VideoStreamValues => ({
  name: detail.name,
  type: detail.type,
  categoryId: detail.categoryId,
  logoUrl: detail.logoUrl ?? '',
  enigma2Sid: detail.enigma2Sid ?? '',
  delayMinutes: detail.delayMinutes,
  seriesId: detail.seriesId ?? '',
  vodId: detail.vodId ?? '',
  epgSourceId: detail.epgSourceId ?? '',
  epgChannelKey: detail.epgChannelKey ?? '',
  archiveDays: detail.archiveDays,
  restreamUserAgent: detail.restreamUserAgent ?? '',
  restreamProxy: detail.restreamProxy ?? '',
  restreamCookies: detail.restreamCookies ?? '',
  restreamHeaders: detail.restreamHeaders ?? '',
  // The lists arrive in relay order and are sent back in that same order, which is the
  // contract on both sides. An empty list still becomes one blank row for the editor: a
  // stored stream always has at least one entry of the list its kind uses, so this only
  // ever happens to the list its kind *does not* use - and then it is the list nobody
  // draws, holding one field nobody presses anything on.
  sources: detail.sources.length > 0 ? detail.sources : [''],
  files: detail.files.length > 0 ? detail.files : [''],
})

interface VideoStreamFormPageProps {
  /**
   * The menu key this route carries, accepted and deliberately unread - the same as on
   * `UserFormPage` and for the same reason: one component serves two addresses, so the
   * heading is chosen by whether there is an id rather than read from a key that names
   * only the first of them. The locale files say the same above `videoStreams.addTitle`.
   */
  sectionKey: string
}

/**
 * The administrator's add and edit form for a stream - one component behind two addresses.
 *
 * `video-streams/add` is a menu item and `video-streams/{id}` is not: the sidebar offers
 * the two things an administrator can decide to do (add a stream, look at the streams),
 * while the second address is only ever reached by choosing a row, and a third entry
 * saying "edit" would be a page that is empty until you tell it *which*. React-router
 * ranks the static segment above the dynamic one, so `video-streams/add` is always this
 * form in its add mode no matter which of the two lazy types is mounted over the module
 * (see `routes.tsx` for why there are two).
 *
 * The stream is fetched rather than passed, exactly like the account form: a form painted
 * from whatever the previous screen happened to hold would edit the wrong row after a
 * reload, and while that fetch is in flight the page is a spinner - never empty fields
 * against nothing, which is what a static render of this page lands in and what
 * `probe:render` asserts.
 *
 * Four rules on the page are the server's, not this form's:
 *
 * - **The kind decides which payload exists.** The three address-taking kinds draw the
 *   ordered list, a series stream draws one reference, a video-playlist stream draws its
 *   own, and the file playlist draws the ordered list of paths. The others are *cleared*
 *   rather than refused - a visitor who has just changed the kind is still carrying the
 *   previous kind's values, and refusing them would answer a request nobody meant to make.
 *   That is why `toBody` sends both lists whatever the kind is and lets the service drop
 *   what the kind cannot carry.
 * - **The archive length belongs to `Permanent` alone**, so the field is drawn greyed with
 *   the reason attached while the kind is anything else, and the number a save sends for
 *   those kinds is zero - see `toBody` for why this one is enforced here rather than left
 *   to the server's refusal.
 * - **A guide is two facts or neither.** Setting one of `epgSourceId` and `epgChannelKey`
 *   without the other is refused under *both* inputs with one sentence, because the server
 *   collects the same refusal into the same two keys: a half-configured pair would be
 *   stored as "no guide", which would tell the operator the form had not been saved.
 * - **`status` and `activeSource` are shown and never sent.** Neither is a field: the
 *   first belongs to the start and stop actions on the table, and the second is written by
 *   no one in this interface at all. They are labels with no `name`, so nothing here can
 *   collect them - which is the difference between "shown" and "collectable", the same
 *   one the account id's own `Form.Item` rests on.
 *
 * Success goes back to the list, which is where the new row is visible among the others.
 */
export default function VideoStreamFormPage(_props: VideoStreamFormPageProps) {
  const { id } = useParams()
  const isEdit = id !== undefined

  const navigate = useNavigate()
  const { t } = useTranslation()
  const { message } = App.useApp()
  const session = useSession()
  const [form] = Form.useForm<VideoStreamValues>()

  /**
   * The stream being edited, fetched through a query rather than painted from whatever the
   * previous screen happened to hold. `enabled` only when the address carries an id, so on
   * the add address nothing fetches and nothing is pending - which is also why `loading`
   * below is gated on `isEdit`: a disabled query is `pending` forever, and the add form is
   * exactly the address that must not wait for anything.
   */
  const detailQuery = useVideoStreamDetail(id)
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
  const create = useCreateVideoStream()
  const update = useUpdateVideoStream()
  const submitting = create.isPending || update.isPending

  // Whether anything on this form was edited by hand - what the unsaved-changes guard asks
  // about before letting the page be left. Only hand edits count: `onValuesChange` is not
  // reached by the `setFieldsValue` calls in code below (painting the stream, defaulting the
  // category) or by the two effects that write a value *because* a hand edit made them
  // untrue - which is what keeps a form that was merely *loaded* from asking about edits
  // nobody made.
  const [dirty, setDirty] = useState(false)
  const allowNavigation = useUnsavedChangesWarning(dirty)

  /**
   * The categories the picker draws, in a query of its own from the table's.
   *
   * The kind of content a stream is filed under is fixed here rather than chosen: a video
   * stream belongs to a video-stream category, and offering the other three kinds would be
   * a picker that can be answered with a row the server will not accept. An alert rather
   * than a failure across the whole form, for the reason the account form treats its own
   * group lookup the same way - the rest of the fields are editable without knowing which
   * categories exist, and the sentence is the server's own, so a missing permission reads
   * as one and a dead connection as one.
   */
  const categoriesQuery = useContentCategories(CONTENT_TYPE_PARAMS.VideoStream)
  const categories = categoriesQuery.data?.items ?? []
  const categoriesError = categoriesQuery.isError ? messageForError(categoriesQuery.error) : null

  // The area this page hangs from, for both buttons below. `null` only under the probes,
  // which render no provider: `RequireAuth` publishes one for every visitor this page can
  // have, and without it the navigation is inert rather than wrong.
  const area = session ? areaForRole(session.role) : ''
  const listPath = `${area}/video-streams`

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
  const chosenType: VideoStreamType =
    Form.useWatch('type', form) ?? detail?.type ?? CREATE_VALUES.type

  /**
   * The *raw* watch, for the effect that greys the archive field.
   *
   * Kept apart from `chosenType` above on purpose: that one has to be a value so the
   * payload section can be drawn, and this one has to be able to be `undefined` so the
   * effect does nothing until the visitor has actually chosen. An effect that reacted to a
   * fallback would zero the archive of a `Permanent` stream the moment the form mounted.
   */
  const watchedType = Form.useWatch('type', form)

  useEffect(() => {
    if (!watchedType || watchedType === 'Permanent') return
    if (form.getFieldValue('archiveDays') === 0) return

    // Written here rather than left to the server's refusal, for the reason `toBody`
    // repeats: the field is greyed out for this kind, so a number would be a value nothing
    // on screen claims, and a save would then be refused for a mistake the form made.
    form.setFieldValue('archiveDays', 0)
  }, [watchedType, form])

  const categoryOptions = useMemo(
    () => categories.map((category) => ({ value: category.id, label: category.name })),
    [categories],
  )

  const typeOptions = useMemo(
    () => TYPES.map((type) => ({ value: type, label: t(`videoStreams.types.${type}`) })),
    [t],
  )

  /**
   * One optional reference: blank means "none", anything else has to be a uuid, because
   * that is what the endpoint parses before it reaches the service. A separate validator
   * rather than a `pattern` rule - async-validator applies a pattern to an empty string
   * too, and "none" is the correct answer for a stream that names no series.
   */
  const optionalIdRule = (name: 'seriesId' | 'vodId' | 'epgSourceId') => ({
    validator: (_: unknown, value: string | undefined) => {
      const text = (value ?? '').trim()

      if (text === '' || UUID.test(text)) return Promise.resolve()

      // The sentence names the field it belongs to, because the two halves of the guide
      // pair below are refused together and a message that said only "not an identifier"
      // under two inputs would leave the visitor to work out which fact is missing.
      return Promise.reject(new Error(t('videoStreamsForm.idInvalid', { field: t(`videoStreamsForm.${name}`) })))
    },
  })

  /**
   * The guide pair's rule, built per side because each one reads the *other* field.
   *
   * `dependencies` on both `Form.Item`s is what makes it live: editing one re-validates
   * the other, so a visitor who fills in the channel key sees the red under the reference
   * disappear without having to touch it. A rule alone would only run when its own field
   * changed, which is the exact moment both halves already agree.
   */
  const epgPairRule = (mine: 'epgSourceId' | 'epgChannelKey') => ({
    validator: (_: unknown, value: string | undefined) => {
      const present = (text: string | undefined) => (text ?? '').trim() !== ''
      const other: 'epgSourceId' | 'epgChannelKey' =
        mine === 'epgSourceId' ? 'epgChannelKey' : 'epgSourceId'

      const mineSet = present(value)
      const otherSet = present(form.getFieldValue(other))

      return mineSet === otherSet
        ? Promise.resolve()
        : Promise.reject(new Error(t('videoStreamsForm.epgPair')))
    },
  })

  const handleFinish = async (values: VideoStreamValues) => {
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
            name: field.name as keyof VideoStreamValues,
            errors: field.errors,
          })),
        )
      message.error(messageForError(error))
    }
  }

  const cardTitle = isEdit ? t('videoStreams.editTitle') : t('videoStreams.addTitle')

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
                {t('videoStreams.backToList')}
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
        <Form<VideoStreamValues>
          form={form}
          layout="vertical"
          initialValues={CREATE_VALUES}
          onFinish={handleFinish}
          onValuesChange={() => setDirty(true)}
          style={{ maxWidth: 720 }}
        >
          {detail && (
            <Form.Item label={t('videoStreamsForm.idLabel')}>
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
            label={t('videoStreamsForm.nameLabel')}
            rules={[
              { required: true, message: t('videoStreamsForm.nameRequired') },
              { max: NAME_MAX, message: t('videoStreamsForm.maxLength', { max: NAME_MAX }) },
            ]}
          >
            <Input placeholder={t('videoStreamsForm.namePlaceholder')} />
          </Form.Item>

          <Form.Item
            name="type"
            label={t('videoStreams.columns.type')}
            // Always one of the six. The kind is not a preference - it decides which
            // payload this form collects and what the relaying process does with it - so
            // there is no state for the visitor to clear their way into.
            rules={[{ required: true, message: t('videoStreamsForm.typeRequired') }]}
            // What the choice *is* rather than what it is called: the label above says
            // "Type" and the visitor is choosing between names, and the payload below is
            // the fact they are actually deciding. Drawn live rather than from a lookup,
            // so the sentence cannot disagree with the controls it introduces.
            extra={t(`videoStreamsForm.hints.${chosenType}`)}
          >
            <Select options={typeOptions} />
          </Form.Item>

          <Form.Item
            name="categoryId"
            label={t('videoStreams.columns.category')}
            // Required, and there is no "no category" state for a stream to be in: the
            // position within its category, the uniqueness of its name and the whole
            // grouping of the table are read out of wherever this lands.
            rules={[{ required: true, message: t('videoStreamsForm.categoryRequired') }]}
          >
            <Select
              showSearch
              options={categoryOptions}
              optionFilterProp="label"
              placeholder={t('videoStreamsForm.categoryRequired')}
            />
          </Form.Item>

          {categoriesError && (
            // Under the field it blocks and nowhere else: the rest of the form still works,
            // and an alert across the top of the card would be reporting one unavailable
            // lookup as a broken page.
            <Alert type="error" showIcon style={{ marginBottom: 16 }} message={categoriesError} />
          )}

          <Divider titlePlacement="start">{t('videoStreamsForm.payloadDivider')}</Divider>

          {/* The payload the chosen kind can carry, and nothing else. The kinds that take
              an ordered list share one component, so the two lists are the same control
              twice - which is what keeps their order, their buttons and their rule from
              drifting apart. The other three draw one reference each, and every one of them
              says what it points at rather than "id". */}
          {URL_TYPES.includes(chosenType) ? (
            <Form.Item label={t('videoStreamsForm.sourcesLabel')} required>
              <SourceListEditor
                name={['sources']}
                max={SOURCE_URL_MAX}
                placeholder={t('videoStreamsForm.sourcesPlaceholder')}
                labels={{
                  up: t('videoStreamsForm.moveUp'),
                  down: t('videoStreamsForm.moveDown'),
                  remove: t('videoStreamsForm.removeRow'),
                  add: t('videoStreamsForm.listAdd'),
                }}
              />
            </Form.Item>
          ) : chosenType === 'OnlineSeries' ? (
            <Form.Item
              name="seriesId"
              label={t('videoStreamsForm.seriesId')}
              rules={[optionalIdRule('seriesId')]}
              extra={t('videoStreamsForm.idHint')}
            >
              <Input placeholder={t('videoStreamsForm.idPlaceholder')} />
            </Form.Item>
          ) : chosenType === 'VodPlaylist' ? (
            <Form.Item
              name="vodId"
              label={t('videoStreamsForm.vodId')}
              rules={[optionalIdRule('vodId')]}
              extra={t('videoStreamsForm.idHint')}
            >
              <Input placeholder={t('videoStreamsForm.idPlaceholder')} />
            </Form.Item>
          ) : (
            <Form.Item label={t('videoStreamsForm.filesLabel')} required>
              <SourceListEditor
                name={['files']}
                max={FILE_PATH_MAX}
                placeholder={t('videoStreamsForm.filesPlaceholder')}
                labels={{
                  up: t('videoStreamsForm.moveUp'),
                  down: t('videoStreamsForm.moveDown'),
                  remove: t('videoStreamsForm.removeRow'),
                  add: t('videoStreamsForm.listAdd'),
                }}
              />
            </Form.Item>
          )}

          <Divider titlePlacement="start">{t('videoStreamsForm.scheduleDivider')}</Divider>

          <Form.Item
            name="delayMinutes"
            label={t('videoStreamsForm.delayLabel')}
            // Min rather than a rule of our own: a negative delay is the one refusal the
            // server gives about this field, and the spinner under `min` says it before the
            // request rather than explaining it after.
            rules={[
              {
                validator: (_: unknown, value: number | null) =>
                  (value ?? 0) >= 0
                    ? Promise.resolve()
                    : Promise.reject(new Error(t('videoStreamsForm.nonNegative'))),
              },
            ]}
            extra={t('videoStreamsForm.delayHint')}
          >
            <InputNumber min={0} step={1} precision={0} style={{ width: 160 }} />
          </Form.Item>

          <Form.Item
            name="archiveDays"
            label={t('videoStreamsForm.archiveLabel')}
            rules={[
              {
                validator: (_: unknown, value: number | null) =>
                  (value ?? 0) >= 0
                    ? Promise.resolve()
                    : Promise.reject(new Error(t('videoStreamsForm.nonNegative'))),
              },
            ]}
            // Greyed with the reason attached rather than hidden, for the reason every
            // disabled control on this side of the application is drawn rather than removed:
            // a field that vanishes teaches nothing about why it was there, and this one is
            // the whole of the difference between a permanent stream and the other five.
            // The number itself is zeroed as well - see `toBody`.
            extra={
              chosenType === 'Permanent'
                ? t('videoStreamsForm.archiveHint')
                : t('videoStreamsForm.archivePermanentOnly')
            }
          >
            <InputNumber
              min={0}
              step={1}
              precision={0}
              style={{ width: 160 }}
              // On the input rather than on the `Form.Item`: antd 6's `FormItemProps` has no
              // `disabled` of its own, so the grey belongs to the control that actually
              // refuses a keystroke. Nothing else about the field is conditional - the
              // label, the hint and the rule are all drawn either way, which is what keeps
              // the reason readable while the field is closed.
              disabled={chosenType !== 'Permanent'}
            />
          </Form.Item>

          {/* The guide pair, refused together when only one half is present - which is what
              `dependencies` below buys: filling in either side re-checks the other, so the
              red under the untouched input clears the moment the pair becomes complete. */}
          <Form.Item
            name="epgSourceId"
            dependencies={['epgChannelKey']}
            label={t('videoStreamsForm.epgSourceId')}
            rules={[optionalIdRule('epgSourceId'), epgPairRule('epgSourceId')]}
            extra={t('videoStreamsForm.epgPairHint')}
          >
            <Input placeholder={t('videoStreamsForm.idPlaceholder')} />
          </Form.Item>

          <Form.Item
            name="epgChannelKey"
            dependencies={['epgSourceId']}
            label={t('videoStreamsForm.epgChannelKey')}
            rules={[
              { max: EPG_CHANNEL_KEY_MAX, message: t('videoStreamsForm.maxLength', { max: EPG_CHANNEL_KEY_MAX }) },
              epgPairRule('epgChannelKey'),
            ]}
          >
            <Input placeholder={t('videoStreamsForm.epgChannelKeyPlaceholder')} />
          </Form.Item>

          <Divider titlePlacement="start">{t('videoStreamsForm.identityDivider')}</Divider>

          <Form.Item
            name="logoUrl"
            label={t('videoStreamsForm.logoUrlLabel')}
            // Length only, deliberately: the server accepts any text that fits the column,
            // and a `type: 'url'` rule here would refuse addresses it happily stores -
            // an internal host, a scheme the validator has never heard of. The rule a form
            // invents on top of the server's is a rule that can only ever disagree with it.
            rules={[{ max: LOGO_URL_MAX, message: t('videoStreamsForm.maxLength', { max: LOGO_URL_MAX }) }]}
          >
            <Input placeholder={t('videoStreamsForm.urlPlaceholder')} />
          </Form.Item>

          <Form.Item
            name="enigma2Sid"
            label={t('videoStreamsForm.enigma2SidLabel')}
            rules={[{ max: ENIGMA2_SID_MAX, message: t('videoStreamsForm.maxLength', { max: ENIGMA2_SID_MAX }) }]}
          >
            <Input placeholder={t('videoStreamsForm.sidPlaceholder')} />
          </Form.Item>

          <Divider titlePlacement="start">{t('videoStreamsForm.restreamDivider')}</Divider>

          <Form.Item
            name="restreamUserAgent"
            label={t('videoStreamsForm.userAgentLabel')}
            rules={[
              { max: RESTREAM_USER_AGENT_MAX, message: t('videoStreamsForm.maxLength', { max: RESTREAM_USER_AGENT_MAX }) },
            ]}
          >
            <Input placeholder={t('videoStreamsForm.userAgentPlaceholder')} />
          </Form.Item>

          <Form.Item
            name="restreamProxy"
            label={t('videoStreamsForm.proxyLabel')}
            rules={[{ max: RESTREAM_PROXY_MAX, message: t('videoStreamsForm.maxLength', { max: RESTREAM_PROXY_MAX }) }]}
          >
            <Input placeholder={t('videoStreamsForm.proxyPlaceholder')} />
          </Form.Item>

          {/* Cookies and headers are free text with no length bound on either side, because
              what they hold is a request line the relay copies verbatim - a maxlength here
              would be a number this form invented. */}
          <Form.Item name="restreamCookies" label={t('videoStreamsForm.cookiesLabel')}>
            <Input.TextArea rows={3} placeholder={t('videoStreamsForm.cookiesPlaceholder')} />
          </Form.Item>

          <Form.Item name="restreamHeaders" label={t('videoStreamsForm.headersLabel')}>
            <Input.TextArea rows={3} placeholder={t('videoStreamsForm.headersPlaceholder')} />
          </Form.Item>

          {/* The two facts about this row that nothing here writes. Labels with no `name`,
              so nothing is collected: `status` belongs to the start and stop actions on the
              table, and `activeSource` belongs to no writer in this interface at all. Both
              are shown because a form that opened on a running stream and said nothing
              about it would be a form that hides the one fact most likely to matter before
              a save. */}
          {detail && (
            <>
              <Form.Item label={t('videoStreams.columns.status')}>
                <Tag
                  color={detail.status === 'Running' ? 'success' : 'default'}
                  style={{ marginInlineEnd: 0 }}
                >
                  {t(`videoStreams.statuses.${detail.status}`)}
                </Tag>
              </Form.Item>

              <Form.Item label={t('videoStreams.columns.activeSource')}>
                {detail.activeSource ? (
                  <Typography.Text copyable={{ text: detail.activeSource }}>
                    {detail.activeSource}
                  </Typography.Text>
                ) : (
                  <span>—</span>
                )}
              </Form.Item>
            </>
          )}

          <Form.Item style={{ marginTop: 24 }}>
            <Space>
              <Button type="primary" htmlType="submit" loading={submitting}>
                {isEdit ? t('actions.save') : t('videoStreams.create')}
              </Button>
              <Button disabled={submitting} onClick={() => navigate(listPath)}>
                {t('videoStreams.backToList')}
              </Button>
            </Space>
          </Form.Item>
        </Form>
      )}
    </Card>
  )
}
