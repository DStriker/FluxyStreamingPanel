import { Form, Input } from 'antd'
import { useTranslation } from 'react-i18next'
import { BLACKLIST_NOTE_MAX, BLACKLIST_VALUE_MAX } from '../api'
import type { BlacklistType } from '../types'

interface BlacklistEntryFieldsProps {
  /** Which list the dialog was opened on: the value's three labels follow it. */
  type: BlacklistType
}

/**
 * The two fields the add/edit dialog collects, and only those two.
 *
 * Split out of the dialog because the dialog itself is portalled and never reaches a static
 * render: `probe:render` draws this inside a bare `Form` and asserts that exactly the value and
 * the note are asked for, and that the value's label is the one the list calls for. A third
 * field added to the dialog would fail there rather than shipping as a control nobody asked
 * for - the type, the counter and the added date are all facts the row already has.
 *
 * The field names are the server's camelCase JSON names (`value`, `note`), so `fieldErrors()`
 * lands a refusal under the input that caused it with no translation step.
 */
export default function BlacklistEntryFields({ type }: BlacklistEntryFieldsProps) {
  const { t } = useTranslation()

  return (
    <>
      <Form.Item
        name="value"
        label={t(`blacklistsForm.valueLabel.${type}`)}
        rules={[
          { required: true, whitespace: true, message: t('blacklistsForm.valueRequired') },
          {
            max: BLACKLIST_VALUE_MAX,
            message: t('blacklistsForm.maxLength', { max: BLACKLIST_VALUE_MAX }),
          },
        ]}
        // The hint is the IP list's alone: an address has a shape worth stating ("a single
        // IPv4 or IPv6 address; ranges are not supported"), while an ISP or user-agent value is
        // the literal string the list matches on and a sentence about it would only repeat the
        // label. The placeholders stay on all three - they show the shape, and are not hints.
        extra={type === 'ip' ? t('blacklistsForm.valueHint.ip') : undefined}
      >
        <Input
          maxLength={BLACKLIST_VALUE_MAX}
          placeholder={t(`blacklistsForm.valuePlaceholder.${type}`)}
        />
      </Form.Item>
      <Form.Item
        name="note"
        label={t('blacklistsForm.noteLabel')}
        rules={[
          {
            max: BLACKLIST_NOTE_MAX,
            message: t('blacklistsForm.maxLength', { max: BLACKLIST_NOTE_MAX }),
          },
        ]}
      >
        <Input.TextArea rows={3} maxLength={BLACKLIST_NOTE_MAX} />
      </Form.Item>
    </>
  )
}
