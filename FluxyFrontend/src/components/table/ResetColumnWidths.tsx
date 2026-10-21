import { Button } from 'antd'
import { ColumnWidthOutlined } from '@ant-design/icons'
import { hinted } from '../../utils/hinted'

/**
 * The way back to a table's default column widths, as an icon in the card header.
 *
 * It is one component rather than five copies for the reason the rest of the kit is: the
 * five server-paged tables all offer this control, and a rule stated once here cannot drift
 * between them. The label arrives already translated rather than as a key, because the
 * page's own literal translation call is what `probe:i18n` can see - a key handed over as a
 * plain string would be a key nothing checks.
 *
 * `type="text"` and the glyph are the point: `Reset widths` is `Сбросить ширину` in Russian,
 * and a text button in an already crowded toolbar is part of what pushed that row sideways.
 * The label has not stopped mattering, it just does not have to sit in the row - it lives in
 * the tooltip and in the button's accessible name, both of which `hinted` keeps readable
 * while the button is disabled.
 */
export default function ResetColumnWidths({
  label,
  disabled,
  onClick,
}: {
  /** The page's own translated label, used for the tooltip and the accessible name. */
  label: string
  /** True while the widths are already the defaults and there is nothing to reset. */
  disabled: boolean
  onClick: () => void
}) {
  return hinted(
    label,
    <Button
      type="text"
      aria-label={label}
      disabled={disabled}
      icon={<ColumnWidthOutlined />}
      onClick={onClick}
    />,
  )
}
