import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  Alert,
  App,
  Button,
  Card,
  Checkbox,
  Divider,
  Form,
  Input,
  Select,
  Space,
  Table,
  Tag,
  Typography,
  Upload,
} from 'antd'
import { InboxOutlined } from '@ant-design/icons'
import type { TableColumnsType, UploadFile } from 'antd'
import { useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { CONTENT_TYPE_PARAMS } from '../../features/content-categories/api'
import { useContentCategories } from '../../features/content-categories/hooks/queries'
import {
  AUDIO_STREAM_IMPORT_MAX_ENTRIES,
  AUDIO_STREAM_IMPORT_MAX_FILE_BYTES,
} from '../../features/audio-streams/api'
import type {
  AudioStreamImportType,
  AudioStreamImportValues,
} from '../../features/audio-streams/api'
import { useImportAudioStreams } from '../../features/audio-streams/hooks/mutations'
import type {
  AudioStreamImportProblem,
  AudioStreamImportResponse,
  AudioStreamStatus,
} from '../../features/audio-streams/types'
import { useUnsavedChangesWarning } from '../../hooks/useUnsavedChangesWarning'
import { ApiError, fieldErrors, messageForError, successTextFor } from '../../utils/http'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'

/**
 * The two kinds a playlist can supply: the ones that relay from a list of addresses.
 *
 * Both of them, and that is the whole difference from the video import: an audio stream has
 * only two kinds and both carry the same payload, so there is no kind here the service would
 * refuse.
 */
const IMPORT_TYPES: AudioStreamImportType[] = ['OnDemand', 'Redirect']

/**
 * The import form's starting content.
 *
 * `Stopped` rather than the add form's `Running`: an import can create thousands of streams
 * at once, and starting them all because a form happened to open on the active state would
 * be a batch job the operator never asked for. The server's own default for a missing status
 * is `Stopped` as well, so the form and the endpoint agree before anything is sent.
 */
const IMPORT_VALUES: AudioStreamImportValues = {
  type: 'OnDemand',
  status: 'Stopped',
  categoryId: '',
  restreamUserAgent: '',
  restreamProxy: '',
  restreamCookies: '',
  restreamHeaders: '',
  appendSourceToExisting: false,
  overwriteExistingSettings: false,
}

const STATUSES: AudioStreamStatus[] = ['Stopped', 'Running']

/**
 * Every field this form draws, so a refusal that landed under one of them on the previous
 * attempt can be cleared before the next attempt is judged.
 */
const FIELD_NAMES: readonly (keyof AudioStreamImportValues)[] = [
  'type',
  'status',
  'categoryId',
  'restreamUserAgent',
  'restreamProxy',
  'restreamCookies',
  'restreamHeaders',
  'appendSourceToExisting',
  'overwriteExistingSettings',
]

/**
 * What the form holds, spelled as the multipart fields the endpoint reads.
 *
 * The `??` on every field is the same rule the create form follows: a field the visitor
 * never opened is not in the store at all, and a body that asked the server about
 * `undefined` would be a request the endpoint cannot describe.
 */
const toBody = (values: AudioStreamImportValues): AudioStreamImportValues => ({
  type: values.type ?? IMPORT_VALUES.type,
  status: values.status ?? IMPORT_VALUES.status,
  categoryId: values.categoryId ?? '',
  restreamUserAgent: values.restreamUserAgent ?? '',
  restreamProxy: values.restreamProxy ?? '',
  restreamCookies: values.restreamCookies ?? '',
  restreamHeaders: values.restreamHeaders ?? '',
  appendSourceToExisting: values.appendSourceToExisting ?? false,
  overwriteExistingSettings: values.overwriteExistingSettings ?? false,
})

interface AudioStreamImportPageProps {
  /**
   * The menu key this route carries, accepted and deliberately unread - the page has a title
   * of its own rather than echoing the item the visitor clicked, the same rule the other
   * admin pages follow.
   */
  sectionKey: string
}

/**
 * The administrator's playlist import: one M3U file in, a category's worth of audio streams
 * out.
 *
 * The form is the create form without the three things a playlist supplies - the name and
 * the source list - and without the fields an imported row has no business inventing (a
 * logo, an Enigma2 service id). What remains is the frame every created stream shares:
 * where it is filed, whether it starts active, its kind, and the restreaming settings every
 * kind carries.
 *
 * The result is drawn rather than reduced to a toast. An import can skip forty names and
 * refuse five lines, and "imported" as a single sentence would be the one place those facts
 * never appeared.
 */
export default function AudioStreamImportPage(_props: AudioStreamImportPageProps) {
  const navigate = useNavigate()
  const { t } = useTranslation()
  const { message } = App.useApp()
  const session = useSession()
  const queryClient = useQueryClient()
  const [form] = Form.useForm<AudioStreamImportValues>()

  const [fileList, setFileList] = useState<UploadFile[]>([])
  const [fileError, setFileError] = useState<string | null>(null)
  const [result, setResult] = useState<AudioStreamImportResponse | null>(null)
  const [dirty, setDirty] = useState(false)

  // The same guard the create form uses: a chosen file or an edited setting is work the
  // server has not seen, and leaving the page without a word would throw it away.
  useUnsavedChangesWarning(dirty)

  const importStreams = useImportAudioStreams()

  const categoriesQuery = useContentCategories(CONTENT_TYPE_PARAMS.AudioStream)
  const categories = categoriesQuery.data?.items ?? []
  const categoriesError = categoriesQuery.isError ? messageForError(categoriesQuery.error) : null

  const area = session ? areaForRole(session.role) : ''
  const listPath = `${area}/audio-streams`

  useEffect(() => {
    // The first category the operator arranged, chosen for the same reason the create form
    // chooses it: a stream must be filed under *a* category, and the picker's first entry
    // is where one that has not said otherwise belongs. Written only while nothing is
    // chosen, so a reload or a back button keeps the visitor's own answer.
    if (categories.length === 0) return
    if (form.getFieldValue('categoryId')) return

    const first = categories[0]
    if (first) form.setFieldsValue({ categoryId: first.id })
  }, [form, categories])

  const chosenType: AudioStreamImportType = Form.useWatch('type', form) ?? IMPORT_VALUES.type

  const categoryOptions = useMemo(
    () => categories.map((category) => ({ value: category.id, label: category.name })),
    [categories],
  )

  const typeOptions = useMemo(
    () => IMPORT_TYPES.map((type) => ({ value: type, label: t(`audioStreams.types.${type}`) })),
    [t],
  )

  const statusOptions = useMemo(
    () => STATUSES.map((status) => ({ value: status, label: t(`audioStreams.statuses.${status}`) })),
    [t],
  )

  const problemColumns: TableColumnsType<AudioStreamImportProblem> = useMemo(
    () => [
      {
        title: t('audioStreamsImport.columns.line'),
        dataIndex: 'line',
        width: 90,
      },
      {
        title: t('audioStreamsImport.columns.name'),
        dataIndex: 'name',
        width: 280,
        ellipsis: true,
        render: (name: string | null) => name ?? t('audioStreamsImport.unnamed'),
      },
      {
        title: t('audioStreamsImport.columns.outcome'),
        dataIndex: 'skipped',
        width: 140,
        render: (skipped: boolean) => (
          <Tag color={skipped ? 'gold' : 'red'}>
            {skipped ? t('audioStreamsImport.skippedTag') : t('audioStreamsImport.failedTag')}
          </Tag>
        ),
      },
      {
        title: t('audioStreamsImport.columns.reason'),
        dataIndex: 'message',
      },
    ],
    [t],
  )

  /**
   * The browser's own check before the wire.
   *
   * The server checks the size again and its 413 is the answer that counts - this one exists
   * so a mistaken upload of a huge file is refused without spending the connection on it.
   * The entry ceiling is not checked here: counting `#EXTINF` lines in the browser would be a
   * second parser, and the server's parser is the one whose count is the contract.
   */
  const beforeUpload = (file: File) => {
    if (file.size <= AUDIO_STREAM_IMPORT_MAX_FILE_BYTES) {
      setFileError(null)
      setResult(null)
      setDirty(true)
      return false
    }

    const text = t('audioStreamsImport.fileTooLarge', {
      size: AUDIO_STREAM_IMPORT_MAX_FILE_BYTES / (1024 * 1024),
    })

    message.error(text)
    setFileError(text)
    setFileList([])
    return Upload.LIST_IGNORE
  }

  const handleFileChange = (info: { fileList: UploadFile[] }) => {
    setFileList(info.fileList.slice(-1))
    setFileError(null)
    setResult(null)
    setDirty(true)
  }

  const handleRemove = () => {
    setFileList([])
    setResult(null)
    setDirty(true)
    return true
  }

  const handleFinish = async (values: AudioStreamImportValues) => {
    setFileError(null)

    const picked = fileList[0]?.originFileObj

    if (!picked) {
      setFileError(t('audioStreamsImport.playlistRequired'))
      return
    }

    form.setFields(FIELD_NAMES.map((name) => ({ name, errors: [] })))

    try {
      const answer = await importStreams.mutateAsync({ values: toBody(values), file: picked })

      setResult(answer)
      message.success(
        successTextFor(answer, {
          imported: answer.imported,
          total: answer.total,
          skipped: answer.skipped,
          failed: answer.failed,
        }),
      )
      // The file has been read and its streams created; keeping it selected would invite a
      // second click that imports the same names, and every one of them would be skipped.
      setFileList([])
      setDirty(false)
      await queryClient.invalidateQueries({ queryKey: ['audioStreams'] })
    } catch (error) {
      const fields = fieldErrors(error instanceof ApiError ? error.errors : null)
      if (fields.length > 0)
        form.setFields(
          fields.map((field) => ({
            name: field.name as keyof AudioStreamImportValues,
            errors: field.errors,
          })),
        )
      message.error(messageForError(error))
    }
  }

  const alertType =
    result && result.failed > 0 ? 'warning' : result && result.skipped > 0 ? 'info' : 'success'

  return (
    <Card title={t('audioStreamsImport.title')}>
      {result && (
        <div style={{ marginBottom: 24 }}>
          <Alert
            type={alertType}
            showIcon
            message={successTextFor(result)}
            description={
              <Space size="large" wrap>
                <span>{t('audioStreamsImport.totalCount', { total: result.total })}</span>
                <span>{t('audioStreamsImport.importedCount', { imported: result.imported })}</span>
                <span>{t('audioStreamsImport.updatedCount', { updated: result.updated })}</span>
                <span>{t('audioStreamsImport.skippedCount', { skipped: result.skipped })}</span>
                <span>{t('audioStreamsImport.failedCount', { failed: result.failed })}</span>
              </Space>
            }
            style={{ marginBottom: result.problems.length > 0 ? 16 : 0 }}
          />

          {result.problems.length > 0 && (
            <>
              <Typography.Title level={5} style={{ marginTop: 0 }}>
                {t('audioStreamsImport.problemsTitle')}
              </Typography.Title>
              <Table<AudioStreamImportProblem>
                size="small"
                rowKey={(problem) => `${problem.line}:${problem.code}:${problem.name ?? ''}`}
                columns={problemColumns}
                dataSource={result.problems}
                pagination={{
                  pageSize: 10,
                  showSizeChanger: false,
                  hideOnSinglePage: true,
                  showTotal: (total) => t('audioStreamsImport.problemsTotal', { total }),
                }}
              />
            </>
          )}
        </div>
      )}

      <Form<AudioStreamImportValues>
        form={form}
        layout="vertical"
        initialValues={IMPORT_VALUES}
        onFinish={handleFinish}
        onValuesChange={() => setDirty(true)}
        style={{ maxWidth: 720 }}
      >
        {/* The playlist comes first: it is the one thing the page is for, and everything
            below describes what to do with the streams it names. */}
        <Form.Item
          label={t('audioStreamsImport.playlistLabel')}
          required
          validateStatus={fileError ? 'error' : undefined}
          help={fileError ?? undefined}
          extra={t('audioStreamsImport.playlistHint', {
            entries: AUDIO_STREAM_IMPORT_MAX_ENTRIES,
          })}
        >
          <Upload.Dragger
            accept=".m3u,.m3u8"
            maxCount={1}
            fileList={fileList}
            beforeUpload={beforeUpload}
            onChange={handleFileChange}
            onRemove={handleRemove}
          >
            <p className="ant-upload-drag-icon">
              <InboxOutlined />
            </p>
            <p className="ant-upload-text">{t('audioStreamsImport.dropHint')}</p>
            <p className="ant-upload-hint">{t('audioStreamsImport.chooseHint')}</p>
          </Upload.Dragger>
        </Form.Item>

        <Form.Item
          name="categoryId"
          label={t('audioStreams.columns.category')}
          // Required, and there is no "no category" state for an imported stream either: the
          // uniqueness of every name and the whole grouping of the table are read out of
          // wherever this lands.
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
          <Alert type="error" showIcon style={{ marginBottom: 16 }} message={categoriesError} />
        )}

        <Form.Item
          name="status"
          label={t('audioStreams.columns.status')}
          rules={[{ required: true, message: t('audioStreamsForm.statusRequired') }]}
          extra={t('audioStreamsForm.statusHint')}
        >
          <Select options={statusOptions} />
        </Form.Item>

        <Form.Item
          name="type"
          label={t('audioStreams.columns.type')}
          rules={[{ required: true, message: t('audioStreamsForm.typeRequired') }]}
          extra={t(`audioStreamsForm.hints.${chosenType}`)}
        >
          <Select options={typeOptions} />
        </Form.Item>

        <Divider titlePlacement="start">{t('audioStreamsImport.existingDivider')}</Divider>

        <Form.Item
          name="appendSourceToExisting"
          valuePropName="checked"
          extra={t('audioStreamsImport.appendSourceHint')}
        >
          <Checkbox>{t('audioStreamsImport.appendSourceLabel')}</Checkbox>
        </Form.Item>

        <Form.Item
          name="overwriteExistingSettings"
          valuePropName="checked"
          extra={t('audioStreamsImport.overwriteSettingsHint')}
        >
          <Checkbox>{t('audioStreamsImport.overwriteSettingsLabel')}</Checkbox>
        </Form.Item>

        <Divider titlePlacement="start">{t('audioStreamsForm.restreamDivider')}</Divider>

        {/* Both kinds carry these four, so they are drawn once and never greyed - the
            difference from the video import, where a kind that could not hold them had to
            say so under each field. */}
        <Form.Item
          name="restreamUserAgent"
          label={t('audioStreamsForm.userAgentLabel')}
          rules={[
            { max: 500, message: t('audioStreamsForm.maxLength', { max: 500 }) },
          ]}
        >
          <Input placeholder={t('audioStreamsForm.userAgentPlaceholder')} />
        </Form.Item>

        <Form.Item
          name="restreamProxy"
          label={t('audioStreamsForm.proxyLabel')}
          rules={[
            { max: 500, message: t('audioStreamsForm.maxLength', { max: 500 }) },
          ]}
        >
          <Input placeholder={t('audioStreamsForm.proxyPlaceholder')} />
        </Form.Item>

        <Form.Item name="restreamCookies" label={t('audioStreamsForm.cookiesLabel')}>
          <Input.TextArea rows={3} placeholder={t('audioStreamsForm.cookiesPlaceholder')} />
        </Form.Item>

        <Form.Item name="restreamHeaders" label={t('audioStreamsForm.headersLabel')}>
          <Input.TextArea rows={3} placeholder={t('audioStreamsForm.headersPlaceholder')} />
        </Form.Item>

        <Form.Item style={{ marginTop: 24 }}>
          <Space>
            <Button type="primary" htmlType="submit" loading={importStreams.isPending}>
              {t('audioStreamsImport.submit')}
            </Button>
            <Button disabled={importStreams.isPending} onClick={() => navigate(listPath)}>
              {t('audioStreams.backToList')}
            </Button>
          </Space>
        </Form.Item>
      </Form>
    </Card>
  )
}
