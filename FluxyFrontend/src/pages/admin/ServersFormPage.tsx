import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import {
  Alert,
  App,
  Button,
  Card,
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
import {
  SERVER_CLIENT_COUNT_MAX,
  SERVER_CLIENT_COUNT_MIN,
  SERVER_GUARANTEED_SPEED_MAX_MBPS,
  SERVER_HOSTNAME_MAX,
  SERVER_IP_MAX,
  SERVER_NAME_MAX,
  SERVER_NETWORK_INTERFACE_MAX,
  SERVER_OS_NAME_MAX,
  SERVER_PORT_MAX,
  SERVER_PORT_MIN,
} from '../../features/servers/api'
import type { ServerValues } from '../../features/servers/api'
import { kbpsToMbps } from '../../features/servers/serverFields'
import {
  useCreateServer,
  useUpdateServer,
} from '../../features/servers/hooks/mutations'
import { useServer } from '../../features/servers/hooks/queries'
import { useProfile } from '../../features/profile/hooks/queries'
import { useUnsavedChangesWarning } from '../../hooks/useUnsavedChangesWarning'
import { countryOptions } from '../../utils/countries'
import { hinted } from '../../utils/hinted'
import { ApiError, fieldErrors, messageForError, successTextFor } from '../../utils/http'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'

/**
 * The add form's starting content.
 *
 * The three numeric fields open on their own sentinels rather than on blanks: `0` clients
 * means unlimited and `0` kbps means unknown on the contract, so the form starts on the same
 * answer a create that names nothing would get. The port is the exception: it starts empty
 * because no value is a sensible guess for it, and the required rule is what asks.
 */
const CREATE_VALUES: ServerValues = {
  name: '',
  ip: '',
  port: null,
  status: 'Active',
  osName: '',
  networkInterface: '',
  clientCount: SERVER_CLIENT_COUNT_MIN,
  guaranteedMbps: 0,
  latency: 0,
  timeshiftOnly: false,
  hostname: '',
  vpnIp: '',
  countryCodes: [],
}

/** Every field this form draws, so a refusal can be cleared before the next attempt. */
const FIELD_NAMES: readonly (keyof ServerValues)[] = [
  'name',
  'ip',
  'port',
  'status',
  'osName',
  'networkInterface',
  'clientCount',
  'guaranteedMbps',
  'latency',
  'timeshiftOnly',
  'hostname',
  'vpnIp',
  'countryCodes',
]

/**
 * The wire's field names that are not the form's own.
 *
 * `guaranteedSpeedKbps` is the contract's name for the throughput, while the form holds the
 * same value in Mbps under `guaranteedMbps`; a `validation_failed` refusal arrives keyed by
 * the wire's name, so it has to be put back under the control the visitor can see.
 */
const FIELD_NAME_OF: Partial<Record<string, keyof ServerValues>> = {
  guaranteedSpeedKbps: 'guaranteedMbps',
}

/** The statuses, in the order the backend's enum declares them. */
const STATUSES = ['Active', 'Disabled'] as const

interface ServersFormPageProps {
  /** The menu key this route carries, accepted and deliberately unread. */
  sectionKey: string
}

/**
 * The administrator's add and edit form for one server - one component behind two
 * addresses.
 *
 * **One field per row** and a single column: the fields are read in the order an operator
 * works through them, from what the server is called to which countries it serves. There is
 * no divider and no grouping sentence, because none of the thirteen fields is a separate
 * concern.
 *
 * **Latency exists only in edit mode.** The create contract has no such property - a new
 * row always starts at "not measured" - so the add form does not draw a control that could
 * not be sent. The field appears once the row exists, where the full-write `PATCH` carries
 * it. There is deliberately no follow-up `PATCH` after a create: one submit is one request,
 * and an operator who has a reading to record edits the row.
 *
 * The form is fetched rather than passed in edit mode, for the reason every other edit form
 * gives: a form painted from whatever the previous screen held would edit the wrong row after
 * a reload. While the fetch is in flight the page is a spinner.
 */
export default function ServersFormPage(_props: ServersFormPageProps) {
  const { id } = useParams()
  const isEdit = id !== undefined

  const navigate = useNavigate()
  const { t, i18n } = useTranslation()
  const { message } = App.useApp()
  const session = useSession()
  const [form] = Form.useForm<ServerValues>()

  const profileQuery = useProfile()
  const granted = profileQuery.data?.permissions ?? []
  const canEdit = granted.includes('editServers')

  const detailQuery = useServer(id)
  const detail = detailQuery.data ?? null
  const loading = isEdit && (detailQuery.isPending || detailQuery.isFetching)
  const loadError = isEdit && detailQuery.isError ? messageForError(detailQuery.error) : null

  const create = useCreateServer()
  const update = useUpdateServer()
  const submitting = create.isPending || update.isPending

  const [dirty, setDirty] = useState(false)
  // The returned function is the deliberate-navigation bypass: the guard sees the form still
  // dirty at the instant after a save, so the success path says that this move is intended.
  const allowNavigation = useUnsavedChangesWarning(dirty)

  const area = session ? areaForRole(session.role) : ''
  const listPath = `${area}/servers`

  /**
   * The country names in the language on screen. The codes underneath are what the server
   * stores; the names are only what the selector searches and draws, so they are rebuilt
   * when the visitor changes language rather than stored beside the form.
   */
  const countryList = useMemo(
    () => countryOptions(i18n.resolvedLanguage ?? i18n.language),
    [i18n.language, i18n.resolvedLanguage],
  )

  const statusOptions = useMemo(
    () => STATUSES.map((status) => ({ value: status, label: t(`servers.statuses.${status}`) })),
    [t],
  )

  useEffect(() => {
    if (!detail) return

    form.setFieldsValue({
      name: detail.name,
      ip: detail.ip,
      port: detail.port,
      status: detail.status,
      osName: detail.osName,
      networkInterface: detail.networkInterface,
      clientCount: detail.clientCount,
      guaranteedMbps: kbpsToMbps(detail.guaranteedSpeedKbps),
      latency: detail.latency,
      timeshiftOnly: detail.timeshiftOnly,
      hostname: detail.hostname ?? '',
      vpnIp: detail.vpnIp ?? '',
      countryCodes: detail.countryCodes,
    })
  }, [detail, form])

  const handleFinish = async (values: ServerValues) => {
    form.setFields(FIELD_NAMES.map((name) => ({ name, errors: [] })))

    try {
      const answer = isEdit
        ? await update.mutateAsync({ id: id!, values })
        : await create.mutateAsync(values)

      message.success(successTextFor(answer))
      setDirty(false)
      allowNavigation()
      navigate(listPath)
    } catch (error) {
      const fields = fieldErrors(error instanceof ApiError ? error.errors : null)
      if (fields.length > 0)
        form.setFields(
          fields.map((field) => ({
            name: FIELD_NAME_OF[field.name] ?? (field.name as keyof ServerValues),
            errors: field.errors,
          })),
        )
      message.error(messageForError(error))
    }
  }

  if (loading) {
    return (
      <Card title={t(isEdit ? 'serversForm.editTitle' : 'serversForm.addTitle')}>
        <Spin />
      </Card>
    )
  }

  if (loadError) {
    return (
      <Card title={t(isEdit ? 'serversForm.editTitle' : 'serversForm.addTitle')}>
        <Alert
          type="error"
          showIcon
          message={loadError}
          action={<Button onClick={() => navigate(listPath)}>{t('servers.backToList')}</Button>}
        />
      </Card>
    )
  }

  const refused = t('serversForm.noPermission')

  /**
   * The save button, drawn outside the `Form` because the form carries `disabled` for a
   * read-only visitor and antd hands that down to every control inside it - the Cancel button
   * included. A visitor who may not save must still be able to leave the page, so the action
   * row sits beside the form and Save submits it through `form.submit()`.
   */
  const saveButton = (
    <Button
      type="primary"
      loading={submitting}
      disabled={!canEdit}
      onClick={() => form.submit()}
    >
      {t(isEdit ? 'serversForm.save' : 'serversForm.create')}
    </Button>
  )

  return (
    <Card title={t(isEdit ? 'serversForm.editTitle' : 'serversForm.addTitle')}>
      <Form<ServerValues>
        form={form}
        layout="vertical"
        initialValues={CREATE_VALUES}
        onFinish={handleFinish}
        onValuesChange={() => setDirty(true)}
        disabled={!canEdit}
        style={{ maxWidth: 720 }}
      >
        {detail && (
          <Form.Item label={t('serversForm.idLabel')}>
            {/* No `name`: layout only, so the identifier is never part of the body. */}
            <Typography.Text code copyable={{ text: detail.id }}>
              {detail.id}
            </Typography.Text>
          </Form.Item>
        )}

        <Form.Item
          name="name"
          label={t('serversForm.nameLabel')}
          rules={[
            { required: true, whitespace: true, message: t('serversForm.nameRequired') },
            { max: SERVER_NAME_MAX, message: t('serversForm.maxLength', { max: SERVER_NAME_MAX }) },
          ]}
        >
          <Input maxLength={SERVER_NAME_MAX} />
        </Form.Item>

        <Form.Item
          name="ip"
          label={t('serversForm.ipLabel')}
          rules={[
            { required: true, whitespace: true, message: t('serversForm.ipRequired') },
            { max: SERVER_IP_MAX, message: t('serversForm.maxLength', { max: SERVER_IP_MAX }) },
          ]}
        >
          <Input placeholder={t('serversForm.ipPlaceholder')} maxLength={SERVER_IP_MAX} />
        </Form.Item>

        <Form.Item
          name="port"
          label={t('serversForm.portLabel')}
          rules={[
            { required: true, message: t('serversForm.portRequired') },
            {
              type: 'number',
              min: SERVER_PORT_MIN,
              max: SERVER_PORT_MAX,
              message: t('serversForm.portHint', { min: SERVER_PORT_MIN, max: SERVER_PORT_MAX }),
            },
          ]}
        >
          <InputNumber
            min={SERVER_PORT_MIN}
            max={SERVER_PORT_MAX}
            precision={0}
            placeholder={t('serversForm.portPlaceholder')}
            style={{ width: 220 }}
          />
        </Form.Item>

        <Form.Item
          name="status"
          label={t('serversForm.statusLabel')}
          rules={[{ required: true, message: t('serversForm.statusRequired') }]}
        >
          <Select options={statusOptions} />
        </Form.Item>

        <Form.Item
          name="osName"
          label={t('serversForm.osNameLabel')}
          rules={[
            { required: true, whitespace: true, message: t('serversForm.osNameRequired') },
            {
              max: SERVER_OS_NAME_MAX,
              message: t('serversForm.maxLength', { max: SERVER_OS_NAME_MAX }),
            },
          ]}
        >
          <Input maxLength={SERVER_OS_NAME_MAX} />
        </Form.Item>

        <Form.Item
          name="networkInterface"
          label={t('serversForm.networkInterfaceLabel')}
          rules={[
            {
              required: true,
              whitespace: true,
              message: t('serversForm.networkInterfaceRequired'),
            },
            {
              max: SERVER_NETWORK_INTERFACE_MAX,
              message: t('serversForm.maxLength', { max: SERVER_NETWORK_INTERFACE_MAX }),
            },
          ]}
        >
          <Input
            placeholder={t('serversForm.networkInterfacePlaceholder')}
            maxLength={SERVER_NETWORK_INTERFACE_MAX}
          />
        </Form.Item>

        <Form.Item
          name="clientCount"
          label={t('serversForm.clientCountLabel')}
          extra={t('serversForm.clientCountHint')}
        >
          <InputNumber
            min={SERVER_CLIENT_COUNT_MIN}
            max={SERVER_CLIENT_COUNT_MAX}
            precision={0}
            style={{ width: 220 }}
          />
        </Form.Item>

        <Form.Item
          name="guaranteedMbps"
          label={t('serversForm.guaranteedMbpsLabel')}
          extra={t('serversForm.guaranteedMbpsHint', {
            max: SERVER_GUARANTEED_SPEED_MAX_MBPS,
          })}
        >
          <InputNumber min={0} max={SERVER_GUARANTEED_SPEED_MAX_MBPS} step={0.1} style={{ width: 220 }} />
        </Form.Item>

        {/* Latency is a property of an existing row: the create contract does not carry it,
            so the add form does not pretend it could. The edit form is where a reading is
            recorded, and the full-write PATCH is what sends it. */}
        {isEdit && (
          <Form.Item
            name="latency"
            label={t('serversForm.latencyLabel')}
            extra={t('serversForm.latencyHint')}
          >
            <InputNumber min={0} step={0.001} precision={3} style={{ width: 220 }} />
          </Form.Item>
        )}

        <Form.Item
          name="timeshiftOnly"
          label={t('serversForm.timeshiftOnlyLabel')}
          valuePropName="checked"
          extra={t('serversForm.timeshiftOnlyHint')}
        >
          <Switch />
        </Form.Item>

        <Form.Item
          name="hostname"
          label={t('serversForm.hostnameLabel')}
          rules={[
            {
              max: SERVER_HOSTNAME_MAX,
              message: t('serversForm.maxLength', { max: SERVER_HOSTNAME_MAX }),
            },
          ]}
        >
          <Input placeholder={t('serversForm.hostnamePlaceholder')} maxLength={SERVER_HOSTNAME_MAX} />
        </Form.Item>

        <Form.Item
          name="vpnIp"
          label={t('serversForm.vpnIpLabel')}
          rules={[
            { max: SERVER_IP_MAX, message: t('serversForm.maxLength', { max: SERVER_IP_MAX }) },
          ]}
        >
          <Input placeholder={t('serversForm.vpnIpPlaceholder')} maxLength={SERVER_IP_MAX} />
        </Form.Item>

        <Form.Item name="countryCodes" label={t('serversForm.countryCodesLabel')}>
          {/* A multi-select over the localized names; the stored value stays the two-letter
              codes. Empty is "serves every country", the contract's own reading of no codes. */}
          <Select
            mode="multiple"
            allowClear
            showSearch
            options={countryList}
            optionFilterProp="label"
            placeholder={t('serversForm.countryCodesPlaceholder')}
            aria-label={t('serversForm.countryCodesLabel')}
          />
        </Form.Item>
      </Form>

      {/* The action row sits outside the `Form`, and that is deliberate: the form carries
          `disabled` for a read-only visitor, and antd hands that down to every control inside
          it - the Cancel button included. A visitor who may not save must still be able to
          leave the page. */}
      <Space style={{ marginTop: 24 }}>
        {canEdit ? saveButton : hinted(refused, saveButton)}
        <Button disabled={submitting} onClick={() => navigate(listPath)}>
          {t('serversForm.cancel')}
        </Button>
      </Space>
    </Card>
  )
}
