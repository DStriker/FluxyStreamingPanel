import { Form, Input, Modal } from 'antd'
import { useTranslation } from 'react-i18next'

/**
 * The one-question dialog that opens a ticket.
 *
 * **One field, and the modal's own OK button is the submit.** A subject is the whole of what
 * the endpoint takes; the first message is written inside the ticket, where the conversation
 * is, rather than collected here as a second field the server would have to accept as an
 * ordered pair. That is also why the form is not drawn with a footer of its own: antd's OK
 * is the button, and it is disabled while the subject is empty rather than letting a press
 * produce a refusal the visitor could have seen coming.
 *
 * **It writes nothing.** The page owns the mutation, the toast, the close and the navigation;
 * the component collects the subject and hands it over. The page mounts it only while the
 * dialog is open, so neither the form nor the typed subject outlives a close - the shape
 * `SendEventModal` already uses.
 */
export default function CreateTicketModal({
  submitting,
  onCancel,
  onSubmit,
}: {
  /** Whether the create write is in flight - the OK button's spinner. */
  submitting: boolean
  /** Closes the dialog without writing. */
  onCancel: () => void
  /** Opens the ticket; the page reports, closes and navigates. */
  onSubmit: (subject: string) => void
}) {
  const { t } = useTranslation()
  const [form] = Form.useForm<{ subject: string }>()

  // Watched rather than tracked by hand: `Form.useWatch` answers the field's current value,
  // and OK is disabled on the *trimmed* one - a subject of spaces is what it is to the
  // server, which trims before it looks.
  const subject: string = Form.useWatch('subject', form) ?? ''

  const submit = () => {
    void form
      .validateFields()
      .then((values) => onSubmit(values.subject.trim()))
      // The only way the rules reject here is the empty subject the OK button already greyed
      // out; the catch is what keeps that impossible path from becoming an unhandled promise.
      .catch(() => undefined)
  }

  return (
    <Modal
      title={t('tickets.createTitle')}
      open
      okText={t('tickets.create')}
      okButtonProps={{ disabled: subject.trim() === '', loading: submitting }}
      cancelText={t('actions.cancel')}
      onOk={submit}
      onCancel={onCancel}
      destroyOnHidden
    >
      <Form form={form} layout="vertical">
        <Form.Item
          name="subject"
          label={t('tickets.subjectLabel')}
          rules={[{ required: true, whitespace: true, message: t('tickets.subjectRequired') }]}
        >
          <Input maxLength={200} />
        </Form.Item>
      </Form>
    </Modal>
  )
}
