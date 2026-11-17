import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
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
  Typography,
} from 'antd'
import { useTranslation } from 'react-i18next'
import {
  TRANSCODING_PROFILE_ASPECT_RATIO_MAX,
  TRANSCODING_PROFILE_CRF_MAX,
  TRANSCODING_PROFILE_CRF_MIN,
  TRANSCODING_PROFILE_LOGO_URL_MAX,
  TRANSCODING_PROFILE_NAME_MAX,
} from '../../features/transcoding-profiles/api'
import type { TranscodingProfileValues } from '../../features/transcoding-profiles/api'
import {
  AUDIO_CHANNELS,
  AUDIO_CODECS,
  PRESETS,
  SAMPLE_RATES,
  VIDEO_CODECS,
  VIDEO_PROFILES,
} from '../../features/transcoding-profiles/ffmpegOptions'
import type { FfmpegOption } from '../../features/transcoding-profiles/ffmpegOptions'
import {
  useCreateTranscodingProfile,
  useUpdateTranscodingProfile,
} from '../../features/transcoding-profiles/hooks/mutations'
import { useTranscodingProfile } from '../../features/transcoding-profiles/hooks/queries'
import { useUnsavedChangesWarning } from '../../hooks/useUnsavedChangesWarning'
import { ApiError, fieldErrors, messageForError, successTextFor } from '../../utils/http'
import { areaForRole } from '../../utils/session'
import { useSession } from '../../utils/sessionContext'

/**
 * The add form's starting content.
 *
 * The three defaults are the three ffmpeg values that mean "pass no flag": `copy` for both
 * streams and `default` for the preset. Everything else starts empty, and empty is a real state
 * - the profile simply does not name that option.
 */
const CREATE_VALUES: TranscodingProfileValues = {
  name: '',
  videoCodec: 'copy',
  audioCodec: 'copy',
  preset: 'default',
  videoProfile: null,
  videoBitrateAvg: null,
  audioBitrateAvg: null,
  minBitrateTolerance: null,
  maxBitrateTolerance: null,
  bufferSize: null,
  crf: null,
  width: null,
  height: null,
  aspectRatio: null,
  framerate: null,
  audioSampleRate: null,
  audioChannels: null,
  threads: null,
  logoUrl: null,
  redactionX: null,
  redactionY: null,
  redactionWidth: null,
  redactionHeight: null,
}

/** The four redaction fields, which are one rectangle and are judged together. */
const REDACTION_FIELDS = ['redactionX', 'redactionY', 'redactionWidth', 'redactionHeight'] as const

/** Every field this form draws, so a refusal can be cleared before the next attempt. */
const FIELD_NAMES: readonly (keyof TranscodingProfileValues)[] = [
  'name',
  'videoCodec',
  'audioCodec',
  'preset',
  'videoProfile',
  'videoBitrateAvg',
  'audioBitrateAvg',
  'minBitrateTolerance',
  'maxBitrateTolerance',
  'bufferSize',
  'crf',
  'width',
  'height',
  'aspectRatio',
  'framerate',
  'audioSampleRate',
  'audioChannels',
  'threads',
  'logoUrl',
  ...REDACTION_FIELDS,
]

interface TranscodingProfileFormPageProps {
  /** The menu key this route carries, accepted and deliberately unread. */
  sectionKey: string
}

/**
 * The administrator's add and edit form for one transcoding profile - one component behind two
 * addresses.
 *
 * **One field per row.** The options are drawn in the order an operator reaches for them: the
 * three encoders, then the H.264 profile, then the bitrate group, the size group, the audio
 * group and finally the two optional decorations. The one exception is the redaction rectangle,
 * which is one parameter with four parts and is therefore one `Form.Item` holding a
 * `Space.Compact`.
 *
 * **Three hint sentences and no more.** The width field says `-1` keeps the proportions, the
 * threads field says `0` means automatic, and nothing else needs explaining - the section's own
 * sentence below its heading already says that an empty option is not passed to ffmpeg.
 *
 * The form is fetched rather than passed in edit mode, for the reason every other edit form
 * gives: a form painted from whatever the previous screen held would edit the wrong row after a
 * reload. While the fetch is in flight the page is a spinner, which is the state
 * `probe:render` asserts.
 */
export default function TranscodingProfileFormPage(_props: TranscodingProfileFormPageProps) {
  const { id } = useParams()
  const isEdit = id !== undefined

  const navigate = useNavigate()
  const { t } = useTranslation()
  const { message } = App.useApp()
  const session = useSession()
  const [form] = Form.useForm<TranscodingProfileValues>()

  const detailQuery = useTranscodingProfile(id)
  const detail = detailQuery.data ?? null
  const loading = isEdit && (detailQuery.isPending || detailQuery.isFetching)
  const loadError = isEdit && detailQuery.isError ? messageForError(detailQuery.error) : null

  const create = useCreateTranscodingProfile()
  const update = useUpdateTranscodingProfile()
  const submitting = create.isPending || update.isPending

  const [dirty, setDirty] = useState(false)
  // The returned function is the deliberate-navigation bypass: the guard sees the form still
  // dirty at the instant after a save, so the success path says that this move is intended.
  const allowNavigation = useUnsavedChangesWarning(dirty)

  const area = session ? areaForRole(session.role) : ''
  const listPath = `${area}/transcoding-profiles`

  const optionsOf = (items: readonly FfmpegOption[]) =>
    items.map((item) => ({ value: item.value, label: t(item.labelKey) }))

  const videoCodecOptions = useMemo(() => optionsOf(VIDEO_CODECS), [t])
  const audioCodecOptions = useMemo(() => optionsOf(AUDIO_CODECS), [t])
  const presetOptions = useMemo(() => optionsOf(PRESETS), [t])
  const videoProfileOptions = useMemo(() => optionsOf(VIDEO_PROFILES), [t])

  useEffect(() => {
    if (!detail) return

    form.setFieldsValue({
      name: detail.name,
      videoCodec: detail.videoCodec,
      audioCodec: detail.audioCodec,
      preset: detail.preset,
      videoProfile: detail.videoProfile,
      videoBitrateAvg: detail.videoBitrateAvg,
      audioBitrateAvg: detail.audioBitrateAvg,
      minBitrateTolerance: detail.minBitrateTolerance,
      maxBitrateTolerance: detail.maxBitrateTolerance,
      bufferSize: detail.bufferSize,
      crf: detail.crf,
      width: detail.width,
      height: detail.height,
      aspectRatio: detail.aspectRatio,
      framerate: detail.framerate,
      audioSampleRate: detail.audioSampleRate,
      audioChannels: detail.audioChannels,
      threads: detail.threads,
      logoUrl: detail.logoUrl,
      redactionX: detail.redactionX,
      redactionY: detail.redactionY,
      redactionWidth: detail.redactionWidth,
      redactionHeight: detail.redactionHeight,
    })
  }, [detail, form])

  const handleFinish = async (values: TranscodingProfileValues) => {
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
            name: field.name as keyof TranscodingProfileValues,
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
            <Button onClick={() => navigate(listPath)}>{t('transcodingProfiles.backToList')}</Button>
          }
        />
      </Card>
    )
  }

  return (
    <Card title={t(isEdit ? 'transcodingProfilesForm.editTitle' : 'transcodingProfilesForm.addTitle')}>
      <Form<TranscodingProfileValues>
        form={form}
        layout="vertical"
        initialValues={CREATE_VALUES}
        onFinish={handleFinish}
        onValuesChange={() => setDirty(true)}
        style={{ maxWidth: 720 }}
      >
        {detail && (
          <Form.Item label={t('transcodingProfilesForm.idLabel')}>
            {/* No `name`: layout only, so the identifier is never part of the body. */}
            <Typography.Text code copyable={{ text: detail.id }}>
              {detail.id}
            </Typography.Text>
          </Form.Item>
        )}

        <Form.Item
          name="name"
          label={t('transcodingProfilesForm.nameLabel')}
          rules={[
            { required: true, whitespace: true, message: t('transcodingProfilesForm.nameRequired') },
            {
              max: TRANSCODING_PROFILE_NAME_MAX,
              message: t('transcodingProfilesForm.maxLength', { max: TRANSCODING_PROFILE_NAME_MAX }),
            },
          ]}
        >
          <Input maxLength={TRANSCODING_PROFILE_NAME_MAX} />
        </Form.Item>

        <Divider titlePlacement="start" style={{ marginTop: 24, marginBottom: 12 }}>
          {t('transcodingProfilesForm.optionsDivider')}
        </Divider>

        {/* The section's one sentence, directly under its heading: what an empty option means.
            No size override - antd's own secondary text is the size every other hint uses. */}
        <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
          {t('transcodingProfilesForm.optionsHint')}
        </Typography.Paragraph>

        <Form.Item
          name="videoCodec"
          label={t('transcodingProfilesForm.videoCodecLabel')}
          rules={[{ required: true, message: t('transcodingProfilesForm.videoCodecRequired') }]}
        >
          <Select allowClear showSearch optionFilterProp="label" options={videoCodecOptions} />
        </Form.Item>

        <Form.Item
          name="audioCodec"
          label={t('transcodingProfilesForm.audioCodecLabel')}
          rules={[{ required: true, message: t('transcodingProfilesForm.audioCodecRequired') }]}
        >
          <Select allowClear showSearch optionFilterProp="label" options={audioCodecOptions} />
        </Form.Item>

        <Form.Item
          name="preset"
          label={t('transcodingProfilesForm.presetLabel')}
          rules={[{ required: true, message: t('transcodingProfilesForm.presetRequired') }]}
        >
          <Select allowClear options={presetOptions} />
        </Form.Item>

        <Form.Item name="videoProfile" label={t('transcodingProfilesForm.videoProfileLabel')}>
          <Select allowClear options={videoProfileOptions} />
        </Form.Item>

        <Form.Item name="videoBitrateAvg" label={t('transcodingProfilesForm.videoBitrateAvgLabel')}>
          <InputNumber min={0} precision={0} style={{ width: 220 }} />
        </Form.Item>

        <Form.Item name="audioBitrateAvg" label={t('transcodingProfilesForm.audioBitrateAvgLabel')}>
          <InputNumber min={0} precision={0} style={{ width: 220 }} />
        </Form.Item>

        <Form.Item
          name="minBitrateTolerance"
          label={t('transcodingProfilesForm.minBitrateToleranceLabel')}
        >
          <InputNumber min={0} precision={0} style={{ width: 220 }} />
        </Form.Item>

        <Form.Item
          name="maxBitrateTolerance"
          label={t('transcodingProfilesForm.maxBitrateToleranceLabel')}
        >
          <InputNumber min={0} precision={0} style={{ width: 220 }} />
        </Form.Item>

        <Form.Item name="bufferSize" label={t('transcodingProfilesForm.bufferSizeLabel')}>
          <InputNumber min={0} precision={0} style={{ width: 220 }} />
        </Form.Item>

        <Form.Item name="crf" label={t('transcodingProfilesForm.crfLabel')}>
          <InputNumber
            min={TRANSCODING_PROFILE_CRF_MIN}
            max={TRANSCODING_PROFILE_CRF_MAX}
            precision={0}
            style={{ width: 220 }}
          />
        </Form.Item>

        <Form.Item
          name="width"
          label={t('transcodingProfilesForm.widthLabel')}
          extra={t('transcodingProfilesForm.widthHint')}
        >
          <InputNumber min={-1} precision={0} style={{ width: 220 }} />
        </Form.Item>

        <Form.Item name="height" label={t('transcodingProfilesForm.heightLabel')}>
          <InputNumber min={-1} precision={0} style={{ width: 220 }} />
        </Form.Item>

        <Form.Item name="aspectRatio" label={t('transcodingProfilesForm.aspectRatioLabel')}>
          <Input maxLength={TRANSCODING_PROFILE_ASPECT_RATIO_MAX} />
        </Form.Item>

        <Form.Item name="framerate" label={t('transcodingProfilesForm.framerateLabel')}>
          <InputNumber min={0.001} step={0.001} style={{ width: 220 }} />
        </Form.Item>

        <Form.Item name="audioSampleRate" label={t('transcodingProfilesForm.audioSampleRateLabel')}>
          <Select
            allowClear
            options={SAMPLE_RATES.map((rate) => ({ value: rate, label: String(rate) }))}
          />
        </Form.Item>

        <Form.Item name="audioChannels" label={t('transcodingProfilesForm.audioChannelsLabel')}>
          <Select
            allowClear
            options={AUDIO_CHANNELS.map((count) => ({ value: count, label: String(count) }))}
          />
        </Form.Item>

        <Form.Item
          name="threads"
          label={t('transcodingProfilesForm.threadsLabel')}
          extra={t('transcodingProfilesForm.threadsHint')}
        >
          <InputNumber min={0} precision={0} style={{ width: 220 }} />
        </Form.Item>

        <Form.Item name="logoUrl" label={t('transcodingProfilesForm.logoUrlLabel')}>
          <Input maxLength={TRANSCODING_PROFILE_LOGO_URL_MAX} />
        </Form.Item>

        {/* One parameter with four parts: a rectangle the encoder blurs. The four travel as one
            composite control because they are one decision; the first field carries the
            all-or-none validator and depends on its three siblings, which is the shape the
            tariff plan's duration pair already uses - a `rules`-bearing `Form.Item` with no
            `name` of its own draws no control at all. */}
        <Form.Item label={t('transcodingProfilesForm.redactionLabel')}>
          <Space.Compact>
            <Form.Item
              name="redactionX"
              noStyle
              dependencies={['redactionY', 'redactionWidth', 'redactionHeight']}
              rules={[
                {
                  validator: () => {
                    const values = form.getFieldsValue([...REDACTION_FIELDS])
                    const present = REDACTION_FIELDS.filter(
                      (field) => values[field] !== null && values[field] !== undefined,
                    )

                    if (present.length === 0 || present.length === REDACTION_FIELDS.length) {
                      return Promise.resolve()
                    }

                    return Promise.reject(new Error(t('transcodingProfilesForm.redactionRequired')))
                  },
                },
              ]}
            >
              <InputNumber
                min={0}
                precision={0}
                placeholder={t('transcodingProfilesForm.redactionX')}
                style={{ width: 120 }}
              />
            </Form.Item>
            <Form.Item name="redactionY" noStyle>
              <InputNumber
                min={0}
                precision={0}
                placeholder={t('transcodingProfilesForm.redactionY')}
                style={{ width: 120 }}
              />
            </Form.Item>
            <Form.Item name="redactionWidth" noStyle>
              <InputNumber
                min={0}
                precision={0}
                placeholder={t('transcodingProfilesForm.redactionWidth')}
                style={{ width: 120 }}
              />
            </Form.Item>
            <Form.Item name="redactionHeight" noStyle>
              <InputNumber
                min={0}
                precision={0}
                placeholder={t('transcodingProfilesForm.redactionHeight')}
                style={{ width: 120 }}
              />
            </Form.Item>
          </Space.Compact>
        </Form.Item>

        <Form.Item style={{ marginTop: 24 }}>
          <Space>
            <Button type="primary" htmlType="submit" loading={submitting}>
              {t(isEdit ? 'transcodingProfiles.save' : 'transcodingProfiles.create')}
            </Button>
            <Button disabled={submitting} onClick={() => navigate(listPath)}>
              {t('transcodingProfiles.backToList')}
            </Button>
          </Space>
        </Form.Item>
      </Form>
    </Card>
  )
}
