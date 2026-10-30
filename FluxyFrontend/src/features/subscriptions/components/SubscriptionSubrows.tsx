import { Button, Space, Tag, Tooltip, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { CopyOutlined, DeleteOutlined, EditOutlined, ReloadOutlined } from '@ant-design/icons'
import { Fragment } from 'react'
import type { ReactNode } from 'react'
import type { Subscription } from '../types'
import { hinted } from '../../../utils/hinted'
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
 *
 * **Every write button is gated on `editSubscriptions`, and copy is not.** The page is readable
 * with the view grant alone, and an operator who may only read must not be handed a button whose
 * request the server refuses - a raw 403 on a support call is worse than a greyed button with the
 * reason attached. Copying a value changes nothing, so it stays live.
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
        canEdit={actions.canEdit}
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
        canEdit={actions.canEdit}
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
        canEdit={actions.canEdit}
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
      <AddServiceButton
        key="add-playlist"
        canEdit={actions.canEdit}
        label={t('subscriptions.services.addPlaylist')}
        onClick={() => actions.addPlaylist(subscription)}
      />,
    )
  }

  if (subscription.magMac === null && subscription.supportsMag) {
    addable.push(
      <AddServiceButton
        key="add-mag"
        canEdit={actions.canEdit}
        label={t('subscriptions.services.addMag')}
        onClick={() => actions.changeMac(subscription, 'mag')}
      />,
    )
  }

  if (subscription.enigma2Mac === null && subscription.supportsEnigma2) {
    addable.push(
      <AddServiceButton
        key="add-enigma2"
        canEdit={actions.canEdit}
        label={t('subscriptions.services.addEnigma2')}
        onClick={() => actions.changeMac(subscription, 'enigma2')}
      />,
    )
  }

  return (
    // Indented to line up with the "Subscription" column rather than flush with the row edge:
    // with the expand column hidden (design decision 17) the strips are the only thing telling a
    // reader these lines belong to the row above, and the indent is that statement.
    //
    // The strips do **not** inherit the row's highlight on their own: antd paints the expanded
    // `<tr>` with a fixed background. The page marks a selected row with `subscription-row-selected`
    // and `index.css` repaints that row *and* the expanded row beside it, which is what makes the
    // highlight reach here.
    <div style={{ width: '100%', paddingInlineStart: 16, paddingBlock: 4 }}>
      <Space direction="vertical" size={4} style={{ width: '100%' }}>
        {strips}
        {addable.length > 0 && (
          <Space size={8} wrap>
            {addable}
          </Space>
        )}
      </Space>
    </div>
  )
}

/** One attached service, its value, and the actions this strip offers. */
function ServiceStrip({
  label,
  value,
  canEdit,
  onCopy,
  extra,
}: {
  label: string
  value: string
  /** Whether the operator may change subscriptions at all. Copy is never gated on it. */
  canEdit: boolean
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
      {/* Copying changes nothing, so it stays live for a read-only operator. */}
      <Tooltip title={t('subscriptions.services.copy')}>
        <Button size="small" type="text" icon={<CopyOutlined />} onClick={onCopy} />
      </Tooltip>
      {extra.map((action) => (
        <Fragment key={action.key}>
          {hinted(
            canEdit ? action.label : t('subscriptions.noPermission'),
            <Button
              size="small"
              type="text"
              danger={action.danger}
              icon={action.icon}
              disabled={!canEdit}
              onClick={action.run}
            />,
          )}
        </Fragment>
      ))}
    </Space>
  )
}

/** One "add a service" button, gated on the same grant the strip's own actions are. */
function AddServiceButton({
  canEdit,
  label,
  onClick,
}: {
  canEdit: boolean
  label: string
  onClick: () => void
}) {
  const { t } = useTranslation()

  return hinted(
    canEdit ? label : t('subscriptions.noPermission'),
    <Button size="small" type="link" disabled={!canEdit} onClick={onClick}>
      {label}
    </Button>,
  )
}
