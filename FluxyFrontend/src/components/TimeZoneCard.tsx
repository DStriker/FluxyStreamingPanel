import { useMemo } from 'react'
import { Card, Select, Space, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { AutoTimeZone, knownZones } from '../lib/timeZones'

interface TimeZoneCardProps {
  /** The zone stored on the account, or null for "follow the browser". */
  value: string | null
  /** Whether a save is in flight - shown on the selector rather than as a toast. */
  saving: boolean
  /** Reports the picked zone; `null` means the visitor chose "automatic". */
  onChange: (timeZone: string | null) => void
}

/**
 * The card that picks the time zone the profile is displayed in, saving on selection.
 *
 * Deliberately its own card rather than a row of the account card or a mode of the change
 * form: it is a display preference, it saves by itself, and neither of the two things next
 * to it - the read-only facts and the password-confirmed changes - describes it. Splitting
 * it out is also what lets it be rendered on its own, which is how the probe asserts it
 * draws at all: the profile page itself is a spinner until the server answers, and an
 * effect never runs under `renderToStaticMarkup`.
 *
 * The component is presentational. Fetching and saving stay with the page, because the
 * page owns the same two things for every other card here and a second copy of the
 * "reload on success, revert on failure" rule would eventually drift from the first.
 */
export default function TimeZoneCard({ value, saving, onChange }: TimeZoneCardProps) {
  const { t } = useTranslation()
  const zones = useMemo(knownZones, [])

  return (
    <Card title={t('profile.timezoneTitle')}>
      <Space orientation="vertical" size={8} style={{ width: '100%' }}>
        <Select
          value={value ?? AutoTimeZone}
          loading={saving}
          showSearch
          optionFilterProp="label"
          style={{ width: '100%', maxWidth: 420 }}
          aria-label={t('profile.timezoneTitle')}
          options={[
            { value: AutoTimeZone, label: t('profile.timezoneAuto') },
            ...zones.map((zone) => ({ value: zone, label: zone })),
          ]}
          onChange={(next) => onChange(next === AutoTimeZone ? null : next)}
        />

        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('profile.timezoneHint')}
        </Typography.Paragraph>
      </Space>
    </Card>
  )
}
