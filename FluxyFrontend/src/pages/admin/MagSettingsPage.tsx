import { useEffect, useMemo, useState } from 'react'
import {
  Alert,
  App,
  Button,
  Card,
  Divider,
  Form,
  InputNumber,
  Select,
  Space,
  Spin,
  Switch,
} from 'antd'
import { useTranslation } from 'react-i18next'
import { useProfile } from '../../features/profile/hooks/queries'
import {
  CONTAINER_KEY_OF,
  MAG_CONTAINERS,
  MAG_THEMES,
  STB_TYPES,
  THEME_KEY_OF,
} from '../../features/settings/options'
import { useMagSettings } from '../../features/settings/hooks/queries'
import { useUpdateMagSettings } from '../../features/settings/hooks/mutations'
import {
  MAG_SETTINGS_FIELDS,
  magSettingsToFormValues,
  toMagSettingsBody,
} from '../../features/settings/settingsForm'
import {
  MAX_LOCAL_RECORDINGS_MAX,
  MAX_LOCAL_RECORDINGS_MIN,
  PLAYBACK_LIMIT_MAX,
  PLAYBACK_LIMIT_MIN,
  RECORDING_MINUTES_MAX,
  RECORDING_MINUTES_MIN,
} from '../../features/settings/limits'
import type { MagSettingsValues } from '../../features/settings/types'
import { useUnsavedChangesWarning } from '../../hooks/useUnsavedChangesWarning'
import { hinted } from '../../utils/hinted'
import { ApiError, fieldErrors, messageForError, successTextFor } from '../../utils/http'

interface MagSettingsPageProps {
  /** The menu key this route carries, accepted and deliberately unread. */
  sectionKey: string
}

/**
 * The MAG portal's settings: the second one-row resource, read and written whole.
 *
 * The shape is `SettingsPage`'s - one titled card, one vertical form, one field per row, the save
 * row outside the form so a read-only visitor can still leave, four sections because the eleven
 * values answer four separate questions. Not a table page: no `page-card`, no toolbar.
 *
 * **Both model lists are drawn from the STB catalogue and nowhere else.** The endpoint refuses a
 * name outside it rather than dropping it, so a free-text field would be a field whose typo
 * refuses the whole save. The two lists must also each name at least one model, which the
 * required rule states before the wire does.
 *
 * **The theme and container values are the owner's wire words**, mapped to their own locale keys
 * rather than composed from the value: `ocean blue` has a space in it and `cappucino` is
 * deliberately spelled with one `c`, so a template key would be a key neither bundle has.
 */
export default function MagSettingsPage(_props: MagSettingsPageProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm<MagSettingsValues>()

  const profileQuery = useProfile()
  const granted = profileQuery.data?.permissions ?? []
  const canEdit = granted.includes('editSettings')
  const canView = granted.includes('viewSettings')

  /** The refusal once the profile has answered without the read grant - see `SettingsPage`. */
  const refused = profileQuery.data !== undefined && !canView

  const settingsQuery = useMagSettings({ enabled: !refused })
  const settings = settingsQuery.data ?? null
  const loading = !refused && settingsQuery.isPending
  const loadError =
    !refused && settingsQuery.isError ? messageForError(settingsQuery.error) : null

  const update = useUpdateMagSettings()
  const [dirty, setDirty] = useState(false)
  // No navigation on a save - see `SettingsPage` for why the returned bypass is not kept.
  useUnsavedChangesWarning(dirty)

  const stbOptions = useMemo(() => STB_TYPES.map((type) => ({ value: type, label: type })), [])

  const themeOptions = useMemo(
    () =>
      MAG_THEMES.map((theme) => ({
        value: theme,
        label: t(`magSettingsForm.themes.${THEME_KEY_OF[theme]}`),
      })),
    [t],
  )

  const containerOptions = useMemo(
    () =>
      MAG_CONTAINERS.map((container) => ({
        value: container,
        label: t(`magSettingsForm.containers.${CONTAINER_KEY_OF[container]}`),
      })),
    [t],
  )

  /** Seeds the form from the read, and never over an edit in progress - see `SettingsPage`. */
  useEffect(() => {
    if (!settings || dirty) return

    form.setFieldsValue(magSettingsToFormValues(settings))
  }, [settings, dirty, form])

  const handleFinish = async (values: MagSettingsValues) => {
    form.setFields(MAG_SETTINGS_FIELDS.map((name) => ({ name, errors: [] })))

    try {
      const answer = await update.mutateAsync(toMagSettingsBody(values))

      message.success(successTextFor(answer))
      setDirty(false)
    } catch (error) {
      const fields = fieldErrors(error instanceof ApiError ? error.errors : null)

      if (fields.length > 0) {
        form.setFields(
          fields.map((field) => ({
            name: field.name as keyof MagSettingsValues,
            errors: field.errors,
          })),
        )
      }

      message.error(messageForError(error))
    }
  }

  const title = t('magSettingsForm.title')

  if (refused) {
    return (
      <Card title={title}>
        <Alert type="warning" showIcon message={t('magSettingsForm.noPermission')} />
      </Card>
    )
  }

  if (loading) {
    return (
      <Card title={title}>
        <Spin />
      </Card>
    )
  }

  if (loadError) {
    return (
      <Card title={title}>
        <Alert
          type="error"
          showIcon
          message={loadError}
          action={<Button onClick={() => void settingsQuery.refetch()}>{t('actions.retry')}</Button>}
        />
      </Card>
    )
  }

  const refusedHint = t('magSettingsForm.noPermission')

  const saveButton = (
    <Button
      type="primary"
      loading={update.isPending}
      disabled={!canEdit}
      onClick={() => form.submit()}
    >
      {t('magSettingsForm.save')}
    </Button>
  )

  return (
    <Card title={title}>
      <Form<MagSettingsValues>
        form={form}
        layout="vertical"
        onFinish={handleFinish}
        onValuesChange={() => setDirty(true)}
        disabled={!canEdit}
        style={{ maxWidth: 720 }}
      >
        <Divider titlePlacement="start">{t('magSettingsForm.sections.playback')}</Divider>

        <Form.Item
          name="showAllCategories"
          label={t('magSettingsForm.showAllCategoriesLabel')}
          valuePropName="checked"
        >
          <Switch />
        </Form.Item>

        <Form.Item
          name="strictSecurity"
          label={t('magSettingsForm.strictSecurityLabel')}
          valuePropName="checked"
        >
          <Switch />
        </Form.Item>

        <Form.Item
          name="playbackLimit"
          label={t('magSettingsForm.playbackLimitLabel')}
          rules={[
            {
              type: 'number',
              min: PLAYBACK_LIMIT_MIN,
              max: PLAYBACK_LIMIT_MAX,
              message: t('magSettingsForm.numberRange', {
                min: PLAYBACK_LIMIT_MIN,
                max: PLAYBACK_LIMIT_MAX,
              }),
            },
          ]}
        >
          <InputNumber
            min={PLAYBACK_LIMIT_MIN}
            max={PLAYBACK_LIMIT_MAX}
            precision={0}
            style={{ width: 220 }}
          />
        </Form.Item>

        <Divider titlePlacement="start">{t('magSettingsForm.sections.appearance')}</Divider>

        <Form.Item
          name="showChannelLogos"
          label={t('magSettingsForm.showChannelLogosLabel')}
          valuePropName="checked"
        >
          <Switch />
        </Form.Item>

        <Form.Item
          name="showPreviewLogos"
          label={t('magSettingsForm.showPreviewLogosLabel')}
          valuePropName="checked"
        >
          <Switch />
        </Form.Item>

        <Form.Item
          name="defaultTheme"
          label={t('magSettingsForm.defaultThemeLabel')}
          rules={[{ required: true, message: t('magSettingsForm.required') }]}
        >
          <Select options={themeOptions} />
        </Form.Item>

        <Form.Item
          name="defaultContainer"
          label={t('magSettingsForm.defaultContainerLabel')}
          rules={[{ required: true, message: t('magSettingsForm.required') }]}
        >
          <Select options={containerOptions} />
        </Form.Item>

        <Divider titlePlacement="start">{t('magSettingsForm.sections.recording')}</Divider>

        <Form.Item
          name="recordingMaxLengthMinutes"
          label={t('magSettingsForm.recordingMaxLengthMinutesLabel')}
          rules={[
            {
              type: 'number',
              min: RECORDING_MINUTES_MIN,
              max: RECORDING_MINUTES_MAX,
              message: t('magSettingsForm.numberRange', {
                min: RECORDING_MINUTES_MIN,
                max: RECORDING_MINUTES_MAX,
              }),
            },
          ]}
        >
          <InputNumber
            min={RECORDING_MINUTES_MIN}
            max={RECORDING_MINUTES_MAX}
            precision={0}
            style={{ width: 220 }}
          />
        </Form.Item>

        <Form.Item
          name="maxLocalRecordings"
          label={t('magSettingsForm.maxLocalRecordingsLabel')}
          extra={t('magSettingsForm.maxLocalRecordingsHint')}
          rules={[
            {
              type: 'number',
              min: MAX_LOCAL_RECORDINGS_MIN,
              max: MAX_LOCAL_RECORDINGS_MAX,
              message: t('magSettingsForm.numberRange', {
                min: MAX_LOCAL_RECORDINGS_MIN,
                max: MAX_LOCAL_RECORDINGS_MAX,
              }),
            },
          ]}
        >
          <InputNumber
            min={MAX_LOCAL_RECORDINGS_MIN}
            max={MAX_LOCAL_RECORDINGS_MAX}
            precision={0}
            style={{ width: 220 }}
          />
        </Form.Item>

        <Form.Item
          name="allowedRecordingStbTypes"
          label={t('magSettingsForm.allowedRecordingStbTypesLabel')}
          rules={[
            {
              validator: (_, value: string[] | undefined) =>
                value !== undefined && value.length > 0
                  ? Promise.resolve()
                  : Promise.reject(new Error(t('magSettingsForm.stbRequired'))),
            },
          ]}
        >
          {/* The catalogue is the only source of these options; a multi-select rather than
              checkboxes because thirty-one boxes would be a wall the operator scrolls through
              twice on one screen. */}
          <Select
            mode="multiple"
            allowClear
            showSearch
            options={stbOptions}
            optionFilterProp="label"
            maxTagCount="responsive"
            aria-label={t('magSettingsForm.allowedRecordingStbTypesLabel')}
          />
        </Form.Item>

        <Divider titlePlacement="start">{t('magSettingsForm.sections.devices')}</Divider>

        <Form.Item
          name="allowedStbTypes"
          label={t('magSettingsForm.allowedStbTypesLabel')}
          rules={[
            {
              validator: (_, value: string[] | undefined) =>
                value !== undefined && value.length > 0
                  ? Promise.resolve()
                  : Promise.reject(new Error(t('magSettingsForm.stbRequired'))),
            },
          ]}
        >
          <Select
            mode="multiple"
            allowClear
            showSearch
            options={stbOptions}
            optionFilterProp="label"
            maxTagCount="responsive"
            aria-label={t('magSettingsForm.allowedStbTypesLabel')}
          />
        </Form.Item>
      </Form>

      {/* Outside the `Form` for the reason `SettingsPage` states: a disabled form would disable
          its own Cancel. There is no list to return to, so Cancel restores the answered row, and
          it stays enabled in every state. */}
      <Space style={{ marginTop: 24 }}>
        {canEdit ? saveButton : hinted(refusedHint, saveButton)}
        <Button
          disabled={update.isPending}
          onClick={() => {
            if (settings) form.setFieldsValue(magSettingsToFormValues(settings))
            setDirty(false)
          }}
        >
          {t('magSettingsForm.cancel')}
        </Button>
      </Space>
    </Card>
  )
}
