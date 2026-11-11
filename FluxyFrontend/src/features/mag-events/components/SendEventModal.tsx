import { App, Form, Input, Modal, Select } from 'antd'
import { useTranslation } from 'react-i18next'
import { MAG_EVENT_TYPES } from '../events'
import { MESSAGE_MAX_LENGTH, eventMessageRule, messageErrorFor } from '../eventRules'
import type { MagEventType } from '../types'
import { useSendMagEvent } from '../hooks/mutations'
import { ApiError, fieldErrors, messageForError, successTextFor } from '../../../utils/http'

/**
 * The one dialog that sends an event to a subscription's MAG device.
 *
 * **One field per row, and no flags at all.** The operation decides the rest of the form: a
 * `sendMessage` asks for the text the device shows, a `playChannel` asks which video stream to
 * open (the stream's identifier travels as `message`), and the other four carry no payload, so
 * no second field is drawn. `needConfirm`, `rebootAfterOk`, `autoHideTimeout`,
 * `additionalServicesOn`, `anec` and `vclub` are deliberately absent: the server resolves every
 * one of them, and a form that offered them would be inventing defaults the database has been
 * kept from carrying.
 *
 * **The rules are the server's, mirrored in `eventRules.ts`.** The form refuses a blank
 * message, a message past the column width and a `playChannel` payload that is not a uuid
 * before a request is made, so the refusal under the field is the one the server would have
 * given.
 *
 * **A refusal keeps the dialog open with its values.** A server answer that names fields lands
 * them under their inputs through `fieldErrors`, whose keys are the camelCase JSON names this
 * form already uses; anything else is one toast. Closing on failure would make the operator
 * retype the message that was refused.
 *
 * `channelOptions` arrives from the page rather than being read here: a feature imports
 * neither another feature nor a page, so the streams list is the page's query and this
 * component only draws it. The page mounts this dialog only while it is open, so no dialog
 * state survives a close. The list itself is read at the page level, which the query client
 * deduplicates with the streams table's own read.
 */

/** What the dialog is about: the subscription the event is sent under. */
interface SendEventTarget {
  /** Identifier of the subscription, as text. */
  id: string
  /** Name the subscription is named by in the dialog's body. */
  title: string
}

/** One option of the video stream picker - the page's query, handed over already mapped. */
interface ChannelOption {
  value: string
  label: string
}

export default function SendEventModal({
  target,
  channelOptions,
  onClose,
}: {
  /** The row the dialog was opened from. */
  target: SendEventTarget
  /** The video streams a `playChannel` event may open. */
  channelOptions: ChannelOption[]
  /** Closes the dialog; the page owns whether it is open. */
  onClose: () => void
}) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm<{ event: MagEventType; message?: string }>()
  const sendEvent = useSendMagEvent()

  /**
   * The six operations' words, one literal `t()` per wire value.
   *
   * Written as a `Record` over the union rather than as a hand-kept array beside
   * `MAG_EVENT_TYPES`: the picker's membership and the label's spelling then come from one
   * list, and a seventh operation is a compile error here rather than an option that quietly
   * has no text. The keys are literal so `probe:i18n` can see all six.
   */
  const eventLabelOf: Record<MagEventType, string> = {
    playChannel: t('magEvents.events.playChannel'),
    closePortal: t('magEvents.events.closePortal'),
    reloadPortal: t('magEvents.events.reloadPortal'),
    rebootDevice: t('magEvents.events.rebootDevice'),
    sendMessage: t('magEvents.events.sendMessage'),
    resetStbLock: t('magEvents.events.resetStbLock'),
  }

  /** The operation being composed, or undefined until one is chosen. */
  const chosenEvent: MagEventType | undefined = Form.useWatch('event', form)

  /** The payload rule that operation follows: two ask for a value, four ask for nothing. */
  const messageRule = chosenEvent ? eventMessageRule(chosenEvent) : 'none'

  /**
   * The words for one refusal of the payload field.
   *
   * The two fields are never on screen together, so the sentence follows the operation rather
   * than the error: a `playChannel` payload that is blank and a payload that is not a uuid are
   * the same problem to the reader - no channel is chosen - and a `sendMessage` has the
   * message's own sentence. `tooLong` is prevented by the textarea's `maxLength`; it falls back
   * to the field's sentence because the visitor has to retype the value either way.
   */
  const payloadText = (): string =>
    messageRule === 'channel'
      ? t('subscriptions.sendEvent.channelRequired')
      : t('subscriptions.sendEvent.messageRequired')

  /**
   * Refuses a payload the server would refuse, beyond the empty field `required` already
   * answers: a `playChannel` identifier that is not a uuid, or a message past the column
   * width. Both come from `eventRules.ts`, so the field and the server agree by construction.
   */
  const validateMessage = (_: unknown, value: string | undefined) => {
    // Empty is `required`'s answer, and saying it twice would put two sentences under one field.
    if (!chosenEvent || !value || value.trim() === '') return Promise.resolve()

    const error = messageErrorFor(chosenEvent, value)

    return error ? Promise.reject(new Error(payloadText())) : Promise.resolve()
  }

  const submit = async ({
    event,
    message: payload,
  }: {
    event: MagEventType
    message?: string
  }) => {
    // The four payload-less operations carry nothing: a value left in the field from another
    // operation is dropped here, which is the same answer the server gives a message sent for
    // one of them.
    const carried = eventMessageRule(event) === 'none' ? undefined : payload?.trim()

    try {
      const result = await sendEvent.mutateAsync({
        subscriptionId: target.id,
        event,
        ...(carried !== undefined ? { message: carried } : {}),
      })

      message.success(successTextFor(result))
      onClose()
    } catch (error) {
      const fields = fieldErrors(error instanceof ApiError ? error.errors : null)

      // The server's keys are the camelCase JSON names this form already uses (`event`,
      // `message`), so the refusal lands under the input that caused it with no translation
      // step - the `fieldErrors` contract every form in this application follows.
      if (fields.length > 0) {
        form.setFields(
          fields.map((field) => ({
            name: field.name as 'event' | 'message',
            errors: field.errors,
          })),
        )
      }

      message.error(messageForError(error))
    }
  }

  return (
    <Modal
      title={t('subscriptions.sendEvent.title')}
      open
      okText={t('subscriptions.sendEvent.submit')}
      confirmLoading={sendEvent.isPending}
      cancelText={t('actions.cancel')}
      onOk={() => void form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      {/* The body names the row the dialog was opened from, because the same six operations
          look identical on every subscription and the operator may have arrived from a list. */}
      <p style={{ marginTop: 0 }}>
        {t('subscriptions.sendEvent.target', { name: target.title })}
      </p>

      <Form form={form} layout="vertical" onFinish={submit}>
        <Form.Item
          name="event"
          label={t('subscriptions.sendEvent.eventLabel')}
          rules={[{ required: true, message: t('subscriptions.sendEvent.eventRequired') }]}
        >
          {/* No default: an event is a decision, and a preselected one would let a press of
              "Send" record an operation nobody chose. */}
          <Select
            options={MAG_EVENT_TYPES.map((event) => ({
              value: event,
              label: eventLabelOf[event],
            }))}
          />
        </Form.Item>

        {/* `sendMessage` asks for the text and `playChannel` for the stream; the other four
            ask for nothing at all, so the dialog literally has one field for them. */}
        {messageRule !== 'none' && (
          <Form.Item
            name="message"
            label={
              messageRule === 'text'
                ? t('subscriptions.sendEvent.messageLabel')
                : t('subscriptions.sendEvent.channelLabel')
            }
            rules={[
              // The empty field is refused in the field's own words - `whitespace` is what
              // makes it agree with the server, which trims before it looks at the value.
              { required: true, whitespace: true, message: payloadText() },
              // ...and the shape is the server's: a uuid for `playChannel`, the column width
              // for both. Both rules are pure functions from `eventRules.ts`.
              { validator: validateMessage },
            ]}
          >
            {messageRule === 'text' ? (
              <Input.TextArea
                autoSize={{ minRows: 3, maxRows: 8 }}
                maxLength={MESSAGE_MAX_LENGTH}
              />
            ) : (
              <Select showSearch optionFilterProp="label" options={channelOptions} />
            )}
          </Form.Item>
        )}
      </Form>
    </Modal>
  )
}
