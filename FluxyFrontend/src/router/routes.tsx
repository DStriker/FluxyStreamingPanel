import { lazy } from 'react'
import { Navigate } from 'react-router-dom'
import type { RouteObject } from 'react-router-dom'
import config, { areaOf, insideArea, routePath } from '../config'
import GuestOnly from '../features/auth/components/GuestOnly'
import RequireAuth from '../features/auth/components/RequireAuth'
import AccountLayout from '../layouts/AccountLayout'
import ErrorFallback from '../components/ErrorFallback'
import NotFoundPage from '../pages/NotFoundPage'
import { flatItems } from '../utils/navigation'
import type { Role } from '../types'

/**
 * The five guest pages and the placeholder section, each behind a dynamic import.
 *
 * These were the reason the whole application built as one chunk: the route table imported
 * every page it could open, so a first visit to a sign-in form downloaded the profile
 * editor, the dashboard and all three entrances as well. `lazy` turns each entry into a
 * chunk that arrives when its address does.
 *
 * `NotFoundPage` is the deliberate exception. `RequireAuth` imports it *statically* to
 * render for a role mismatch, so it is in the main bundle whether the catch-all asks for
 * it or not - wrapping that one in `lazy` would buy nothing and cost a second path to the
 * same component.
 *
 * `App` supplies the `Suspense` boundary these resolve into; without it a first visit to
 * any of these routes would throw instead of waiting.
 */
const RegisterPage = lazy(() => import('../pages/auth/RegisterPage'))
const ClientLoginPage = lazy(() => import('../pages/auth/ClientLoginPage'))
const ResellerLoginPage = lazy(() => import('../pages/auth/ResellerLoginPage'))
const AdminLoginPage = lazy(() => import('../pages/auth/AdminLoginPage'))
const ForgotPasswordPage = lazy(() => import('../pages/auth/ForgotPasswordPage'))
const SectionPage = lazy(() => import('../pages/SectionPage'))

/**
 * The edit form, loaded here rather than through `NAVIGATION`.
 *
 * `/admin/users/{id}` is a section of the admin area with **no menu item beside it**: the
 * sidebar offers the two addresses you can decide to open (add a user, look at the users),
 * while this one is only ever reached by choosing a row to edit, and a third entry saying
 * "edit" would be a page that is empty until you tell it *which*.
 *
 * It is a second `lazy()` over the same module as the `users/add` item, and that is
 * deliberate rather than an oversight. The bundler resolves both imports to one chunk, so
 * nothing is fetched twice; what differs is the component *type*, and two types is what
 * makes a move between the two addresses remount the form instead of carrying a half-filled
 * one across - which is what you want when the id underneath it just changed.
 */
const UserFormPage = lazy(() => import('../pages/admin/UserFormPage'))

/**
 * The group form, for the same reason and with the same two-address shape as the one above.
 *
 * `/admin/user-groups/add` and `/admin/user-groups/{id}` are both sections of the admin
 * area with **no menu item beside them**: the rail offers one entry, the table itself, and
 * both forms are reached by choosing a button on it - a third entry saying "add a group"
 * would be a page whose only content is the button that brought you here, and a fourth
 * saying "edit" would be empty until you said *which*.
 *
 * The add address is an `extra` route rather than a menu item because of exactly that: the
 * accounts section has one (`usersAdd`, whose label is the verb a visitor scans for), and
 * the groups section's button already sits on the page the visitor is reading. Two of them
 * would be one list reachable from two places and one form reachable from a third.
 *
 * A second `lazy()` over the same module, so a move between the two addresses remounts the
 * form instead of carrying a half-filled one across - which is what you want when the id
 * underneath it just changed.
 */
const UserGroupFormPage = lazy(() => import('../pages/admin/UserGroupFormPage'))

/**
 * The streams form, for the same reason and with the same two-address shape as the two
 * above.
 *
 * `/admin/video-streams/add` is a menu item (its label is the verb a visitor scans for,
 * exactly like `usersAdd`), while `/admin/video-streams/{id}` is an `extra` route with no
 * menu item beside it - it is only ever reached by choosing a row to edit, and a third
 * entry saying "edit" would be a page that is empty until you tell it *which*.
 *
 * A second `lazy()` over the same module as the `add` item, so a move between the two
 * addresses remounts the form instead of carrying a half-filled one across - which is what
 * you want when the id underneath it just changed.
 */
const VideoStreamFormPage = lazy(() => import('../pages/admin/VideoStreamFormPage'))

/**
 * The audio streams form, for the same reason and with the same two-address shape as the
 * one above.
 *
 * `/admin/audio-streams/add` is a menu item (its label is the verb a visitor scans for,
 * exactly like `usersAdd`), while `/admin/audio-streams/{id}` is an `extra` route with no
 * menu item beside it - it is only ever reached by choosing a row to edit, and a third
 * entry saying "edit" would be a page that is empty until you tell it *which*.
 *
 * A second `lazy()` over the same module as the `add` item, so a move between the two
 * addresses remounts the form instead of carrying a half-filled one across - which is what
 * you want when the id underneath it just changed.
 */
const AudioStreamFormPage = lazy(() => import('../pages/admin/AudioStreamFormPage'))

/**
 * The on-demand video form, for the same reason and with the same two-address shape as the
 * stream forms above.
 *
 * `/admin/video-on-demand/add` is a menu item, while `/admin/video-on-demand/{id}` is an
 * `extra` route with no menu item beside it - it is only ever reached by choosing a row to
 * edit, and a third entry saying "edit" would be a page that is empty until you tell it
 * *which*.
 *
 * A second `lazy()` over the same module as the `add` item, so a move between the two
 * addresses remounts the form instead of carrying a half-filled one across - which is what
 * you want when the id underneath it just changed.
 */
const VideoOnDemandFormPage = lazy(() => import('../pages/admin/VideoOnDemandFormPage'))

/**
 * The series pages. The add form and the table are menu items; the series edit form, the
 * episode table and the two episode/season forms are reached only from those pages, so they
 * are `extra` routes with no menu item beside them.
 */
const SeriesFormPage = lazy(() => import('../pages/admin/SeriesFormPage'))
const SeriesEpisodesPage = lazy(() => import('../pages/admin/SeriesEpisodesPage'))
const EpisodeFormPage = lazy(() => import('../pages/admin/EpisodeFormPage'))
const SeasonFormPage = lazy(() => import('../pages/admin/SeasonFormPage'))

/**
 * The guide source form's edit address and the channels of one source. Both are reached only
 * from a row of the sources table, so neither has a menu item; `epg-sources/add` is a menu
 * item and a static segment, which react-router ranks above `:id`.
 *
 * The form is a second `lazy()` over the same module as the `epg-sources/add` item, for the
 * reason every other edit form is: a move between the two addresses must remount the form
 * rather than carry a half-filled one across.
 */
const EpgSourceFormPage = lazy(() => import('../pages/admin/EpgSourceFormPage'))
const EpgSourceChannelsPage = lazy(() => import('../pages/admin/EpgSourceChannelsPage'))

/**
 * The content manager of one bouquet. Lazy for the reason every page is: it is reached from
 * a row of the bouquets table, so a visitor who never opens one never downloads it.
 */
const BouquetContentPage = lazy(() => import('../pages/admin/BouquetContentPage'))

/**
 * The three sign-in areas, each of which has to answer the path the backend names in its
 * `redirect` field - a page inside the area, and therefore a child of it.
 *
 * The paths are configured rather than written here because they have to agree with the
 * `Auth` section of the backend's appsettings.json in two directions: the server sends the
 * path back on a successful sign-in, and this router has to have a matching route or the
 * visitor lands on a 404 after a successful login. One source for the strings, on each
 * side, is what keeps that from drifting.
 *
 * The five guest routes - the three entrances, registration and the password reset - are
 * wrapped in
 * `GuestOnly`, because a visitor who already holds a session has no business being offered
 * a form for creating or re-entering one: the pages would have them stack a second session
 * over a first that nothing can end anymore. The guard sends such a visitor to their own
 * area, which is where a successful sign-in would have taken them; see the component for
 * why the check sits here rather than inside the page.
 *
 * Each area is a *parent* route whose element is `RequireAuth` wrapping `AccountLayout`,
 * with the section pages as its children. Two things follow from that shape and neither is
 * incidental:
 *
 * - The role is checked before the layout renders, not inside a page. `/admin/profile`
 *   matches the admin route whatever the role of the visitor is, so a check on the page
 *   would have already shown them the admin shell. `RequireAuth` refuses with a 404
 *   instead, which is what "this address is not yours" means.
 * - The layout is mounted once per area, so moving between two sections replaces only the
 *   outlet. The sidebar's scroll position and its collapsed state survive a click, while
 *   its one open category follows the page; a layout wrapped around each page individually
 *   would reset all of them.
 *
 * The area's own path is the role's **prefix and nothing more** - `/client`, `/admin` -
 * while the landing path the server names in `redirect` is a page *inside* that prefix:
 * `/client/index`, `/admin/dashboard`. So `profile` hangs off the prefix and comes out as
 * `/admin/profile`, never `/admin/dashboard/profile`; the landing path is one child among
 * the rest, at whatever `insideArea` says. `sectionRoutes` reads the same table
 * `AccountSidebar` renders, which is why no menu item can point at a route that does not
 * exist.
 *
 * The catch-all is the only route here that does not know which role it belongs to, so it
 * uses `RequireAuth` without one: a signed-in visitor who asked for a page this
 * application does not have gets a 404, and a visitor with no session still gets the
 * sign-in form, which is where a typo'd URL always landed before.
 *
 * ## Why this is a data module and not the router
 *
 * `createBrowserRouter` builds a browser history on the spot, and building one reads
 * `document`. `npm run probe:render` runs in Node, so importing a file that constructs the
 * router would fail before a single check executed - which is exactly what happened when
 * the table and the construction were the same file. Splitting them is what lets the probe
 * assert what cannot be seen by rendering: that `homeForRole(role)` lands inside the route
 * guarded for that role (else a sign-in bounces between two guards), that every menu item
 * has a route while every route has a menu item, and that react-router itself resolves each
 * of those addresses to that area rather than to the catch-all.
 */
const sectionRoutes = (role: Role, homeRoute: string): RouteObject[] =>
  flatItems(role).map((item) => {
    // `lazy` is called here rather than at the loader's definition site because a loader
    // belongs to an *item*, and the same page is an item of all three roles: one wrapper
    // per item keeps the nine wrappers static and created exactly once. This function runs
    // a single time, while the table below is being built at module load - which is what
    // makes it safe. Wrapping during render would mint a new component type per render and
    // remount the page on every navigation.
    const Page = item.page ? lazy(item.page) : SectionPage
    const element = <Page sectionKey={item.labelKey} />
    // The item with no path of its own is this role's home - the page a sign-in opens,
    // whose address the server names in `redirect`. It is a child of the area like every
    // other section, at whatever the landing path says sits inside that area (`dashboard`
    // for `admin/dashboard`, `index` for `client/index`), and when the landing path *is*
    // the area there is nothing left to give it and the home becomes the index route.
    const path = item.path || insideArea(homeRoute)
    return path === '' ? { index: true, element } : { path, element }
  })

/**
 * One area: the role's guard and shell, every section of it, and any address that belongs
 * to the area but not to the menu.
 *
 * `extra` is for exactly that second kind - a route react-router must answer which no
 * sidebar item may point at. It is appended rather than merged so that a menu-driven
 * section can never be shadowed by one of these: react-router ranks a static segment above
 * a dynamic one anyway (`users/add` beats `users/:id`), but keeping the two sources
 * separate means nobody has to know that to add a page.
 */
const area = (
  role: Role,
  homeRoute: string,
  titleKey: string,
  extra: RouteObject[] = [],
): RouteObject => ({
  path: routePath(areaOf(homeRoute)),
  element: (
    <RequireAuth role={role}>
      <AccountLayout titleKey={titleKey} />
    </RequireAuth>
  ),
  children: [...sectionRoutes(role, homeRoute), ...extra],
})

/**
 * A top-level entry with the shared crash screen attached.
 *
 * Every address carries `errorElement: <ErrorFallback />`, including the areas: a page that
 * throws while rendering is a bug rather than a refusal, and without a fallback of its own
 * the data router answers it with its own bare English error page - it *does* catch the
 * error, which is why this is a theming and translation gap rather than a blank one - and
 * the crash deserves at least the theme the visitor chose. One builder rather than nine
 * copies of the line is what keeps a route added later from silently dropping it;
 * `probe:render` asserts the property holds for every top-level entry.
 */
const withFallback = (route: RouteObject): RouteObject => ({
  ...route,
  errorElement: <ErrorFallback />,
})

/**
 * Every address this application answers, in the shape react-router builds from.
 *
 * Annotated rather than inferred so that an entry is checked where it is written: `index:
 * true` widens to `boolean` the moment nothing says otherwise, and an entry that carries
 * both a `path` and an `index` is a contradiction the union can only report once a union
 * is being asked for - which, unannotated, would be the first consumer of the table rather
 * than the line the entry sits on.
 *
 * Every entry is built through `withFallback` above, so the crash screen cannot be
 * forgotten on the next route; `probe:render` asserts the property holds for each of them.
 */
export const routes: RouteObject[] = [
  withFallback({
    path: '/',
    element: <Navigate to={routePath(config.CLIENT_LOGIN_ROUTE)} replace />,
  }),
  withFallback({
    path: routePath(config.REGISTER_ROUTE),
    element: (
      <GuestOnly>
        <RegisterPage />
      </GuestOnly>
    ),
  }),
  withFallback({
    path: routePath(config.FORGOT_PASSWORD_ROUTE),
    element: (
      <GuestOnly>
        <ForgotPasswordPage />
      </GuestOnly>
    ),
  }),
  withFallback({
    path: routePath(config.CLIENT_LOGIN_ROUTE),
    element: (
      <GuestOnly>
        <ClientLoginPage />
      </GuestOnly>
    ),
  }),
  withFallback({
    path: routePath(config.RESELLER_LOGIN_ROUTE),
    element: (
      <GuestOnly>
        <ResellerLoginPage />
      </GuestOnly>
    ),
  }),
  withFallback({
    path: routePath(config.ADMIN_LOGIN_ROUTE),
    element: (
      <GuestOnly>
        <AdminLoginPage />
      </GuestOnly>
    ),
  }),
  withFallback(area('Client', config.CLIENT_HOME_ROUTE, 'titles.clientArea')),
  withFallback(area('Reseller', config.RESELLER_HOME_ROUTE, 'titles.resellerArea')),
  withFallback(
    area('Admin', config.ADMIN_HOME_ROUTE, 'titles.adminArea', [
      {
        path: 'users/:id',
        element: <UserFormPage sectionKey="nav.items.usersAdd" />,
      },
      // The group form's two addresses. `add` is written first and is a static segment, so
      // react-router ranks it above `:id` - the same reason `users/add` can be a menu item
      // while `users/:id` is not. Neither has a menu item: the rail offers the table alone,
      // and both forms are reached from buttons on it (see the loader above for why).
      {
        path: 'user-groups/add',
        element: <UserGroupFormPage sectionKey="nav.items.userGroups" />,
      },
      {
        path: 'user-groups/:id',
        element: <UserGroupFormPage sectionKey="nav.items.userGroups" />,
      },
      // The streams form's edit address. `video-streams/add` is a menu item and is a static
      // segment, so react-router ranks it above `:id` - the same reason `users/add` can be a
      // menu item while `users/:id` is not. This one has no menu item: it is reached only by
      // choosing a row of the streams table (see the loader above for why).
      {
        path: 'video-streams/:id',
        element: <VideoStreamFormPage sectionKey="nav.items.videoStreamsAdd" />,
      },
      // The audio streams form's edit address. `audio-streams/add` is a menu item and is a
      // static segment, so react-router ranks it above `:id` - the same reason `users/add`
      // can be a menu item while `users/:id` is not. This one has no menu item: it is
      // reached only by choosing a row of the audio streams table.
      {
        path: 'audio-streams/:id',
        element: <AudioStreamFormPage sectionKey="nav.items.audioStreamsAdd" />,
      },
      // The on-demand video form's edit address. `video-on-demand/add` is a menu item and
      // is a static segment, so react-router ranks it above `:id` - the same reason
      // `video-streams/add` can be a menu item while `video-streams/:id` is not. This one
      // has no menu item: it is reached only by choosing a row of the on-demand table.
      {
        path: 'video-on-demand/:id',
        element: <VideoOnDemandFormPage sectionKey="nav.items.videoOnDemandAdd" />,
      },
      // The series edit form, episode table and the two forms that add an episode or a
      // season. `series/add` is a menu item and is a static segment, so react-router ranks
      // it above `:id`; the remaining addresses have no menu item because each is reached
      // by choosing a row or a button on the series pages.
      {
        path: 'series/:id',
        element: <SeriesFormPage sectionKey="nav.items.seriesAdd" />,
      },
      {
        path: 'series/:id/episodes',
        element: <SeriesEpisodesPage sectionKey="nav.items.seriesManage" />,
      },
      {
        path: 'series/:id/episodes/add',
        element: <EpisodeFormPage sectionKey="nav.items.seriesAdd" />,
      },
      {
        path: 'series/:id/episodes/:episodeId',
        element: <EpisodeFormPage sectionKey="nav.items.seriesAdd" />,
      },
      {
        path: 'series/:id/seasons/add',
        element: <SeasonFormPage sectionKey="nav.items.seriesAdd" />,
      },
      // The guide source form's edit address and the channels of one source. Neither has a
      // menu item: the first is reached by choosing a row to edit, the second by choosing a
      // row's "channels" action.
      {
        path: 'epg-sources/:id',
        element: <EpgSourceFormPage sectionKey="nav.items.epgAdd" />,
      },
      {
        path: 'epg-sources/:id/channels',
        element: <EpgSourceChannelsPage sectionKey="nav.items.epgManage" />,
      },
      // The content manager of one bouquet. It has no menu item on purpose: the bouquets
      // section is one list, and a package's content is reached by choosing that package's
      // own action on the row - the same shape as a guide source's channels above.
      {
        path: 'bouquets/:id/content',
        element: <BouquetContentPage sectionKey="nav.items.bouquetsManage" />,
      },
    ]),
  ),
  withFallback({
    path: '*',
    element: (
      <RequireAuth>
        <NotFoundPage />
      </RequireAuth>
    ),
  }),
]
