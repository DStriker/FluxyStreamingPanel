import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import type { AdminUser } from '../../../types'

/** What the "Last visit" cell is given: the account, the moment, and how to print it. */
type LastSeenCellProps = {
  /** The area the accounts table hangs from, so the address it opens sits inside that area. */
  area: string
  /** The account in this row - its identifier for the filter, its name for the link's own name. */
  user: Pick<AdminUser, 'id' | 'username'>
  /** The last visit as the server reported it, or `null` for an account that has never signed in. */
  value: string | null
  /** How this page prints a moment: its language, its zone, and a dash for a value it cannot. */
  format: (value: string) => string
}

/**
 * One cell of the accounts table's "Last visit" column: the moment, drawn as the way to that
 * account's own visit history.
 *
 * A component rather than an inline `render` body for one reason, and it is why this file
 * exists at all: a column's `render` is reached only once a row is on screen, and every
 * render of the accounts table in `probe:render` lands in the state *before* the first
 * answer arrives - so a link written inline there would be the one piece of that page that
 * nothing in this repository can see. That is precisely the shape of the bug the probe was
 * written after (`AuthCard` accepting a footer it never drew): typecheck, lint and the build
 * were all green and the link was simply absent. Rendered on its own, it is one assertion.
 *
 * **The date is the link rather than a label beside it.** A second line over the moment was
 * the first shape this took, and it cost two lines of every row for a link the row already
 * named: the header of the column says "Last visit", the username is the cell to the left,
 * and "Visits" added on top said neither better. Making the moment itself the link keeps the
 * cell one line, costs no width, and makes the whole of what the column is for clickable -
 * "when did this account last come through" and "show me all of them" are the same cell.
 *
 * An account that has never signed in draws a dash and no link. There is no moment here to
 * turn into one, the destination would be an empty page, and a link over a dash would
 * promise a history the server has none of.
 */
export default function LastSeenCell({ area, user, value, format }: LastSeenCellProps) {
  const { t } = useTranslation()

  if (!value) return '—'

  return (
    <Link
      // The account arrives already chosen: this row *is* the filter, so the page that opens
      // is the one the visitor asked for rather than a list they then have to narrow.
      to={`${area}/user-sessions?userId=${user.id}`}
      // The destination, named for a hover and for a screen reader. It is `title` and not
      // `aria-label` deliberately: an `aria-label` replaces the link's text as its accessible
      // name, and a name that does not contain the visible date breaks Label in Name - the
      // rule that lets somebody say the text they can see. `title` is the description beside
      // the name instead of a replacement for it.
      title={t('users.lastSeenLinkTitle', { name: user.username })}
    >
      {format(value)}
    </Link>
  )
}
