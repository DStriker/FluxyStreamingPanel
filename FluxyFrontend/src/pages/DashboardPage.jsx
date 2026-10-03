import { Card, Col, Row, Space, Statistic, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { useSession } from '../lib/sessionContext'

/**
 * The landing page of an area: a greeting and a row of widget slots.
 *
 * The slots carry no numbers because there is no endpoint behind them yet - a widget that
 * printed `0` would be a claim, and a claim this application cannot make is worse than an
 * empty cell. They exist so the shell has something in it to look at and so the shape the
 * real widgets will take is settled before there is data to fill it.
 *
 * The greeting is the one thing that is true: the name comes from `RequireAuth`'s read of
 * `GET /auth/me`, so it is the account the session actually belongs to rather than a
 * string the page remembered from somewhere.
 */
export default function DashboardPage({ sectionKey }) {
  const { t } = useTranslation()
  const session = useSession()

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Card>
        <Typography.Title level={4} style={{ marginTop: 0 }}>
          {t('dashboard.greeting', { name: session?.username ?? '' })}
        </Typography.Title>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('messages.areaNotBuilt', { section: t(sectionKey) })}
        </Typography.Paragraph>
      </Card>

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={8}>
          <Card>
            <Statistic title={t('dashboard.stats.today')} value="—" />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <Card>
            <Statistic title={t('dashboard.stats.week')} value="—" />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <Card>
            <Statistic title={t('dashboard.stats.month')} value="—" />
          </Card>
        </Col>
      </Row>
    </Space>
  )
}
