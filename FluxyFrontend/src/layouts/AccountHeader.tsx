import { useNavigate } from 'react-router-dom'
import { App, Avatar, Button, Dropdown, Image, Segmented, Tooltip, Typography } from 'antd'
import type { MenuProps } from 'antd'
import {
  LaptopOutlined,
  LogoutOutlined,
  MenuFoldOutlined,
  MenuOutlined,
  MenuUnfoldOutlined,
  MoonOutlined,
  SunOutlined,
  UserOutlined,
} from '@ant-design/icons'
import { useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import LanguageSwitcher from '../components/LanguageSwitcher'
import config, { routePath } from '../config'
import { messageForError } from '../utils/http'
import { areaForRole } from '../utils/session'
import { useSession } from '../utils/sessionContext'
import { ThemeChoice, setThemeChoice, useThemeChoice } from '../utils/theme'
import { internalPath } from '../utils/url'
import { useSignOut } from '../features/auth/hooks/mutations'

/**
 * The pinned top bar: where this area is, who is signed in, how to change the theme, and
 * the account's own two actions - the profile and the way out - behind the identity.
 *
 * The username comes from `RequireAuth`'s read of `/auth/me` rather than from a second
 * request of this component's own - the guard had to ask anyway, and two answers to the
 * same question are two chances for them to disagree.
 *
 * The profile shortcut points at the profile section of *this* role's area, computed
 * through `areaForRole`, because that is the one section every role has and it needs no
 * path invented here that the router might not have. It is the area and never
 * `homeForRole`: profile is a sibling of the dashboard, and hanging it off the landing
 * path is what once made this button go to `/admin/dashboard/profile`.
 *
 * The avatar and the name are one trigger, and the two actions live in the menu it opens
 * on hover or a click - and on a tap below the rail's breakpoint, where there is no pointer
 * to hover.
 * They used to sit beside it as their own buttons, which put two rarely used controls at
 * the end of every page's bar; the extra gesture is the price of that room, and it is the
 * one place this bar deliberately trades one for the other. The identity itself stays
 * visible because knowing whose session is on screen is not a menu's job.
 *
 * The leading button changes meaning with the shell: on the desktop it folds and unfolds
 * the rail, and in compact mode it opens the drawer that replaces the rail. The icon and
 * the accessible name follow that difference, and `aria-expanded` is stated only where
 * there is a drawer whose state it can describe.
 */
export default function AccountHeader({
  titleKey,
  collapsed,
  compact,
  menuOpen,
  onToggle,
}: {
  /** Locale key of the heading the layout put in the bar. */
  titleKey: string
  /** Whether the rail is currently narrowed to its icons. */
  collapsed: boolean
  /** Whether the shell is below the rail's breakpoint and the menu is a drawer. */
  compact: boolean
  /** Whether that drawer is open - what tells a screen reader the button's state. */
  menuOpen: boolean
  /** Opens the drawer in compact mode, or flips and stores `collapsed` on the desktop. */
  onToggle: () => void
}) {
  const navigate = useNavigate()
  const { t } = useTranslation()
  const { message } = App.useApp()
  const queryClient = useQueryClient()
  const session = useSession()
  const storedTheme = useThemeChoice()
  const signOut = useSignOut()

  // Only `RequireAuth` renders this bar, and it always publishes a session - so `null` here
  // means the guard was bypassed rather than that nobody happens to be signed in. An empty
  // bar would still draw the logo, the theme switch and an account menu for a visitor with
  // no session to read or end, so it draws nothing at all.
  if (!session) return null

  const profilePath = `${areaForRole(session.role)}/profile`
  const toggleLabel = compact
    ? t('header.openMenu')
    : collapsed
      ? t('header.expandMenu')
      : t('header.collapseMenu')
  const accountLabel = `${t('header.accountMenu')}: ${session.username}`

  const themeOptions = [
    { value: ThemeChoice.Light, icon: <SunOutlined />, tooltip: t('header.themeLight') },
    { value: ThemeChoice.Dark, icon: <MoonOutlined />, tooltip: t('header.themeDark') },
    { value: ThemeChoice.System, icon: <LaptopOutlined />, tooltip: t('header.themeSystem') },
  ]

  const handleSignOut = async () => {
    // The tokens are in cookies the page cannot see, so the only way out is to ask the
    // server to end the session. `signOut` - through `useSignOut`, which is what mints
    // the antiforgery token and calls it - mints that token immediately before sending: a
    // token carried over from the sign-in page was minted before the session existed and
    // the server refuses it, which is what once made this button look dead. A failure is
    // reported rather than swallowed for the same reason: with no `catch` the rejection
    // disappeared, the page did not move, and the visitor had no way to tell "nothing
    // happened" from "something went wrong".
    try {
      const result = await signOut.mutateAsync()
      // The cache is dropped before the page moves. Every entry in it belongs to the
      // account whose session just ended, and `staleTime: 0` only marks an entry stale -
      // it does not stop the next sign-in on this machine from painting the previous
      // account's rows while their refetch is on its way. The same clearing runs when a
      // session ends by itself, in `SessionExpiredWatcher`.
      queryClient.clear()
      message.success(t('messages.api.signed_out'))
      // The sign-in form is where a sign-out belongs even when the server names no
      // destination, so `internalPath` here only has to refuse what is not walkable -
      // anything else falls back to the entrance this application chose for itself.
      navigate(internalPath(result?.redirect) ?? routePath(config.CLIENT_LOGIN_ROUTE), {
        replace: true,
      })
    } catch (error) {
      message.error(messageForError(error))
    }
  }

  // The two things a visitor does with their own session, in the order they are reached for:
  // the profile (a place they may return to) and then the way out, set apart because ending
  // the session is not one more item in the same list.
  const accountItems: MenuProps['items'] = [
    {
      key: 'profile',
      icon: <UserOutlined />,
      label: t('nav.items.profile'),
    },
    { type: 'divider' },
    {
      key: 'sign-out',
      icon: <LogoutOutlined />,
      label: t('actions.signOut'),
    },
  ]

  const handleAccountMenu: MenuProps['onClick'] = ({ key }) => {
    if (key === 'profile') {
      navigate(profilePath)
      return
    }

    if (key === 'sign-out') void handleSignOut()
  }

  return (
    <>
      <div className="layout-shell__header-side">
        <Tooltip title={toggleLabel}>
          <Button
            type="text"
            aria-label={toggleLabel}
            aria-expanded={compact ? menuOpen : undefined}
            icon={
              compact ? (
                <MenuOutlined />
              ) : collapsed ? (
                <MenuUnfoldOutlined />
              ) : (
                <MenuFoldOutlined />
              )
            }
            onClick={onToggle}
          />
        </Tooltip>

        {/* The width lives in `index.css` so the phone breakpoints can shrink it. The
            image keeps its own aspect ratio from that width. */}
        <Image
          rootClassName="layout-shell__logo"
          src="/logo.jpg"
          alt="Fluxy"
          preview={false}
        />

        <Typography.Title level={5} className="layout-shell__title">
          {t(titleKey)}
        </Typography.Title>
      </div>

      <div className="layout-shell__header-side layout-shell__header-side--end">
        <LanguageSwitcher />

        <Segmented
          size="small"
          value={storedTheme}
          onChange={setThemeChoice}
          options={themeOptions}
        />

        {/* The identity is the trigger: whose session this is and what may be done with it
            are one question, so they are one control rather than three. The menu follows the
            pointer on the desktop and the tap in compact mode; `aria-haspopup` is stated
            because antd's dropdown does not add it to its trigger, and without it nothing
            would tell a screen reader that a menu is coming. */}
        <Dropdown
          menu={{ items: accountItems, onClick: handleAccountMenu }}
          placement="bottomRight"
          // A pointer can hover; a finger cannot. The tap is the compact equivalent of the
          // gesture the desktop has always used. The desktop keeps `click` as well, because
          // a trigger that only opens on hover cannot be opened by Enter on a focused
          // button - the keyboard path would end at a menu that never appears.
          trigger={compact ? ['click'] : ['hover', 'click']}
        >
          <Button
            type="text"
            className="layout-shell__user"
            aria-label={accountLabel}
            aria-haspopup="menu"
          >
            <Avatar size="small" icon={<UserOutlined />} />
            <span className="layout-shell__username">{session.username}</span>
          </Button>
        </Dropdown>
      </div>
    </>
  )
}
