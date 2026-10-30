import { Checkbox, DatePicker, Form, Space } from 'antd'
import { useTranslation } from 'react-i18next'
import { expireAtPickerProps } from '../subscriptionBody'

/**
 * The expiry control: an antd `DatePicker` with a time, and the explicit "never expires" switch.
 *
 * A component of its own rather than a block inside the page for one reason: `probe:render` has to
 * render **this wiring with a populated value**, and no render of the page can get there - the page
 * fetches its row in an effect and `renderToStaticMarkup` runs no effects. Extracting the field
 * lets the probe mount it inside a `Form` whose `initialValues` already hold a stored ISO string,
 * which is the exact state that used to throw `getUDayjs(...).isValid is not a function` and take
 * the whole form down. A probe that only renders the page's spinner would pass on the bug.
 *
 * The conversion lives in `expireAtPickerProps` (`subscriptionBody.ts`) so the pure half is unit
 * tested and this file stays markup.
 */
export default function SubscriptionExpiryField({
  planed,
  neverExpires,
  onNeverExpiresChange,
}: {
  /** Whether a tariff plan is attached, which owns the expiry. */
  planed: boolean
  /** Whether the operator ticked "never expires". */
  neverExpires: boolean
  /** Called when the switch changes; the form's own `expireAt` is cleared alongside it. */
  onNeverExpiresChange: (next: boolean) => void
}) {
  const { t } = useTranslation()

  return (
    <Form.Item label={t('subscriptionsForm.expiryLabel')}>
      <Space direction="vertical" size={4} style={{ width: '100%' }}>
        {/* The form stores the wire string, while `DatePicker` speaks `Dayjs`. Converting at the
            field boundary - the pattern `VideoOnDemandFormPage` uses for its release date - keeps
            every request and every `setFieldsValue` on the string shape the contract uses, instead
            of leaking dayjs objects into the body mapping. */}
        <Form.Item name="expireAt" noStyle getValueProps={expireAtPickerProps}>
          <DatePicker
            showTime={{ format: 'HH:mm' }}
            format="YYYY-MM-DD HH:mm"
            disabled={planed || neverExpires}
            // No clear (×) affordance: the "never expires" switch below is the only way to remove
            // an expiry, so a stray click on the picker cannot wipe a date the operator set.
            allowClear={false}
            style={{ width: 240 }}
            placeholder={t('subscriptionsForm.expiryLabel')}
          />
        </Form.Item>
        {/* "Never expires" is its own switch rather than an empty picker, because an empty picker
            is too easy to read as "not filled in yet" - and on this API the two are different
            requests (absent keeps, an explicit clear empties it). */}
        <Checkbox
          checked={neverExpires}
          disabled={planed}
          onChange={(event) => onNeverExpiresChange(event.target.checked)}
        >
          {t('subscriptionsForm.neverExpiresLabel')}
        </Checkbox>
      </Space>
    </Form.Item>
  )
}
