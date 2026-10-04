import { Card, Typography } from 'antd'
import { useTranslation } from 'react-i18next'

/**
 * Every menu entry that has a route but not a page of its own yet.
 *
 * It renders the section's own heading rather than a generic "coming soon", because the
 * heading is what tells a reader whether the wiring is right: a menu item that opens a
 * panel saying the wrong section's name is a bug that `lint`, `build` and a route table
 * will all pass.
 */
export default function SectionPage({ sectionKey }: { sectionKey: string }) {
  const { t } = useTranslation()

  return (
    <Card title={t(sectionKey)}>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
        {t('messages.areaNotBuilt', { section: t(sectionKey) })}
      </Typography.Paragraph>
    </Card>
  )
}
