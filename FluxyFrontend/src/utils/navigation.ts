import type { ComponentType } from 'react'
import {
  ApartmentOutlined,
  AppstoreOutlined,
  AuditOutlined,
  CalendarOutlined,
  CloudServerOutlined,
  CreditCardOutlined,
  CustomerServiceOutlined,
  DashboardOutlined,
  DesktopOutlined,
  GlobalOutlined,
  HistoryOutlined,
  IdcardOutlined,
  ImportOutlined,
  InboxOutlined,
  LaptopOutlined,
  PlaySquareOutlined,
  PlusOutlined,
  ShoppingCartOutlined,
  StopOutlined,
  TableOutlined,
  TagsOutlined,
  TeamOutlined,
  UnorderedListOutlined,
  UserAddOutlined,
  UserOutlined,
  UsbOutlined,
  VideoCameraOutlined,
  WalletOutlined,
  WarningOutlined,
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
 * `categories` is a real page for all four. `add` and `manage` are real pages for all four
 * sections: the video streams, the audio streams, the on-demand catalogue and the series.
 * The optional loaders are still optional rather than required because `import` is a fourth
 * entry only the two stream sections carry - reading a playlist is the other way to add
 * streams, not a fourth thing to look at - and a section without one must not grow a
 * placeholder where these have a real page.
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
  /**
   * The problem log of this section, present only where one has been built.
   *
   * It carries its own key, path and page rather than being derived from `key`/`path` like the
   * three entries above, because the page it opens is **not** a child of the section's address
   * prefix: `/admin/stream-problems` mirrors the backend's own route, exactly as
   * `api-request-failures` and `credit-logs` do, while the rail still files the entry under the
   * section whose streams it writes about.
   */
  problemLogPage?: () => Promise<{ default: ComponentType<{ sectionKey: string }> }>
  /** The problem log's item key. Deliberately not prefixed by `key` - see the field above. */
  problemLogKey?: string
  /** The problem log's address under the area's prefix. */
  problemLogPath?: string
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
    problemLogPage: () => import('../pages/admin/StreamProblemsPage'),
    problemLogKey: 'streamProblems',
    problemLogPath: 'stream-problems',
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
    addPage: () => import('../pages/admin/VideoOnDemandFormPage'),
    importPage: () => import('../pages/admin/VideoOnDemandImportPage'),
    managePage: () => import('../pages/admin/VideoOnDemandPage'),
  },
  {
    key: 'series',
    path: 'series',
    labelKey: 'nav.groups.series',
    icon: UnorderedListOutlined,
    categoriesPage: () => import('../pages/admin/ContentCategoriesPage'),
    addPage: () => import('../pages/admin/SeriesFormPage'),
    managePage: () => import('../pages/admin/SeriesPage'),
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
      // The section's problem log, last: it is not one of the section's four verbs but the
      // record of what its streams reported. Its key, address and page are the section's own
      // fields rather than derived from `key`/`path`, because the address mirrors the backend's
      // (`admin/stream-problems`) and sits outside the section's path prefix - and because the
      // item key deliberately does not start with the section's, which is the prefix
      // `probe:render` hard-asserts for the four generated entries above.
      ...(section.problemLogPage && section.problemLogKey && section.problemLogPath
        ? [
            {
              key: section.problemLogKey,
              path: section.problemLogPath,
              labelKey: `nav.items.${section.problemLogKey}`,
              icon: WarningOutlined,
              page: section.problemLogPage,
            },
          ]
        : []),
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
        {
          // The reseller balances' history, last in this group: it is the same question the
          // entry above asks with one more subject - which accounts this operator may read,
          // and what moved their money. It sits after the visit history on the owner's call,
          // because a balance is read when something about an account has already gone wrong.
          //
          // `credit-logs` rather than a child of `users`: the address mirrors the backend's
          // own route (`admin/credit-logs`), so the page, the API and the menu entry name one
          // resource the same way.
          key: 'creditLogs',
          path: 'credit-logs',
          labelKey: 'nav.items.creditLogs',
          icon: WalletOutlined,
          page: () => import('../pages/admin/CreditLogsPage'),
        },
        {
          // The user audit log, appended after the credit log: it is the same kind of read -
          // what happened, with an outcome - about accounts rather than about their money, and
          // it is last because it is the widest record of the two. `user-logs` mirrors the
          // backend's own route (`admin/user-logs`), so the page, the API and the menu entry
          // name one resource the same way.
          //
          // The label is `nav.items.userLogs` - "User logs" / «Журнал действий» - and
          // deliberately not the group's own heading: the rail's "Users" is where the visitor
          // is, and this is what they are reading.
          key: 'userLogs',
          path: 'user-logs',
          labelKey: 'nav.items.userLogs',
          icon: AuditOutlined,
          page: () => import('../pages/admin/UserLogsPage'),
        },
      ],
    },
    {
      // The subscriptions: what a subscriber buys, sold under a tariff plan. It sits
      // immediately under the accounts section, which is where the owner asked for it: the
      // accounts say who may sign in, and this is what those accounts are sold. Everything
      // below it - the content sections, the bouquets and the guide - is what a subscription
      // delivers rather than what a subscriber pays.
      //
      // The section has two entries now: the management list, which draws every subscription
      // with its service subrows, and the add form. The three placeholders it used to carry -
      // playlists, MAG devices and Enigma2 devices - are gone, because the subrows of the
      // management list are where each of those is managed: a second screen for one field of a
      // row would be a second place for the same fact to disagree with itself.
      //
      // The order is the owner's: adding a subscription first, then the list, and the price list
      // last - the same "create before manage" order the accounts section follows.
      key: 'subscriptions',
      labelKey: 'nav.groups.subscriptions',
      icon: CreditCardOutlined,
      items: [
        {
          key: 'subscriptionsAdd',
          path: 'subscriptions/add',
          labelKey: 'nav.items.subscriptionsAdd',
          icon: PlusOutlined,
          page: () => import('../pages/admin/SubscriptionFormPage'),
        },
        {
          key: 'subscriptionsManage',
          path: 'subscriptions',
          labelKey: 'nav.items.subscriptionsManage',
          icon: TableOutlined,
          page: () => import('../pages/admin/SubscriptionsPage'),
        },
        {
          // The price list. Its add form and its edit form are addresses with no item of their
          // own - both are reached from the table, exactly as a group's form is - so this is the
          // only entry the section carries for it.
          //
          // `tariff-plans` rather than `subscriptions/tariff-plans`: the address mirrors the
          // backend's own route (`admin/tariff-plans`), so the page, the API and the menu entry
          // name one resource the same way.
          //
          // It sits above the two connection-log entries on the owner's correction: a plan is
          // what a subscription is *sold under*, so it belongs beside the list that shows what
          // was sold, and the log - the record of what those subscriptions then did - follows.
          key: 'tariffPlans',
          path: 'tariff-plans',
          labelKey: 'nav.items.tariffPlans',
          icon: TagsOutlined,
          page: () => import('../pages/admin/TariffPlansPage'),
        },
        {
          // The connection log, as two entries rather than one screen with a switch: "what is
          // running right now" and "what was watched" are the two questions an operator arrives
          // with, and the rail is where that choice is made. Both open the same page with its own
          // `sectionKey` - exactly the shape the three blacklists use - and the mode the key names
          // is what decides the state filter, the end column and whether a kill action is drawn.
          //
          // Each has an address of its own rather than a child of the other (`live-connections`
          // and `connection-logs` are siblings): one path cannot be two pages, and the activity
          // page is the one the subscriptions row's "Show activity" action links to.
          key: 'liveConnections',
          path: 'live-connections',
          labelKey: 'nav.items.liveConnections',
          icon: GlobalOutlined,
          page: () => import('../pages/admin/ConnectionLogsPage'),
        },
        {
          key: 'subscriptionActivity',
          path: 'connection-logs',
          labelKey: 'nav.items.subscriptionActivity',
          icon: HistoryOutlined,
          page: () => import('../pages/admin/ConnectionLogsPage'),
        },
        {
          // The record of what was sent *to* a device, after the two logs of what a device
          // then watched: the journal answers "did it get there?", which is the question a
          // support call asks once the connection log has shown nothing wrong.
          //
          // `mag-events` rather than a child of `subscriptions`: the address mirrors the
          // backend's own route (`admin/mag-events`), so the page, the API and the menu entry
          // name one resource the same way.
          key: 'magEvents',
          path: 'mag-events',
          labelKey: 'nav.items.magEvents',
          icon: UsbOutlined,
          page: () => import('../pages/admin/MagEventsPage'),
        },
      ],
    },
    ...contentGroups(),
    {
      // The bouquets: the packages the four content headings above are delivered in. It sits
      // immediately after the content sections and before the guide for the reason the
      // guide's own comment gives - it is a frame around the four catalogues rather than a
      // fifth one, so it belongs where a reader who has just met all four finds it.
      //
      // One item and no sub-items, on the owner's call: a package has one screen (the list),
      // and everything else about it is reached from a row of that list - the content manager
      // is an address with no item of its own, exactly as a group's accounts page is. A
      // second entry saying "add a bouquet" would be a page whose only content is the button
      // the list already carries.
      key: 'bouquets',
      labelKey: 'nav.groups.bouquets',
      icon: InboxOutlined,
      items: [
        {
          key: 'bouquetsManage',
          path: 'bouquets',
          labelKey: 'nav.items.bouquetsManage',
          icon: TableOutlined,
          page: () => import('../pages/admin/BouquetsPage'),
        },
      ],
    },
    {
      // The programme guide: where a schedule is fetched from, and the table of the sources
      // that exist. It sits after the four content sections because a guide is what the
      // streams are filed against rather than a fifth catalogue of content, and it carries
      // two entries rather than the sections' four: a source has no categories and no
      // playlist import - one file is the source itself.
      //
      // The add form is a menu item and the table is the other, in that order, for the
      // reason the accounts section gives: a visitor scanning the rail decides by the verb
      // first. The edit form and the channels of one source are addresses with no menu item
      // of their own - each is only reached from a row of the table.
      key: 'epg',
      labelKey: 'nav.groups.epg',
      icon: CalendarOutlined,
      items: [
        {
          key: 'epgAdd',
          path: 'epg-sources/add',
          labelKey: 'nav.items.epgAdd',
          icon: PlusOutlined,
          page: () => import('../pages/admin/EpgSourceFormPage'),
        },
        {
          key: 'epgManage',
          path: 'epg-sources',
          labelKey: 'nav.items.epgManage',
          icon: TableOutlined,
          page: () => import('../pages/admin/EpgSourcesPage'),
        },
      ],
    },
    {
      // The three access-control blacklists, immediately after the programme guide because
      // that is where the owner asked for them. One item per list rather than one item with a
      // filter: the three lists are separate questions an operator arrives with, and the rail
      // is where that choice is made. All three open the same page with its own `sectionKey`.
      //
      // The failed-request log sits after them: the lists are the rules that refuse and the log
      // is the record of the refusals, so the family holds both sides of one decision.
      //
      // The icons describe the thing each list holds - a network address, a provider, a
      // client - rather than repeating the group's stop sign three times, which would make
      // the three entries look like one entry drawn thrice.
      key: 'blacklists',
      labelKey: 'nav.groups.blacklists',
      icon: StopOutlined,
      items: [
        {
          key: 'blacklistIp',
          path: 'blacklists/ip',
          labelKey: 'nav.items.blacklistIp',
          icon: GlobalOutlined,
          page: () => import('../pages/admin/BlacklistEntriesPage'),
        },
        {
          key: 'blacklistIsp',
          path: 'blacklists/isp',
          labelKey: 'nav.items.blacklistIsp',
          icon: CloudServerOutlined,
          page: () => import('../pages/admin/BlacklistEntriesPage'),
        },
        {
          key: 'blacklistUa',
          path: 'blacklists/ua',
          labelKey: 'nav.items.blacklistUa',
          icon: DesktopOutlined,
          page: () => import('../pages/admin/BlacklistEntriesPage'),
        },
        {
          // The refused-request log, appended after the three access-control lists on the
          // owner's call: the rail is where an operator arrives when something was refused,
          // and the three lists it sits under are the rules that do the refusing.
          //
          // `api-request-failures` rather than `blacklists/api-request-failures`: the address
          // mirrors the backend's own route (`admin/api-request-failures`), so the page, the API
          // and the menu entry name one resource the same way.
          key: 'apiRequestFailures',
          path: 'api-request-failures',
          labelKey: 'nav.items.apiRequestFailures',
          icon: WarningOutlined,
          page: () => import('../pages/admin/ApiRequestFailuresPage'),
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

/**
 * The absolute address of the menu item with this key, or `null` when the role's menu has none.
 *
 * A page that has to point at another section names that section's own item key instead of
 * repeating its segment: the key is what the rail and the router already agree on, so a
 * section renamed in `CONTENT_SECTIONS` reaches every link built this way and none of the
 * hardcoded strings the note in `AGENTS.md` forbids. `null` rather than an area-relative
 * fallback, because a role whose menu has no such item has nowhere to send the visitor.
 */
export const menuItemPath = (role: Role, key: string): string | null => {
  const item = flatItems(role).find((entry) => entry.key === key)

  return item ? menuPath(role, item.path) : null
}
