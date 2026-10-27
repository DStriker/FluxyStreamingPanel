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
  Typography,
} from 'antd'
import { useTranslation } from 'react-i18next'
import { CONTENT_TYPE_PARAMS } from '../../features/content-categories/api'
import type { VideoStreamValues } from '../../features/video-streams/api'
import { ApiError, fieldErrors, messageForError, successTextFor } from '../../utils/http'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'
import SourceListEditor from '../../components/SourceListEditor'
import { useContentCategories } from '../../features/content-categories/hooks/queries'
import { useAllEpgSources } from '../../features/epg/hooks/queries'
import { useBouquets } from '../../features/bouquets/hooks/queries'
import {
  useCreateVideoStream,
  useUpdateVideoStream,
} from '../../features/video-streams/hooks/mutations'
import { useVideoStreamDetail } from '../../features/video-streams/hooks/queries'
import {
  acceptsArchive,
  acceptsDelay,
  acceptsRestream,
} from '../../features/video-streams/capabilities'
import { useUnsavedChangesWarning } from '../../hooks/useUnsavedChangesWarning'
import type { VideoStreamStatus, VideoStreamType } from '../../features/video-streams/types'

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

/** The two states, in the order the picker offers them: active first, because that is the default. */
const STATUSES: VideoStreamStatus[] = ['Running', 'Stopped']

/**
 * The add form's starting content, and the shape the edit form is painted over once its
 * stream arrives.
 *
 * `Permanent` rather than the least surprising kind: it is the kind that has a name, an
 * address list and an archive - the three things this form is built to collect - so a
 * visitor who opens it and types gets a working stream without first discovering that the
 * kind picker decides which fields exist.
 *
 * `Running` rather than the server's own create default of `Stopped`: a stream an operator
 * has just described is one they mean to relay, and the form is allowed to say so. The API
 * keeps `Stopped` for a caller that sends no status at all, which is a contract older than
 * this picker; the form always sends the value it holds.
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
  status: 'Running',
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
  bouquetIds: [],
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
  'status',
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
  'bouquetIds',
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
const toBody = (values: VideoStreamValues): VideoStreamValues => {
  const type = values.type ?? CREATE_VALUES.type
  const restream = acceptsRestream(type)

  return {
    name: values.name ?? '',
    type,
    status: values.status ?? CREATE_VALUES.status,
    categoryId: values.categoryId ?? '',
    logoUrl: values.logoUrl ?? '',
    enigma2Sid: values.enigma2Sid ?? '',
    // The three settings a kind may not carry are neutralized rather than sent as whatever
    // the form still holds. The fields are greyed out for those kinds, so a value here
    // would be one nothing on screen claims - and the archive refusal would turn it into a
    // 400 the form itself built. Delay and restreaming are the form's own rule today (the
    // server accepts them on every type); keeping all three normalized here means the body
    // always describes what the enabled controls show.
    delayMinutes: acceptsDelay(type) ? (values.delayMinutes ?? 0) : 0,
    seriesId: values.seriesId ?? '',
    vodId: values.vodId ?? '',
    epgSourceId: values.epgSourceId ?? '',
    epgChannelKey: values.epgChannelKey ?? '',
    archiveDays: acceptsArchive(type) ? (values.archiveDays ?? 0) : 0,
    restreamUserAgent: restream ? (values.restreamUserAgent ?? '') : '',
    restreamProxy: restream ? (values.restreamProxy ?? '') : '',
    restreamCookies: restream ? (values.restreamCookies ?? '') : '',
    restreamHeaders: restream ? (values.restreamHeaders ?? '') : '',
    sources: cleanList(values.sources),
    files: cleanList(values.files),
    // Always sent, and an empty array is the statement "in no bouquet" - the set is what the
    // picker holds, and the form never means "leave this field alone".
    bouquetIds: values.bouquetIds ?? [],
  }
}

/** The stored stream spelled as form content, drawn over `CREATE_VALUES` when the answer arrives. */
const detailToFormValues = (detail: {
  name: string
  type: VideoStreamType
  status: VideoStreamStatus
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
  bouquetIds: string[]
}): VideoStreamValues => {
  const restream = acceptsRestream(detail.type)

  return {
    name: detail.name,
    type: detail.type,
    status: detail.status,
    categoryId: detail.categoryId,
    logoUrl: detail.logoUrl ?? '',
    enigma2Sid: detail.enigma2Sid ?? '',
    // Values a kind cannot carry are not painted either, for the same reason `toBody`
    // neutralizes them: a disabled field showing last week's delay would describe a setting
    // the next save is about to clear, and the form would be disagreeing with itself.
    delayMinutes: acceptsDelay(detail.type) ? detail.delayMinutes : 0,
    seriesId: detail.seriesId ?? '',
    vodId: detail.vodId ?? '',
    epgSourceId: detail.epgSourceId ?? '',
    epgChannelKey: detail.epgChannelKey ?? '',
    archiveDays: acceptsArchive(detail.type) ? detail.archiveDays : 0,
    restreamUserAgent: restream ? (detail.restreamUserAgent ?? '') : '',
    restreamProxy: restream ? (detail.restreamProxy ?? '') : '',
    restreamCookies: restream ? (detail.restreamCookies ?? '') : '',
    restreamHeaders: restream ? (detail.restreamHeaders ?? '') : '',
    // The lists arrive in relay order and are sent back in that same order, which is the
    // contract on both sides. An empty list still becomes one blank row for the editor: a
    // stored stream always has at least one entry of the list its kind uses, so this only
    // ever happens to the list its kind *does not* use - and then it is the list nobody
    // draws, holding one field nobody presses anything on.
    sources: detail.sources.length > 0 ? detail.sources : [''],
    files: detail.files.length > 0 ? detail.files : [''],
    bouquetIds: [...detail.bouquetIds],
  }
}

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
 * The rules on the page are the server's, not this form's, with one deliberate exception:
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
 * - **`status` is a field of the form.** An operator decides when adding a stream whether it
 *   should be active straight away (the add form starts at `Running`), and can enable or
 *   disable an existing one from the edit form. It is the same row fact the start and stop
 *   buttons write; the form is simply another way of stating it. `activeSource` stays a
 *   label with no `name`: it is written by no one in this interface at all, only reported by
 *   the relaying process, so nothing here can collect it - which is the difference between
 *   "shown" and "collectable", the same one the account id's own `Form.Item` rests on.
 *
 * One rule is the form's rather than the server's today: **a delay belongs to `Permanent`
 * and the HTTP restreaming settings to `Permanent` and `OnDemand`**. The endpoint accepts
 * those values on every kind, so the greyed fields plus the neutralized body keep this
 * form from building a configuration the product does not offer; another client could
 * still send one. The conditions live in `capabilities.ts` so the disabled state, the
 * reason and the body all read the same fact.
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

  /**
   * The bouquets the picker below the category offers.
   *
   * A separate query from the categories', for the reason the group filter on the accounts
   * page is separate: the two are behind different permissions (`viewStreams` to read this
   * form, `viewBouquets` to read the packages), and sharing one call would fail the whole
   * form for an operator allowed to edit streams but not to see the bouquets. A failed read
   * therefore leaves the picker empty and disables it, and everything else still saves - a
   * stream in no bouquet is a valid row.
   */
  const bouquetsQuery = useBouquets()
  const bouquetOptions = (bouquetsQuery.data?.items ?? []).map((bouquet) => ({
    value: bouquet.id,
    label: bouquet.name,
  }))
  const bouquetsError = bouquetsQuery.isError ? messageForError(bouquetsQuery.error) : null

  /**
   * The guide sources the picker offers. A source is a row of this installation now, so the
   * field chooses one instead of accepting a uuid nobody can look up - the server refuses a
   * source that does not exist, and a list of names is how an operator picks one it will
   * accept. A failed read leaves the picker empty and says why; the rest of the form still
   * saves, and a guide is not a required field.
   */
  const epgSourcesQuery = useAllEpgSources()
  const epgSourceOptions = (epgSourcesQuery.data?.items ?? []).map((source) => ({
    value: source.id,
    label: source.name,
  }))
  const epgSourcesError = epgSourcesQuery.isError
    ? messageForError(epgSourcesQuery.error)
    : null

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

  // The three conditions the fields below share. Read once here rather than rebuilt at
  // every Form.Item, so the greyed state and the sentence beside it cannot disagree.
  const delayAllowed = acceptsDelay(chosenType)
  const archiveAllowed = acceptsArchive(chosenType)
  const restreamAllowed = acceptsRestream(chosenType)

  /**
   * The *raw* watch, for the effect that neutralizes the settings a newly chosen kind cannot
   * carry.
   *
   * Kept apart from `chosenType` above on purpose: that one has to be a value so the
   * payload section can be drawn, and this one has to be able to be `undefined` so the
   * effect does nothing until the visitor has actually chosen. An effect that reacted to a
   * fallback would zero the archive of a `Permanent` stream the moment the form mounted.
   */
  const watchedType = Form.useWatch('type', form)

  useEffect(() => {
    if (!watchedType) return

    // Written here rather than left to the server's refusal, for the reason `toBody`
    // repeats: these fields are greyed out for this kind, so a value would be one nothing
    // on screen claims. Archive is the server's rule; delay and restreaming are the form's.
    if (!acceptsArchive(watchedType)) form.setFieldValue('archiveDays', 0)
    if (!acceptsDelay(watchedType)) form.setFieldValue('delayMinutes', 0)
    if (!acceptsRestream(watchedType)) {
      form.setFieldsValue({
        restreamUserAgent: '',
        restreamProxy: '',
        restreamCookies: '',
        restreamHeaders: '',
      })
    }
  }, [watchedType, form])

  const categoryOptions = useMemo(
    () => categories.map((category) => ({ value: category.id, label: category.name })),
    [categories],
  )

  const typeOptions = useMemo(
    () => TYPES.map((type) => ({ value: type, label: t(`videoStreams.types.${type}`) })),
    [t],
  )

  const statusOptions = useMemo(
    () => STATUSES.map((status) => ({ value: status, label: t(`videoStreams.statuses.${status}`) })),
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
            name="categoryId"
            label={t('videoStreams.columns.category')}
            // Required, and there is no "no category" state for a stream to be in: the
            // position within its category, the uniqueness of its name and the whole
            // grouping of the table are read out of wherever this lands. It comes before
            // the kind because it is the broader fact: the category says where the stream
            // belongs, the kind says what it does once it is there.
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

          <Form.Item
            name="bouquetIds"
            label={t('bouquets.fieldLabel')}
            // Under the category, because the two answer the same question at two scales:
            // the category says where the stream is filed, the bouquets say in which packages
            // it is delivered. Optional - a stream in no package is a valid row - and a set
            // rather than an order, which is why the hint says where the order is arranged.
            extra={t('bouquets.fieldHint')}
          >
            <Select
              mode="multiple"
              allowClear
              showSearch
              optionFilterProp="label"
              options={bouquetOptions}
              placeholder={t('bouquets.fieldPlaceholder')}
              // A failed read leaves the picker unusable rather than empty-and-silent: an
              // operator must not think the installation has no bouquets when the truth is
              // that this group may not read them.
              disabled={bouquetsError !== null}
            />
          </Form.Item>

          {bouquetsError && (
            <Alert type="error" showIcon style={{ marginBottom: 16 }} message={bouquetsError} />
          )}

          <Form.Item
            name="status"
            label={t('videoStreams.columns.status')}
            // Always one of the two states, and the add form starts at `Running`. The value
            // travels in the same body as everything else; the start and stop buttons on the
            // table write this same fact afterwards.
            rules={[{ required: true, message: t('videoStreamsForm.statusRequired') }]}
            extra={t('videoStreamsForm.statusHint')}
          >
            <Select options={statusOptions} />
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
                  remove: t('videoStreamsForm.removeSource'),
                  add: t('videoStreamsForm.addSource'),
                  required: t('videoStreamsForm.sourcesRequired'),
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
                  remove: t('videoStreamsForm.removeFile'),
                  add: t('videoStreamsForm.addFile'),
                  required: t('videoStreamsForm.filesRequired'),
                }}
              />
            </Form.Item>
          )}

          <Divider titlePlacement="start">{t('videoStreamsForm.timingDivider')}</Divider>

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
            // The unit is in the label as well as the hint, because minutes and hours are
            // one keystroke apart in an operator's head and this field used to state
            // neither. The reason replaces the hint while the field is closed: the unit is
            // still in the label, so nothing about the field goes unexplained.
            extra={
              delayAllowed
                ? t('videoStreamsForm.delayHint')
                : t('videoStreamsForm.delayPermanentOnly')
            }
          >
            <InputNumber
              min={0}
              step={1}
              precision={0}
              style={{ width: 160 }}
              disabled={!delayAllowed}
            />
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
              archiveAllowed
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
              disabled={!archiveAllowed}
            />
          </Form.Item>

          <Divider titlePlacement="start">{t('videoStreamsForm.guideDivider')}</Divider>

          {/* The guide pair, refused together when only one half is present - which is what
              `dependencies` below buys: filling in either side re-checks the other, so the
              red under the untouched input clears the moment the pair becomes complete. The
              source is chosen from the ones that exist rather than typed as a uuid: the
              server refuses a source nothing answers to, and the list is the only way an
              operator can pick one it will accept. */}
          <Form.Item
            name="epgSourceId"
            dependencies={['epgChannelKey']}
            label={t('videoStreamsForm.epgSourceId')}
            rules={[optionalIdRule('epgSourceId'), epgPairRule('epgSourceId')]}
            extra={t('videoStreamsForm.epgPairHint')}
          >
            <Select
              showSearch
              allowClear
              options={epgSourceOptions}
              optionFilterProp="label"
              placeholder={t('videoStreamsForm.epgSourcePlaceholder')}
            />
          </Form.Item>

          {epgSourcesError && (
            <Alert type="error" showIcon style={{ marginBottom: 16 }} message={epgSourcesError} />
          )}

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

          {/* The four controls are greyed together, so one sentence above them says why
              rather than four copies of the same reason under each field. It replaces
              nothing: unlike the delay and archive hints, these fields never carried a
              per-field explanation. */}
          {!restreamAllowed && (
            <Typography.Text
              type="secondary"
              style={{ display: 'block', marginBottom: 16 }}
            >
              {t('videoStreamsForm.restreamPermanentOnDemandOnly')}
            </Typography.Text>
          )}

          <Form.Item
            name="restreamUserAgent"
            label={t('videoStreamsForm.userAgentLabel')}
            rules={[
              { max: RESTREAM_USER_AGENT_MAX, message: t('videoStreamsForm.maxLength', { max: RESTREAM_USER_AGENT_MAX }) },
            ]}
          >
            <Input
              placeholder={t('videoStreamsForm.userAgentPlaceholder')}
              disabled={!restreamAllowed}
            />
          </Form.Item>

          <Form.Item
            name="restreamProxy"
            label={t('videoStreamsForm.proxyLabel')}
            rules={[{ max: RESTREAM_PROXY_MAX, message: t('videoStreamsForm.maxLength', { max: RESTREAM_PROXY_MAX }) }]}
          >
            <Input
              placeholder={t('videoStreamsForm.proxyPlaceholder')}
              disabled={!restreamAllowed}
            />
          </Form.Item>

          {/* Cookies and headers are free text with no length bound on either side, because
              what they hold is a request line the relay copies verbatim - a maxlength here
              would be a number this form invented. */}
          <Form.Item name="restreamCookies" label={t('videoStreamsForm.cookiesLabel')}>
            <Input.TextArea
              rows={3}
              placeholder={t('videoStreamsForm.cookiesPlaceholder')}
              disabled={!restreamAllowed}
            />
          </Form.Item>

          <Form.Item name="restreamHeaders" label={t('videoStreamsForm.headersLabel')}>
            <Input.TextArea
              rows={3}
              placeholder={t('videoStreamsForm.headersPlaceholder')}
              disabled={!restreamAllowed}
            />
          </Form.Item>

          {/* The one fact about this row that nothing here writes. A label with no `name`,
              so nothing is collected: `activeSource` belongs to no writer in this interface
              at all. It is shown because a form that opened on a stream and said nothing
              about its current source would hide the fact most likely to matter before a
              save. `status`, which used to sit beside it as a read-only tag, is now the
              editable field above. */}
          {detail && (
            <Form.Item label={t('videoStreams.columns.activeSource')}>
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
