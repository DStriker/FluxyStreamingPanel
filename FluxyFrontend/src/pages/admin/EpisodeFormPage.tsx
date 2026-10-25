import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router-dom'
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
import type { EpisodeValues } from '../../features/series/api'
import {
  useCreateEpisode,
  useUpdateEpisode,
} from '../../features/series/hooks/mutations'
import { useEpisodeDetail, useSeriesDetail } from '../../features/series/hooks/queries'
import type { EpisodeStatus, EpisodeType } from '../../features/series/types'
import { useUnsavedChangesWarning } from '../../hooks/useUnsavedChangesWarning'
import { ApiError, fieldErrors, messageForError, successTextFor } from '../../utils/http'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'

const NAME_MAX = 200
const URL_MAX = 2048
const IMDB_MAX = 32
const PLOT_MAX = 4000

const STATUSES: EpisodeStatus[] = ['Active', 'Inactive']
const TYPES: EpisodeType[] = ['Relay', 'Redirect']

const CREATE_VALUES: EpisodeValues = {
  seasonNumber: 1,
  episodeNumber: 1,
  name: '',
  source: '',
  status: 'Inactive',
  type: 'Relay',
  imdbId: '',
  imageUrl: '',
  plot: '',
  releaseDate: '',
  rating: null,
}

const FIELD_NAMES: readonly (keyof EpisodeValues)[] = [
  'seasonNumber',
  'episodeNumber',
  'name',
  'source',
  'status',
  'type',
  'imdbId',
  'imageUrl',
  'plot',
  'releaseDate',
  'rating',
]

const toBody = (values: EpisodeValues): EpisodeValues => ({
  seasonNumber: values.seasonNumber ?? 1,
  episodeNumber: values.episodeNumber ?? 1,
  name: (values.name ?? '').trim(),
  source: (values.source ?? '').trim(),
  status: values.status ?? CREATE_VALUES.status,
  type: values.type ?? CREATE_VALUES.type,
  imdbId: (values.imdbId ?? '').trim(),
  imageUrl: (values.imageUrl ?? '').trim(),
  plot: (values.plot ?? '').trim(),
  releaseDate: (values.releaseDate ?? '').trim(),
  rating: values.rating ?? null,
})

const detailToFormValues = (detail: {
  seasonNumber: number
  episodeNumber: number
  name: string | null
  source: string
  status: EpisodeStatus
  type: EpisodeType
  imdbId: string | null
  imageUrl: string | null
  plot: string | null
  releaseDate: string | null
  rating: number | null
}): EpisodeValues => ({
  seasonNumber: detail.seasonNumber,
  episodeNumber: detail.episodeNumber,
  name: detail.name ?? '',
  source: detail.source,
  status: detail.status,
  type: detail.type,
  imdbId: detail.imdbId ?? '',
  imageUrl: detail.imageUrl ?? '',
  plot: detail.plot ?? '',
  releaseDate: detail.releaseDate ?? '',
  rating: detail.rating,
})

interface EpisodeFormPageProps {
  /** The menu key this route carries, accepted and deliberately unread. */
  sectionKey: string
}

/**
 * The administrator's add and edit form for one episode of one series.
 *
 * The series is fixed by the route and is shown read-only: an episode is not moved between
 * series by an edit. The form is fetched rather than passed, so a reload edits the row the
 * address names rather than whatever the previous screen held.
 */
export default function EpisodeFormPage(_props: EpisodeFormPageProps) {
  const { id, episodeId } = useParams()
  const seriesId = id ?? ''
  const isEdit = episodeId !== undefined

  const navigate = useNavigate()
  const { t } = useTranslation()
  const { message } = App.useApp()
  const session = useSession()
  const [searchParams] = useSearchParams()
  const [form] = Form.useForm<EpisodeValues>()

  const seriesQuery = useSeriesDetail(seriesId || undefined)
  const detailQuery = useEpisodeDetail(seriesId || undefined, episodeId)
  const detail = detailQuery.data ?? null

  const loading =
    seriesQuery.isPending ||
    (isEdit && (detailQuery.isPending || detailQuery.isFetching))
  const loadError =
    seriesQuery.isError
      ? messageForError(seriesQuery.error)
      : isEdit && detailQuery.isError
        ? messageForError(detailQuery.error)
        : null

  const create = useCreateEpisode()
  const update = useUpdateEpisode()
  const submitting = create.isPending || update.isPending

  const [dirty, setDirty] = useState(false)
  const allowNavigation = useUnsavedChangesWarning(dirty)

  const area = session ? areaForRole(session.role) : ''
  const listPath = `${area}/series/${seriesId}/episodes`

  useEffect(() => {
    if (!detail) return

    form.setFieldsValue(detailToFormValues(detail))
  }, [detail, form])

  useEffect(() => {
    if (isEdit) return
    if (!seriesQuery.data) return
    if (form.getFieldValue('seasonNumber') !== 1) return

    const fromQuery = Number.parseInt(searchParams.get('season') ?? '', 10)
    const season =
      Number.isInteger(fromQuery) && fromQuery > 0
        ? fromQuery
        : Math.max(seriesQuery.data.seasonsCount, 1)

    form.setFieldsValue({ seasonNumber: season })
  }, [form, isEdit, searchParams, seriesQuery.data])

  const statusOptions = useMemo(
    () => STATUSES.map((status) => ({ value: status, label: t(`series.statuses.${status}`) })),
    [t],
  )

  const typeOptions = useMemo(
    () => TYPES.map((type) => ({ value: type, label: t(`seriesEpisodes.types.${type}`) })),
    [t],
  )

  const handleFinish = async (values: EpisodeValues) => {
    form.setFields(FIELD_NAMES.map((name) => ({ name, errors: [] })))

    try {
      const body = toBody(values)
      const answer = isEdit
        ? await update.mutateAsync({
            seriesId,
            episodeId: episodeId!,
            values: body,
          })
        : await create.mutateAsync({ seriesId, values: body })

      message.success(successTextFor(answer))
      setDirty(false)
      allowNavigation()
      navigate(`${listPath}?season=${body.seasonNumber}`)
    } catch (error) {
      const fields = fieldErrors(error instanceof ApiError ? error.errors : null)
      if (fields.length > 0)
        form.setFields(
          fields.map((field) => ({
            name: field.name as keyof EpisodeValues,
            errors: field.errors,
          })),
        )
      message.error(messageForError(error))
    }
  }

  if (loading) {
    return (
      <Card title={t(isEdit ? 'episodeForm.editTitle' : 'episodeForm.addTitle')}>
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
              {t('seriesEpisodes.backToList')}
            </Button>
          }
        />
      </Card>
    )
  }

  return (
    <Card title={t(isEdit ? 'episodeForm.editTitle' : 'episodeForm.addTitle')}>
      <Form<EpisodeValues>
        form={form}
        layout="vertical"
        initialValues={CREATE_VALUES}
        onFinish={handleFinish}
        onValuesChange={() => setDirty(true)}
        style={{ maxWidth: 820 }}
      >
        {detail && (
          <Form.Item label={t('episodeForm.idLabel')}>
            <Typography.Text code copyable={{ text: detail.id }}>
              {detail.id}
            </Typography.Text>
          </Form.Item>
        )}

        <Form.Item label={t('episodeForm.seriesLabel')}>
          <Typography.Text>{seriesQuery.data?.name ?? ''}</Typography.Text>
        </Form.Item>

        <Space size="large" wrap>
          <Form.Item
            name="seasonNumber"
            label={t('seriesEpisodes.columns.season')}
            rules={[{ required: true, message: t('episodeForm.seasonRequired') }]}
          >
            <InputNumber min={1} max={999} precision={0} style={{ width: 140 }} />
          </Form.Item>

          <Form.Item
            name="episodeNumber"
            label={t('seriesEpisodes.columns.episode')}
            rules={[{ required: true, message: t('episodeForm.episodeRequired') }]}
          >
            <InputNumber min={1} max={9999} precision={0} style={{ width: 140 }} />
          </Form.Item>
        </Space>

        <Form.Item
          name="name"
          label={t('episodeForm.nameLabel')}
          rules={[{ max: NAME_MAX, message: t('episodeForm.maxLength', { max: NAME_MAX }) }]}
        >
          <Input placeholder={t('episodeForm.namePlaceholder')} maxLength={NAME_MAX} />
        </Form.Item>

        <Form.Item
          name="status"
          label={t('seriesEpisodes.columns.status')}
          rules={[{ required: true, message: t('episodeForm.statusRequired') }]}
          extra={t('episodeForm.statusHint')}
        >
          <Select options={statusOptions} />
        </Form.Item>

        <Form.Item
          name="type"
          label={t('seriesEpisodes.columns.type')}
          rules={[{ required: true, message: t('episodeForm.typeRequired') }]}
          extra={t('episodeForm.typeHint')}
        >
          <Select options={typeOptions} />
        </Form.Item>

        <Divider titlePlacement="start">{t('episodeForm.mediaDivider')}</Divider>

        <Form.Item
          name="source"
          label={t('episodeForm.sourceLabel')}
          rules={[
            { required: true, message: t('episodeForm.sourceRequired') },
            { max: URL_MAX, message: t('episodeForm.maxLength', { max: URL_MAX }) },
          ]}
          extra={t('episodeForm.sourceHint')}
        >
          <Input placeholder={t('episodeForm.sourcePlaceholder')} />
        </Form.Item>

        <Divider titlePlacement="start">{t('episodeForm.metadataDivider')}</Divider>

        <Form.Item
          name="imdbId"
          label={t('episodeForm.imdbLabel')}
          rules={[{ max: IMDB_MAX, message: t('episodeForm.maxLength', { max: IMDB_MAX }) }]}
        >
          <Input placeholder="tt0903747" />
        </Form.Item>

        <Form.Item
          name="imageUrl"
          label={t('episodeForm.imageLabel')}
          rules={[{ max: URL_MAX, message: t('episodeForm.maxLength', { max: URL_MAX }) }]}
        >
          <Input placeholder={t('episodeForm.urlPlaceholder')} />
        </Form.Item>

        <Form.Item
          name="plot"
          label={t('episodeForm.plotLabel')}
          rules={[{ max: PLOT_MAX, message: t('episodeForm.maxLength', { max: PLOT_MAX }) }]}
        >
          <Input.TextArea rows={4} />
        </Form.Item>

        <Form.Item
          name="releaseDate"
          label={t('episodeForm.releaseDateLabel')}
          getValueProps={(value?: string | null) => ({
            value: value ? dayjs(value) : null,
          })}
          normalize={(value: Dayjs | null) => (value ? value.format('YYYY-MM-DD') : '')}
        >
          <DatePicker
            format="YYYY-MM-DD"
            placeholder={t('episodeForm.releaseDatePlaceholder')}
            style={{ width: 200 }}
          />
        </Form.Item>

        <Form.Item
          name="rating"
          label={t('episodeForm.ratingLabel')}
          rules={[
            {
              validator: (_: unknown, value: number | null) =>
                value === null || (value >= 0 && value <= 10)
                  ? Promise.resolve()
                  : Promise.reject(new Error(t('episodeForm.ratingInvalid'))),
            },
          ]}
        >
          <InputNumber min={0} max={10} step={0.1} precision={1} style={{ width: 160 }} />
        </Form.Item>

        <Form.Item style={{ marginTop: 24 }}>
          <Space>
            <Button type="primary" htmlType="submit" loading={submitting}>
              {t(isEdit ? 'episodeForm.save' : 'episodeForm.create')}
            </Button>
            <Button disabled={submitting} onClick={() => navigate(listPath)}>
              {t('seriesEpisodes.backToList')}
            </Button>
          </Space>
        </Form.Item>
      </Form>
    </Card>
  )
}
