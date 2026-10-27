import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import {
  Alert,
  App,
  Button,
  Card,
  DatePicker,
  Divider,
  Form,
  Input,
  InputNumber,
  Select,
  Space,
  Spin,
  Typography,
} from 'antd'
import dayjs from 'dayjs'
import type { Dayjs } from 'dayjs'
import { useTranslation } from 'react-i18next'
import { CONTENT_TYPE_PARAMS } from '../../features/content-categories/api'
import { useContentCategories } from '../../features/content-categories/hooks/queries'
import { useBouquets } from '../../features/bouquets/hooks/queries'
import type { VideoOnDemandValues } from '../../features/video-on-demand/api'
import {
  useCreateVideoOnDemand,
  useUpdateVideoOnDemand,
} from '../../features/video-on-demand/hooks/mutations'
import { useVideoOnDemandDetail } from '../../features/video-on-demand/hooks/queries'
import type {
  VideoOnDemandStatus,
  VideoOnDemandType,
} from '../../features/video-on-demand/types'
import { useUnsavedChangesWarning } from '../../hooks/useUnsavedChangesWarning'
import { countryOptions } from '../../utils/countries'
import { ApiError, fieldErrors, messageForError, successTextFor } from '../../utils/http'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'

/** The column widths the other side of these fields already fixed, written here by hand. */
const NAME_MAX = 200
const SOURCE_MAX = 2048
const URL_MAX = 2048
const IMDB_MAX = 32
const PLOT_MAX = 4000
const DIRECTOR_MAX = 200
const CAST_MAX = 20
const GENRES_MAX = 10

/** The two states, active first: the form's own default is the safe one below. */
const STATUSES: VideoOnDemandStatus[] = ['Active', 'Inactive']

/** The two delivery kinds, in the order the backend declares them. */
const TYPES: VideoOnDemandType[] = ['Relay', 'Redirect']

/**
 * The add form's starting content.
 *
 * `Inactive` rather than `Active`: a new video is not published until its operator says so,
 * and the server's own create default is the same word. `Relay` is the kind that lets this
 * installation serve the file, which is what a local path needs.
 */
const CREATE_VALUES: VideoOnDemandValues = {
  name: '',
  type: 'Relay',
  status: 'Inactive',
  categoryId: '',
  source: '',
  subtitleUrl: '',
  imdbId: '',
  posterUrl: '',
  backdropUrl: '',
  plot: '',
  cast: [],
  director: '',
  genres: [],
  releaseDate: '',
  rating: null,
  countryCode: '',
  bouquetIds: [],
}

/** Every field this form draws, so a refusal can be cleared before the next attempt. */
const FIELD_NAMES: readonly (keyof VideoOnDemandValues)[] = [
  'name',
  'type',
  'status',
  'categoryId',
  'source',
  'subtitleUrl',
  'imdbId',
  'posterUrl',
  'backdropUrl',
  'plot',
  'cast',
  'director',
  'genres',
  'releaseDate',
  'rating',
  'countryCode',
  'bouquetIds',
]

/** Trims what is there, drops what nobody typed, and keeps the order. */
const cleanList = (list: string[] | undefined): string[] => {
  const result: string[] = []

  for (const entry of list ?? []) {
    const trimmed = entry.trim()

    if (trimmed !== '' && !result.includes(trimmed)) result.push(trimmed)
  }

  return result
}

/**
 * What the form holds, spelled as the two endpoints take it.
 *
 * The `??` on every field is the same guard the stream form uses: `onFinish` hands over the
 * whole store, and a field the visitor never opened is simply not in it. An empty string is
 * how this form says "none" to both writes; the update call turns the rating into text, for
 * the reason `api.ts` gives.
 */
const toBody = (values: VideoOnDemandValues): VideoOnDemandValues => ({
  name: (values.name ?? '').trim(),
  type: values.type ?? CREATE_VALUES.type,
  status: values.status ?? CREATE_VALUES.status,
  categoryId: values.categoryId ?? '',
  source: (values.source ?? '').trim(),
  subtitleUrl: (values.subtitleUrl ?? '').trim(),
  imdbId: (values.imdbId ?? '').trim(),
  posterUrl: (values.posterUrl ?? '').trim(),
  backdropUrl: (values.backdropUrl ?? '').trim(),
  plot: (values.plot ?? '').trim(),
  cast: cleanList(values.cast),
  director: (values.director ?? '').trim(),
  genres: cleanList(values.genres),
  releaseDate: (values.releaseDate ?? '').trim(),
  rating: values.rating ?? null,
  countryCode: (values.countryCode ?? '').trim().toUpperCase(),
  bouquetIds: values.bouquetIds ?? [],
})

/** The stored video spelled as form content. */
const detailToFormValues = (detail: {
  name: string
  type: VideoOnDemandType
  status: VideoOnDemandStatus
  categoryId: string
  source: string
  subtitleUrl: string | null
  imdbId: string | null
  posterUrl: string | null
  backdropUrl: string | null
  plot: string | null
  cast: string[]
  director: string | null
  genres: string[]
  releaseDate: string | null
  rating: number | null
  countryCode: string | null
  bouquetIds: string[]
}): VideoOnDemandValues => ({
  name: detail.name,
  type: detail.type,
  status: detail.status,
  categoryId: detail.categoryId,
  source: detail.source,
  subtitleUrl: detail.subtitleUrl ?? '',
  imdbId: detail.imdbId ?? '',
  posterUrl: detail.posterUrl ?? '',
  backdropUrl: detail.backdropUrl ?? '',
  plot: detail.plot ?? '',
  cast: detail.cast,
  director: detail.director ?? '',
  genres: detail.genres,
  releaseDate: detail.releaseDate ?? '',
  rating: detail.rating,
  countryCode: detail.countryCode ?? '',
  bouquetIds: [...detail.bouquetIds],
})

interface VideoOnDemandFormPageProps {
  /** The menu key this route carries, accepted and deliberately unread. */
  sectionKey: string
}

/**
 * The administrator's add and edit form for one on-demand video - one component behind two
 * addresses.
 *
 * The form is fetched rather than passed, for the reason the stream form gives: a form
 * painted from whatever the previous screen happened to hold would edit the wrong row after
 * a reload. While the fetch is in flight the page is a spinner, which is the state
 * `probe:render` asserts.
 *
 * The rules on this page are the server's. The two things this form enforces ahead of the
 * request are the shapes it can check without asking: a release date written as
 * `yyyy-MM-dd`, a rating between 0 and 10, and a country of two letters. Everything else -
 * the name unique within its category, the category being an on-demand one, the list
 * limits - is the service's, and its refusal lands under the field it named.
 */
export default function VideoOnDemandFormPage(_props: VideoOnDemandFormPageProps) {
  const { id } = useParams()
  const isEdit = id !== undefined

  const navigate = useNavigate()
  const { t, i18n } = useTranslation()
  const { message } = App.useApp()
  const session = useSession()
  const [form] = Form.useForm<VideoOnDemandValues>()

  const detailQuery = useVideoOnDemandDetail(id)
  const detail = detailQuery.data ?? null
  const loading = isEdit && (detailQuery.isPending || detailQuery.isFetching)
  const loadError = isEdit && detailQuery.isError ? messageForError(detailQuery.error) : null

  const create = useCreateVideoOnDemand()
  const update = useUpdateVideoOnDemand()
  const submitting = create.isPending || update.isPending

  const [dirty, setDirty] = useState(false)
  // The returned function is the deliberate-navigation bypass: the guard sees the form
  // still dirty at the instant after a save (React has not repainted `dirty` yet), so the
  // success path has to say that this move is intended rather than merely clear the flag.
  const allowNavigation = useUnsavedChangesWarning(dirty)

  const categoriesQuery = useContentCategories(CONTENT_TYPE_PARAMS.VideoOnDemand)
  const categories = categoriesQuery.data?.items ?? []
  const categoriesError = categoriesQuery.isError ? messageForError(categoriesQuery.error) : null

  /**
   * The bouquets the picker below the category offers.
   *
   * A separate query, behind its own permission (`viewBouquets`): sharing one call with the
   * categories would fail the whole form for an operator allowed to edit the catalogue but
   * not to read the packages. A failed read leaves the picker disabled rather than
   * empty-and-silent, and everything else still saves.
   */
  const bouquetsQuery = useBouquets()
  const bouquetOptions = (bouquetsQuery.data?.items ?? []).map((bouquet) => ({
    value: bouquet.id,
    label: bouquet.name,
  }))
  const bouquetsError = bouquetsQuery.isError ? messageForError(bouquetsQuery.error) : null

  const area = session ? areaForRole(session.role) : ''
  const listPath = `${area}/video-on-demand`

  useEffect(() => {
    if (!detail) return

    form.setFieldsValue(detailToFormValues(detail))
  }, [detail, form])

  useEffect(() => {
    // The add form's first category, chosen from the list rather than invented here: a video
    // must be filed under one. Written only while nothing is chosen, and never in edit mode,
    // where the answer above painted the row's own category.
    if (isEdit) return
    if (categories.length === 0) return
    if (form.getFieldValue('categoryId')) return

    const first = categories[0]
    if (first) form.setFieldsValue({ categoryId: first.id })
  }, [form, isEdit, categories])

  const categoryOptions = useMemo(
    () => categories.map((category) => ({ value: category.id, label: category.name })),
    [categories],
  )

  const typeOptions = useMemo(
    () => TYPES.map((type) => ({ value: type, label: t(`videoOnDemand.types.${type}`) })),
    [t],
  )

  const statusOptions = useMemo(
    () => STATUSES.map((status) => ({ value: status, label: t(`videoOnDemand.statuses.${status}`) })),
    [t],
  )

  /**
   * The country names in the language on screen. The codes underneath are what the server
   * stores; the names are only what the selector searches and draws, so they are rebuilt
   * when the visitor changes language rather than stored beside the form.
   */
  const countryList = useMemo(
    () => countryOptions(i18n.resolvedLanguage ?? i18n.language),
    [i18n.language, i18n.resolvedLanguage],
  )

  const handleFinish = async (values: VideoOnDemandValues) => {
    form.setFields(FIELD_NAMES.map((name) => ({ name, errors: [] })))

    try {
      const body = toBody(values)
      const answer = isEdit
        ? await update.mutateAsync({ id: id!, values: body })
        : await create.mutateAsync({ values: body })

      message.success(successTextFor(answer))
      setDirty(false)
      allowNavigation()
      navigate(listPath)
    } catch (error) {
      const fields = fieldErrors(error instanceof ApiError ? error.errors : null)
      if (fields.length > 0)
        form.setFields(
          fields.map((field) => ({
            name: field.name as keyof VideoOnDemandValues,
            errors: field.errors,
          })),
        )
      message.error(messageForError(error))
    }
  }

  if (loading) {
    return (
      <Card>
        <Spin />
      </Card>
    )
  }

  if (loadError) {
    return (
      <Card>
        <Alert
          type="error"
          showIcon
          message={loadError}
          action={
            <Button onClick={() => navigate(listPath)}>
              {t('videoOnDemand.backToList')}
            </Button>
          }
        />
      </Card>
    )
  }

  return (
    <Card title={t(isEdit ? 'videoOnDemand.editTitle' : 'videoOnDemand.addTitle')}>
      <Form<VideoOnDemandValues>
        form={form}
        layout="vertical"
        initialValues={CREATE_VALUES}
        onFinish={handleFinish}
        onValuesChange={() => setDirty(true)}
        style={{ maxWidth: 820 }}
      >
        {detail && (
          <Form.Item label={t('videoOnDemandForm.idLabel')}>
            {/* No `name`: layout only, so the identifier is never part of the body. `code`
                gives it the same background the other edit forms draw, and `copyable` is how
                it leaves the screen. */}
            <Typography.Text code copyable={{ text: detail.id }}>
              {detail.id}
            </Typography.Text>
          </Form.Item>
        )}

        <Form.Item
          name="name"
          label={t('videoOnDemandForm.nameLabel')}
          rules={[
            { required: true, message: t('videoOnDemandForm.nameRequired') },
            { max: NAME_MAX, message: t('videoOnDemandForm.maxLength', { max: NAME_MAX }) },
          ]}
        >
          <Input placeholder={t('videoOnDemandForm.namePlaceholder')} maxLength={NAME_MAX} />
        </Form.Item>

        <Form.Item
          name="categoryId"
          label={t('videoOnDemand.columns.category')}
          rules={[{ required: true, message: t('videoOnDemandForm.categoryRequired') }]}
        >
          <Select
            showSearch
            options={categoryOptions}
            optionFilterProp="label"
            placeholder={t('videoOnDemandForm.categoryRequired')}
          />
        </Form.Item>

        {categoriesError && (
          <Alert type="error" showIcon style={{ marginBottom: 16 }} message={categoriesError} />
        )}

        <Form.Item
          name="bouquetIds"
          label={t('bouquets.fieldLabel')}
          // Under the category, because the two answer the same question at two scales: the
          // category files the video, the bouquets say in which packages it is delivered.
          // Optional, and a set rather than an order - see the hint.
          extra={t('bouquets.fieldHint')}
        >
          <Select
            mode="multiple"
            allowClear
            showSearch
            optionFilterProp="label"
            options={bouquetOptions}
            placeholder={t('bouquets.fieldPlaceholder')}
            disabled={bouquetsError !== null}
          />
        </Form.Item>

        {bouquetsError && (
          <Alert type="error" showIcon style={{ marginBottom: 16 }} message={bouquetsError} />
        )}

        <Form.Item
          name="status"
          label={t('videoOnDemand.columns.status')}
          rules={[{ required: true, message: t('videoOnDemandForm.statusRequired') }]}
          extra={t('videoOnDemandForm.statusHint')}
        >
          <Select options={statusOptions} />
        </Form.Item>

        <Form.Item
          name="type"
          label={t('videoOnDemand.columns.type')}
          rules={[{ required: true, message: t('videoOnDemandForm.typeRequired') }]}
          extra={t('videoOnDemandForm.typeHint')}
        >
          <Select options={typeOptions} />
        </Form.Item>

        <Divider titlePlacement="start">{t('videoOnDemandForm.mediaDivider')}</Divider>

        <Form.Item
          name="source"
          label={t('videoOnDemandForm.sourceLabel')}
          rules={[
            { required: true, message: t('videoOnDemandForm.sourceRequired') },
            { max: SOURCE_MAX, message: t('videoOnDemandForm.maxLength', { max: SOURCE_MAX }) },
          ]}
          extra={t('videoOnDemandForm.sourceHint')}
        >
          <Input placeholder={t('videoOnDemandForm.sourcePlaceholder')} />
        </Form.Item>

        <Form.Item
          name="subtitleUrl"
          label={t('videoOnDemandForm.subtitleLabel')}
          rules={[{ max: URL_MAX, message: t('videoOnDemandForm.maxLength', { max: URL_MAX }) }]}
        >
          <Input placeholder={t('videoOnDemandForm.urlPlaceholder')} />
        </Form.Item>

        <Divider titlePlacement="start">{t('videoOnDemandForm.metadataDivider')}</Divider>

        <Form.Item
          name="imdbId"
          label={t('videoOnDemandForm.imdbLabel')}
          rules={[{ max: IMDB_MAX, message: t('videoOnDemandForm.maxLength', { max: IMDB_MAX }) }]}
        >
          <Input placeholder="tt1160419" />
        </Form.Item>

        <Form.Item
          name="posterUrl"
          label={t('videoOnDemandForm.posterLabel')}
          rules={[{ max: URL_MAX, message: t('videoOnDemandForm.maxLength', { max: URL_MAX }) }]}
        >
          <Input placeholder={t('videoOnDemandForm.urlPlaceholder')} />
        </Form.Item>

        <Form.Item
          name="backdropUrl"
          label={t('videoOnDemandForm.backdropLabel')}
          rules={[{ max: URL_MAX, message: t('videoOnDemandForm.maxLength', { max: URL_MAX }) }]}
        >
          <Input placeholder={t('videoOnDemandForm.urlPlaceholder')} />
        </Form.Item>

        <Form.Item
          name="plot"
          label={t('videoOnDemandForm.plotLabel')}
          rules={[{ max: PLOT_MAX, message: t('videoOnDemandForm.maxLength', { max: PLOT_MAX }) }]}
        >
          <Input.TextArea rows={4} />
        </Form.Item>

        <Form.Item
          name="cast"
          label={t('videoOnDemandForm.castLabel')}
          extra={t('videoOnDemandForm.tagsHint', { max: CAST_MAX })}
        >
          <Select mode="tags" tokenSeparators={[',']} open={false} />
        </Form.Item>

        <Form.Item
          name="director"
          label={t('videoOnDemandForm.directorLabel')}
          rules={[
            { max: DIRECTOR_MAX, message: t('videoOnDemandForm.maxLength', { max: DIRECTOR_MAX }) },
          ]}
        >
          <Input />
        </Form.Item>

        <Form.Item
          name="genres"
          label={t('videoOnDemandForm.genresLabel')}
          extra={t('videoOnDemandForm.tagsHint', { max: GENRES_MAX })}
        >
          <Select mode="tags" tokenSeparators={[',']} open={false} />
        </Form.Item>

        <Form.Item
          name="releaseDate"
          label={t('videoOnDemandForm.releaseDateLabel')}
          // The form stores the server's `yyyy-MM-dd` text, while the control speaks
          // `Dayjs`. Converting at the field boundary keeps every request and every
          // `setFieldsValue` on the same string shape the contract uses.
          getValueProps={(value?: string | null) => ({
            value: value ? dayjs(value) : null,
          })}
          normalize={(value: Dayjs | null) => (value ? value.format('YYYY-MM-DD') : '')}
        >
          <DatePicker
            format="YYYY-MM-DD"
            placeholder={t('videoOnDemandForm.releaseDatePlaceholder')}
            style={{ width: 200 }}
          />
        </Form.Item>

        <Form.Item
          name="rating"
          label={t('videoOnDemandForm.ratingLabel')}
          rules={[
            {
              validator: (_: unknown, value: number | null) =>
                value === null || (value >= 0 && value <= 10)
                  ? Promise.resolve()
                  : Promise.reject(new Error(t('videoOnDemandForm.ratingInvalid'))),
            },
          ]}
        >
          <InputNumber min={0} max={10} step={0.1} precision={1} style={{ width: 160 }} />
        </Form.Item>

        <Form.Item
          name="countryCode"
          label={t('videoOnDemandForm.countryLabel')}
        >
          {/* A selector rather than two typed letters, so the operator picks a country the
              server can act on instead of guessing a code - and clears it to "none", which
              is what an empty value means on the contract. The options carry the localized
              name while the stored value stays the two-letter code. */}
          <Select
            showSearch
            allowClear
            options={countryList}
            optionFilterProp="label"
            placeholder={t('videoOnDemandForm.countryPlaceholder')}
            aria-label={t('videoOnDemandForm.countryLabel')}
            style={{ width: '100%', maxWidth: 320 }}
          />
        </Form.Item>

        <Form.Item style={{ marginTop: 24 }}>
          <Space>
            <Button type="primary" htmlType="submit" loading={submitting}>
              {t(isEdit ? 'videoOnDemand.save' : 'videoOnDemand.create')}
            </Button>
            <Button disabled={submitting} onClick={() => navigate(listPath)}>
              {t('videoOnDemand.backToList')}
            </Button>
          </Space>
        </Form.Item>
      </Form>
    </Card>
  )
}
