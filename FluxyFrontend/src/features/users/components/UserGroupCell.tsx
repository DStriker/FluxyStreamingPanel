import { Space } from 'antd'
import { WalletOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { TruncatedCell } from '../../../components/table'
import type { AdminUser } from '../types'

/**
 * One row's group cell in the accounts table: the group's name, and - for a reseller only -
 * the account's balance as a quiet second line.
 *
 * The two are one fact read together: which group this account is in, and, when that group is
 * a reseller's, what it holds. The second line is deliberately *inside* this cell rather than
 * a ninth column: the table's pinned 1190 is what keeps the four action buttons inside a Full
 * HD viewport, and the precedent is the subscriptions table's name cell.
 *
 * It is a component rather than an expression because a column `render` runs only once a row
 * exists, and every render in `probe:render` lands before the first answer - so nothing there
 * would ever draw this line. Rendering it directly is the only check that sees the two
 * branches at all (the same reason `LastSeenCell` is one).
 *
 * `0` is drawn as `0`: an empty wallet is a fact about a reseller. Every other role draws
 * nothing on that line - not even a dash - because credits do not apply to it, and a dash
 * would claim a value the server could not determine. The glyph carries no text of its own,
 * so the amount has its own accessible name rather than leaving a bare number beside a group
 * name.
 */
export default function UserGroupCell({ user }: { user: AdminUser }) {
  const { t } = useTranslation()

  return (
    <Space orientation="vertical" size={0} style={{ lineHeight: 1.4, width: '100%' }}>
      <TruncatedCell text={user.groupName} full={user.groupName} />
      {user.role === 'Reseller' && (
        <span
          className="users-credits"
          aria-label={t('users.creditsAria', { count: user.credits })}
        >
          <WalletOutlined />
          <span>{user.credits}</span>
        </span>
      )}
    </Space>
  )
}
