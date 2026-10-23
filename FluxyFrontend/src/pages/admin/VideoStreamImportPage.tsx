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
  InputNumber,
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
  VIDEO_STREAM_IMPORT_MAX_ENTRIES,
  VIDEO_STREAM_IMPORT_MAX_FILE_BYTES,
} from '../../features/video-streams/api'
import type {
  VideoStreamImportType,
  VideoStreamImportValues,
} from '../../features/video-streams/api'
import {
  acceptsArchive,
  acceptsDelay,
  acceptsRestream,
} from '../../features/video-streams/capabilities'
import { useImportVideoStreams } from '../../features/video-streams/hooks/mutations'
import type {
  VideoStreamImportProblem,
  VideoStreamImportResponse,
  VideoStreamStatus,
} from '../../features/video-streams/types'
import { useUnsavedChangesWarning } from '../../hooks/useUnsavedChangesWarning'
import { ApiError, fieldErrors, messageForError, successTextFor } from '../../utils/http'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'

/**
 * The three kinds a playlist can supply: the ones that relay from a list of addresses.
 *
 * The other three need a series reference, an on-demand reference or a list of stored files,
 * none of which an `#EXTINF` line carries. The service refuses them, so the picker never
 * offers one.
 */
const IMPORT_TYPES: VideoStreamImportType[] = ['Permanent', 'OnDemand', 'Redirect']

/**
 * The import form's starting content.
 *
 * `Stopped` rather than the add form's `Running`: an import can create thousands of streams
 * at once, and starting them all because a form happened to open on the active state would
 * be a batch job the operator never asked for. The server's own default for a missing status
 * is `Stopped` as well, so the form and the endpoint agree before anything is sent.
 */
const IMPORT_VALUES: VideoStreamImportValues = {
  type: 'Permanent',
  status: 'Stopped',
  categoryId: '',
  delayMinutes: 0,
  archiveDays: 0,
  restreamUserAgent: '',
  restreamProxy: '',
  restreamCookies: '',
  restreamHeaders: '',
  appendSourceToExisting: false,
  overwriteExistingSettings: false,
}

const STATUSES: VideoStreamStatus[] = ['Stopped', 'Running']

/**
 * Every field this form draws, so a refusal that landed under one of them on the previous
 * attempt can be cleared before the next attempt is judged.
 */
const FIELD_NAMES: readonly (keyof VideoStreamImportValues)[] = [
  'type',
  'status',
  'categoryId',
  'delayMinutes',
  'archiveDays',
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
 * The settings a kind cannot carry are neutralized rather than sent as whatever the form
 * still holds, for the reason the create form gives: the controls are greyed out for those
 * kinds, so a value here would be one nothing on screen claims - and the archive refusal
 * would turn it into a 400 the form itself built.
 */
const toBody = (values: VideoStreamImportValues): VideoStreamImportValues => {
  const type = values.type ?? IMPORT_VALUES.type
  const restream = acceptsRestream(type)

  return {
    type,
    status: values.status ?? IMPORT_VALUES.status,
    categoryId: values.categoryId ?? '',
    delayMinutes: acceptsDelay(type) ? (values.delayMinutes ?? 0) : 0,
    archiveDays: acceptsArchive(type) ? (values.archiveDays ?? 0) : 0,
    restreamUserAgent: restream ? (values.restreamUserAgent ?? '') : '',
    restreamProxy: restream ? (values.restreamProxy ?? '') : '',
    restreamCookies: restream ? (values.restreamCookies ?? '') : '',
    restreamHeaders: restream ? (values.restreamHeaders ?? '') : '',
    appendSourceToExisting: values.appendSourceToExisting ?? false,
    overwriteExistingSettings: values.overwriteExistingSettings ?? false,
  }
}

interface VideoStreamImportPageProps {
  /**
   * The menu key this route carries, accepted and deliberately unread - the page has a title
   * of its own rather than echoing the item the visitor clicked, the same rule the other
   * admin pages follow.
   */
  sectionKey: string
}

/**
 * The administrator's playlist import: one M3U file in, a category's worth of streams out.
 *
 * The form is the create form without the three things a playlist supplies - the name and
 * the source list - and without the fields an imported row has no business inventing (a
 * logo, an Enigma2 service id, a programme guide). What remains is the frame every created
 * stream shares: where it is filed, whether it starts active, its kind, and the settings a
 * kind may carry.
 *
 * The result is drawn rather than reduced to a toast. An import can skip forty names and
 * refuse five lines, and "imported" as a single sentence would be the one place those facts
 * never appeared.
 */
export default function VideoStreamImportPage(_props: VideoStreamImportPageProps) {
  const navigate = useNavigate()
  const { t } = useTranslation()
  const { message } = App.useApp()
  const session = useSession()
  const queryClient = useQueryClient()
  const [form] = Form.useForm<VideoStreamImportValues>()

  const [fileList, setFileList] = useState<UploadFile[]>([])
  const [fileError, setFileError] = useState<string | null>(null)
  const [result, setResult] = useState<VideoStreamImportResponse | null>(null)
  const [dirty, setDirty] = useState(false)

  // The same guard the create form uses: a chosen file or an edited setting is work the
  // server has not seen, and leaving the page without a word would throw it away.
  useUnsavedChangesWarning(dirty)

  const importStreams = useImportVideoStreams()

  const categoriesQuery = useContentCategories(CONTENT_TYPE_PARAMS.VideoStream)
  const categories = categoriesQuery.data?.items ?? []
  const categoriesError = categoriesQuery.isError ? messageForError(categoriesQuery.error) : null

  const area = session ? areaForRole(session.role) : ''
  const listPath = `${area}/video-streams`

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

  const chosenType: VideoStreamImportType =
    Form.useWatch('type', form) ?? IMPORT_VALUES.type

  const delayAllowed = acceptsDelay(chosenType)
  const archiveAllowed = acceptsArchive(chosenType)
  const restreamAllowed = acceptsRestream(chosenType)

  useEffect(() => {
    // Written here rather than left to the server's refusal, for the reason `toBody`
    // repeats: these fields are greyed out for this kind, so a value would be one nothing
    // on screen claims. Archive is the server's rule; delay and restreaming are the form's.
    if (!acceptsArchive(chosenType)) form.setFieldValue('archiveDays', 0)
    if (!acceptsDelay(chosenType)) form.setFieldValue('delayMinutes', 0)
    if (!acceptsRestream(chosenType)) {
      form.setFieldsValue({
        restreamUserAgent: '',
        restreamProxy: '',
        restreamCookies: '',
        restreamHeaders: '',
      })
    }
  }, [chosenType, form])

  const categoryOptions = useMemo(
    () => categories.map((category) => ({ value: category.id, label: category.name })),
    [categories],
  )

  const typeOptions = useMemo(
    () => IMPORT_TYPES.map((type) => ({ value: type, label: t(`videoStreams.types.${type}`) })),
    [t],
  )

  const statusOptions = useMemo(
    () => STATUSES.map((status) => ({ value: status, label: t(`videoStreams.statuses.${status}`) })),
    [t],
  )

  const problemColumns: TableColumnsType<VideoStreamImportProblem> = useMemo(
    () => [
      {
        title: t('videoStreamsImport.columns.line'),
        dataIndex: 'line',
        width: 90,
      },
      {
        title: t('videoStreamsImport.columns.name'),
        dataIndex: 'name',
        width: 280,
        ellipsis: true,
        render: (name: string | null) => name ?? t('videoStreamsImport.unnamed'),
      },
      {
        title: t('videoStreamsImport.columns.outcome'),
        dataIndex: 'skipped',
        width: 140,
        render: (skipped: boolean) => (
          <Tag color={skipped ? 'gold' : 'red'}>
            {skipped ? t('videoStreamsImport.skippedTag') : t('videoStreamsImport.failedTag')}
          </Tag>
        ),
      },
      {
        title: t('videoStreamsImport.columns.reason'),
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
    if (file.size <= VIDEO_STREAM_IMPORT_MAX_FILE_BYTES) {
      setFileError(null)
      setResult(null)
      setDirty(true)
      return false
    }

    const text = t('videoStreamsImport.fileTooLarge', {
      size: VIDEO_STREAM_IMPORT_MAX_FILE_BYTES / (1024 * 1024),
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

  const handleFinish = async (values: VideoStreamImportValues) => {
    setFileError(null)

    const picked = fileList[0]?.originFileObj

    if (!picked) {
      setFileError(t('videoStreamsImport.playlistRequired'))
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
      await queryClient.invalidateQueries({ queryKey: ['videoStreams'] })
    } catch (error) {
      const fields = fieldErrors(error instanceof ApiError ? error.errors : null)
      if (fields.length > 0)
        form.setFields(
          fields.map((field) => ({
            name: field.name as keyof VideoStreamImportValues,
            errors: field.errors,
          })),
        )
      message.error(messageForError(error))
    }
  }

  const alertType =
    result && result.failed > 0 ? 'warning' : result && result.skipped > 0 ? 'info' : 'success'

  return (
    <Card title={t('videoStreamsImport.title')}>
      {result && (
        <div style={{ marginBottom: 24 }}>
          <Alert
            type={alertType}
            showIcon
            message={successTextFor(result)}
            description={
              <Space size="large" wrap>
                <span>{t('videoStreamsImport.totalCount', { total: result.total })}</span>
                <span>{t('videoStreamsImport.importedCount', { imported: result.imported })}</span>
                <span>{t('videoStreamsImport.updatedCount', { updated: result.updated })}</span>
                <span>{t('videoStreamsImport.skippedCount', { skipped: result.skipped })}</span>
                <span>{t('videoStreamsImport.failedCount', { failed: result.failed })}</span>
              </Space>
            }
            style={{ marginBottom: result.problems.length > 0 ? 16 : 0 }}
          />

          {result.problems.length > 0 && (
            <>
              <Typography.Title level={5} style={{ marginTop: 0 }}>
                {t('videoStreamsImport.problemsTitle')}
              </Typography.Title>
              <Table<VideoStreamImportProblem>
                size="small"
                rowKey={(problem) => `${problem.line}:${problem.code}:${problem.name ?? ''}`}
                columns={problemColumns}
                dataSource={result.problems}
                pagination={{
                  pageSize: 10,
                  showSizeChanger: false,
                  hideOnSinglePage: true,
                  showTotal: (total) => t('videoStreamsImport.problemsTotal', { total }),
                }}
              />
            </>
          )}
        </div>
      )}

      <Form<VideoStreamImportValues>
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
          label={t('videoStreamsImport.playlistLabel')}
          required
          validateStatus={fileError ? 'error' : undefined}
          help={fileError ?? undefined}
          extra={t('videoStreamsImport.playlistHint', {
            entries: VIDEO_STREAM_IMPORT_MAX_ENTRIES,
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
            <p className="ant-upload-text">{t('videoStreamsImport.dropHint')}</p>
            <p className="ant-upload-hint">{t('videoStreamsImport.chooseHint')}</p>
          </Upload.Dragger>
        </Form.Item>

        <Form.Item
          name="categoryId"
          label={t('videoStreams.columns.category')}
          // Required, and there is no "no category" state for an imported stream either: the
          // position within the category, the uniqueness of every name and the whole grouping
          // of the table are read out of wherever this lands.
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
          <Alert type="error" showIcon style={{ marginBottom: 16 }} message={categoriesError} />
        )}

        <Form.Item
          name="status"
          label={t('videoStreams.columns.status')}
          rules={[{ required: true, message: t('videoStreamsForm.statusRequired') }]}
          extra={t('videoStreamsForm.statusHint')}
        >
          <Select options={statusOptions} />
        </Form.Item>

        <Form.Item
          name="type"
          label={t('videoStreams.columns.type')}
          rules={[{ required: true, message: t('videoStreamsForm.typeRequired') }]}
          extra={t(`videoStreamsForm.hints.${chosenType}`)}
        >
          <Select options={typeOptions} />
        </Form.Item>

        <Divider titlePlacement="start">{t('videoStreamsImport.existingDivider')}</Divider>

        <Form.Item
          name="appendSourceToExisting"
          valuePropName="checked"
          extra={t('videoStreamsImport.appendSourceHint')}
        >
          <Checkbox>{t('videoStreamsImport.appendSourceLabel')}</Checkbox>
        </Form.Item>

        <Form.Item
          name="overwriteExistingSettings"
          valuePropName="checked"
          extra={t('videoStreamsImport.overwriteSettingsHint')}
        >
          <Checkbox>{t('videoStreamsImport.overwriteSettingsLabel')}</Checkbox>
        </Form.Item>

        <Divider titlePlacement="start">{t('videoStreamsForm.timingDivider')}</Divider>

        <Form.Item
          name="delayMinutes"
          label={t('videoStreamsForm.delayLabel')}
          rules={[
            {
              validator: (_: unknown, value: number | null) =>
                (value ?? 0) >= 0
                  ? Promise.resolve()
                  : Promise.reject(new Error(t('videoStreamsForm.nonNegative'))),
            },
          ]}
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
            disabled={!archiveAllowed}
          />
        </Form.Item>

        <Divider titlePlacement="start">{t('videoStreamsForm.restreamDivider')}</Divider>

        {!restreamAllowed && (
          <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 16 }}>
            {t('videoStreamsForm.restreamPermanentOnDemandOnly')}
          </Typography.Text>
        )}

        <Form.Item
          name="restreamUserAgent"
          label={t('videoStreamsForm.userAgentLabel')}
          rules={[
            { max: 500, message: t('videoStreamsForm.maxLength', { max: 500 }) },
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
          rules={[
            { max: 500, message: t('videoStreamsForm.maxLength', { max: 500 }) },
          ]}
        >
          <Input
            placeholder={t('videoStreamsForm.proxyPlaceholder')}
            disabled={!restreamAllowed}
          />
        </Form.Item>

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

        <Form.Item style={{ marginTop: 24 }}>
          <Space>
            <Button type="primary" htmlType="submit" loading={importStreams.isPending}>
              {t('videoStreamsImport.submit')}
            </Button>
            <Button disabled={importStreams.isPending} onClick={() => navigate(listPath)}>
              {t('videoStreams.backToList')}
            </Button>
          </Space>
        </Form.Item>
      </Form>
    </Card>
  )
}
