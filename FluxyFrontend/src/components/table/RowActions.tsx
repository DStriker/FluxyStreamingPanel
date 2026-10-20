import { Fragment } from 'react'
import type { ReactNode } from 'react'
import { Button, Space } from 'antd'
import { hinted } from '../../utils/hinted'
import { ROW_ACTION_GAP } from '../../utils/tableWidths'

/**
 * One button of a row's action cluster, as the page that owns the row names it.
 *
 * `label` is the button's whole identity: an icon carries no text of its own, so this string
 * is both what the tooltip says and what a screen reader announces. `key` is React's, `icon`
 * is the glyph, and the states are the ones the server owns the truth of - a button disabled
 * here is one whose request would be refused, and the label is where that reason lives.
 */
export interface RowAction {
  key: string
  label: string
  icon: ReactNode
  onClick?: () => void
  disabled?: boolean
  loading?: boolean
  /** The one destructive move of the set, drawn in red. */
  danger?: boolean
}

/**
 * A row's actions as icon buttons with tooltips - the cluster the administrator tables draw
 * on the right of every row.
 *
 * Five tables state the same three rules, so they are stated once here: the button is an
 * icon-only `text` button of the small size (the row's own height is the table's, not the
 * button's), every button is wrapped in `hinted` - an icon has no text of its own, and a
 * disabled button is exactly the state whose reason has to stay readable - and the actions
 * sit four pixels apart in one `Space`, so the cluster keeps its rhythm when a status
 * changes which buttons a row draws.
 *
 * The gap is `ROW_ACTION_GAP` rather than a literal, because the same number is what
 * `actionsColumnWidth` adds up to size the column the cluster sits in: the column is meant
 * to be exactly as wide as this cluster, and two copies of the gap would be two places for
 * that to be quietly wrong.
 *
 * The page keeps what differs: which actions a row has, in what order, what each one says,
 * and whether it may be pressed - where "may" is a per-row question (`isSelf`, `isBase`, the
 * one id whose request is in flight) that only the page can answer.
 */
export default function RowActions({ actions }: { actions: readonly RowAction[] }) {
  return (
    <Space size={ROW_ACTION_GAP}>
      {actions.map((action) => (
        <Fragment key={action.key}>
          {hinted(
            action.label,
            <Button
              type="text"
              size="small"
              icon={action.icon}
              danger={action.danger}
              loading={action.loading}
              disabled={action.disabled}
              onClick={action.onClick}
            />,
          )}
        </Fragment>
      ))}
    </Space>
  )
}
