import type { ComponentType } from 'react'
import {
  AppstoreOutlined,
  DashboardOutlined,
  IdcardOutlined,
  SettingOutlined,
  ShoppingCartOutlined,
  TeamOutlined,
  UserOutlined,
  UsergroupAddOutlined,
} from '@ant-design/icons'
import DashboardPage from '../pages/DashboardPage'
import ProfilePage from '../pages/ProfilePage'
import type { Role } from '../types'

/**
 * An icon as this table stores it.
 *
 * Taken from one of the icons rather than written out, because every entry in the table is
 * an export of the same package with the same shape - a component antd's `Menu` renders
 * from `createElement(icon)` with no props at all. Naming the type after the value means
 * the table cannot drift from what the renderer accepts.
 */
type NavIcon = typeof DashboardOutlined

/** One entry of a category: a route, the label it is shown under, and the page it opens. */
export interface NavItem {
  /** The item's own id, distinct from the address - `dashboard`, `profile`. */
  key: string
  /** Relative to the area's prefix. Empty for the role's home - see `NAVIGATION`. */
  path: string
  /** Locale key of the label. */
  labelKey: string
  icon: NavIcon
  /**
   * The page this item opens, or nothing for the placeholder.
   *
   * Every page behind a menu takes the section key, including the ones that ignore it:
   * the router renders all of them through one `<Page sectionKey={...} />` call, and a
   * page typed as taking no props would not accept it.
   */
  page?: ComponentType<{ sectionKey: string }>
}

/** A category of items - `overview`, `management`, `account`. */
export interface NavGroup {
  key: string
  labelKey: string
  icon: NavIcon
  items: NavItem[]
}

/**
 * What each signed-in role sees in the sidebar, as categories of items.
 *
 * This is the single source for two things that would otherwise drift apart: the menu
 * `AccountSidebar` renders and the child routes the router declares. A menu item without a
 * route is a click that lands on the catch-all 404, and a route without a menu item is a
 * page nobody can reach - both are invisible to `lint` and to `build`, which is exactly
 * how the missing `footer` prop in `AuthCard` shipped. So routes are generated from this
 * table rather than written out next to it.
 *
 * `path` is relative to the area's own prefix - `/admin`, `/client` - which is exactly what
 * `areaForRole` names, so `profile` comes out as `/admin/profile` and never as
 * `/admin/dashboard/profile`. An empty path is this role's **home**: the dashboard, sitting
 * at the address the server names in `redirect` (`config.CLIENT_HOME_ROUTE` and friends),
 * which is one page inside the prefix rather than the prefix itself.
 *
 * An item without a `page` renders `SectionPage`, the placeholder with its own heading;
 * only the dashboard is a real page for now. That keeps the placeholder honest (it says
 * which section is unfinished) without writing one near-identical file per menu entry.
 *
 * Keyed by the role name exactly as the backend reports it in `me`, so this and
 * `homeForRole` read the same strings.
 */
export const NAVIGATION: Record<Role, NavGroup[]> = {
  Client: [
    {
      key: 'overview',
      labelKey: 'nav.groups.overview',
      icon: AppstoreOutlined,
      items: [
        {
          key: 'dashboard',
          path: '',
          labelKey: 'nav.items.dashboard',
          icon: DashboardOutlined,
          page: DashboardPage,
        },
        {
          key: 'orders',
          path: 'orders',
          labelKey: 'nav.items.orders',
          icon: ShoppingCartOutlined,
        },
      ],
    },
    {
      key: 'account',
      labelKey: 'nav.groups.account',
      icon: UserOutlined,
      items: [
        {
          key: 'profile',
          path: 'profile',
          labelKey: 'nav.items.profile',
          icon: IdcardOutlined,
          page: ProfilePage,
        },
      ],
    },
  ],
  Reseller: [
    {
      key: 'overview',
      labelKey: 'nav.groups.overview',
      icon: AppstoreOutlined,
      items: [
        {
          key: 'dashboard',
          path: '',
          labelKey: 'nav.items.dashboard',
          icon: DashboardOutlined,
          page: DashboardPage,
        },
        {
          key: 'clients',
          path: 'clients',
          labelKey: 'nav.items.clients',
          icon: TeamOutlined,
        },
      ],
    },
    {
      key: 'account',
      labelKey: 'nav.groups.account',
      icon: UserOutlined,
      items: [
        {
          key: 'profile',
          path: 'profile',
          labelKey: 'nav.items.profile',
          icon: IdcardOutlined,
          page: ProfilePage,
        },
      ],
    },
  ],
  Admin: [
    {
      key: 'overview',
      labelKey: 'nav.groups.overview',
      icon: AppstoreOutlined,
      items: [
        {
          key: 'dashboard',
          path: '',
          labelKey: 'nav.items.dashboard',
          icon: DashboardOutlined,
          page: DashboardPage,
        },
      ],
    },
    {
      key: 'management',
      labelKey: 'nav.groups.management',
      icon: SettingOutlined,
      items: [
        {
          key: 'users',
          path: 'users',
          labelKey: 'nav.items.users',
          icon: UsergroupAddOutlined,
        },
        {
          key: 'settings',
          path: 'settings',
          labelKey: 'nav.items.settings',
          icon: SettingOutlined,
        },
      ],
    },
    {
      key: 'account',
      labelKey: 'nav.groups.account',
      icon: UserOutlined,
      items: [
        {
          key: 'profile',
          path: 'profile',
          labelKey: 'nav.items.profile',
          icon: IdcardOutlined,
          page: ProfilePage,
        },
      ],
    },
  ],
}

/** Every menu item of a role, flattened in display order. */
export const flatItems = (role: Role): NavItem[] =>
  (NAVIGATION[role] ?? []).flatMap((group) => group.items)
