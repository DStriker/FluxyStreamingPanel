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
import type { SeriesValues } from '../../features/series/api'
import {
  useCreateSeries,
  useUpdateSeries,
} from '../../features/series/hooks/mutations'
import { useSeriesDetail } from '../../features/series/hooks/queries'
import type { SeriesStatus } from '../../features/series/types'
import { useUnsavedChangesWarning } from '../../hooks/useUnsavedChangesWarning'
import { ApiError, fieldErrors, messageForError, successTextFor } from '../../utils/http'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'

/** The column widths the other side of these fields already fixed, written here by hand. */
const NAME_MAX = 200
const URL_MAX = 2048
const IMDB_MAX = 32
const PLOT_MAX = 4000
const DIRECTOR_MAX = 200
const CAST_MAX = 20
const GENRES_MAX = 10

/** The two states, active first: the form's own default is the safe one below. */
const STATUSES: SeriesStatus[] = ['Active', 'Inactive']

/** The add form's starting content. */
const CREATE_VALUES: SeriesValues = {
  name: '',
  status: 'Inactive',
  categoryId: '',
  imdbId: '',
  posterUrl: '',
  backdropUrl: '',
  plot: '',
  cast: [],
  director: '',
  genres: [],
  releaseDate: '',
  rating: null,
  bouquetIds: [],
}

/** Every field this form draws, so a refusal can be cleared before the next attempt. */
const FIELD_NAMES: readonly (keyof SeriesValues)[] = [
  'name',
  'status',
  'categoryId',
  'imdbId',
  'posterUrl',
  'backdropUrl',
  'plot',
  'cast',
  'director',
  'genres',
  'releaseDate',
  'rating',
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

/** What the form holds, spelled as the two endpoints take it. */
const toBody = (values: SeriesValues): SeriesValues => ({
  name: (values.name ?? '').trim(),
  status: values.status ?? CREATE_VALUES.status,
  categoryId: values.categoryId ?? '',
  imdbId: (values.imdbId ?? '').trim(),
  posterUrl: (values.posterUrl ?? '').trim(),
  backdropUrl: (values.backdropUrl ?? '').trim(),
  plot: (values.plot ?? '').trim(),
  cast: cleanList(values.cast),
  director: (values.director ?? '').trim(),
  genres: cleanList(values.genres),
  releaseDate: (values.releaseDate ?? '').trim(),
  rating: values.rating ?? null,
  bouquetIds: values.bouquetIds ?? [],
})

/** The stored series spelled as form content. */
const detailToFormValues = (detail: {
  name: string
  status: SeriesStatus
  categoryId: string
  imdbId: string | null
  posterUrl: string | null
  backdropUrl: string | null
  plot: string | null
  cast: string[]
  director: string | null
  genres: string[]
  releaseDate: string | null
  rating: number | null
  bouquetIds: string[]
}): SeriesValues => ({
  name: detail.name,
  status: detail.status,
  categoryId: detail.categoryId,
  imdbId: detail.imdbId ?? '',
  posterUrl: detail.posterUrl ?? '',
  backdropUrl: detail.backdropUrl ?? '',
  plot: detail.plot ?? '',
  cast: detail.cast,
  director: detail.director ?? '',
  genres: detail.genres,
  releaseDate: detail.releaseDate ?? '',
  rating: detail.rating,
  bouquetIds: [...detail.bouquetIds],
})

interface SeriesFormPageProps {
  /** The menu key this route carries, accepted and deliberately unread. */
  sectionKey: string
}

/**
 * The administrator's add and edit form for one series - one component behind two addresses.
 *
 * The form is fetched rather than passed, for the reason the other edit forms give: a form
 * painted from whatever the previous screen happened to hold would edit the wrong row after a
 * reload. The two counts are read-only: they are computed from the episodes, and a form field
 * for them would be a promise the server does not keep.
 */
export default function SeriesFormPage(_props: SeriesFormPageProps) {
  const { id } = useParams()
  const isEdit = id !== undefined

  const navigate = useNavigate()
  const { t } = useTranslation()
  const { message } = App.useApp()
  const session = useSession()
  const [form] = Form.useForm<SeriesValues>()

  const detailQuery = useSeriesDetail(id)
  const detail = detailQuery.data ?? null
  const loading = isEdit && (detailQuery.isPending || detailQuery.isFetching)
  const loadError = isEdit && detailQuery.isError ? messageForError(detailQuery.error) : null

  const create = useCreateSeries()
  const update = useUpdateSeries()
  const submitting = create.isPending || update.isPending

  const [dirty, setDirty] = useState(false)
  const allowNavigation = useUnsavedChangesWarning(dirty)

  const categoriesQuery = useContentCategories(CONTENT_TYPE_PARAMS.Series)
  const categories = categoriesQuery.data?.items ?? []
  const categoriesError = categoriesQuery.isError ? messageForError(categoriesQuery.error) : null

  /**
   * The bouquets the picker below the category offers.
   *
   * A separate query, behind its own permission (`viewBouquets`): sharing one call with the
   * categories would fail the whole form for an operator allowed to edit the series and not
   * to read the packages. A failed read leaves the picker disabled rather than
   * empty-and-silent, and everything else still saves.
   */
  const bouquetsQuery = useBouquets()
  const bouquetOptions = (bouquetsQuery.data?.items ?? []).map((bouquet) => ({
    value: bouquet.id,
    label: bouquet.name,
  }))
  const bouquetsError = bouquetsQuery.isError ? messageForError(bouquetsQuery.error) : null

  const area = session ? areaForRole(session.role) : ''
  const listPath = `${area}/series`

  useEffect(() => {
    if (!detail) return

    form.setFieldsValue(detailToFormValues(detail))
  }, [detail, form])

  useEffect(() => {
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

  const statusOptions = useMemo(
    () => STATUSES.map((status) => ({ value: status, label: t(`series.statuses.${status}`) })),
    [t],
  )

  const handleFinish = async (values: SeriesValues) => {
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
            name: field.name as keyof SeriesValues,
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
              {t('series.backToList')}
            </Button>
          }
        />
      </Card>
    )
  }

  return (
    <Card title={t(isEdit ? 'series.editTitle' : 'series.addTitle')}>
      <Form<SeriesValues>
        form={form}
        layout="vertical"
        initialValues={CREATE_VALUES}
        onFinish={handleFinish}
        onValuesChange={() => setDirty(true)}
        style={{ maxWidth: 820 }}
      >
        {detail && (
          <Form.Item label={t('seriesForm.idLabel')}>
            <Typography.Text code copyable={{ text: detail.id }}>
              {detail.id}
            </Typography.Text>
          </Form.Item>
        )}

        {detail && (
          <Form.Item label={t('seriesForm.countsLabel')}>
            <Typography.Text>
              {t('seriesForm.counts', {
                seasons: detail.seasonsCount,
                episodes: detail.episodesCount,
              })}
            </Typography.Text>
          </Form.Item>
        )}

        <Form.Item
          name="name"
          label={t('seriesForm.nameLabel')}
          rules={[
            { required: true, message: t('seriesForm.nameRequired') },
            { max: NAME_MAX, message: t('seriesForm.maxLength', { max: NAME_MAX }) },
          ]}
        >
          <Input placeholder={t('seriesForm.namePlaceholder')} maxLength={NAME_MAX} />
        </Form.Item>

        <Form.Item
          name="categoryId"
          label={t('series.columns.category')}
          rules={[{ required: true, message: t('seriesForm.categoryRequired') }]}
        >
          <Select
            showSearch
            options={categoryOptions}
            optionFilterProp="label"
            placeholder={t('seriesForm.categoryRequired')}
          />
        </Form.Item>

        {categoriesError && (
          <Alert type="error" showIcon style={{ marginBottom: 16 }} message={categoriesError} />
        )}

        <Form.Item
          name="bouquetIds"
          label={t('bouquets.fieldLabel')}
          // Under the category, because the two answer the same question at two scales: the
          // category files the series, the bouquets say in which packages it is delivered.
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
          label={t('series.columns.status')}
          rules={[{ required: true, message: t('seriesForm.statusRequired') }]}
          extra={t('seriesForm.statusHint')}
        >
          <Select options={statusOptions} />
        </Form.Item>

        <Divider titlePlacement="start">{t('seriesForm.metadataDivider')}</Divider>

        <Form.Item
          name="imdbId"
          label={t('seriesForm.imdbLabel')}
          rules={[{ max: IMDB_MAX, message: t('seriesForm.maxLength', { max: IMDB_MAX }) }]}
        >
          <Input placeholder="tt0903747" />
        </Form.Item>

        <Form.Item
          name="posterUrl"
          label={t('seriesForm.posterLabel')}
          rules={[{ max: URL_MAX, message: t('seriesForm.maxLength', { max: URL_MAX }) }]}
        >
          <Input placeholder={t('seriesForm.urlPlaceholder')} />
        </Form.Item>

        <Form.Item
          name="backdropUrl"
          label={t('seriesForm.backdropLabel')}
          rules={[{ max: URL_MAX, message: t('seriesForm.maxLength', { max: URL_MAX }) }]}
        >
          <Input placeholder={t('seriesForm.urlPlaceholder')} />
        </Form.Item>

        <Form.Item
          name="plot"
          label={t('seriesForm.plotLabel')}
          rules={[{ max: PLOT_MAX, message: t('seriesForm.maxLength', { max: PLOT_MAX }) }]}
        >
          <Input.TextArea rows={4} />
        </Form.Item>

        <Form.Item
          name="cast"
          label={t('seriesForm.castLabel')}
          extra={t('seriesForm.tagsHint', { max: CAST_MAX })}
        >
          <Select mode="tags" tokenSeparators={[',']} open={false} />
        </Form.Item>

        <Form.Item
          name="director"
          label={t('seriesForm.directorLabel')}
          rules={[
            { max: DIRECTOR_MAX, message: t('seriesForm.maxLength', { max: DIRECTOR_MAX }) },
          ]}
        >
          <Input />
        </Form.Item>

        <Form.Item
          name="genres"
          label={t('seriesForm.genresLabel')}
          extra={t('seriesForm.tagsHint', { max: GENRES_MAX })}
        >
          <Select mode="tags" tokenSeparators={[',']} open={false} />
        </Form.Item>

        <Form.Item
          name="releaseDate"
          label={t('seriesForm.releaseDateLabel')}
          getValueProps={(value?: string | null) => ({
            value: value ? dayjs(value) : null,
          })}
          normalize={(value: Dayjs | null) => (value ? value.format('YYYY-MM-DD') : '')}
        >
          <DatePicker
            format="YYYY-MM-DD"
            placeholder={t('seriesForm.releaseDatePlaceholder')}
            style={{ width: 200 }}
          />
        </Form.Item>

        <Form.Item
          name="rating"
          label={t('seriesForm.ratingLabel')}
          rules={[
            {
              validator: (_: unknown, value: number | null) =>
                value === null || (value >= 0 && value <= 10)
                  ? Promise.resolve()
                  : Promise.reject(new Error(t('seriesForm.ratingInvalid'))),
            },
          ]}
        >
          <InputNumber min={0} max={10} step={0.1} precision={1} style={{ width: 160 }} />
        </Form.Item>

        <Form.Item style={{ marginTop: 24 }}>
          <Space>
            <Button type="primary" htmlType="submit" loading={submitting}>
              {t(isEdit ? 'series.save' : 'series.create')}
            </Button>
            <Button disabled={submitting} onClick={() => navigate(listPath)}>
              {t('series.backToList')}
            </Button>
          </Space>
        </Form.Item>
      </Form>
    </Card>
  )
}
