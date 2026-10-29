import { Button, Space, Tag, Tooltip, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { CopyOutlined, DeleteOutlined, EditOutlined, ReloadOutlined } from '@ant-design/icons'
import type { ReactNode } from 'react'
import type { Subscription } from '../types'
import { useSubscriptionsListActions } from './SubscriptionsListContext'

/**
 * The subrows drawn below one subscription's main row: one strip per attached service, then one
 * "add a service" line.
 *
 * Rendered through `ServerTable`'s forced-open `expandable.expandedRowRender`, so it appears under
 * every row with no chevron and no interaction - the services are part of the row, not something
 * hidden behind a disclosure. The strips answer what a support call asks: which services this
 * subscription has, and what to do with each.
 *
 * A strip is drawn only for a service the row actually holds. The add line offers the services it
 * does not hold, and only the ones the effective plan allows - so a subscription with a playlist
 * whose plan supports playlists and MAG but not Enigma2 offers "add MAG" and nothing else.
 */
export default function SubscriptionSubrows({ subscription }: { subscription: Subscription }) {
  const { t } = useTranslation()
  const actions = useSubscriptionsListActions()

  const strips: ReactNode[] = []

  if (subscription.playlistId !== null) {
    strips.push(
      <ServiceStrip
        key="playlist"
        label={t('subscriptions.services.playlist')}
        value={subscription.playlistId}
        onCopy={() => actions.copy(subscription.playlistId!)}
        extra={[
          {
            key: 'regenerate',
            label: t('subscriptions.services.regenerate'),
            icon: <ReloadOutlined />,
            run: () => actions.regenerate(subscription),
          },
          {
            key: 'delete',
            label: t('subscriptions.services.delete'),
            icon: <DeleteOutlined />,
            danger: true,
            run: () => actions.clearPlaylist(subscription),
          },
        ]}
      />,
    )
  }

  if (subscription.magMac !== null) {
    strips.push(
      <ServiceStrip
        key="mag"
        label={t('subscriptions.services.mag')}
        value={subscription.magMac}
        onCopy={() => actions.copy(subscription.magMac!)}
        extra={[
          {
            key: 'change',
            label: t('subscriptions.services.change'),
            icon: <EditOutlined />,
            run: () => actions.changeMac(subscription, 'mag'),
          },
          {
            key: 'delete',
            label: t('subscriptions.services.delete'),
            icon: <DeleteOutlined />,
            danger: true,
            run: () => actions.clearMag(subscription),
          },
        ]}
      />,
    )
  }

  if (subscription.enigma2Mac !== null) {
    strips.push(
      <ServiceStrip
        key="enigma2"
        label={t('subscriptions.services.enigma2')}
        value={subscription.enigma2Mac}
        onCopy={() => actions.copy(subscription.enigma2Mac!)}
        extra={[
          {
            key: 'change',
            label: t('subscriptions.services.change'),
            icon: <EditOutlined />,
            run: () => actions.changeMac(subscription, 'enigma2'),
          },
          {
            key: 'delete',
            label: t('subscriptions.services.delete'),
            icon: <DeleteOutlined />,
            danger: true,
            run: () => actions.clearEnigma2(subscription),
          },
        ]}
      />,
    )
  }

  // The add line: only the services that are missing *and* that the effective plan allows.
  const addable: ReactNode[] = []

  if (subscription.playlistId === null && subscription.supportsPlaylist) {
    addable.push(
      <Button
        key="add-playlist"
        size="small"
        type="link"
        disabled={!actions.canEdit}
        onClick={() => actions.addPlaylist(subscription)}
      >
        {t('subscriptions.services.addPlaylist')}
      </Button>,
    )
  }

  if (subscription.magMac === null && subscription.supportsMag) {
    addable.push(
      <Button
        key="add-mag"
        size="small"
        type="link"
        disabled={!actions.canEdit}
        onClick={() => actions.changeMac(subscription, 'mag')}
      >
        {t('subscriptions.services.addMag')}
      </Button>,
    )
  }

  if (subscription.enigma2Mac === null && subscription.supportsEnigma2) {
    addable.push(
      <Button
        key="add-enigma2"
        size="small"
        type="link"
        disabled={!actions.canEdit}
        onClick={() => actions.changeMac(subscription, 'enigma2')}
      >
        {t('subscriptions.services.addEnigma2')}
      </Button>,
    )
  }

  return (
    <Space direction="vertical" size={4} style={{ width: '100%' }}>
      {strips}
      {addable.length > 0 && (
        <Space size={8} wrap>
          {addable}
        </Space>
      )}
    </Space>
  )
}

/** One attached service, its value, and the actions this strip offers. */
function ServiceStrip({
  label,
  value,
  onCopy,
  extra,
}: {
  label: string
  value: string
  onCopy: () => void
  extra: readonly {
    key: string
    label: string
    icon: ReactNode
    danger?: boolean
    run: () => void
  }[]
}) {
  const { t } = useTranslation()

  return (
    <Space size={8} wrap>
      <Tag style={{ marginInlineEnd: 0 }}>{label}</Tag>
      <Typography.Text code>{value}</Typography.Text>
      <Tooltip title={t('subscriptions.services.copy')}>
        <Button size="small" type="text" icon={<CopyOutlined />} onClick={onCopy} />
      </Tooltip>
      {extra.map((action) => (
        <Tooltip key={action.key} title={action.label}>
          <Button
            size="small"
            type="text"
            danger={action.danger}
            icon={action.icon}
            onClick={action.run}
          />
        </Tooltip>
      ))}
    </Space>
  )
}
