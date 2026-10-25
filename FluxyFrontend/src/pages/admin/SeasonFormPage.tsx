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
  InputNumber,
  Select,
  Space,
  Spin,
  Typography,
} from 'antd'
import { useTranslation } from 'react-i18next'
import type { SeasonValues } from '../../features/series/api'
import { useCreateSeason } from '../../features/series/hooks/mutations'
import { useSeriesDetail } from '../../features/series/hooks/queries'
import type { EpisodeStatus, EpisodeType } from '../../features/series/types'
import { useUnsavedChangesWarning } from '../../hooks/useUnsavedChangesWarning'
import { ApiError, fieldErrors, messageForError } from '../../utils/http'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'

const FOLDER_MAX = 2048

const STATUSES: EpisodeStatus[] = ['Active', 'Inactive']
const TYPES: EpisodeType[] = ['Relay', 'Redirect']

const CREATE_VALUES: SeasonValues = {
  seasonNumber: 1,
  folderPath: '',
  addSeasonNumberToName: true,
  addEpisodeNumberToName: true,
  status: 'Inactive',
  type: 'Relay',
}

const FIELD_NAMES: readonly (keyof SeasonValues)[] = [
  'seasonNumber',
  'folderPath',
  'addSeasonNumberToName',
  'addEpisodeNumberToName',
  'status',
  'type',
]

interface SeasonFormPageProps {
  /** The menu key this route carries, accepted and deliberately unread. */
  sectionKey: string
}

/**
 * The add-season form: one folder in, a season's worth of episode rows out.
 *
 * The series is fixed by the route and shown read-only. The folder is read on the server, and
 * an existing season is refused rather than topped up: this form is the way to add a season,
 * and adding episodes to one that already exists is the single-row form's job.
 */
export default function SeasonFormPage(_props: SeasonFormPageProps) {
  const { id } = useParams()
  const seriesId = id ?? ''

  const navigate = useNavigate()
  const { t } = useTranslation()
  const { message } = App.useApp()
  const session = useSession()
  const [form] = Form.useForm<SeasonValues>()

  const seriesQuery = useSeriesDetail(seriesId || undefined)
  const loading = seriesQuery.isPending
  const loadError = seriesQuery.isError ? messageForError(seriesQuery.error) : null

  const create = useCreateSeason()
  const [dirty, setDirty] = useState(false)
  const allowNavigation = useUnsavedChangesWarning(dirty)

  const area = session ? areaForRole(session.role) : ''
  const episodesPath = `${area}/series/${seriesId}/episodes`

  useEffect(() => {
    if (!seriesQuery.data) return
    if (form.getFieldValue('seasonNumber') !== 1) return

    form.setFieldsValue({
      seasonNumber: Math.max(seriesQuery.data.seasonsCount + 1, 1),
    })
  }, [form, seriesQuery.data])

  const statusOptions = useMemo(
    () => STATUSES.map((status) => ({ value: status, label: t(`series.statuses.${status}`) })),
    [t],
  )

  const typeOptions = useMemo(
    () => TYPES.map((type) => ({ value: type, label: t(`seriesEpisodes.types.${type}`) })),
    [t],
  )

  const handleFinish = async (values: SeasonValues) => {
    form.setFields(FIELD_NAMES.map((name) => ({ name, errors: [] })))

    try {
      const answer = await create.mutateAsync({
        seriesId,
        values: {
          seasonNumber: values.seasonNumber,
          folderPath: (values.folderPath ?? '').trim(),
          addSeasonNumberToName: values.addSeasonNumberToName ?? false,
          addEpisodeNumberToName: values.addEpisodeNumberToName ?? false,
          status: values.status ?? CREATE_VALUES.status,
          type: values.type ?? CREATE_VALUES.type,
        },
      })

      message.success(t('seasonForm.created', { count: answer.addedCount }))
      setDirty(false)
      allowNavigation()
      navigate(`${episodesPath}?season=${answer.seasonNumber}`)
    } catch (error) {
      const fields = fieldErrors(error instanceof ApiError ? error.errors : null)
      if (fields.length > 0)
        form.setFields(
          fields.map((field) => ({
            name: field.name as keyof SeasonValues,
            errors: field.errors,
          })),
        )
      message.error(messageForError(error))
    }
  }

  if (loading) {
    return (
      <Card title={t('seasonForm.title')}>
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
            <Button onClick={() => navigate(episodesPath)}>
              {t('seriesEpisodes.backToList')}
            </Button>
          }
        />
      </Card>
    )
  }

  return (
    <Card title={t('seasonForm.title')}>
      <Form<SeasonValues>
        form={form}
        layout="vertical"
        initialValues={CREATE_VALUES}
        onFinish={handleFinish}
        onValuesChange={() => setDirty(true)}
        style={{ maxWidth: 820 }}
      >
        <Form.Item label={t('seasonForm.seriesLabel')}>
          <Typography.Text>{seriesQuery.data?.name ?? ''}</Typography.Text>
        </Form.Item>

        <Form.Item
          name="seasonNumber"
          label={t('seasonForm.seasonLabel')}
          rules={[{ required: true, message: t('seasonForm.seasonRequired') }]}
        >
          <InputNumber min={1} max={999} precision={0} style={{ width: 160 }} />
        </Form.Item>

        <Form.Item
          name="folderPath"
          label={t('seasonForm.folderLabel')}
          rules={[
            { required: true, message: t('seasonForm.folderRequired') },
            { max: FOLDER_MAX, message: t('seasonForm.folderMax', { max: FOLDER_MAX }) },
          ]}
          extra={t('seasonForm.folderHint')}
        >
          <Input placeholder={t('seasonForm.folderPlaceholder')} />
        </Form.Item>

        <Form.Item
          name="addSeasonNumberToName"
          valuePropName="checked"
          extra={t('seasonForm.seasonNameHint')}
        >
          <Checkbox>{t('seasonForm.addSeasonNumber')}</Checkbox>
        </Form.Item>

        <Form.Item
          name="addEpisodeNumberToName"
          valuePropName="checked"
          extra={t('seasonForm.episodeNameHint')}
        >
          <Checkbox>{t('seasonForm.addEpisodeNumber')}</Checkbox>
        </Form.Item>

        <Form.Item
          name="status"
          label={t('seriesEpisodes.columns.status')}
          rules={[{ required: true, message: t('seasonForm.statusRequired') }]}
          extra={t('seasonForm.statusHint')}
        >
          <Select options={statusOptions} />
        </Form.Item>

        <Form.Item
          name="type"
          label={t('seriesEpisodes.columns.type')}
          rules={[{ required: true, message: t('seasonForm.typeRequired') }]}
          extra={t('seasonForm.typeHint')}
        >
          <Select options={typeOptions} />
        </Form.Item>

        <Form.Item style={{ marginTop: 24 }}>
          <Space>
            <Button type="primary" htmlType="submit" loading={create.isPending}>
              {t('seasonForm.create')}
            </Button>
            <Button disabled={create.isPending} onClick={() => navigate(episodesPath)}>
              {t('seriesEpisodes.backToList')}
            </Button>
          </Space>
        </Form.Item>
      </Form>
    </Card>
  )
}
