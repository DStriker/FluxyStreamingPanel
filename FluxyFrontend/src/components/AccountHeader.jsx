import { useNavigate } from 'react-router-dom'
import { App, Avatar, Button, Image, Segmented, Space, Tooltip, Typography } from 'antd'
import {
  LaptopOutlined,
  LogoutOutlined,
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  MoonOutlined,
  SettingOutlined,
  SunOutlined,
  UserOutlined,
} from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import LanguageSwitcher from './LanguageSwitcher'
import config, { routePath } from '../config'
import { signOut } from '../lib/api'
import { messageForError } from '../lib/http'
import { areaForRole } from '../lib/session'
import { useSession } from '../lib/sessionContext'
import { ThemeChoice, setThemeChoice, useThemeChoice } from '../lib/theme'

/**
 * The pinned top bar: where this area is, who is signed in, how to get to their settings,
 * how to change the theme, and how to get out.
 *
 * The username comes from `RequireAuth`'s read of `/auth/me` rather than from a second
 * request of this component's own - the guard had to ask anyway, and two answers to the
 * same question are two chances for them to disagree.
 *
 * The settings shortcut points at the profile section of *this* role's area, computed
 * through `areaForRole`, because that is the one section every role has and it needs no
 * path invented here that the router might not have. It is the area and never
 * `homeForRole`: profile is a sibling of the dashboard, and hanging it off the landing
 * path is what once made this button go to `/admin/dashboard/profile`.
 */
export default function AccountHeader({ titleKey, collapsed, onToggle }) {
  const navigate = useNavigate()
  const { t } = useTranslation()
  const { message } = App.useApp()
  const session = useSession()
  const storedTheme = useThemeChoice()

  const profilePath = `${areaForRole(session.role)}/profile`
  const toggleLabel = collapsed ? t('header.expandMenu') : t('header.collapseMenu')

  const themeOptions = [
    { value: ThemeChoice.Light, icon: <SunOutlined />, tooltip: t('header.themeLight') },
    { value: ThemeChoice.Dark, icon: <MoonOutlined />, tooltip: t('header.themeDark') },
    { value: ThemeChoice.System, icon: <LaptopOutlined />, tooltip: t('header.themeSystem') },
  ]

  const handleSignOut = async () => {    // The tokens are in cookies the page cannot see, so the only way out is to ask the
    // server to end the session. `signOut` mints its own antiforgery token immediately
    // before sending - a token carried over from the sign-in page was minted before the
    // session existed and the server refuses it, which is what once made this button look
    // dead. A failure is reported rather than swallowed for the same reason: with no
    // `catch` the rejection disappeared, the page did not move, and the visitor had no way
    // to tell "nothing happened" from "something went wrong".
    try {
      const result = await signOut()
      message.success(t('messages.api.signed_out'))
      navigate(result?.redirect ?? routePath(config.CLIENT_LOGIN_ROUTE), { replace: true })
    } catch (error) {
      message.error(messageForError(error))
    }
  }

  return (
    <>
      <div className="layout-shell__header-side">
        <Tooltip title={toggleLabel}>
          <Button
            type="text"
            aria-label={toggleLabel}
            icon={collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}
            onClick={onToggle}
          />
        </Tooltip>

        <Image
          className="layout-shell__logo"
          src="/logo.jpg"
          alt="Fluxy"
          width={132}
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

        {/* Identity rather than a control: it says whose session this is, and the two
            things a visitor does with it sit beside it as their own buttons. Hiding both
            behind one menu would have made "quick transition to settings" two clicks. */}
        <Space size={8} className="layout-shell__user">
          <Avatar size="small" icon={<UserOutlined />} />
          <span className="layout-shell__username">{session.username}</span>
        </Space>

        <Tooltip title={t('header.settings')}>
          <Button
            type="text"
            aria-label={t('header.settings')}
            icon={<SettingOutlined />}
            onClick={() => navigate(profilePath)}
          />
        </Tooltip>

        <Button type="text" icon={<LogoutOutlined />} onClick={handleSignOut}>
          <span className="layout-shell__signout-label">{t('actions.signOut')}</span>
        </Button>
      </div>
    </>
  )
}
