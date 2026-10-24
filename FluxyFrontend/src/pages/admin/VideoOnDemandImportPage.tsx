import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  Alert,
  App,
  Button,
  Card,
  Form,
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
  VIDEO_ON_DEMAND_IMPORT_MAX_ENTRIES,
  VIDEO_ON_DEMAND_IMPORT_MAX_FILE_BYTES,
} from '../../features/video-on-demand/api'
import type { VideoOnDemandImportValues } from '../../features/video-on-demand/api'
import { useImportVideoOnDemand } from '../../features/video-on-demand/hooks/mutations'
import type {
  VideoOnDemandImportProblem,
  VideoOnDemandImportResponse,
  VideoOnDemandStatus,
  VideoOnDemandType,
} from '../../features/video-on-demand/types'
import { useUnsavedChangesWarning } from '../../hooks/useUnsavedChangesWarning'
import { ApiError, fieldErrors, messageForError, successTextFor } from '../../utils/http'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'

/** The two delivery kinds a playlist can supply: both take one address per entry. */
const IMPORT_TYPES: VideoOnDemandType[] = ['Relay', 'Redirect']

/** The two states, active first. The form starts inactive, which is the safe default. */
const STATUSES: VideoOnDemandStatus[] = ['Active', 'Inactive']

/**
 * The import form's starting content.
 *
 * `Inactive` because an import can create thousands of videos at once, and publishing them
 * all because a form happened to open on the active state would be a batch job nobody asked
 * for. The endpoint's own default for a missing status is the same word.
 */
const IMPORT_VALUES: VideoOnDemandImportValues = {
  type: 'Relay',
  status: 'Inactive',
  categoryId: '',
}

/** Every field this form draws, so a refusal can be cleared before the next attempt. */
const FIELD_NAMES: readonly (keyof VideoOnDemandImportValues)[] = [
  'type',
  'status',
  'categoryId',
]

interface VideoOnDemandImportPageProps {
  /** The menu key this route carries, accepted and deliberately unread. */
  sectionKey: string
}

/**
 * The administrator's playlist import for on-demand videos: one M3U file in, a category's
 * worth of rows out.
 *
 * The form is the create form without the name and the source - each `#EXTINF` entry names
 * its own video and carries its own address - and without the metadata the file cannot
 * supply. What remains is the frame every created row shares: where it is filed, whether it
 * opens published, and how it is delivered.
 */
export default function VideoOnDemandImportPage(_props: VideoOnDemandImportPageProps) {
  const navigate = useNavigate()
  const { t } = useTranslation()
  const { message } = App.useApp()
  const session = useSession()
  const queryClient = useQueryClient()
  const [form] = Form.useForm<VideoOnDemandImportValues>()

  const [fileList, setFileList] = useState<UploadFile[]>([])
  const [fileError, setFileError] = useState<string | null>(null)
  const [result, setResult] = useState<VideoOnDemandImportResponse | null>(null)
  const [dirty, setDirty] = useState(false)

  useUnsavedChangesWarning(dirty)

  const importVideos = useImportVideoOnDemand()

  const categoriesQuery = useContentCategories(CONTENT_TYPE_PARAMS.VideoOnDemand)
  const categories = categoriesQuery.data?.items ?? []
  const categoriesError = categoriesQuery.isError ? messageForError(categoriesQuery.error) : null

  const area = session ? areaForRole(session.role) : ''
  const listPath = `${area}/video-on-demand`

  useEffect(() => {
    if (categories.length === 0) return
    if (form.getFieldValue('categoryId')) return

    const first = categories[0]
    if (first) form.setFieldsValue({ categoryId: first.id })
  }, [form, categories])

  const categoryOptions = useMemo(
    () => categories.map((category) => ({ value: category.id, label: category.name })),
    [categories],
  )

  const typeOptions = useMemo(
    () => IMPORT_TYPES.map((type) => ({ value: type, label: t(`videoOnDemand.types.${type}`) })),
    [t],
  )

  const statusOptions = useMemo(
    () => STATUSES.map((status) => ({ value: status, label: t(`videoOnDemand.statuses.${status}`) })),
    [t],
  )

  const problemColumns: TableColumnsType<VideoOnDemandImportProblem> = useMemo(
    () => [
      {
        title: t('videoOnDemandImport.columns.line'),
        dataIndex: 'line',
        width: 90,
      },
      {
        title: t('videoOnDemandImport.columns.name'),
        dataIndex: 'name',
        width: 280,
        ellipsis: true,
        render: (name: string | null) => name ?? t('videoOnDemandImport.unnamed'),
      },
      {
        title: t('videoOnDemandImport.columns.outcome'),
        dataIndex: 'skipped',
        width: 140,
        render: (skipped: boolean) => (
          <Tag color={skipped ? 'gold' : 'red'}>
            {skipped ? t('videoOnDemandImport.skippedTag') : t('videoOnDemandImport.failedTag')}
          </Tag>
        ),
      },
      {
        title: t('videoOnDemandImport.columns.reason'),
        dataIndex: 'message',
      },
    ],
    [t],
  )

  const beforeUpload = (file: File) => {
    if (file.size <= VIDEO_ON_DEMAND_IMPORT_MAX_FILE_BYTES) {
      setFileError(null)
      setResult(null)
      setDirty(true)
      return false
    }

    const text = t('videoOnDemandImport.fileTooLarge', {
      size: VIDEO_ON_DEMAND_IMPORT_MAX_FILE_BYTES / (1024 * 1024),
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

  const handleFinish = async (values: VideoOnDemandImportValues) => {
    setFileError(null)

    const picked = fileList[0]?.originFileObj

    if (!picked) {
      setFileError(t('videoOnDemandImport.playlistRequired'))
      return
    }

    form.setFields(FIELD_NAMES.map((name) => ({ name, errors: [] })))

    try {
      const answer = await importVideos.mutateAsync({
        values: {
          type: values.type ?? IMPORT_VALUES.type,
          status: values.status ?? IMPORT_VALUES.status,
          categoryId: values.categoryId ?? '',
        },
        file: picked,
      })

      setResult(answer)
      message.success(
        successTextFor(answer, {
          imported: answer.imported,
          total: answer.total,
          skipped: answer.skipped,
          failed: answer.failed,
        }),
      )
      // The file has been read and its videos created; keeping it selected would invite a
      // second click that imports the same names, and every one of them would be skipped.
      setFileList([])
      setDirty(false)
      await queryClient.invalidateQueries({ queryKey: ['videoOnDemand'] })
    } catch (error) {
      const fields = fieldErrors(error instanceof ApiError ? error.errors : null)
      if (fields.length > 0)
        form.setFields(
          fields.map((field) => ({
            name: field.name as keyof VideoOnDemandImportValues,
            errors: field.errors,
          })),
        )
      message.error(messageForError(error))
    }
  }

  const alertType =
    result && result.failed > 0 ? 'warning' : result && result.skipped > 0 ? 'info' : 'success'

  return (
    <Card title={t('videoOnDemandImport.title')}>
      {result && (
        <div style={{ marginBottom: 24 }}>
          <Alert
            type={alertType}
            showIcon
            message={successTextFor(result)}
            description={
              <Space size="large" wrap>
                <span>{t('videoOnDemandImport.totalCount', { total: result.total })}</span>
                <span>{t('videoOnDemandImport.importedCount', { imported: result.imported })}</span>
                <span>{t('videoOnDemandImport.skippedCount', { skipped: result.skipped })}</span>
                <span>{t('videoOnDemandImport.failedCount', { failed: result.failed })}</span>
              </Space>
            }
            style={{ marginBottom: result.problems.length > 0 ? 16 : 0 }}
          />

          {result.problems.length > 0 && (
            <>
              <Typography.Title level={5} style={{ marginTop: 0 }}>
                {t('videoOnDemandImport.problemsTitle')}
              </Typography.Title>
              <Table<VideoOnDemandImportProblem>
                size="small"
                rowKey={(problem) => `${problem.line}:${problem.code}:${problem.name ?? ''}`}
                columns={problemColumns}
                dataSource={result.problems}
                pagination={{
                  pageSize: 10,
                  showSizeChanger: false,
                  hideOnSinglePage: true,
                  showTotal: (total) => t('videoOnDemandImport.problemsTotal', { total }),
                }}
              />
            </>
          )}
        </div>
      )}

      <Form<VideoOnDemandImportValues>
        form={form}
        layout="vertical"
        initialValues={IMPORT_VALUES}
        onFinish={handleFinish}
        onValuesChange={() => setDirty(true)}
        style={{ maxWidth: 720 }}
      >
        <Form.Item
          label={t('videoOnDemandImport.playlistLabel')}
          required
          validateStatus={fileError ? 'error' : undefined}
          help={fileError ?? undefined}
          extra={t('videoOnDemandImport.playlistHint', {
            entries: VIDEO_ON_DEMAND_IMPORT_MAX_ENTRIES,
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
            <p className="ant-upload-text">{t('videoOnDemandImport.dropHint')}</p>
            <p className="ant-upload-hint">{t('videoOnDemandImport.chooseHint')}</p>
          </Upload.Dragger>
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

        <Form.Item style={{ marginTop: 24 }}>
          <Space>
            <Button type="primary" htmlType="submit" loading={importVideos.isPending}>
              {t('videoOnDemandImport.submit')}
            </Button>
            <Button disabled={importVideos.isPending} onClick={() => navigate(listPath)}>
              {t('videoOnDemand.backToList')}
            </Button>
          </Space>
        </Form.Item>
      </Form>
    </Card>
  )
}
