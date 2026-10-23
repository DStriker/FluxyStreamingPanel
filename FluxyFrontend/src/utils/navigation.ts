import type { ComponentType } from 'react'
import {
  ApartmentOutlined,
  AppstoreOutlined,
  CustomerServiceOutlined,
  DashboardOutlined,
  GlobalOutlined,
  HistoryOutlined,
  IdcardOutlined,
  ImportOutlined,
  LaptopOutlined,
  PlaySquareOutlined,
  PlusOutlined,
  ShoppingCartOutlined,
  TableOutlined,
  TagsOutlined,
  TeamOutlined,
  UnorderedListOutlined,
  UserAddOutlined,
  UserOutlined,
  VideoCameraOutlined,
} from '@ant-design/icons'
import type { Role } from '../types'
import { areaForRole, homeForRole } from './session'

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
interface NavItem {
  /** The item's own id, distinct from the address - `dashboard`, `profile`. */
  key: string
  /** Relative to the area's prefix. Empty for the role's home - see `NAVIGATION`. */
  path: string
  /** Locale key of the label. */
  labelKey: string
  icon: NavIcon
  /**
   * How to get the page this item opens, or nothing for the placeholder.
   *
   * A loader rather than the component itself, and that is the whole reason this field is
   * shaped like a function: this table is what the sidebar *reads*, so importing the pages
   * here would drag the profile editor and the dashboard into the first chunk every
   * visitor downloads to see a sign-in form. `routes.tsx` calls the loader and wraps the
   * answer in `React.lazy`, which means the module is fetched when an address asks for it
   * rather than when the table is defined.
   *
   * Every page behind a menu takes the section key, including the ones that ignore it:
   * the router renders all of them through one `<Page sectionKey={...} />` call, and a
   * page typed as taking no props would not accept it. The `default` of the module is what
   * has to satisfy that, so the shape is stated here once instead of at six call sites.
   */
  page?: () => Promise<{ default: ComponentType<{ sectionKey: string }> }>
}

/** A category of items - `overview`, `users`, `account`. */
interface NavGroup {
  key: string
  labelKey: string
  icon: NavIcon
  items: NavItem[]
}

/**
 * The four content sections, one group per kind, in the order the domain names them.
 *
 * Each section offers the same three entries - add, manage, categories - because the four
 * sections are the same section wearing four names: what differs between them is the kind
 * of content everything inside them works on, which is a fact the group carries and the
 * items repeat. Building them from one table is what keeps four copies of that shape from
 * drifting: a fifth kind is one entry here plus its labels in the locale files, not a
 * hand-written fourth of a rail.
 *
 * `categories` is a real page for all four. `add` and `manage` are real pages for the
 * **streams and the audio streams** sections - this pass built the video streams table and
 * its form, then the audio ones - and carry no `page` for the other two, so the router
 * renders `SectionPage` for them: the placeholder that says the section is still under
 * construction, which is the honest answer for entries this pass deliberately did not build.
 * Their menu keys exist either way, so filling them in later is one optional loader away
 * rather than a re-reading of the rail.
 *
 * `import` is a fourth entry only the two stream sections carry, and it sits immediately
 * after `add`: reading a playlist is the other way to add streams, not a fourth thing to
 * look at. It is optional for the same reason the other loaders are - the two sections
 * without an import page must not grow a placeholder where these have a real page.
 *
 * The optional loaders are typed by an interface rather than inferred from `as const`,
 * because an array of objects where only one carries a field is a *union* of two shapes and
 * `section.addPage` would then not exist on three of its four members. Stating the optionality
 * once here is what lets one table hold a real page and three placeholders at the same time.
 */
interface ContentSection {
  /** Item-key prefix: `videoStreams` makes `videoStreamsAdd`, `videoStreamsManage`, … */
  key: string
  /** Address under the area's prefix: `video-streams` becomes `/admin/video-streams`. */
  path: string
  /** Locale key of the category's own label. */
  labelKey: string
  icon: NavIcon
  /** The categories page, the one entry all four sections have in common. */
  categoriesPage: () => Promise<{ default: ComponentType<{ sectionKey: string }> }>
  /** The add form, present only where one has been built. */
  addPage?: () => Promise<{ default: ComponentType<{ sectionKey: string }> }>
  /** The playlist import form, present only where one has been built. */
  importPage?: () => Promise<{ default: ComponentType<{ sectionKey: string }> }>
  /** The table, present only where one has been built. */
  managePage?: () => Promise<{ default: ComponentType<{ sectionKey: string }> }>
}

const CONTENT_SECTIONS: ContentSection[] = [
  {
    key: 'videoStreams',
    path: 'video-streams',
    labelKey: 'nav.groups.videoStreams',
    icon: VideoCameraOutlined,
    categoriesPage: () => import('../pages/admin/ContentCategoriesPage'),
    addPage: () => import('../pages/admin/VideoStreamFormPage'),
    importPage: () => import('../pages/admin/VideoStreamImportPage'),
    managePage: () => import('../pages/admin/VideoStreamsPage'),
  },
  {
    key: 'audioStreams',
    path: 'audio-streams',
    labelKey: 'nav.groups.audioStreams',
    icon: CustomerServiceOutlined,
    categoriesPage: () => import('../pages/admin/ContentCategoriesPage'),
    addPage: () => import('../pages/admin/AudioStreamFormPage'),
    importPage: () => import('../pages/admin/AudioStreamImportPage'),
    managePage: () => import('../pages/admin/AudioStreamsPage'),
  },
  {
    key: 'videoOnDemand',
    path: 'video-on-demand',
    labelKey: 'nav.groups.videoOnDemand',
    icon: PlaySquareOutlined,
    categoriesPage: () => import('../pages/admin/ContentCategoriesPage'),
  },
  {
    key: 'series',
    path: 'series',
    labelKey: 'nav.groups.series',
    icon: UnorderedListOutlined,
    categoriesPage: () => import('../pages/admin/ContentCategoriesPage'),
  },
]

const contentGroups = (): NavGroup[] =>
  CONTENT_SECTIONS.map((section) => ({
    key: section.key,
    labelKey: section.labelKey,
    icon: section.icon,
    items: [
      {
        key: `${section.key}Add`,
        path: `${section.path}/add`,
        labelKey: `nav.items.${section.key}Add`,
        icon: PlusOutlined,
        page: section.addPage,
      },
      ...(section.importPage
        ? [
            {
              key: `${section.key}Import`,
              path: `${section.path}/import`,
              labelKey: `nav.items.${section.key}Import`,
              icon: ImportOutlined,
              page: section.importPage,
            },
          ]
        : []),
      {
        key: `${section.key}Manage`,
        path: section.path,
        labelKey: `nav.items.${section.key}Manage`,
        icon: TableOutlined,
        page: section.managePage,
      },
      {
        key: `${section.key}Categories`,
        path: `${section.path}/categories`,
        labelKey: `nav.items.${section.key}Categories`,
        icon: TagsOutlined,
        page: section.categoriesPage,
      },
    ],
  }))

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
          page: () => import('../pages/account/DashboardPage'),
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
          page: () => import('../pages/account/ProfilePage'),
        },
        {
          // Present under all three roles, and under `account` rather than a role's own
          // category, because what it lists is a property of the account and not of the job:
          // every one of them signs in, every one of them should be able to see from where.
          // The item is identical in each table for the same reason `profile` is - one shared
          // page, one shared address, three menus that would otherwise drift.
          key: 'sessions',
          path: 'sessions',
          labelKey: 'nav.items.sessions',
          icon: HistoryOutlined,
          page: () => import('../pages/account/SessionsPage'),
        },
        {
          // The live sessions, next to the history that records them. Same reasoning as the
          // entry above - a property of the account, identical under every role - and the
          // two sit side by side because they answer opposite questions about the same rows:
          // "what did this account do" and "what is still signed in".
          key: 'activeSessions',
          path: 'sessions/active',
          labelKey: 'nav.items.activeSessions',
          icon: LaptopOutlined,
          page: () => import('../pages/account/ActiveSessionsPage'),
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
          page: () => import('../pages/account/DashboardPage'),
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
          page: () => import('../pages/account/ProfilePage'),
        },
        {
          // Same item, same page, same address under every role - see the note on the
          // Client entry for why it lives here rather than in a category of its own.
          key: 'sessions',
          path: 'sessions',
          labelKey: 'nav.items.sessions',
          icon: HistoryOutlined,
          page: () => import('../pages/account/SessionsPage'),
        },
        {
          key: 'activeSessions',
          path: 'sessions/active',
          labelKey: 'nav.items.activeSessions',
          icon: LaptopOutlined,
          page: () => import('../pages/account/ActiveSessionsPage'),
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
          page: () => import('../pages/account/DashboardPage'),
        },
      ],
    },
    {
      // The admin's own section: four addresses behind one heading. It replaces the old
      // `management` group, whose `users` entry was a placeholder pointing at nothing -
      // `SectionPage` saying the section was unfinished - and whose `settings` entry went
      // with it, because the two of them described a control panel this application does
      // not have. What is here instead is the set of pages that answer one need from four
      // sides: making an account, seeing the ones that exist, deciding what a *kind* of
      // account may do, and seeing where they have been signing in from.
      //
      // The order is deliberate and follows how the work goes: you add before you manage,
      // and a stranger reading the rail meets them in that order. Groups come next because
      // they are the layer above the two - an administrator who never opens this list still
      // has the three base groups doing their work - and a page reached second must not
      // describe a thing the first page has not introduced. The history comes last: it is
      // the only one of the four that is a question rather than a change, and it is reached
      // when something has already gone wrong rather than as part of the ordinary work.
      key: 'users',
      labelKey: 'nav.groups.users',
      icon: TeamOutlined,
      items: [
        {
          key: 'usersAdd',
          path: 'users/add',
          labelKey: 'nav.items.usersAdd',
          icon: UserAddOutlined,
          page: () => import('../pages/admin/UserFormPage'),
        },
        {
          key: 'usersManage',
          path: 'users',
          labelKey: 'nav.items.usersManage',
          icon: TableOutlined,
          page: () => import('../pages/admin/UsersPage'),
        },
        {
          key: 'userGroups',
          path: 'user-groups',
          labelKey: 'nav.items.userGroups',
          icon: ApartmentOutlined,
          page: () => import('../pages/admin/UserGroupsPage'),
        },
        {
          // The visit history of every account this operator may read. Its own address
          // rather than a child of `sessions`, because that prefix belongs to the
          // *account's own* two pages - `sessions` is this operator's own sign-in history
          // and `sessions/active` is their own live sessions, both in the `account` group
          // under every role. One path cannot be two pages, and the question here is a
          // different one: not "where did I sign in from" but "where are the accounts I
          // mind signing in from".
          //
          // `user-sessions` rather than something shorter, for the reason the rail already
          // reads the way it does: the section names its subjects in the plural
          // (`users`, `user-groups`), and this is the sessions of those users. It is also
          // the one name that does not collide with the two addresses above.
          key: 'adminSessions',
          path: 'user-sessions',
          labelKey: 'nav.items.adminSessions',
          icon: GlobalOutlined,
          page: () => import('../pages/admin/AdminSessionsPage'),
        },
      ],
    },
    ...contentGroups(),
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
          page: () => import('../pages/account/ProfilePage'),
        },
        {
          // Same item, same page, same address under every role - see the note on the
          // Client entry for why it lives here rather than in a category of its own.
          key: 'sessions',
          path: 'sessions',
          labelKey: 'nav.items.sessions',
          icon: HistoryOutlined,
          page: () => import('../pages/account/SessionsPage'),
        },
        {
          key: 'activeSessions',
          path: 'sessions/active',
          labelKey: 'nav.items.activeSessions',
          icon: LaptopOutlined,
          page: () => import('../pages/account/ActiveSessionsPage'),
        },
      ],
    },
  ],
}

/**
 * The absolute address of one menu item.
 *
 * Section items hang off the role's area, while the item with no path of its own *is* the
 * landing page the server names in `redirect` - so `''` takes `homeForRole`, not the area.
 * This is the key `AccountSidebar` hands to antd, and the same key `groupKeyForPath` has to
 * rebuild to decide which category is open; keeping the rule in one function is what stops
 * the two from disagreeing about an address.
 */
export const menuPath = (role: Role, path: string): string =>
  path ? `${areaForRole(role)}/${path}` : homeForRole(role)

/**
 * The category a browser path belongs to, or `null` when no menu item covers it.
 *
 * The rail needs this to open the category of the page being read, and `selectedKeys`
 * cannot answer it alone: `/admin/users/{id}`, `/admin/user-groups/add` and
 * `/admin/video-streams/{id}` are routes with no item of their own. An item therefore owns
 * its exact address **and everything below it**, which is the same rule the router uses
 * when it resolves a nested section - with the segment boundary kept on purpose, because
 * `/admin/users` is not the owner of `/admin/usersomething`.
 *
 * A trailing slash is stripped before matching, exactly as `AccountSidebar` strips it from
 * `location.pathname`; the index route's lone `/` survives that operation.
 */
export const groupKeyForPath = (role: Role, pathname: string): string | null => {
  const path = pathname.replace(/\/+$/, '') || '/'

  for (const group of NAVIGATION[role] ?? []) {
    for (const item of group.items) {
      const itemPath = menuPath(role, item.path)

      if (path === itemPath || path.startsWith(`${itemPath}/`)) return group.key
    }
  }

  return null
}

/** Every menu item of a role, flattened in display order. */
export const flatItems = (role: Role): NavItem[] =>
  (NAVIGATION[role] ?? []).flatMap((group) => group.items)
