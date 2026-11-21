import { useEffect, useMemo, useState } from 'react'
import {
  Alert,
  App,
  Button,
  Card,
  Divider,
  Form,
  Input,
  InputNumber,
  Select,
  Space,
  Spin,
  Switch,
  Typography,
} from 'antd'
import { useTranslation } from 'react-i18next'
import { useProfile } from '../../features/profile/hooks/queries'
import { BACKUP_PERIODS } from '../../features/settings/options'
import { useSettings } from '../../features/settings/hooks/queries'
import { useUpdateSettings } from '../../features/settings/hooks/mutations'
import {
  SETTINGS_FIELDS,
  isValidFallbackUrl,
  isValidWhitelist,
  settingsToFormValues,
  toSettingsBody,
} from '../../features/settings/settingsForm'
import {
  BACKUP_ROTATION_MAX,
  BACKUP_ROTATION_MIN,
  FALLBACK_URL_MAX,
  FLOOD_LIMIT_MAX,
  FLOOD_LIMIT_MIN,
  FLOOD_WHITELIST_MAX,
  RECAPTCHA_KEY_MAX,
  RETENTION_MAX_DAYS,
  RETENTION_MIN_DAYS,
  SETTINGS_TEXT_MAX,
} from '../../features/settings/limits'
import type { ApplicationSettingsValues } from '../../features/settings/types'
import { useUnsavedChangesWarning } from '../../hooks/useUnsavedChangesWarning'
import { hinted } from '../../utils/hinted'
import { ApiError, fieldErrors, messageForError, successTextFor } from '../../utils/http'
import { knownZones } from '../../utils/timeZones'

interface SettingsPageProps {
  /** The menu key this route carries, accepted and deliberately unread. */
  sectionKey: string
}

/**
 * The installation's own settings: one row, read and written whole.
 *
 * **Not a table page.** There is no `page-card` class and no toolbar - this card draws a form,
 * not a list, so the compact stylesheet's toolbar rules have nothing to reach. The title stays
 * the card's, in the loading, failed and refused states too, because the address still names the
 * screen when its data does not arrive.
 *
 * **Six sections, and they are the justification for the dividers.** The one-thing-once rule
 * forbids a `Divider` around the key fields of a form; here each section is a genuinely separate
 * concern - the panel's identity, its reCAPTCHA, its public API, log retention, backups, and the
 * four fallback addresses - and the owner's own grouping is the one this screen follows.
 *
 * **The page gates itself.** The rail is role-only today, so both settings items are drawn for
 * every administrator; the content requires `viewSettings` and the save button requires
 * `editSettings`, and a visitor without the read gets an explanation under the same title rather
 * than an empty form. The save row sits **outside** the `Form` because a disabled form would
 * disable its own Cancel button.
 *
 * **The private reCAPTCHA key is write-only.** The read answers `recaptchaConfigured` and never
 * the key, so the secret field opens empty and the sentence under it says what empty means. A
 * save that leaves it empty keeps the stored key; the success handler also resets the guest
 * site-key memo, so a changed key reaches the next sign-in in this tab without a reload.
 */
export default function SettingsPage(_props: SettingsPageProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm<ApplicationSettingsValues>()

  const profileQuery = useProfile()
  const granted = profileQuery.data?.permissions ?? []
  const canEdit = granted.includes('editSettings')
  const canView = granted.includes('viewSettings')

  /**
   * The refusal is answered **only once the profile has answered** - an unanswered profile is
   * "not known yet", not "no". The read is enabled from the first render, though, so a cold
   * navigation whose profile then turns out to lack `viewSettings` has already spent one read
   * the server answers `403 permission_denied`; `refused` hides that answer and shows the
   * explanation instead, and disabling the read from that point keeps a retry from repeating it.
   */
  const refused = profileQuery.data !== undefined && !canView

  const settingsQuery = useSettings({ enabled: !refused })
  const settings = settingsQuery.data ?? null
  const loading = !refused && settingsQuery.isPending
  const loadError =
    !refused && settingsQuery.isError ? messageForError(settingsQuery.error) : null

  const update = useUpdateSettings()
  const [dirty, setDirty] = useState(false)
  // No navigation on a save, so the returned bypass is deliberately not kept: arming it would
  // silence the guard for the rest of this page's life, and a later edit would leave without a
  // question.
  useUnsavedChangesWarning(dirty)

  /**
   * The zones the browser knows, rebuilt only when the language changes does not matter here:
   * identifiers are the same in every language, so the list is built once.
   */
  const zones = useMemo(knownZones, [])

  const zoneOptions = useMemo(
    () => zones.map((zone) => ({ value: zone, label: zone })),
    [zones],
  )

  const periodOptions = useMemo(
    () =>
      BACKUP_PERIODS.map((period) => ({
        value: period,
        label: t(`settingsForm.backupPeriods.${period}`),
      })),
    [t],
  )

  /**
   * Seeds the form from the read.
   *
   * Only while nothing has been edited: a background re-read after some other page's save must
   * not replace what the operator is typing, and the one deliberate re-read - this page's own
   * successful save - runs after `setDirty(false)` so it lands here and refreshes the
   * `recaptchaConfigured` line with the row the server now holds.
   */
  useEffect(() => {
    if (!settings || dirty) return

    form.setFieldsValue(settingsToFormValues(settings))
  }, [settings, dirty, form])

  const handleFinish = async (values: ApplicationSettingsValues) => {
    form.setFields(SETTINGS_FIELDS.map((name) => ({ name, errors: [] })))

    try {
      const answer = await update.mutateAsync(toSettingsBody(values))

      message.success(successTextFor(answer))
      // The secret is never drawn back: clearing it here is what makes the next save "keep the
      // stored key" again rather than re-sending the value the operator typed once.
      form.setFieldValue('recaptchaSecretKey', '')
      setDirty(false)
    } catch (error) {
      const fields = fieldErrors(error instanceof ApiError ? error.errors : null)

      if (fields.length > 0) {
        form.setFields(
          fields.map((field) => ({
            name: field.name as keyof ApplicationSettingsValues,
            errors: field.errors,
          })),
        )
      }

      message.error(messageForError(error))
    }
  }

  const title = t('settingsForm.title')

  /**
   * The refusal first, and **only once the profile has answered**: the address still names the
   * screen, so the card keeps its title and the body says why the form is not there. Drawn
   * before the loading branch because a refused visitor's read is disabled and would otherwise
   * sit on the spinner forever.
   */
  if (refused) {
    return (
      <Card title={title}>
        <Alert type="warning" showIcon message={t('settingsForm.noPermission')} />
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
          action={
            <Button onClick={() => void settingsQuery.refetch()}>{t('actions.retry')}</Button>
          }
        />
      </Card>
    )
  }

  const refusedHint = t('settingsForm.noPermission')

  const saveButton = (
    <Button type="primary" loading={update.isPending} disabled={!canEdit} onClick={() => form.submit()}>
      {t('settingsForm.save')}
    </Button>
  )

  return (
    <Card title={title}>
      <Form<ApplicationSettingsValues>
        form={form}
        layout="vertical"
        onFinish={handleFinish}
        onValuesChange={() => setDirty(true)}
        disabled={!canEdit}
        style={{ maxWidth: 720 }}
      >
        <Divider titlePlacement="start">{t('settingsForm.sections.general')}</Divider>

        <Form.Item
          name="serviceName"
          label={t('settingsForm.serviceNameLabel')}
          rules={[
            { required: true, whitespace: true, message: t('settingsForm.required') },
            { max: SETTINGS_TEXT_MAX, message: t('settingsForm.maxLength', { max: SETTINGS_TEXT_MAX }) },
          ]}
        >
          <Input maxLength={SETTINGS_TEXT_MAX} />
        </Form.Item>

        {/* The installation's own zone, the profile card's selector over `knownZones()`. It is
            required: the contract says the row always names one, unlike the account's own zone
            which may be "follow the browser". */}
        <Form.Item
          name="defaultTimeZone"
          label={t('settingsForm.defaultTimeZoneLabel')}
          rules={[{ required: true, message: t('settingsForm.required') }]}
        >
          <Select showSearch optionFilterProp="label" options={zoneOptions} />
        </Form.Item>

        <Form.Item
          name="enigma2BouquetName"
          label={t('settingsForm.enigma2BouquetNameLabel')}
          rules={[
            { required: true, whitespace: true, message: t('settingsForm.required') },
            { max: SETTINGS_TEXT_MAX, message: t('settingsForm.maxLength', { max: SETTINGS_TEXT_MAX }) },
          ]}
        >
          <Input maxLength={SETTINGS_TEXT_MAX} />
        </Form.Item>

        <Divider titlePlacement="start">{t('settingsForm.sections.recaptcha')}</Divider>

        <Form.Item
          name="recaptchaSiteKey"
          label={t('settingsForm.recaptchaSiteKeyLabel')}
          rules={[
            { max: RECAPTCHA_KEY_MAX, message: t('settingsForm.maxLength', { max: RECAPTCHA_KEY_MAX }) },
          ]}
        >
          <Input maxLength={RECAPTCHA_KEY_MAX} />
        </Form.Item>

        <Form.Item
          name="recaptchaSecretKey"
          label={t('settingsForm.recaptchaSecretKeyLabel')}
          extra={t('settingsForm.recaptchaSecretKeepHint')}
          rules={[
            { max: RECAPTCHA_KEY_MAX, message: t('settingsForm.maxLength', { max: RECAPTCHA_KEY_MAX }) },
          ]}
        >
          <Input.Password maxLength={RECAPTCHA_KEY_MAX} autoComplete="new-password" />
        </Form.Item>

        {/* The one fact about the secret the server does answer. No form item: it is a statement
            about the stored row, not a field to edit, so it must not become part of the body. */}
        <Form.Item label={t('settingsForm.recaptchaStatusLabel')}>
          <Typography.Text type={settings?.recaptchaConfigured ? 'success' : 'secondary'}>
            {t(
              settings?.recaptchaConfigured
                ? 'settingsForm.recaptchaConfigured'
                : 'settingsForm.recaptchaNotConfigured',
            )}
          </Typography.Text>
        </Form.Item>

        <Divider titlePlacement="start">{t('settingsForm.sections.access')}</Divider>

        <Form.Item
          name="enableTrials"
          label={t('settingsForm.enableTrialsLabel')}
          valuePropName="checked"
        >
          <Switch />
        </Form.Item>

        <Form.Item
          name="publicApiFloodLimit"
          label={t('settingsForm.publicApiFloodLimitLabel')}
          extra={t('settingsForm.publicApiFloodLimitHint')}
          rules={[
            {
              type: 'number',
              min: FLOOD_LIMIT_MIN,
              max: FLOOD_LIMIT_MAX,
              message: t('settingsForm.numberRange', { min: FLOOD_LIMIT_MIN, max: FLOOD_LIMIT_MAX }),
            },
          ]}
        >
          <InputNumber min={FLOOD_LIMIT_MIN} max={FLOOD_LIMIT_MAX} precision={0} style={{ width: 220 }} />
        </Form.Item>

        <Form.Item
          name="publicApiFloodWhitelist"
          label={t('settingsForm.publicApiFloodWhitelistLabel')}
          extra={t('settingsForm.publicApiFloodWhitelistHint', { max: FLOOD_WHITELIST_MAX })}
          rules={[
            {
              validator: (_, value: string[] | undefined) =>
                isValidWhitelist(value)
                  ? Promise.resolve()
                  : Promise.reject(new Error(t('settingsForm.publicApiFloodWhitelistInvalid'))),
            },
            {
              validator: (_, value: string[] | undefined) =>
                (value ?? []).length <= FLOOD_WHITELIST_MAX
                  ? Promise.resolve()
                  : Promise.reject(
                      new Error(
                        t('settingsForm.publicApiFloodWhitelistHint', { max: FLOOD_WHITELIST_MAX }),
                      ),
                    ),
            },
          ]}
        >
          <Select
            mode="tags"
            open={false}
            suffixIcon={null}
            placeholder={t('settingsForm.publicApiFloodWhitelistPlaceholder')}
          />
        </Form.Item>

        <Form.Item
          name="requireUserAgent"
          label={t('settingsForm.requireUserAgentLabel')}
          valuePropName="checked"
        >
          <Switch />
        </Form.Item>

        <Divider titlePlacement="start">{t('settingsForm.sections.retention')}</Divider>

        {RETENTION_FIELDS.map((field) => (
          <Form.Item
            key={field.name}
            name={field.name}
            label={t(field.labelKey)}
            extra={t('settingsForm.retentionDays', {
              min: RETENTION_MIN_DAYS,
              max: RETENTION_MAX_DAYS,
            })}
            rules={[
              {
                type: 'number',
                min: RETENTION_MIN_DAYS,
                max: RETENTION_MAX_DAYS,
                message: t('settingsForm.numberRange', {
                  min: RETENTION_MIN_DAYS,
                  max: RETENTION_MAX_DAYS,
                }),
              },
            ]}
          >
            <InputNumber
              min={RETENTION_MIN_DAYS}
              max={RETENTION_MAX_DAYS}
              precision={0}
              style={{ width: 220 }}
            />
          </Form.Item>
        ))}

        <Divider titlePlacement="start">{t('settingsForm.sections.backups')}</Divider>

        <Form.Item
          name="backupEnabled"
          label={t('settingsForm.backupEnabledLabel')}
          valuePropName="checked"
        >
          <Switch />
        </Form.Item>

        <Form.Item
          name="backupPeriod"
          label={t('settingsForm.backupPeriodLabel')}
          rules={[{ required: true, message: t('settingsForm.required') }]}
        >
          <Select options={periodOptions} />
        </Form.Item>

        <Form.Item
          name="backupRotationCount"
          label={t('settingsForm.backupRotationCountLabel')}
          extra={t('settingsForm.backupRotationCountHint')}
          rules={[
            {
              type: 'number',
              min: BACKUP_ROTATION_MIN,
              max: BACKUP_ROTATION_MAX,
              message: t('settingsForm.numberRange', {
                min: BACKUP_ROTATION_MIN,
                max: BACKUP_ROTATION_MAX,
              }),
            },
          ]}
        >
          <InputNumber
            min={BACKUP_ROTATION_MIN}
            max={BACKUP_ROTATION_MAX}
            precision={0}
            style={{ width: 220 }}
          />
        </Form.Item>

        <Divider titlePlacement="start">{t('settingsForm.sections.fallback')}</Divider>

        {FALLBACK_FIELDS.map((field) => (
          <Form.Item
            key={field.name}
            name={field.name}
            label={t(field.labelKey)}
            rules={[
              {
                validator: (_, value: string | undefined) =>
                  isValidFallbackUrl(value)
                    ? Promise.resolve()
                    : Promise.reject(new Error(t('settingsForm.fallbackUrlInvalid'))),
              },
              {
                max: FALLBACK_URL_MAX,
                message: t('settingsForm.maxLength', { max: FALLBACK_URL_MAX }),
              },
            ]}
          >
            <Input maxLength={FALLBACK_URL_MAX} placeholder={t('settingsForm.fallbackUrlPlaceholder')} />
          </Form.Item>
        ))}
      </Form>

      {/* Outside the `Form`, so a visitor who may not save can still act: a form carrying
          `disabled` hands it to every control inside it, the buttons included. Cancel restores
          the row the server answered - there is no list to go back to on this screen - and it
          therefore stays enabled even when nothing is dirty or the form is read-only. */}
      <Space style={{ marginTop: 24 }}>
        {canEdit ? saveButton : hinted(refusedHint, saveButton)}
        <Button
          disabled={update.isPending}
          onClick={() => {
            if (settings) form.setFieldsValue(settingsToFormValues(settings))
            setDirty(false)
          }}
        >
          {t('settingsForm.cancel')}
        </Button>
      </Space>
    </Card>
  )
}

/** The six retention fields, in the order the contract names them. */
const RETENTION_FIELDS: readonly {
  name: keyof ApplicationSettingsValues
  labelKey: string
}[] = [
  { name: 'retentionUserLogs', labelKey: 'settingsForm.retentionUserLogsLabel' },
  { name: 'retentionCreditLogs', labelKey: 'settingsForm.retentionCreditLogsLabel' },
  { name: 'retentionConnectionLogs', labelKey: 'settingsForm.retentionConnectionLogsLabel' },
  { name: 'retentionApiRequestFailures', labelKey: 'settingsForm.retentionApiRequestFailuresLabel' },
  { name: 'retentionStreamProblems', labelKey: 'settingsForm.retentionStreamProblemsLabel' },
  { name: 'retentionMagEvents', labelKey: 'settingsForm.retentionMagEventsLabel' },
]

/** The four fallback addresses, in the order the contract names them. */
const FALLBACK_FIELDS: readonly {
  name: keyof ApplicationSettingsValues
  labelKey: string
}[] = [
  { name: 'fallbackStreamDownUrl', labelKey: 'settingsForm.fallbackStreamDownUrlLabel' },
  { name: 'fallbackUserBannedUrl', labelKey: 'settingsForm.fallbackUserBannedUrlLabel' },
  { name: 'fallbackUserExpiredUrl', labelKey: 'settingsForm.fallbackUserExpiredUrlLabel' },
  {
    name: 'fallbackMaxConnectionsUrl',
    labelKey: 'settingsForm.fallbackMaxConnectionsUrlLabel',
  },
]
