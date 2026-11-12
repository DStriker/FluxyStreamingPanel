import { Alert, App, Button, Card, Form, Input, Space } from 'antd'
import { useTranslation } from 'react-i18next'
import type { SourceReplacementResult } from '../../features/source-replacements/types'
import { useReplaceSources } from '../../features/source-replacements/hooks/mutations'
import { useProfile } from '../../features/profile/hooks/queries'
import { ApiError, fieldErrors, messageForError, successTextFor, textForCode } from '../../utils/http'
import { hinted } from '../../utils/hinted'

/** The two values the form submits, named exactly as the server names them. */
interface SourceReplacementValues {
  find: string
  replace?: string
}

/** Every field the form draws, so a server refusal can be cleared before the next attempt. */
const FIELD_NAMES: readonly (keyof SourceReplacementValues)[] = ['find', 'replace']

/**
 * The mass source replacement: two strings in, every playback address rewritten.
 *
 * **The screen is one form and one command.** There is no table because the run has no rows to
 * browse: it reads every source column of four tables, writes the replacement over every
 * occurrence of the search text, and answers a report. The report becomes a toast plus - when
 * some entities were deliberately left alone - a dialog naming each one, because a partial
 * success that vanished after a moment would leave the operator believing the whole
 * installation had moved.
 *
 * **Four grants are needed at once.** The endpoint carries all four edit gates stacked, so the
 * page asks the profile for them and refuses before the form: a button that would answer
 * `permission_denied` is a worse control than an explanation. The refusal is answered only once
 * the profile has answered - "not known yet" must draw an ordinary loading page rather than an
 * accusation.
 *
 * The form mirrors the server's rules rather than trusting it alone: a blank search and a
 * replacement identical to the search are both caught here, and a server-side field error still
 * lands under its input through `fieldErrors`.
 */
export default function SourceReplacementPage(_props: { sectionKey: string }) {
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const [form] = Form.useForm<SourceReplacementValues>()

  const profileQuery = useProfile()
  const grants = profileQuery.data?.permissions ?? []

  /**
   * Whether the operator holds all four edit grants. Answered only after the profile has
   * answered, so an unanswered profile is "not known yet" rather than a refusal.
   */
  const refused =
    profileQuery.data !== undefined &&
    !(
      grants.includes('editStreams') &&
      grants.includes('editAudioStreams') &&
      grants.includes('editVideoOnDemand') &&
      grants.includes('editSeries')
    )

  const replaceSources = useReplaceSources()

  /**
   * Shows the report: the success line, then - when entities were refused - one dialog line
   * per refused row, naming its domain and its own name.
   */
  const report = (result: SourceReplacementResult) => {
    message.success(successTextFor(result))

    if (result.refusals.length === 0) return

    modal.info({
      title: t('sourceReplacement.refusedTitle'),
      content: (
        <div>
          <p style={{ marginBottom: 8 }}>
            {t('sourceReplacement.done', {
              updated: result.updated,
              skipped: result.skipped,
            })}
          </p>
          {result.refusals.map((refusal) => (
            <p key={`${refusal.domain}:${refusal.id}`} style={{ marginBottom: 4 }}>
              {t(`sourceReplacement.domains.${refusal.domain}`)}: {refusal.name} —{' '}
              {textForCode(refusal.code) ?? refusal.code}
            </p>
          ))}
        </div>
      ),
      okText: t('actions.ok'),
    })
  }

  const handleFinish = async (values: SourceReplacementValues) => {
    // The button is disabled without the four grants, but Enter in a field still submits the
    // form: the same refusal has to hold here, or the grant would be a decoration.
    if (refused || profileQuery.data === undefined) return

    form.setFields(FIELD_NAMES.map((name) => ({ name, errors: [] })))

    try {
      const result = await replaceSources.mutateAsync({
        payload: { find: values.find, replace: values.replace ?? '' },
      })

      report(result)
      form.resetFields()
    } catch (error) {
      const fields = fieldErrors(error instanceof ApiError ? error.errors : null)

      if (fields.length > 0) {
        form.setFields(
          fields.map((field) => ({
            name: field.name as keyof SourceReplacementValues,
            errors: field.errors,
          })),
        )
      }

      message.error(messageForError(error))
    }
  }

  const cardTitle = t('sourceReplacement.title')

  /**
   * Whether the four grants are known to be held. Answering "not yet" is not "yes".
   */
  const allowed = profileQuery.data !== undefined && !refused

  const submit = (
    <Button type="primary" htmlType="submit" loading={replaceSources.isPending}>
      {t('sourceReplacement.submit')}
    </Button>
  )

  /** The same button with no action behind it, used in both not-yet and refused states. */
  const disabledSubmit = (
    <Button type="primary" htmlType="submit" disabled>
      {t('sourceReplacement.submit')}
    </Button>
  )

  return (
    <Card className="page-card" title={cardTitle} styles={{ title: { whiteSpace: 'nowrap' } }}>
      {refused && (
        <Alert
          type="warning"
          showIcon
          message={t('sourceReplacement.noPermission')}
          style={{ marginBottom: 16 }}
        />
      )}

      <Form<SourceReplacementValues>
        form={form}
        layout="vertical"
        onFinish={handleFinish}
        initialValues={{ find: '', replace: '' }}
        style={{ maxWidth: 640 }}
      >
        <Form.Item
          name="find"
          label={t('sourceReplacement.findLabel')}
          // The asterisk is drawn by the item rather than by a `required` rule: the rule that
          // decides "blank" is the whitespace-aware one below, and a second `required` rule
          // beside it would put the same sentence under the input twice.
          required
          rules={[
            {
              validator: (_: unknown, value: string | undefined) =>
                value === undefined || value.trim() === ''
                  ? Promise.reject(new Error(t('sourceReplacement.emptyFind')))
                  : Promise.resolve(),
            },
          ]}
        >
          <Input />
        </Form.Item>

        <Form.Item
          name="replace"
          label={t('sourceReplacement.replaceLabel')}
          extra={t('sourceReplacement.replaceHint')}
          dependencies={['find']}
          rules={[
            ({ getFieldValue }) => ({
              validator: (_: unknown, value: string | undefined) => {
                const find = getFieldValue('find') as string | undefined

                if (find === undefined || find.trim() === '') return Promise.resolve()

                return (value ?? '') === find
                  ? Promise.reject(new Error(t('sourceReplacement.sameFindAndReplace')))
                  : Promise.resolve()
              },
            }),
          ]}
        >
          <Input />
        </Form.Item>

        <Form.Item style={{ marginTop: 24 }}>
          <Space>
            {/* Three states, and only the answered one accuses. While the profile is still
                pending the form is harmless and the button is simply disabled - a tooltip
                saying "your group does not allow this" before the group has answered would
                accuse an operator who may hold every grant. */}
            {allowed
              ? submit
              : refused
                ? hinted(t('sourceReplacement.noPermission'), disabledSubmit)
                : disabledSubmit}
          </Space>
        </Form.Item>
      </Form>
    </Card>
  )
}
