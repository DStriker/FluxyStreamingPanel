import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
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
  Spin,
  Typography,
} from 'antd'
import { useTranslation } from 'react-i18next'
import {
  PLAYLIST_ID_LENGTH,
  SUBSCRIPTION_CONNECTIONS_MAX,
  SUBSCRIPTION_CONNECTIONS_MIN,
  WHITELIST_ENTRY_MAX_LENGTH,
  WHITELIST_MAX_ENTRIES,
  hasOutputFormat,
  hasService,
  isMac,
  isPlaylistId,
  isUsableConnections,
} from '../../features/subscriptions/subscriptionRules'
import type { SubscriptionValues } from '../../features/subscriptions/api'
import type { SubscriptionOutputFormat } from '../../features/subscriptions/types'
import { SUBSCRIPTION_OUTPUT_FORMATS } from '../../features/subscriptions/subscriptionOptions'
import {
  useCreateSubscription,
  useGeneratePlaylistId,
  useUpdateSubscription,
} from '../../features/subscriptions/hooks/mutations'
import { useSubscription } from '../../features/subscriptions/hooks/queries'
import SubscriptionExpiryField from '../../features/subscriptions/components/SubscriptionExpiryField'
import { useAllUsers } from '../../features/users/hooks/queries'
import { useBouquets } from '../../features/bouquets/hooks/queries'
import { useTariffPlans } from '../../features/tariff-plans/hooks/queries'
import { useUnsavedChangesWarning } from '../../hooks/useUnsavedChangesWarning'
import { ApiError, fieldErrors, messageForError, successTextFor } from '../../utils/http'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'

/**
 * The add form's starting content.
 *
 * Every dependent control starts on the answer the domain states: a subscription is not a trial,
 * it delivers MPEG-TS, it is not pinned to a country or a provider, and it allows restreaming off.
 * The connection ceiling starts empty, which is "unlimited" - a form that opened on zero would be
 * a form whose first submit was a refusal.
 */
const CREATE_VALUES: SubscriptionValues = {
  userId: null,
  playlistId: null,
  expireAt: null,
  tariffPlanId: null,
  // One, not empty: the domain's floor is one connection, and a form that opened on "unlimited"
  // would store a subscription with no ceiling unless the operator noticed. "Unlimited" is what
  // the *picker's clear* means once the field is editable.
  maxConnections: 1,
  isRestreamer: false,
  ipWhitelist: [],
  userAgentWhitelist: [],
  isIspLock: false,
  isCountryLock: false,
  isTrial: false,
  outputFormats: ['mpegTs'],
  magMac: null,
  isStbLock: false,
  enigma2Mac: null,
  bouquetIds: [],
}

/** Every field this form draws, so a refusal can be cleared before the next attempt. */
const FIELD_NAMES: readonly (keyof SubscriptionValues)[] = [
  'userId',
  'playlistId',
  'expireAt',
  'tariffPlanId',
  'maxConnections',
  'isRestreamer',
  'ipWhitelist',
  'userAgentWhitelist',
  'isIspLock',
  'isCountryLock',
  'isTrial',
  'outputFormats',
  'magMac',
  'isStbLock',
  'enigma2Mac',
  'bouquetIds',
]

interface SubscriptionFormPageProps {
  /** The menu key this route carries, accepted and deliberately unread. */
  sectionKey: string
}

/**
 * The administrator's add and edit form for one subscription - one component behind two
 * addresses, and every parameter on one screen.
 *
 * **The tariff plan owns what it owns.** While a plan is selected the dependent controls are
 * disabled and not part of the save: the plan decides the connection ceiling, restreaming, formats,
 * the device checks, the trial flag and which services are supported. Clearing the plan nulls the
 * reference and re-activates the fields, opening them on what the row carries underneath.
 *
 * **Rules the server owns, mirrored here so the form refuses before the wire:** at least one
 * service, at least one bouquet without a plan, at least one output format, a playlist id of 32
 * lower-case hex characters, a MAC of six hex pairs, a connection ceiling of 1..10000, and
 * whitelists of at most twenty entries.
 *
 * The form is fetched rather than passed in edit mode, for the reason every other edit form gives:
 * a form painted from whatever the previous screen held would edit the wrong row after a reload.
 * While the fetch is in flight the page is a spinner, which is the state `probe:render` asserts.
 */
export default function SubscriptionFormPage(_props: SubscriptionFormPageProps) {
  const { id } = useParams()
  const isEdit = id !== undefined

  const navigate = useNavigate()
  const { t } = useTranslation()
  const { message } = App.useApp()
  const session = useSession()
  const [form] = Form.useForm<SubscriptionValues>()

  const detailQuery = useSubscription(id)
  const detail = detailQuery.data ?? null
  // `isPending` alone, which means "no answer yet". `isFetching` is true again on every background
  // re-read, and a form that replaced itself with a spinner at that moment would drop whatever the
  // operator had typed into it.
  const loading = isEdit && detailQuery.isPending
  const loadError = isEdit && detailQuery.isError ? messageForError(detailQuery.error) : null

  // The pickers, each behind its own grant and its own request: an operator who may issue a
  // subscription but not read the accounts must still be able to save one.
  const usersQuery = useAllUsers()
  const plansQuery = useTariffPlans({
    page: 1,
    pageSize: 1000,
    search: '',
    sortBy: 'name',
    sortOrder: 'asc',
  })
  const bouquetsQuery = useBouquets()

  const usersError = usersQuery.isError ? messageForError(usersQuery.error) : null
  const plansError = plansQuery.isError ? messageForError(plansQuery.error) : null
  const bouquetsError = bouquetsQuery.isError ? messageForError(bouquetsQuery.error) : null

  const ownerOptions = useMemo(
    () => (usersQuery.data?.items ?? []).map((user) => ({ value: user.id, label: user.username })),
    [usersQuery.data],
  )

  const plans = plansQuery.data?.items ?? []

  const bouquetOptions = useMemo(
    () =>
      (bouquetsQuery.data?.items ?? []).map((bouquet) => ({
        value: bouquet.id,
        label: bouquet.name,
      })),
    [bouquetsQuery.data],
  )

  const create = useCreateSubscription()
  const update = useUpdateSubscription()
  const generatePlaylistId = useGeneratePlaylistId()
  const submitting = create.isPending || update.isPending

  const [dirty, setDirty] = useState(false)
  // The returned function is the deliberate-navigation bypass: the guard sees the form still dirty
  // at the instant after a save, so the success path says that this move is intended.
  const allowNavigation = useUnsavedChangesWarning(dirty)

  const area = session ? areaForRole(session.role) : ''
  const listPath = `${area}/subscriptions`

  // Watched rather than read at submit time, because several controls exist only in relation to
  // another one: a plan disables six fields, and a plan's support flags gate the three service
  // inputs.
  const tariffPlanId = Form.useWatch('tariffPlanId', form) ?? CREATE_VALUES.tariffPlanId
  /** Whether the operator ticked "never expires", which is what sends the explicit clear. */
  const [neverExpires, setNeverExpires] = useState(false)

  // The picker's dayjs value is a moment; the contract takes an ISO 8601 date-time, so the
  // conversion happens here, at the one boundary that knows both shapes.
  const expireAtValue = Form.useWatch('expireAt', form) as unknown

  /** The plan the form currently has selected, or null when it has none. */
  const selectedPlan = useMemo(
    () => plans.find((plan) => plan.id === tariffPlanId) ?? null,
    [plans, tariffPlanId],
  )

  /** Whether a plan is attached, which is what disables everything the plan owns. */
  const planed = tariffPlanId !== null && tariffPlanId !== undefined

  /**
   * The effective support flags. While a plan is attached they are the plan's; without one, all
   * three are available and it is the service inputs that decide.
   */
  const supportsPlaylist = !planed || (selectedPlan?.supportsM3u ?? false)
  const supportsMag = !planed || (selectedPlan?.supportsMag ?? false)
  const supportsEnigma2 = !planed || (selectedPlan?.supportsEnigma2 ?? false)

  /**
   * The form's values as the form currently holds them.
   *
   * Read through one helper because several controls are dropped on the way out: the dependent
   * fields are not sent while a plan is attached, which is exactly what "the plan is the only
   * source of truth" means on the wire.
   *
   * **Two fields are normalized to `null` after the spread, and that is not cosmetic.** A cleared
   * antd `Select` calls `onChange(undefined)` and rc-field-form stores the key with an `undefined`
   * value, so the store holds `tariffPlanId: undefined` rather than `null` - a state the declared
   * `string | null` cannot even express. Left alone it would spread over `CREATE_VALUES`' null and
   * the body would send `clearTariffPlan: false` with the identifier dropped by JSON, which the
   * controller reads as "keep" - the plan would stay attached while the form reported success.
   * Coercing here makes "the operator cleared it" and "the value is absent" one representation
   * before anything downstream reads it.
   */
  const currentValues = (): SubscriptionValues => {
    const held = form.getFieldsValue(true) as Partial<SubscriptionValues>

    return {
      ...CREATE_VALUES,
      ...held,
      expireAt: expireAtWire(expireAtValue, neverExpires),
      tariffPlanId: held.tariffPlanId ?? null,
      maxConnections: held.maxConnections ?? null,
    }
  }

  const visibleValues = (): SubscriptionValues => {
    const values = currentValues()

    if (!planed) {
      return values
    }

    // The plan owns these, so the request must not carry them: a body that named them would be a
    // body contradicting the plan, and the server would have to decide which to believe. The
    // server drops them anyway; sending them would still be a lie about what the form asked for.
    // The two clear flags need no handling here - `toUpdateBody` derives them from the values it
    // is handed, so a null ceiling stays "unlimited" whether the form or the plan produced it.
    return {
      ...values,
      maxConnections: null,
      isRestreamer: false,
      ipWhitelist: values.ipWhitelist,
      userAgentWhitelist: values.userAgentWhitelist,
      isIspLock: values.isIspLock,
      isTrial: false,
      outputFormats: values.outputFormats,
      isStbLock: false,
      bouquetIds: values.bouquetIds,
    }
  }

  useEffect(() => {
    if (!detail) return

    form.setFieldsValue({
      userId: detail.userId,
      playlistId: detail.playlistId,
      expireAt: detail.expireAt,
      tariffPlanId: detail.tariffPlanId,
      maxConnections: detail.maxConnections,
      isRestreamer: detail.isRestreamer,
      ipWhitelist: [...detail.ipWhitelist],
      userAgentWhitelist: [...detail.userAgentWhitelist],
      isIspLock: detail.isIspLock,
      isCountryLock: detail.isCountryLock,
      isTrial: detail.isTrial,
      outputFormats: [...detail.outputFormats],
      magMac: detail.magMac,
      isStbLock: detail.isStbLock,
      enigma2Mac: detail.enigma2Mac,
      bouquetIds: [...detail.bouquetIds],
    })

    // "Never expires" is a state of the form, not of the field, so it has to be seeded from the
    // row as well: without this the checkbox would open unticked on a row that has no expiry and
    // the first save would silently set one from an empty picker.
    setNeverExpires(detail.expireAt === null)
  }, [detail, form])

  /**
   * Clearing the plan re-activates the stored bouquet list, and an empty one is preselected
   * again - the spec's "all preselected again". A subscription that always carried a plan never
   * stored its own bouquets, so without this the select would open empty and the save would be
   * refused by the minimum-one rule with nothing the operator could see.
   */
  useEffect(() => {
    if (planed || bouquetOptions.length === 0) return

    const held = form.getFieldValue('bouquetIds') as string[] | undefined

    if (held && held.length > 0) return

    form.setFieldValue(
      'bouquetIds',
      bouquetOptions.map((option) => option.value),
    )
  }, [planed, bouquetOptions, form])

  /**
   * The add form opens with every bouquet preselected.
   *
   * Once, and only while the field is still untouched: the server applies the same default to a
   * body that names none, and opening the form on it means the operator sees what the create would
   * do before they touch anything. An operator who cleared the picker must not have it refilled the
   * moment the bouquet list re-rendered.
   */
  const [bouquetsApplied, setBouquetsApplied] = useState(false)

  useEffect(() => {
    if (isEdit || bouquetsApplied || planed || bouquetOptions.length === 0) return

    setBouquetsApplied(true)

    if ((form.getFieldValue('bouquetIds') as string[] | undefined)?.length) return

    form.setFieldValue(
      'bouquetIds',
      bouquetOptions.map((option) => option.value),
    )
  }, [isEdit, bouquetsApplied, planed, bouquetOptions, form])

  /**
   * Minting a playlist id on the create form when the plan supports playlists and nothing else.
   *
   * The rule the specification states as "auto-generated on creation only when the selected plan
   * supports playlists and nothing else" - because in that case the playlist is the only service
   * the plan can deliver, and an operator who has to press a button to get the one service they
   * could possibly have is being asked to do the server's job.
   */
  const [minted, setMinted] = useState(false)

  useEffect(() => {
    if (isEdit || minted || !selectedPlan) return
    if (!selectedPlan.supportsM3u || selectedPlan.supportsMag || selectedPlan.supportsEnigma2) return
    if ((form.getFieldValue('playlistId') as string | null | undefined) !== null) return

    setMinted(true)

    void generatePlaylistId
      .mutateAsync({})
      .then((answer) => {
        form.setFieldValue('playlistId', answer.playlistId)
        setDirty(true)
      })
      .catch(() => {
        // A generator that could not answer is not a reason to block the form: the operator can
        // still press "generate" themselves, and the field accepts a pasted identifier.
      })
  }, [isEdit, minted, selectedPlan, form, generatePlaylistId])

  /**
   * Mints a fresh playlist id and puts it in the field.
   *
   * The value is written with `setFields`, not `setFieldsValue`: it sets the field *and* clears any
   * error left over from a previous failed validation, so the answer is not hidden behind a red
   * message about the id that was there before.
   *
   * That alone would still show nothing if the `Input` were not bound to the form, and it was not,
   * until round 3: `Form.Item name="playlistId"` had a `Space.Compact` as its direct child, and a
   * named item injects `value`/`onChange` into that child. A compact wrapper is a layout `div` and
   * keeps neither, so every write to the store - `setFieldValue`, `setFields`, `setFieldsValue`
   * alike - went somewhere no control read from. The `Input` is now the direct child of the named
   * item (the generate button lives in a `Form.Item` of its own), which is what makes any of these
   * writers visible.
   */
  const generatePlaylist = () => {
    void generatePlaylistId
      .mutateAsync({})
      .then((answer) => {
        form.setFields([{ name: 'playlistId', value: answer.playlistId, errors: [] }])
        setDirty(true)
      })
      .catch((error) => void message.error(messageForError(error)))
  }

  const handleFinish = async (values: SubscriptionValues) => {
    form.setFields(FIELD_NAMES.map((name) => ({ name, errors: [] })))

    const body = visibleValues()

    try {
      const answer = isEdit
        ? await update.mutateAsync({ id: id!, values: body })
        : await create.mutateAsync({ values: { ...body, userId: values.userId } })

      message.success(successTextFor(answer))
      setDirty(false)
      allowNavigation()
      navigate(listPath)
    } catch (error) {
      const fields = fieldErrors(error instanceof ApiError ? error.errors : null)
      if (fields.length > 0)
        form.setFields(
          fields.map((field) => ({
            name: field.name as keyof SubscriptionValues,
            errors: field.errors,
          })),
        )
      message.error(messageForError(error))
    }
  }

  /** Chooses every bouquet of one picker, or clears it. */
  const setAllBouquets = (choose: boolean) => {
    form.setFieldValue(
      'bouquetIds',
      choose ? bouquetOptions.map((option) => option.value) : [],
    )
    setDirty(true)
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
          action={<Button onClick={() => navigate(listPath)}>{t('subscriptions.backToList')}</Button>}
        />
      </Card>
    )
  }

  return (
    <Card title={t(isEdit ? 'subscriptions.editTitle' : 'subscriptions.addTitle')}>
      <Form<SubscriptionValues>
        form={form}
        layout="vertical"
        initialValues={CREATE_VALUES}
        onFinish={handleFinish}
        onValuesChange={() => setDirty(true)}
        style={{ maxWidth: 720 }}
      >
        {detail && (
          <Form.Item label={t('subscriptionsForm.idLabel')}>
            {/* No `name`: layout only, so the identifier is never part of the body. */}
            <Typography.Text code copyable={{ text: detail.id }}>
              {detail.id}
            </Typography.Text>
          </Form.Item>
        )}

        {/* The owner is chosen only on create: it never changes after the subscription is issued,
            and the server ignores the field on a change. `planAvailableToOwner` re-filters the
            plan list when the owner is chosen, because a plan is only attachable while its groups
            include the owner's. */}
        <Form.Item
          name="userId"
          label={t('subscriptionsForm.ownerLabel')}
          rules={
            isEdit
              ? []
              : [
                  {
                    required: true,
                    message: t('subscriptionsForm.ownerRequired'),
                  },
                ]
          }
          extra={usersError ?? undefined}
          validateStatus={usersError ? 'warning' : undefined}
        >
          <Select
            showSearch
            disabled={isEdit || usersError !== null}
            options={ownerOptions}
            placeholder={t('subscriptionsForm.ownerLabel')}
            optionFilterProp="label"
            onChange={() => {
              // A plan the new owner may not have must not stay selected: the server would refuse
              // the save, and the refusal would look like a bug in the form.
              form.setFieldValue('tariffPlanId', null)
            }}
          />
        </Form.Item>

        <Form.Item
          name="tariffPlanId"
          label={t('subscriptionsForm.planLabel')}
          extra={plansError ?? undefined}
          validateStatus={plansError ? 'warning' : undefined}
        >
          <Select
            allowClear
            showSearch
            disabled={plansError !== null}
            options={plans.map((plan) => ({ value: plan.id, label: plan.name }))}
            placeholder={t('subscriptionsForm.planNone')}
            optionFilterProp="label"
          />
        </Form.Item>

        <SubscriptionExpiryField
          planed={planed}
          neverExpires={neverExpires}
          onNeverExpiresChange={(next) => {
            setNeverExpires(next)
            form.setFieldValue('expireAt', null)
            setDirty(true)
          }}
        />

        <Divider titlePlacement="start">{t('subscriptionsForm.servicesSection')}</Divider>

        <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
          {t('subscriptionsForm.servicesHint')}
        </Typography.Paragraph>

        {/* The three service inputs. Each is disabled when the effective plan does not support that
            kind, and the refusal belongs under the set rather than under one of them. */}
        <Form.Item
          name="playlistId"
          label={t('subscriptionsForm.playlistIdLabel')}
          dependencies={['magMac', 'enigma2Mac']}
          rules={[
            {
              validator: (_, value: string | null | undefined) => {
                if (value !== null && value !== undefined && value !== '' && !isPlaylistId(value)) {
                  return Promise.reject(new Error(t('subscriptionsForm.playlistIdInvalid')))
                }

                return serviceOrRefusal(form, supportsPlaylist, supportsMag, supportsEnigma2, t)
              },
            },
          ]}
          extra={t('subscriptionsForm.playlistIdHint')}
        >
          <Input
            disabled={!supportsPlaylist}
            maxLength={PLAYLIST_ID_LENGTH}
            style={{ fontFamily: 'monospace' }}
          />
        </Form.Item>

        {/*
         * The generate button sits in a `Form.Item` of its own, the pattern the account form's
         * password generator uses. It used to live inside a `Space.Compact` *as the child of the
         * named `Form.Item`* - and a named item injects `value`/`onChange` into its direct child,
         * which was the compact wrapper, a layout `div` that drops both. The `Input` therefore was
         * never bound to the form at all, which is why the id the server returned never appeared:
         * nothing was writing where the input read from.
         */}
        <Form.Item style={{ marginTop: -12 }}>
          <Button
            disabled={!supportsPlaylist || generatePlaylistId.isPending}
            loading={generatePlaylistId.isPending}
            onClick={generatePlaylist}
          >
            {t('subscriptionsForm.generatePlaylistId')}
          </Button>
        </Form.Item>

        <Form.Item
          name="magMac"
          label={t('subscriptionsForm.magLabel')}
          dependencies={['playlistId', 'enigma2Mac']}
          rules={[
            {
              validator: (_, value: string | null | undefined) => {
                if (value !== null && value !== undefined && value !== '' && !isMac(value)) {
                  return Promise.reject(new Error(t('subscriptionsForm.magInvalid')))
                }

                return serviceOrRefusal(form, supportsPlaylist, supportsMag, supportsEnigma2, t)
              },
            },
          ]}
          extra={supportsMag ? undefined : t('subscriptionsForm.magNeedsPlanSupport')}
        >
          <Input disabled={!supportsMag} placeholder="AA:BB:CC:DD:EE:FF" />
        </Form.Item>

        <Form.Item
          name="enigma2Mac"
          label={t('subscriptionsForm.enigma2Label')}
          dependencies={['playlistId', 'magMac']}
          rules={[
            {
              validator: (_, value: string | null | undefined) => {
                if (value !== null && value !== undefined && value !== '' && !isMac(value)) {
                  return Promise.reject(new Error(t('subscriptionsForm.enigma2Invalid')))
                }

                return serviceOrRefusal(form, supportsPlaylist, supportsMag, supportsEnigma2, t)
              },
            },
          ]}
          extra={supportsEnigma2 ? undefined : t('subscriptionsForm.enigma2NeedsPlanSupport')}
        >
          <Input disabled={!supportsEnigma2} placeholder="AA:BB:CC:DD:EE:FF" />
        </Form.Item>

        <Divider titlePlacement="start">{t('subscriptionsForm.formatsSection')}</Divider>

        <Form.Item
          name="outputFormats"
          label={t('subscriptionsForm.formatsSection')}
          extra={planed ? t('subscriptionsForm.planOwnedHint') : t('subscriptionsForm.formatsHint')}
          rules={[
            {
              validator: (_, value: SubscriptionOutputFormat[] | undefined) =>
                hasOutputFormat(value ?? [])
                  ? Promise.resolve()
                  : Promise.reject(new Error(t('subscriptionsForm.formatsRequired'))),
            },
          ]}
        >
          <Checkbox.Group
            disabled={planed}
            options={SUBSCRIPTION_OUTPUT_FORMATS.map((format) => ({
              value: format,
              label: t(`subscriptionsForm.formats.${format}`),
            }))}
          />
        </Form.Item>

        <Divider titlePlacement="start">{t('subscriptionsForm.limitsSection')}</Divider>

        <Form.Item
          name="maxConnections"
          label={t('subscriptionsForm.maxConnectionsLabel')}
          extra={
            planed
              ? t('subscriptionsForm.planOwnedHint')
              : t('subscriptionsForm.maxConnectionsHint', {
                  min: SUBSCRIPTION_CONNECTIONS_MIN,
                  max: SUBSCRIPTION_CONNECTIONS_MAX,
                })
          }
          rules={[
            {
              validator: (_, value: number | null | undefined) =>
                isUsableConnections(value ?? null)
                  ? Promise.resolve()
                  : Promise.reject(new Error(t('subscriptionsForm.maxConnectionsRequired'))),
            },
          ]}
        >
          <InputNumber
            min={SUBSCRIPTION_CONNECTIONS_MIN}
            max={SUBSCRIPTION_CONNECTIONS_MAX}
            precision={0}
            disabled={planed}
            style={{ width: 220 }}
            placeholder={t('subscriptionsForm.unlimitedConnections')}
          />
        </Form.Item>

        <Form.Item
          name="isRestreamer"
          valuePropName="checked"
          extra={planed ? t('subscriptionsForm.planOwnedHint') : t('subscriptionsForm.restreamHint')}
        >
          <Checkbox disabled={planed}>{t('subscriptionsForm.restreamLabel')}</Checkbox>
        </Form.Item>

        <Form.Item
          name="isStbLock"
          valuePropName="checked"
          extra={planed ? t('subscriptionsForm.planOwnedHint') : t('subscriptionsForm.stbLockHint')}
        >
          <Checkbox disabled={planed || !supportsMag}>
            {t('subscriptionsForm.stbLockLabel')}
          </Checkbox>
        </Form.Item>

        <Form.Item
          name="isCountryLock"
          valuePropName="checked"
          extra={planed ? t('subscriptionsForm.planOwnedHint') : t('subscriptionsForm.countryLockHint')}
        >
          <Checkbox disabled={planed}>{t('subscriptionsForm.countryLockLabel')}</Checkbox>
        </Form.Item>

        {/* The provider lock is not a plan-derived field, so it is never disabled by a plan. */}
        <Form.Item
          name="isIspLock"
          valuePropName="checked"
          extra={t('subscriptionsForm.ispLockHint')}
        >
          <Checkbox>{t('subscriptionsForm.ispLockLabel')}</Checkbox>
        </Form.Item>

        <Form.Item
          name="isTrial"
          valuePropName="checked"
          extra={planed ? t('subscriptionsForm.planOwnedHint') : t('subscriptionsForm.trialHint')}
        >
          <Checkbox disabled={planed}>{t('subscriptionsForm.trialLabel')}</Checkbox>
        </Form.Item>

        <Divider titlePlacement="start">{t('subscriptionsForm.accessSection')}</Divider>

        <Form.Item
          name="ipWhitelist"
          label={t('subscriptionsForm.ipWhitelistLabel')}
          extra={t('subscriptionsForm.ipWhitelistHint', { max: WHITELIST_MAX_ENTRIES })}
          rules={[
            {
              validator: (_, value: string[] | undefined) => {
                const entries = (value ?? []).filter((entry) => entry.trim().length > 0)

                if (entries.length > WHITELIST_MAX_ENTRIES) {
                  return Promise.reject(
                    new Error(
                      t('subscriptionsForm.ipWhitelistHint', { max: WHITELIST_MAX_ENTRIES }),
                    ),
                  )
                }

                const parsed = entries.every((entry) => isIpEntry(entry))

                return parsed
                  ? Promise.resolve()
                  : Promise.reject(new Error(t('subscriptionsForm.ipWhitelistInvalid')))
              },
            },
          ]}
        >
          <Select mode="tags" open={false} suffixIcon={null} placeholder="192.168.1.0/24" />
        </Form.Item>

        <Form.Item
          name="userAgentWhitelist"
          label={t('subscriptionsForm.userAgentWhitelistLabel')}
          extra={t('subscriptionsForm.userAgentWhitelistHint', { max: WHITELIST_MAX_ENTRIES })}
          rules={[
            {
              validator: (_, value: string[] | undefined) => {
                const entries = (value ?? []).filter((entry) => entry.trim().length > 0)

                if (entries.length > WHITELIST_MAX_ENTRIES) {
                  return Promise.reject(
                    new Error(
                      t('subscriptionsForm.userAgentWhitelistHint', {
                        max: WHITELIST_MAX_ENTRIES,
                      }),
                    ),
                  )
                }

                const valid = entries.every(
                  (entry) => entry.length > 0 && entry.length <= WHITELIST_ENTRY_MAX_LENGTH,
                )

                return valid
                  ? Promise.resolve()
                  : Promise.reject(
                      new Error(
                        t('subscriptionsForm.userAgentWhitelistInvalid', {
                          max: WHITELIST_ENTRY_MAX_LENGTH,
                        }),
                      ),
                    )
              },
            },
          ]}
        >
          <Select mode="tags" open={false} suffixIcon={null} placeholder="Mozilla/5.0" />
        </Form.Item>

        <Divider titlePlacement="start">{t('subscriptionsForm.contentSection')}</Divider>

        <Form.Item
          name="bouquetIds"
          label={t('subscriptionsForm.bouquetsLabel')}
          rules={[
            {
              validator: (_, value: string[] | undefined) => {
                // With a plan the plan's bouquets are delivered, so this list is not required and
                // not read; without one it is the subscription's whole content.
                if (planed) return Promise.resolve()

                return value !== undefined && value.length > 0
                  ? Promise.resolve()
                  : Promise.reject(new Error(t('subscriptionsForm.bouquetsRequired')))
              },
            },
          ]}
          extra={bouquetsError ?? t('subscriptionsForm.bouquetsHint')}
          validateStatus={bouquetsError ? 'warning' : undefined}
        >
          <Select
            mode="multiple"
            allowClear
            disabled={planed || bouquetsError !== null}
            options={bouquetOptions}
            placeholder={t('subscriptionsForm.bouquetsPlaceholder')}
            optionFilterProp="label"
            maxTagCount="responsive"
          />
        </Form.Item>

        <Form.Item style={{ marginTop: -8 }}>
          <Space size={8}>
            <Button
              size="small"
              disabled={planed || bouquetsError !== null || bouquetOptions.length === 0}
              onClick={() => setAllBouquets(true)}
            >
              {t('subscriptionsForm.selectAll')}
            </Button>
            <Button
              size="small"
              disabled={planed || bouquetsError !== null}
              onClick={() => setAllBouquets(false)}
            >
              {t('subscriptionsForm.clear')}
            </Button>
          </Space>
        </Form.Item>

        {bouquetsError && (
          <Alert
            type="warning"
            showIcon
            style={{ marginBottom: 16 }}
            message={bouquetsError}
            description={t('subscriptionsForm.bouquetsUnavailable')}
          />
        )}

        <Form.Item style={{ marginTop: 24 }}>
          <Space>
            <Button type="primary" htmlType="submit" loading={submitting}>
              {t(isEdit ? 'subscriptions.save' : 'subscriptions.create')}
            </Button>
            <Button disabled={submitting} onClick={() => navigate(listPath)}>
              {t('subscriptions.backToList')}
            </Button>
          </Space>
        </Form.Item>
      </Form>
    </Card>
  )
}

/**
 * The expiry as the wire expects it, or null.
 *
 * The picker hands back a dayjs object (or null); the contract takes an ISO 8601 date-time string.
 *
 * **The null-versus-empty sentinel is the whole point of this function, and it has bitten this
 * form three times.** On `PATCH` the contract reads an absent or null expiry as *"leave it
 * alone"*, and only the **empty string** means *"clear it"* - so a ticked "never expires" must
 * send `''` or the clear is silently a keep. An empty picker is the other half of the rule: it is
 * *not* a statement, so it answers null and the stored expiry stays (design decision 15).
 */
function expireAtWire(value: unknown, never: boolean): string | null {
  // The explicit clear: the operator ticked the box, which is the only statement that removes an
  // expiry. `null` here would be read as "keep" and the tick would do nothing.
  if (never) return ''

  if (value === null || value === undefined) return null

  // `toISOString` is the round-trippable form the backend parses, offset included.
  if (typeof value === 'object' && value !== null && 'toISOString' in value) {
    const iso = (value as { toISOString: () => string }).toISOString()

    return typeof iso === 'string' ? iso : null
  }

  return typeof value === 'string' && value.length > 0 ? value : null
}

/**
 * The minimum-one-service rule, as a validator.
 *
 * It reads the three service fields together, because they answer one question between them and
 * antd only re-checks the field that changed. The effective support flags matter: the server
 * *clears* a service the plan cannot deliver, so a row whose only service is unsupported holds
 * nothing once the save runs.
 */
function serviceOrRefusal(
  form: ReturnType<typeof Form.useForm<SubscriptionValues>>[0],
  supportsPlaylist: boolean,
  supportsMag: boolean,
  supportsEnigma2: boolean,
  t: (key: string) => string,
): Promise<void> {
  const values = form.getFieldsValue(['playlistId', 'magMac', 'enigma2Mac']) as {
    playlistId?: string | null
    magMac?: string | null
    enigma2Mac?: string | null
  }

  const present = (value: string | null | undefined): string | null =>
    value === undefined || value === null || value.trim().length === 0 ? null : value

  return hasService({
    playlistId: present(values.playlistId),
    magMac: present(values.magMac),
    enigma2Mac: present(values.enigma2Mac),
    supportsPlaylist,
    supportsMag,
    supportsEnigma2,
  })
    ? Promise.resolve()
    : Promise.reject(new Error(t('subscriptionsForm.servicesRequired')))
}

/**
 * Whether a whitelist entry is an IP address or a CIDR range.
 *
 * A shape check rather than a parser: the browser has no address type, and the server's own
 * normalizer is the authority. What this catches is the typo a form can catch - a value with no
 * dots or colons in it - so the operator learns before the wire instead of after.
 */
function isIpEntry(value: string): boolean {
  const trimmed = value.trim()
  const [address, prefix] = trimmed.split('/')

  if (prefix !== undefined && !/^\d{1,3}$/.test(prefix)) return false

  if (/^\d{1,3}(\.\d{1,3}){3}$/.test(address ?? '')) return true

  return /^[0-9a-f]*:[0-9a-f:]*$/i.test(address ?? '') && (address ?? '').includes(':')
}
