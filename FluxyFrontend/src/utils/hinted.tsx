import type { ReactElement, ReactNode } from 'react'
import { Tooltip } from 'antd'

/**
 * A tooltip around a row action that also works while the button is disabled.
 *
 * A native `<button disabled>` swallows the pointer events a tooltip listens for, and every
 * table on this side of the application has buttons that are disabled *on purpose* - the
 * ones the server refuses for a particular row (`cannot_block_self`, `user_group_immutable`)
 * or refuses because the row belongs to nobody yet. The span is what keeps the reason
 * reachable: a greyed button that says nothing is a dead control, and the whole point of
 * disabling it rather than letting the refusal arrive as a toast was to say why before the
 * click.
 *
 * It lives here rather than beside the first page that used it because it is now two pages'
 * rather than one's - two copies of this wrapper would be two places for one of them to lose
 * the span, and nothing in `typecheck`, `lint` or the build would notice the difference.
 * The same reasoning extracted `useColumnWidths` a table earlier: the rules are short, and
 * they are the kind of short that reads as obvious until one copy drifts.
 *
 * Called as a function rather than declared as a component because its arguments are a
 * label and the element to wrap, and both call sites already read that way.
 */
export const hinted = (title: string, button: ReactNode): ReactElement => (
  <Tooltip title={title}>
    <span style={{ display: 'inline-flex' }}>{button}</span>
  </Tooltip>
)
