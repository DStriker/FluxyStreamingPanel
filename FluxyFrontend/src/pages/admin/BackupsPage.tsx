import { useState } from 'react'
import { Alert, App, Button, Card, Space, Table, Tag, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { DeleteOutlined, PlusOutlined, UndoOutlined } from '@ant-design/icons'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import type { BackupFile } from '../../features/backups/types'
import { useBackups } from '../../features/backups/hooks/queries'
import {
  useCreateBackup,
  useDeleteBackup,
  useRestoreBackup,
} from '../../features/backups/hooks/mutations'
import { formatSizeBytes } from '../../features/backups/size'
import { useSettings } from '../../features/settings/hooks/queries'
import { useProfile } from '../../features/profile/hooks/queries'
import { LoadErrorAlert, RowActions, TruncatedCell } from '../../components/table'
import type { RowAction } from '../../components/table'
import { useRowAction } from '../../hooks/table'
import { formatTimestamp } from '../../utils/dateFormat'
import { hinted } from '../../utils/hinted'
import { messageForError, successTextFor } from '../../utils/http'
import { useSession } from '../../utils/sessionContext'
import { areaForRole } from '../../utils/session'
import { actionsColumnWidth } from '../../utils/tableWidths'

interface BackupsPageProps {
  /** The menu key the route carries; accepted and deliberately unread - the card names the list. */
  sectionKey: string
}

/**
 * The database archives kept on the API host: the list, the create, the restore and the delete.
 *
 * **The three backup settings are echoed, never edited.** They stay editable only on
 * `/admin/settings`, whose PATCH is a full write of the whole row: a second editing surface here
 * would have to send seventeen values it never showed. So the card body carries one read-only row
 * and a link to the page that owns them.
 *
 * **The table is a plain client-side `Table`, not `ServerTable`.** The endpoint answers the whole
 * directory - it is bounded by the rotation, not paged - so a server-table frame would add a pager
 * over a list the server already sent in full, and a second page that is empty by construction.
 *
 * **A restore is not a row action like the others.** It replaces the database: everyone signed in
 * after the backup is signed out, the log rows written since it are gone, and there is a short
 * interruption. The dialog says all four facts rather than asking "restore?" over a verb the
 * operator already pressed, and the success is followed by a standing warning that their own
 * session may be one of the ones that ended.
 */
export default function BackupsPage(_props: BackupsPageProps) {
  const { t, i18n } = useTranslation()
  const { message, modal } = App.useApp()
  const session = useSession()

  const backupsQuery = useBackups()
  const items = backupsQuery.data ?? []
  const loadError = backupsQuery.isError ? messageForError(backupsQuery.error) : null
  const loading = backupsQuery.isFetching

  // The settings echo is a courtesy, not the screen: a refusal or a failed read draws dashes
  // rather than an error banner above a table that loaded perfectly well.
  const settingsQuery = useSettings()
  const settings = settingsQuery.data

  const granted = useProfile().data?.permissions ?? []
  const canEdit = granted.includes('editSettings')

  const createBackup = useCreateBackup()
  const restoreBackup = useRestoreBackup()
  const deleteBackup = useDeleteBackup()

  const { busyId, confirmDelete } = useRowAction<{ id: string }>({
    refetch: () => void backupsQuery.refetch(),
  })

  /** Set once a restore succeeded: the one fact that outlives the toast. */
  const [restored, setRestored] = useState(false)

  const language = i18n.resolvedLanguage ?? i18n.language

  /**
   * The row whose action is running, including the restore - which is not `useRowAction`'s,
   * because its success has a consequence to report and its own spinner comes from the mutation's
   * `variables`, as every row action on this side of the application does.
   */
  const restoreFileName = restoreBackup.isPending ? restoreBackup.variables?.fileName : undefined
  const acting = busyId ?? restoreFileName ?? null

  /**
   * Whether *any* write of this screen is in flight. A create, a restore and a delete all pass
   * through one server-side gate, so a second one pressed while the first runs would be answered
   * `409 backup_busy` - the controls say so before the click rather than after it.
   */
  const anyWrite = acting !== null || createBackup.isPending

  const periodLabel = settings
    ? t(`settingsForm.backupPeriods.${settings.backupPeriod}`)
    : '—'

  const runCreate = async () => {
    try {
      const result = await createBackup.mutateAsync({})

      message.success(successTextFor(result))
      void backupsQuery.refetch()
    } catch (error) {
      message.error(messageForError(error))
    }
  }

  const confirmRestore = (file: BackupFile) => {
    modal.confirm({
      title: t('backups.confirm.restoreTitle'),
      content: t('backups.confirm.restoreText'),
      okText: t('backups.confirm.restoreOk'),
      okButtonProps: { danger: true },
      cancelText: t('actions.cancel'),
      onOk: async () => {
        try {
          const result = await restoreBackup.mutateAsync({ fileName: file.fileName })

          message.success(successTextFor(result))
          setRestored(true)
          void backupsQuery.refetch()
        } catch (error) {
          message.error(messageForError(error))
        }
      },
    })
  }

  const askDelete = (file: BackupFile) =>
    confirmDelete(
      { id: file.fileName },
      {
        name: file.fileName,
        titleKey: 'backups.confirm.deleteTitle',
        bodyKey: 'backups.confirm.deleteText',
        okKey: 'backups.confirm.deleteOk',
        run: () => deleteBackup.mutateAsync({ fileName: file.fileName }),
      },
    )

  const columns: TableColumnsType<BackupFile> = [
    {
      title: t('backups.column.fileName'),
      dataIndex: 'fileName',
      key: 'fileName',
      ellipsis: true,
      render: (fileName: string) => <TruncatedCell text={fileName} full={fileName} />,
    },
    {
      title: t('backups.column.size'),
      dataIndex: 'sizeBytes',
      key: 'sizeBytes',
      width: 120,
      align: 'right',
      render: (sizeBytes: number) => formatSizeBytes(sizeBytes),
    },
    {
      title: t('backups.column.createdAt'),
      dataIndex: 'createdAtUtc',
      key: 'createdAtUtc',
      width: 180,
      render: (createdAtUtc: string) => formatTimestamp(createdAtUtc, language, null),
    },
    {
      title: t('backups.column.actions'),
      key: 'actions',
      // The +16 is the room the heading needs: `actionsColumnWidth` is measured against the two
      // icon buttons, and a translated heading ("Действия") is wider than the English one and
      // would otherwise wrap onto two lines inside a column that is only as wide as its buttons.
      width: actionsColumnWidth(2) + 16,
      render: (_, file) => {
        const busy = acting === file.fileName
        const blocked = anyWrite

        const actions: RowAction[] = [
          {
            key: 'restore',
            label: t('backups.restore'),
            icon: <UndoOutlined />,
            danger: true,
            loading: busy && restoreFileName === file.fileName,
            disabled: blocked || !canEdit,
            onClick: () => confirmRestore(file),
          },
          {
            key: 'delete',
            label: t('backups.delete'),
            icon: <DeleteOutlined />,
            danger: true,
            loading: busy && busyId === file.fileName,
            disabled: blocked || !canEdit,
            onClick: () => askDelete(file),
          },
        ]

        return <RowActions actions={actions} />
      },
    },
  ]

  const createDisabled = !canEdit || acting !== null

  const createButton = (
    <Button
      type="primary"
      icon={<PlusOutlined />}
      loading={createBackup.isPending}
      disabled={createDisabled}
      onClick={() => void runCreate()}
    >
      {t('backups.create')}
    </Button>
  )

  /**
   * The one control that can be greyed for two different reasons, so the reason it is greyed
   * travels with it: a visitor without the write grant, or a write that is already running. A
   * disabled button swallows the pointer events a tooltip listens for, which is why `hinted`'s
   * span is the only way either sentence is reachable.
   */
  const createWithReason = !canEdit
    ? hinted(t('settingsForm.noPermission'), createButton)
    : acting !== null
      ? hinted(t('backups.writeInProgress'), createButton)
      : createButton

  return (
    <Card
      className="page-card"
      title={t('backups.listTitle')}
      styles={{ title: { whiteSpace: 'nowrap' } }}
      extra={createWithReason}
    >
      <Space size="large" wrap style={{ marginBottom: 16 }}>
        <Space size={4}>
          <Typography.Text type="secondary">{t('backups.settings.enabled')}</Typography.Text>
          {settings ? (
            <Tag color={settings.backupEnabled ? 'green' : 'default'}>
              {settings.backupEnabled
                ? t('backups.settings.enabledOn')
                : t('backups.settings.enabledOff')}
            </Tag>
          ) : (
            <Typography.Text>—</Typography.Text>
          )}
        </Space>
        <Space size={4}>
          <Typography.Text type="secondary">{t('backups.settings.period')}</Typography.Text>
          <Typography.Text>{periodLabel}</Typography.Text>
        </Space>
        <Space size={4}>
          <Typography.Text type="secondary">{t('backups.settings.rotation')}</Typography.Text>
          <Typography.Text>
            {settings ? settings.backupRotationCount : '—'}
            {settings && settings.backupRotationCount === 0
              ? ` (${t('settingsForm.backupRotationCountHint')})`
              : ''}
          </Typography.Text>
        </Space>
        <Link to={`${session ? areaForRole(session.role) : '/admin'}/settings`}>
          {t('backups.settings.editLink')}
        </Link>
      </Space>

      {restored && (
        <Alert
          type="warning"
          showIcon
          closable
          message={t('backups.restoreNotice')}
          onClose={() => setRestored(false)}
          style={{ marginBottom: 16 }}
        />
      )}

      {loadError ? (
        <LoadErrorAlert
          message={loadError}
          loading={loading}
          onRetry={() => void backupsQuery.refetch()}
        />
      ) : (
        <Table<BackupFile>
          rowKey="fileName"
          columns={columns}
          dataSource={items}
          loading={loading}
          pagination={false}
        />
      )}
    </Card>
  )
}
