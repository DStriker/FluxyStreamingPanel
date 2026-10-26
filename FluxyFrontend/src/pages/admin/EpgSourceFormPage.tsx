import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { Alert, App, Button, Card, Form, Input, InputNumber, Space, Spin, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import {
  EPG_DAYS_KEEP_DEFAULT,
  EPG_DAYS_KEEP_MAX,
  EPG_DAYS_KEEP_MIN,
  EPG_NAME_MAX,
  EPG_URL_MAX,
} from '../../features/epg/api'
import type { EpgSourceValues } from '../../features/epg/api'
import {
  useCreateEpgSource,
  useUpdateEpgSource,
} from '../../features/epg/hooks/mutations'
import { useEpgSourceDetail } from '../../features/epg/hooks/queries'
import { useUnsavedChangesWarning } from '../../hooks/useUnsavedChangesWarning'
import { ApiError, fieldErrors, messageForError, successTextFor } from '../../utils/http'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'

/**
 * The add form's starting content.
 *
 * Seven days is the server's own default and the value the domain states, so the form opens
 * on the answer a create that names nothing would get rather than on an empty box the
 * server would have to interpret.
 */
const CREATE_VALUES: EpgSourceValues = {
  name: '',
  url: '',
  daysKeep: EPG_DAYS_KEEP_DEFAULT,
}

/** Every field this form draws, so a refusal can be cleared before the next attempt. */
const FIELD_NAMES: readonly (keyof EpgSourceValues)[] = ['name', 'url', 'daysKeep']

/** Whether a value is an absolute http(s) URL - the shape the server will accept. */
const isHttpUrl = (value: string): boolean => {
  try {
    const url = new URL(value.trim())

    return url.protocol === 'http:' || url.protocol === 'https:'
  } catch {
    return false
  }
}

interface EpgSourceFormPageProps {
  /** The menu key this route carries, accepted and deliberately unread. */
  sectionKey: string
}

/**
 * The administrator's add and edit form for one programme guide source - one component
 * behind two addresses.
 *
 * The form is fetched rather than passed, for the reason the other edit forms give: a form
 * painted from whatever the previous screen happened to hold would edit the wrong row after
 * a reload. While the fetch is in flight the page is a spinner, which is the state
 * `probe:render` asserts.
 *
 * The three fields are the whole of what an operator owns. The raw guide and the moment of
 * the last refresh are the updater's, so this page neither shows nor sends them.
 */
export default function EpgSourceFormPage(_props: EpgSourceFormPageProps) {
  const { id } = useParams()
  const isEdit = id !== undefined

  const navigate = useNavigate()
  const { t } = useTranslation()
  const { message } = App.useApp()
  const session = useSession()
  const [form] = Form.useForm<EpgSourceValues>()

  const detailQuery = useEpgSourceDetail(id)
  const detail = detailQuery.data ?? null
  const loading = isEdit && (detailQuery.isPending || detailQuery.isFetching)
  const loadError = isEdit && detailQuery.isError ? messageForError(detailQuery.error) : null

  const create = useCreateEpgSource()
  const update = useUpdateEpgSource()
  const submitting = create.isPending || update.isPending

  const [dirty, setDirty] = useState(false)
  // The returned function is the deliberate-navigation bypass: the guard sees the form still
  // dirty at the instant after a save, so the success path says that this move is intended.
  const allowNavigation = useUnsavedChangesWarning(dirty)

  const area = session ? areaForRole(session.role) : ''
  const listPath = `${area}/epg-sources`

  useEffect(() => {
    if (!detail) return

    form.setFieldsValue({
      name: detail.name,
      url: detail.url,
      daysKeep: detail.daysKeep,
    })
  }, [detail, form])

  const handleFinish = async (values: EpgSourceValues) => {
    form.setFields(FIELD_NAMES.map((name) => ({ name, errors: [] })))

    try {
      const answer = isEdit
        ? await update.mutateAsync({ id: id!, values })
        : await create.mutateAsync({ values })

      message.success(successTextFor(answer))
      setDirty(false)
      allowNavigation()
      navigate(listPath)
    } catch (error) {
      const fields = fieldErrors(error instanceof ApiError ? error.errors : null)
      if (fields.length > 0)
        form.setFields(
          fields.map((field) => ({
            name: field.name as keyof EpgSourceValues,
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
            <Button onClick={() => navigate(listPath)}>{t('epgSources.backToList')}</Button>
          }
        />
      </Card>
    )
  }

  return (
    <Card title={t(isEdit ? 'epgSources.editTitle' : 'epgSources.addTitle')}>
      <Form<EpgSourceValues>
        form={form}
        layout="vertical"
        initialValues={CREATE_VALUES}
        onFinish={handleFinish}
        onValuesChange={() => setDirty(true)}
        style={{ maxWidth: 820 }}
      >
        {detail && (
          <Form.Item label={t('epgSourcesForm.idLabel')}>
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
          label={t('epgSourcesForm.nameLabel')}
          rules={[
            { required: true, message: t('epgSourcesForm.nameRequired') },
            { max: EPG_NAME_MAX, message: t('epgSourcesForm.maxLength', { max: EPG_NAME_MAX }) },
          ]}
        >
          <Input placeholder={t('epgSourcesForm.namePlaceholder')} maxLength={EPG_NAME_MAX} />
        </Form.Item>

        <Form.Item
          name="url"
          label={t('epgSourcesForm.urlLabel')}
          rules={[
            { required: true, message: t('epgSourcesForm.urlRequired') },
            { max: EPG_URL_MAX, message: t('epgSourcesForm.maxLength', { max: EPG_URL_MAX }) },
            {
              validator: (_: unknown, value: string | undefined) =>
                value === undefined || value.trim() === '' || isHttpUrl(value)
                  ? Promise.resolve()
                  : Promise.reject(new Error(t('epgSourcesForm.urlInvalid'))),
            },
          ]}
          extra={t('epgSourcesForm.urlHint')}
        >
          <Input placeholder={t('epgSourcesForm.urlPlaceholder')} />
        </Form.Item>

        <Form.Item
          name="daysKeep"
          label={t('epgSourcesForm.daysKeepLabel')}
          rules={[{ required: true, message: t('epgSourcesForm.daysKeepRequired') }]}
          extra={t('epgSourcesForm.daysKeepHint', {
            min: EPG_DAYS_KEEP_MIN,
            max: EPG_DAYS_KEEP_MAX,
          })}
        >
          <InputNumber
            min={EPG_DAYS_KEEP_MIN}
            max={EPG_DAYS_KEEP_MAX}
            precision={0}
            style={{ width: 160 }}
          />
        </Form.Item>

        <Form.Item style={{ marginTop: 24 }}>
          <Space>
            <Button type="primary" htmlType="submit" loading={submitting}>
              {t(isEdit ? 'epgSources.save' : 'epgSources.create')}
            </Button>
            <Button disabled={submitting} onClick={() => navigate(listPath)}>
              {t('epgSources.backToList')}
            </Button>
          </Space>
        </Form.Item>
      </Form>
    </Card>
  )
}
