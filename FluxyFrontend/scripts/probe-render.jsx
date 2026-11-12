/**
 * Renders every page and asserts what a visitor would actually see.
 *
 * Written because a footer prop was accepted by `AuthCard` and silently dropped: the prop
 * was passed by all four pages, nothing complained, lint passed and the build succeeded -
 * the link was only ever missing in the DOM. Reading the code did not catch it; rendering
 * it does.
 */
/**
 * `.jsx` rather than `.mjs` only so that `eslint.config.js` parses the JSX below - the
 * config enables `jsx` for `**\/*.{js,jsx}` and not for `.mjs`.
 */
import { readFileSync } from 'node:fs'
import { renderToStaticMarkup } from 'react-dom/server'
import { createMemoryRouter, matchRoutes, RouterProvider } from 'react-router-dom'
import { App as AntApp, ConfigProvider, Form } from 'antd'
import { DeleteOutlined, EditOutlined, LockOutlined } from '@ant-design/icons'
import { QueryClientProvider } from '@tanstack/react-query'
import i18n from '../src/i18n'
import RegisterPage from '../src/pages/auth/RegisterPage'
import ClientLoginPage from '../src/pages/auth/ClientLoginPage'
import ResellerLoginPage from '../src/pages/auth/ResellerLoginPage'
import AdminLoginPage from '../src/pages/auth/AdminLoginPage'
import ForgotPasswordPage from '../src/pages/auth/ForgotPasswordPage'
import ProfilePage from '../src/pages/account/ProfilePage'
import SessionsPage from '../src/pages/account/SessionsPage'
import ActiveSessionsPage from '../src/pages/account/ActiveSessionsPage'
import UsersPage from '../src/pages/admin/UsersPage'
import UserFormPage from '../src/pages/admin/UserFormPage'
import UserGroupsPage from '../src/pages/admin/UserGroupsPage'
import UserGroupFormPage from '../src/pages/admin/UserGroupFormPage'
import AdminSessionsPage from '../src/pages/admin/AdminSessionsPage'
import ContentCategoriesPage from '../src/pages/admin/ContentCategoriesPage'
import VideoStreamsPage from '../src/pages/admin/VideoStreamsPage'
import VideoStreamFormPage from '../src/pages/admin/VideoStreamFormPage'
import VideoStreamImportPage from '../src/pages/admin/VideoStreamImportPage'
import AudioStreamsPage from '../src/pages/admin/AudioStreamsPage'
import VideoOnDemandPage from '../src/pages/admin/VideoOnDemandPage'
import VideoOnDemandFormPage from '../src/pages/admin/VideoOnDemandFormPage'
import VideoOnDemandImportPage from '../src/pages/admin/VideoOnDemandImportPage'
import SeriesPage from '../src/pages/admin/SeriesPage'
import SeriesFormPage from '../src/pages/admin/SeriesFormPage'
import SeriesEpisodesPage from '../src/pages/admin/SeriesEpisodesPage'
import EpisodeFormPage from '../src/pages/admin/EpisodeFormPage'
import SeasonFormPage from '../src/pages/admin/SeasonFormPage'
import AudioStreamFormPage from '../src/pages/admin/AudioStreamFormPage'
import AudioStreamImportPage from '../src/pages/admin/AudioStreamImportPage'
import EpgSourcesPage from '../src/pages/admin/EpgSourcesPage'
import EpgSourceFormPage from '../src/pages/admin/EpgSourceFormPage'
import EpgSourceChannelsPage from '../src/pages/admin/EpgSourceChannelsPage'
import BlacklistEntriesPage from '../src/pages/admin/BlacklistEntriesPage'
import BlacklistEntryFields from '../src/features/blacklists/components/BlacklistEntryFields'
import TariffPlansPage from '../src/pages/admin/TariffPlansPage'
import TariffPlanFormPage from '../src/pages/admin/TariffPlanFormPage'
import SubscriptionsPage from '../src/pages/admin/SubscriptionsPage'
import SubscriptionFormPage from '../src/pages/admin/SubscriptionFormPage'
import ConnectionLogsPage from '../src/pages/admin/ConnectionLogsPage'
import CreditLogsPage from '../src/pages/admin/CreditLogsPage'
import UserLogsPage from '../src/pages/admin/UserLogsPage'
import ApiRequestFailuresPage from '../src/pages/admin/ApiRequestFailuresPage'
import StreamProblemsPage from '../src/pages/admin/StreamProblemsPage'
import SourceReplacementPage from '../src/pages/admin/SourceReplacementPage'
import MagEventsPage from '../src/pages/admin/MagEventsPage'
import ConnectionNetworkCell from '../src/features/connection-logs/components/ConnectionNetworkCell'
import { COLUMN_WIDTHS_KEYS } from '../src/features/connection-logs/layout'
import {
  COLUMN_KEYS as CREDIT_LOG_COLUMN_KEYS,
  COLUMN_WIDTHS_KEY as CREDIT_LOG_WIDTHS_KEY,
  DEFAULT_COLUMN_WIDTHS as CREDIT_LOG_WIDTHS,
  sortFieldOf as creditLogSortFieldOf,
} from '../src/features/credit-logs/layout'
import {
  creditLogActorName,
  creditLogTone,
} from '../src/features/credit-logs/creditLogActor'
import {
  COLUMN_KEYS as USER_LOG_COLUMN_KEYS,
  COLUMN_WIDTHS_KEY as USER_LOG_WIDTHS_KEY,
  DEFAULT_COLUMN_WIDTHS as USER_LOG_WIDTHS,
  TABLE_WIDTH as USER_LOG_TABLE_WIDTH,
  columnWidthTotal as userLogColumnWidthTotal,
  sortFieldOf as userLogSortFieldOf,
} from '../src/features/user-logs/layout'
import { recordTone } from '../src/features/user-logs/recordTone'
import { USER_LOG_RECORD_TYPES } from '../src/features/user-logs/recordTypes'
import {
  COLUMN_KEYS as API_REQUEST_FAILURE_COLUMN_KEYS,
  DEFAULT_COLUMN_WIDTHS as API_REQUEST_FAILURE_WIDTHS,
  TABLE_WIDTH as API_REQUEST_FAILURE_TABLE_WIDTH,
  columnWidthTotal as apiRequestFailureColumnWidthTotal,
  sortFieldOf as apiRequestFailureSortFieldOf,
} from '../src/features/api-request-failures/layout'
import { API_REQUEST_FAILURE_REASONS } from '../src/features/api-request-failures/reasons'
import { reasonTone } from '../src/features/api-request-failures/reasonTone'
import {
  COLUMN_KEYS as STREAM_PROBLEM_COLUMN_KEYS,
  DEFAULT_COLUMN_WIDTHS as STREAM_PROBLEM_WIDTHS,
  TABLE_WIDTH as STREAM_PROBLEM_TABLE_WIDTH,
  columnWidthTotal as streamProblemColumnWidthTotal,
  sortFieldOf as streamProblemSortFieldOf,
} from '../src/features/stream-problems/layout'
import {
  COLUMN_KEYS as MAG_EVENT_COLUMN_KEYS,
  DEFAULT_COLUMN_WIDTHS as MAG_EVENT_WIDTHS,
  TABLE_WIDTH as MAG_EVENT_TABLE_WIDTH,
  columnWidthTotal as magEventColumnWidthTotal,
  sortFieldOf as magEventSortFieldOf,
} from '../src/features/mag-events/layout'
import { MAG_EVENT_TYPES } from '../src/features/mag-events/events'
import { actionsColumnWidth } from '../src/utils/tableWidths'
import SubscriptionExpiryField from '../src/features/subscriptions/components/SubscriptionExpiryField'
import SubscriptionSubrows from '../src/features/subscriptions/components/SubscriptionSubrows'
import BouquetsPage from '../src/pages/admin/BouquetsPage'
import BouquetContentPage from '../src/pages/admin/BouquetContentPage'
import LastSeenCell from '../src/features/users/components/LastSeenCell'
import UserGroupCell from '../src/features/users/components/UserGroupCell'
import StreamStateCell from '../src/features/video-streams/components/StreamStateCell'
import AudioStreamStateCell from '../src/features/audio-streams/components/StreamStateCell'
import { RowActions } from '../src/components/table'
import UserAgentCell from '../src/components/table/UserAgentCell'
import TimeZoneCard from '../src/features/profile/components/TimeZoneCard'
import LoginGuardCard from '../src/features/profile/components/LoginGuardCard'
import ConfirmCodeForm from '../src/features/auth/components/ConfirmCodeForm'
import GuestOnly from '../src/features/auth/components/GuestOnly'
import RequireAuth from '../src/features/auth/components/RequireAuth'
import ErrorFallback from '../src/components/ErrorFallback'
import AccountLayout from '../src/layouts/AccountLayout'
import { routes as routeTable } from '../src/router/routes'
import { SessionContext } from '../src/utils/sessionContext'
import { NAVIGATION, flatItems, menuItemPath, menuPath } from '../src/utils/navigation'
import { areaForRole, homeForRole, loginRouteForPath, sessionOpensArea } from '../src/utils/session'
import { appTheme, setThemeChoice, ThemeChoice } from '../src/utils/theme'
import { countryName } from '../src/utils/countryName'
import { describeUserAgent } from '../src/utils/userAgent'
import { formatTimestamp } from '../src/utils/dateFormat'
import {
  ALL_PERMISSIONS,
  canEdit,
  canEditAny,
  canView,
  canViewAny,
  permissionsForRole,
} from '../src/features/user-groups/permissions'
import {
  GENERATED_PASSWORD_LENGTH,
  generatePassword,
  meetsPasswordComplexity,
} from '../src/features/auth/policy'
import { routePath } from '../src/config/index'
import { createQueryClient } from '../src/utils/queryClient'

// Every page that reads the API does so through `useQuery` now, so every render of one
// needs a QueryClient around it - and a **fresh** one per render, which is the whole
// point: the state this script asserts at every turn is "before the first answer arrives",
// and a shared client would let one render's cached answer leak into the next, painting
// exactly the loaded state the assertions below exist to stand against. The real
// application builds its own in `App.tsx`; this is the probe's twin of it.
const withQuery = (children) => (
  <QueryClientProvider client={createQueryClient()}>{children}</QueryClientProvider>
)

// The same client with one query already answered, which is the only way a `renderToStaticMarkup`
// can see a page *after* its data arrived: no effect runs, so nothing can fetch on its own. It
// exists for the subscription edit form, whose whole point is what it draws from a populated row -
// the empty-spinner render next to it cannot prove that a stored expiry reaches the picker.
const withSeededQuery = (key, data, children) => {
  const client = createQueryClient()
  client.setQueryData(key, data)

  return <QueryClientProvider client={client}>{children}</QueryClientProvider>
}

// One data router per render, built and thrown away inside the single synchronous call.
// The probe has to go through one because the unsaved-changes guard on the three forms
// reaches for `useBlocker`, which throws outside a data router - so a bare render of a
// guarded form would take the probe down instead of describing the page. The route is a
// catch-all: the pages that use this helper read nothing from the match, and `*` is the
// smallest route that satisfies the router. Nothing navigates anywhere - no effect runs
// under `renderToStaticMarkup` - so the initial entry is the only location there is.
const withRouter = (path, element) => (
  <RouterProvider
    router={createMemoryRouter([{ path: '*', element }], { initialEntries: [path] })}
  />
)

// The same router, handed the routes instead of a catch-all, for the three pages that read
// `useParams`: a `*` match has no `:id` in it to find, so the edit address would quietly
// render the *add* form at a URL whose whole meaning is "this one, by id". The routes are
// the same `:id` patterns the application's own table declares.
const withRoutes = (path, routes) => (
  <RouterProvider router={createMemoryRouter(routes, { initialEntries: [path] })} />
)

const render = (Page, path) =>
  renderToStaticMarkup(
    withRouter(path, withQuery(
      <AntApp>
        <Page />
      </AntApp>,
    )),
  )

let failed = 0
const check = (name, ok, detail = '') => {
  if (!ok) failed += 1
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}${detail ? `\n      ${detail}` : ''}`)
}

// WCAG relative luminance and the ratio between two colours. "Readable" has to end up as a
// number if a check is going to say it: 4.5 is the bar for a 14px label, and the menu's two
// selections below are the pair that failed it in opposite directions before it was split.
const luminance = (hex) => {
  const channel = (value) => {
    const v = value / 255
    return v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4
  }
  const rgb = parseInt(hex.slice(1), 16)
  return (
    0.2126 * channel((rgb >> 16) & 255) +
    0.7152 * channel((rgb >> 8) & 255) +
    0.0722 * channel(rgb & 255)
  )
}

const contrast = (fg, bg) => {
  const [hi, lo] = [luminance(fg), luminance(bg)].sort((a, b) => b - a)
  return (hi + 0.05) / (lo + 0.05)
}

async function main() {
  await i18n.changeLanguage('en')

  // Reseller and Admin never had a footer either - `git log -S footer` over those two
  // returns nothing - until the password reset gave them a reason to. Each form now offers
  // the way out that fits its own visitor: registration for a client who has no account,
  // the reset for anybody who has one and cannot open it, both on the same page as the
  // form they belong to.
  const cases = [
    {
      page: ClientLoginPage,
      path: '/login',
      mustContain: ["Don't have an account? Sign up", 'Forgot your password?', 'Sign in'],
      label: 'client login links to registration and to the password reset',
      hasFooter: true,
    },
    {
      page: RegisterPage,
      path: '/register',
      mustContain: ['Already have an account? Sign in', 'Sign up'],
      label: 'registration links to client login',
      hasFooter: true,
    },
    {
      page: ForgotPasswordPage,
      path: '/forgot-password',
      // Nothing here renders an effect, so this is the page while it is still asking the
      // server whether a mail server exists at all - the state that must be a spinner and
      // a heading, never a form whose second step cannot arrive.
      mustContain: ['Password reset', 'Already have an account? Sign in'],
      label: 'the password reset waits for the server and offers a way back to signing in',
      hasFooter: true,
    },
    {
      page: ResellerLoginPage,
      path: '/reseller/login',
      mustContain: ['Sign in', 'Forgot your password?'],
      label: 'reseller login renders its form and links to the password reset',
      hasFooter: true,
    },
    {
      page: AdminLoginPage,
      path: '/admin/login',
      mustContain: ['Sign in', 'Forgot your password?'],
      label: 'admin login renders its form and links to the password reset',
      hasFooter: true,
    },
  ]

  for (const { page, path, mustContain, label, hasFooter } of cases) {
    const html = render(page, path)
    // React escapes `'` as `&#x27;` even in text content, so "Don't" reaches the DOM
    // escaped. Comparing against the source string would then fail on a page that is
    // perfectly correct - which is the failure mode this script exists to avoid, not create.
    const text = html
      .replace(/&#x27;/g, "'")
      .replace(/&quot;/g, '"')
      .replace(/&amp;/g, '&')
      .replace(/&lt;/g, '<')
      .replace(/&gt;/g, '>')
    const missing = mustContain.filter((needle) => !text.includes(needle))
    check(
      label,
      missing.length === 0,
      missing.length === 0
        ? `rendered ${html.length} bytes`
        : `MISSING FROM DOM: ${missing.join(', ')}`,
    )
    // The text alone is not the assertion: a bare sentence elsewhere on the page would
    // satisfy it, while a footer prop that renders nothing would not.
    check(
      `${label}: the footer element is ${hasFooter ? 'present' : 'absent, as designed'}`,
      html.includes('auth-form__footer') === hasFooter,
    )
  }

  // The second step is a component, not a route, so nothing else renders it. Rendering it
  // directly is the only way to prove it is not dead code.
  const confirmHtml = renderToStaticMarkup(
    withRouter(
      '/register',
      <AntApp>
        <ConfirmCodeForm email="probe@example.com" onSubmit={() => {}} onBack={() => {}} />
      </AntApp>,
    ),
  )
  check(
    'the confirmation form renders its code field and action',
    confirmHtml.includes('Confirmation code') && confirmHtml.includes('Confirm'),
    confirmHtml.includes('probe@example.com')
      ? 'email prefilled from the pending registration'
      : 'email is NOT prefilled',
  )

  // The guard wraps every guest route, and its documented choice is to render the children
  // while it is still asking the server - a login form must not sit behind a round trip,
  // and `fetch` has no timeout to hide a hang behind. The effect never runs in
  // `renderToStaticMarkup`, so this render *is* the "still asking" state: the full form
  // appearing here is what pins that choice. A spinner or a blank page would fail it, and
  // so would a guard that swallowed its children.
  const guardedHtml = renderToStaticMarkup(
    withRouter(
      '/login',
      // `withQuery`, because the page's own submit hook is a `useMutation` and reads the
      // client at render time - the same client `App.tsx` puts above the router. The
      // guard is not the reason; it is what this render is here to observe.
      withQuery(
        <AntApp>
          <GuestOnly>
            <ClientLoginPage />
          </GuestOnly>
        </AntApp>,
      ),
    ),
  )
  check(
    'the guest guard shows the form while it is still asking the server',
    guardedHtml.includes('Sign in') && guardedHtml.includes('auth-form__footer'),
    guardedHtml.includes('Sign in')
      ? `children render during the check (${guardedHtml.length} bytes)`
      : 'GUARD RENDERED NO FORM',
  )

  // --- The signed-in shell ---------------------------------------------------
  //
  // Three things cannot be checked by rendering, so they are checked against the route
  // table instead, which is the only place the three areas are declared.
  const ROLES = ['Client', 'Reseller', 'Admin']

  // --- The profile page ------------------------------------------------------
  //
  // It sits behind a session and asks the server for the row, so an effect never runs under
  // `renderToStaticMarkup` and this render *is* the moment before the answer arrives. What
  // it proves is that the page draws at all and that it waits rather than showing a form
  // against nothing - a spinner where the change form would be.
  const profileHtml = render(ProfilePage, '/profile')
  check(
    'the profile page waits for the server instead of rendering a form against nothing',
    profileHtml.includes('ant-spin') && !profileHtml.includes('auth-form__otp'),
    `rendered ${profileHtml.length} bytes with the profile still being fetched`,
  )

  // --- The visit history page ------------------------------------------------
  //
  // Its own props rather than the shared `render` helper, because the router is what
  // supplies them: `routes.tsx` renders every section as
  // `<Page sectionKey={item.labelKey} />` and the helper passes none, so calling it here
  // would measure `t(undefined)` instead of the page. The state it lands in is the one
  // before the first answer arrives - a spinner over an empty table - which is what
  // proves it draws a table at all. The third condition is the i18n one: the nav key has
  // to come back resolved, and a raw `nav.items.sessions` in the markup would mean the
  // label was written in the menu and never defined in the locale files.
  const sessionsHtml = renderToStaticMarkup(
    withRouter('/client/sessions', withQuery(
      <AntApp>
        <SessionsPage sectionKey="nav.items.sessions" />
      </AntApp>,
    )),
  )
  check(
    'the visit history page draws its heading and its table before any rows arrive',
    sessionsHtml.includes('Sign-in history') &&
      sessionsHtml.includes('ant-table') &&
      !sessionsHtml.includes('nav.items.sessions'),
    `rendered ${sessionsHtml.length} bytes with the history still being fetched`,
  )

  // What keeps a long provider name from dragging the table sideways. A column with no width
  // of its own grows to fit its widest cell, and the network column is the one with no natural
  // bound - an organisation name is whatever the registry called the company, and can run to
  // sixty characters. Fixed layout, a width on each of the three columns and a body that scrolls
  // instead of stretching are three separate facts, and dropping any one of them lets the
  // content set the width again. None of that is visible to typecheck, lint or the build.
  //
  // Three columns on the owner's round-3 correction, where there were five: ip, country and
  // network became one cell - the same one the connection logs draw - and its own invariant is
  // 1140, not the 1190 the administration tables reach with columns this page does not have.
  const sessionsCols = [...sessionsHtml.matchAll(/<col\s[^>]*>/g)].map((m) => m[0])
  const sessionsWidths = sessionsCols.map((col) => /width:(\d+)px/.exec(col)?.[1])
  const sessionsTotal = sessionsWidths.reduce((sum, width) => sum + (Number(width) || 0), 0)
  check(
    'the visit history table is fixed-layout with its three merged columns summing to 1140',
    /table-layout:\s*fixed/.test(sessionsHtml) &&
      sessionsWidths.join(',') === '300,440,400' &&
      sessionsTotal === 1140,
    `${sessionsCols.length} columns: [${sessionsWidths.join(', ')}] = ${sessionsTotal}px`,
  )

  check(
    'the visit history draws one network column instead of separate IP, Country and Network ones',
    sessionsHtml.includes('>Network<') &&
      sessionsHtml.includes('>User agent<') &&
      !sessionsHtml.includes('>IP address<') &&
      !sessionsHtml.includes('>Country<'),
    `rendered ${sessionsHtml.length} bytes`,
  )

  // The resize handles, and the fact that they are the whole of the resizing feature. They are
  // produced by `ResizableHeaderCell` through `components.header.cell` from props supplied by
  // `onHeaderCell` on each of the three columns, and every link in that chain is invisible to
  // typecheck, lint and the build: break any one of them and the table still draws three perfect
  // columns with nothing on them to drag and nothing focusable for a keyboard to reach.
  //
  // The last condition is the one that caught a real bug. `aria-label` cannot simply be passed
  // through `onHeaderCell`, because `useSorter` writes that attribute onto a sortable header
  // cell itself, *after* what the column returned - so the sortable columns came back labelled
  // with the column's own name rather than "Resize the …", and the table was perfectly well
  // formed and named two of its handles wrong. Only the rendered markup shows that; the types
  // are satisfied either way.
  const sessionHandles = [...sessionsHtml.matchAll(/<span[^>]*table-col-resizer[^>]*>/g)].map(
    (m) => m[0],
  )
  check(
    'every visit history column carries a keyboard-reachable resize handle, named for resizing',
    sessionHandles.length === 3 &&
      sessionHandles.every(
        (handle) =>
          handle.includes('role="separator"') &&
          handle.includes('aria-orientation="vertical"') &&
          handle.includes('tabindex="0"') &&
          handle.includes('aria-label="Resize the '),
      ),
    `${sessionHandles.length} handles: ${sessionHandles.map((h) => /aria-label="[^"]*"/.exec(h)?.[0]).join(' ') || 'none'}`,
  )

  // ...and the other half of that same bug: what the *cell* kept. `useSorter` gives a sortable
  // header its `tabIndex` and its `aria-label` so that a keyboard can focus it and press Enter
  // to sort, and the handle used to take both of them for itself - which left the header
  // unfocusable and keyboard sorting gone, with nothing anywhere in the output saying so. The
  // handle having a label is not enough: the column still needs its own.
  //
  // The sortable column is now the *network* one: it was the address column, and the address is
  // the first line of the merged cell, so the sorter travelled with it rather than being lost
  // when the column was merged.
  const sessionHeaderCells = [...sessionsHtml.matchAll(/<th\b[^>]*>/g)].map((m) => m[0])
  check(
    'a sortable visit history header keeps its own access to sorting',
    sessionHeaderCells.some(
      (th) => th.includes('aria-label="Network"') && th.includes('tabindex="0"'),
    ),
    `${sessionHeaderCells.length} header cells, sortable one: ${
      sessionHeaderCells.find((th) => th.includes('aria-label="Network"'))?.slice(0, 220) ??
      'none'
    }`,
  )

  const sessionsContent = /<div[^>]*ant-table-content[^>]*>/.exec(sessionsHtml)?.[0] ?? ''
  check(
    'the visit history table scrolls inside its own box instead of widening the page',
    sessionsContent.includes('overflow-x:auto'),
    sessionsContent || 'no ant-table-content div in the markup',
  )

  check(
    'the visit history page offers a search box rather than a sentence about the table',
    sessionsHtml.includes('Search visits') && !sessionsHtml.includes('newest first'),
    `rendered ${sessionsHtml.length} bytes`,
  )

  // --- The active sessions page ----------------------------------------------
  //
  // Same reasoning as the history section above: its own `sectionKey`, because the router is
  // what supplies it, and the state it lands in is the one before the first answer arrives.
  // Three facts, none of which typecheck, lint or the build can see: the heading resolves
  // from the nav key rather than reaching the DOM raw, the bulk button exists while disabled
  // (it is the page's whole second action, and a button that vanishes during load would be
  // the `AuthCard` footer failure all over again - present in props, absent from markup), and
  // the page waits rather than drawing a grid of session cards against no data. The disabled
  // state is itself the correct one here: with no answer in hand the page does not know how
  // many other sessions exist, and a bulk revoke offered from ignorance could end them all.
  const activeSessionsHtml = renderToStaticMarkup(
    withRouter('/client/sessions/active', withQuery(
      <AntApp>
        <ActiveSessionsPage sectionKey="nav.items.activeSessions" />
      </AntApp>,
    )),
  )
  // Where the bulk button's own markup starts, found by walking back from its label: antd
  // wraps the text in a `<span>`, so a regex that expects `>label` to follow the attributes
  // directly would fail on a button that is perfectly correct. Everything between that
  // `<button` and the label is the button's own opening tag plus its span - so a `disabled`
  // found there belongs to this button and not to the retry one somewhere else on the page.
  const bulkLabelAt = activeSessionsHtml.indexOf('End all other sessions')
  const bulkButtonAt = activeSessionsHtml.lastIndexOf('<button', bulkLabelAt)
  const bulkButtonTag = activeSessionsHtml.slice(bulkButtonAt, bulkLabelAt)
  check(
    'the active sessions page draws its heading and its bulk action before any cards arrive',
    activeSessionsHtml.includes('Active sessions') &&
      !activeSessionsHtml.includes('nav.items.activeSessions') &&
      bulkLabelAt !== -1 &&
      bulkButtonTag.includes('disabled') &&
      activeSessionsHtml.includes('ant-spin') &&
      !activeSessionsHtml.includes('This session'),
    `rendered ${activeSessionsHtml.length} bytes with the list still being fetched, bulk tag: ${bulkButtonTag.slice(0, 160)}`,
  )

  // Two helpers the cells above are built from, asserted as functions rather than as markup.
  // Every render above lands in the state before the first answer arrives, so no row is ever
  // drawn and neither helper ever reaches the DOM - which means the cell that shows a country
  // as a name and the one that shows a user agent as one readable line are unreachable from
  // here as HTML. `probe:render` is the probe that needs no backend, so it is the only one of
  // the three that can check them at all; the alternative is a fourth probe for two pure
  // functions, and the reason to put them here is the reason the rest of this file exists -
  // typecheck, lint and the build are all content to pass while the thing renders nothing.
  check(
    'a user agent becomes one readable line, and an unreadable one is left exactly as it is',
    describeUserAgent(
      'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36',
    ) === 'Chrome 140 (Windows 10, x64)' &&
      describeUserAgent(
        'Mozilla/5.0 (Macintosh; Intel Mac OS X 14_5) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.1 Safari/605.1.15',
      ) === 'Safari 18.1 (macOS 14.5)' &&
      describeUserAgent('curl/8.5.0') === null &&
      describeUserAgent(null) === null,
    `${describeUserAgent('curl/8.5.0')} / ${describeUserAgent(null)} / ${describeUserAgent(
      'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36',
    )}`,
  )

  // The second line is the one worth having: `Intl.DisplayNames` answers a lower-case `de`
  // with `de` rather than with an error, so a case the source happened to use would quietly
  // turn the whole column back into codes. The third is the `RangeError` guard - `X1` is not
  // two letters and the runtime refuses it before it looks anything up.
  check(
    'a country code reads as the name of the country, in the language of the page',
    countryName('DE', 'en') === 'Germany' &&
      countryName('DE', 'ru') === 'Германия' &&
      countryName('de', 'en') === 'Germany' &&
      countryName('X1', 'en') === 'X1' &&
      countryName(null, 'en') === null,
    `${countryName('DE', 'en')} / ${countryName('DE', 'ru')} / ${countryName('de', 'en')} / ${countryName('X1', 'en')}`,
  )

  // The time zone card cannot be asserted through the page above: the page is a spinner
  // until the server answers, and an effect never runs under `renderToStaticMarkup`, so
  // the loaded state that holds this card is unreachable from here. That is exactly why
  // the card is its own component - rendered directly, this is what proves it draws its
  // heading, its hint and the zone the account actually stored, rather than accepting the
  // props and putting nothing on the page.
  const timezoneHtml = renderToStaticMarkup(
    withRouter(
      '/profile',
      <AntApp>
        <TimeZoneCard value="Europe/Moscow" saving={false} onChange={() => {}} />
      </AntApp>,
    ),
  )
  check(
    'the profile offers a time zone card with the stored zone selected',
    [
      'Time zone',
      'Dates and times are shown in this zone.',
      'Europe/Moscow',
      'ant-select',
    ].every((needle) => timezoneHtml.includes(needle)),
    timezoneHtml.includes('Europe/Moscow')
      ? `rendered ${timezoneHtml.length} bytes with the saved zone on the selector`
      : 'THE SELECTOR DID NOT RENDER THE STORED ZONE',
  )

  // The login guard card cannot be asserted through the page either, for the same
  // reason: it lives in the loaded state the static render never reaches. Rendered
  // directly, this is what proves it draws its heading, its hint, the stored lists and
  // both switches, rather than accepting the guard and putting nothing on the page.
  const guardHtml = renderToStaticMarkup(
    withRouter(
      '/profile',
      <AntApp>
        <LoginGuardCard
          value={{
            geoProtectionEnabled: true,
            bindSessionToIp: false,
            allowedIps: ['198.51.100.7'],
            allowedCountry: 'RU',
            allowedAutonomousSystemNumber: 12345,
          }}
          onSubmit={() => Promise.resolve({ code: 'profile_updated' })}
          onSubmitted={() => {}}
          onApplied={() => Promise.resolve()}
        />
      </AntApp>,
    ),
  )
  // The stored country now arrives as a *selection* rather than as two letters in an
  // input: `RU` is in the markup only as its name, because the code is what the form
  // holds and the name is what it draws. `Russia` is the English label this probe's
  // language gives it - the language itself is pinned by the heading two needles above.
  check(
    'the profile offers a login guard card with the stored lists on it',
    [
      'Sign-in protection',
      'Protect sign-in by network',
      '198.51.100.7',
      'Russia',
      '12345',
      'ant-switch',
      'ant-select',
    ].every((needle) => guardHtml.includes(needle)),
    guardHtml.includes('198.51.100.7')
      ? `rendered ${guardHtml.length} bytes with the stored guard on the card`
      : 'THE CARD DID NOT RENDER THE STORED GUARD',
  )

  // The switch decides whether the four allow lists are read at all, and while it is off
  // the card disables them rather than unmounting them: a field that is not rendered is a
  // field whose value `onFinish` never reports, and a save through it would empty the very
  // lists it was meant to keep. So the assertion is the `disabled` attribute *and* the
  // values still being there. This is the only thing that can see it - typecheck, lint and
  // build all pass over a switch wired to nothing - and it can only be seen from out here,
  // because `useWatch` never runs under `renderToStaticMarkup`: what is rendered below is
  // the stored value underneath it, which is the same answer the watcher gives after mount.
  const guardOffHtml = renderToStaticMarkup(
    withRouter(
      '/profile',
      <AntApp>
        <LoginGuardCard
          value={{
            geoProtectionEnabled: false,
            bindSessionToIp: false,
            allowedIps: ['198.51.100.7'],
            allowedCountry: 'RU',
            allowedAutonomousSystemNumber: 12345,
          }}
          onSubmit={() => Promise.resolve({ code: 'profile_updated' })}
          onSubmitted={() => {}}
          onApplied={() => Promise.resolve()}
        />
      </AntApp>,
    ),
  )
  const disabledCount = guardOffHtml.split('disabled').length - 1
  check(
    'the login guard disables its allow lists while protection is off, without hiding them',
    guardOffHtml.includes('198.51.100.7') && disabledCount >= 3,
    guardOffHtml.includes('198.51.100.7')
      ? `${disabledCount} occurrences of "disabled", the stored lists still on the card`
      : 'THE LISTS DISAPPEARED FROM THE CARD',
  )

  // --- The admin's users pages -----------------------------------------------
  //
  // Both pages land in the state before their first answer, like every render in this
  // script: `renderToStaticMarkup` never runs an effect, so what is measured here is what a
  // visitor sees while the page is still fetching - and the facts below are the ones all
  // three build checks are satisfied by while broken:
  //
  // - the heading is the page's own title rather than the menu item's name - the two used
  //   to be the same word, which is exactly the echo of its own menu entry the page
  //   stopped making;
  // - the table draws its heading, its toolbar and the table itself before any row exists,
  //   and all eight columns keep their own width *in order*: with the 40px checkbox column
  //   antd prepends, the sum of 1230px is the deliberate total that keeps the action buttons
  //   at the far right inside one Full HD viewport beside the sidebar, and the three content
  //   columns (160/180/130) are narrow *because* their cells truncate into tooltips - the
  //   next person to widen one should learn here what the widening costs. `email` is 160
  //   rather than the 200 it was before the selection arrived, which is where the checkbox
  //   was paid for from;
  // - the selection's toolbar control is drawn with nothing selected, the card pins its
  //   title to one line so the toolbar is what yields when the row is too wide, and the
  //   selection column's header cell is the one that carries **no** resize handle -
  //   `components.header.cell` replaces the cell for every header including the one antd
  //   builds itself, and that column never calls `onHeaderCell`;
  // - every column carries a keyboard-reachable resize handle named for resizing, and a
  //   sortable header keeps its own cell access - the same two `ResizableHeaderCell`
  //   facts the visit history asserts, now asserted over a *second* table, because two
  //   tables disagreeing about either would be two places for one rule to be wrong;
  // - the add form draws its fields at once under the add title with the shared
  //   `LoginGuardFields` present (the extraction below the DOM is invisible to every
  //   checker here - a form that rendered without the guard would still compile), while
  //   carrying neither the id row nor the edit-only password hint;
  // - the edit address is a spinner under the edit title rather than a form of empty
  //   fields pointed at an account that has not arrived.
  // The generator is asserted as a function for the reason the two display helpers in the
  // visit history are: no render reaches it, and every property it is sold on stays
  // invisible until somebody clicks. Ten characters of letters and digits is the button's
  // label, satisfying the complexity rule is the entire point of drawing from three
  // classes, and two draws differing is the difference between a generator and a
  // constant.
  const [firstDraw, secondDraw] = [generatePassword(), generatePassword()]
  check(
    'the generated password is ten letters and digits that satisfy the complexity rule',
    firstDraw.length === GENERATED_PASSWORD_LENGTH &&
      /^[a-zA-Z0-9]+$/.test(firstDraw) &&
      meetsPasswordComplexity(firstDraw) &&
      secondDraw.length === GENERATED_PASSWORD_LENGTH &&
      meetsPasswordComplexity(secondDraw) &&
      firstDraw !== secondDraw,
    `${firstDraw} and ${secondDraw}`,
  )

  const usersHtml = renderToStaticMarkup(
    withRouter('/admin/users', withQuery(
      <AntApp>
        <UsersPage sectionKey="nav.items.usersManage" />
      </AntApp>,
    )),
  )

  // The heading is the page's own title, not the menu item's name: "Manage users" says
  // how the visitor got here, and a card repeating it would say the menu again. The
  // absence is the half that fails if a future edit goes back to `t(sectionKey)`.
  check(
    'the users table draws its heading and its table before any rows arrive',
    usersHtml.includes('>Users<') &&
      !usersHtml.includes('Manage users') &&
      usersHtml.includes('ant-table') &&
      !usersHtml.includes('nav.items.usersManage'),
    `rendered ${usersHtml.length} bytes with the list still being fetched`,
  )

  // The second filter used to read "Any role" - accounts were filtered by the column their
  // row carried directly. They are filtered by group now, because a row no longer *has* a
  // role of its own: the group is what the server joins to answer one, and a filter over a
  // field that is no longer in the query would be a control the endpoint silently ignores.
  // Asserted as text rather than as a key so a filter that quietly disappears fails here.
  check(
    'the users toolbar offers the search, both filters, the width reset and the way to add',
    usersHtml.includes('Search by username or email') &&
      usersHtml.includes('Any group') &&
      usersHtml.includes('Any status') &&
      usersHtml.includes('aria-label="Reset widths"') &&
      usersHtml.includes('Add user'),
    usersHtml.includes('Add user') ? `rendered ${usersHtml.length} bytes` : 'the add button went missing',
  )

  // The selection's control, drawn with nothing selected for the reason the "Reset widths"
  // button is drawn while disabled: a control that appears and disappears reads as a broken
  // one, and the count *inside* the button is what says why it is greyed. Every render here
  // lands in the state before the first answer, so a greyed button reading just "Actions" is
  // the first paint - and a page that hid it until something was ticked would be a page whose
  // bulk actions are invisible until the visitor already knows they exist.
  //
  // Matched on the *button* rather than on the word "Actions", which is also the title of the
  // last column on both tables - an assertion on the word alone would pass on a page that had
  // lost its bulk menu entirely.
  const usersBulkButtons = [...usersHtml.matchAll(/<button[^>]*>[\s\S]*?<\/button>/g)]
    .map((m) => m[0])
    .filter((tag) => /<span[^>]*>Actions<\/span>/.test(tag))
  check(
    'the users toolbar draws the bulk menu, greyed while nothing is selected',
    usersBulkButtons.length === 1 && usersBulkButtons[0].includes('disabled'),
    `${usersBulkButtons.length} button(s) labelled "Actions": ${usersBulkButtons[0]?.slice(0, 180) ?? 'none'}`,
  )

  // The header's desktop tie-break, and the half of the fix a narrower toolbar is not: when
  // the row is still too wide - a longer label, a narrower window, a browser at 150% - the
  // title is pinned to one line so the *toolbar* is what yields, wrapping its controls onto
  // a second row through `Space wrap`. Below 1200px the `page-card` rules in `index.css`
  // take over and give the title a row of its own; below 768px they release this inline
  // `nowrap` so a long heading may wrap. The prop is still the desktop answer and the static
  // render has no media query to consult, so it is asserted here rather than trusted.
  check(
    'the users card pins its title to one line on the desktop so the toolbar is what yields',
    /<div[^>]*class="ant-card-head-title"[^>]*style="white-space:nowrap"/.test(usersHtml),
    `head title: ${
      /<div[^>]*class="ant-card-head-title"[^>]*>/.exec(usersHtml)?.[0] ?? 'not found'
    }`,
  )

  const usersCols = [...usersHtml.matchAll(/<col\s[^>]*>/g)].map((m) => m[0])
  const usersWidths = usersCols.map((col) => /width:(\d+)px/.exec(col)?.[1])
  const usersTotal = usersWidths.reduce((sum, width) => sum + (Number(width) || 0), 0)
  // Nine columns, not eight: antd prepends the checkbox column the selection adds, at the
  // 40px the pages declare. The eight this page owns keep their own widths and their own
  // order, with `email` at 160 rather than the 200 it was before - the forty the checkbox
  // takes came out of the one column that truncates into a tooltip anyway, which is why the
  // total is still the 1230 the sidebar has always left room for.
  //
  // The actions column is the one width that is not chosen: it is what the four buttons of
  // a row need and not a pixel more (`actionsColumnWidth`), and the room that frees went to
  // the username. A column wider than the buttons in it is dead space at the end of every
  // row, which is the part of a table that reads as something missing.
  check(
    'the users table is fixed-layout with a selection column and its eight deliberate widths, in order',
    /table-layout:\s*fixed/.test(usersHtml) &&
      usersWidths.join(',') === '40,100,180,160,150,150,180,130,140' &&
      usersTotal === 1230,
    `${usersCols.length} columns: [${usersWidths.join(', ')}] = ${usersTotal}px`,
  )

  const usersHandles = [...usersHtml.matchAll(/<span[^>]*table-col-resizer[^>]*>/g)].map(
    (m) => m[0],
  )
  check(
    'every users column carries a keyboard-reachable resize handle, named for resizing',
    usersHandles.length === 8 &&
      usersHandles.every(
        (handle) =>
          handle.includes('role="separator"') &&
          handle.includes('aria-orientation="vertical"') &&
          handle.includes('tabindex="0"') &&
          handle.includes('aria-label="Resize the '),
      ),
    `${usersHandles.length} handles: ${usersHandles.map((h) => /aria-label="[^"]*"/.exec(h)?.[0]).join(' ') || 'none'}`,
  )

  // The selection column is the one header cell with no per-column data to hand over, so it
  // is where `ResizableHeaderCell`'s fallback is worth pinning: `components.header.cell`
  // replaces the cell for *every* header, including the one antd builds itself, and that
  // column never calls `onHeaderCell`. The check above counting eight handles over nine
  // header cells already implies the fallback - this one names it, because the failure it
  // guards against is a handle drawn over a checkbox with nowhere to put a width and no
  // `onHeaderCell` to be told about it.
  const usersHeaderCells = [...usersHtml.matchAll(/<th\b[^>]*>/g)].map((m) => m[0])
  check(
    'the selection column draws a plain header cell: no resize handle of its own',
    usersHeaderCells.length === 9 &&
      usersHeaderCells.filter((th) => th.includes('table-col-resizer')).length === 0 &&
      usersHtml.includes('ant-table-selection') &&
      usersHtml.includes('type="checkbox"'),
    `${usersHeaderCells.length} header cells, ${usersHandles.length} of them resizable`,
  )

  check(
    'a sortable users header keeps its own access to sorting',
    usersHeaderCells.some(
      (th) => th.includes('aria-label="Username"') && th.includes('tabindex="0"'),
    ),
    `${usersHeaderCells.length} header cells, sortable one: ${
      usersHeaderCells.find((th) => th.includes('aria-label="Username"'))?.slice(0, 220) ??
      'none'
    }`,
  )

  const userFormHtml = renderToStaticMarkup(
    withRouter('/admin/users/add', withQuery(
      <AntApp>
        <UserFormPage />
      </AntApp>,
    )),
  )
  check(
    'the add form draws its fields at once, under the add title, with the shared guard',
    userFormHtml.includes('Add user') &&
      userFormHtml.includes('Create') &&
      userFormHtml.includes('Sign-in protection') &&
      !userFormHtml.includes('Edit user') &&
      // The id row is the one field-shaped thing the add address must not draw, and it is
      // recognisable by the copy affordance rather than by its text: the edit form's id is
      // `Typography.Text copyable`, and nothing else on either form copies anything.
      !userFormHtml.includes('anticon-copy') &&
      // The edit-only password hint is the other half of that split - present exactly when
      // "keep the current password" is a rule the form is following.
      !userFormHtml.includes('Leave empty to keep the current password'),
    `rendered ${userFormHtml.length} bytes of the add form`,
  )

  // The browser's "generate a strong password" offer is a DOM fact and nothing else: the
  // form says `autoComplete="on"` - antd renders no attribute at all unless told, and
  // `AuthCard` (the registration form that demonstrably triggers the offer) passes exactly
  // this - and the one password input says `new-password`. Both had to be found out the
  // hard way: the form's attribute was simply missing here, and rc-select turned out to
  // hardcode `new-password` on every hidden combobox input with no prop that reaches it
  // (`autoComplete || 'new-password'` in `@rc-component/select` `SelectInput/Input.js`;
  // the Select's own `autoComplete` lands on the wrapper div). So the five extra
  // `new-password` attributes on this form are unavoidable, and the last condition pins
  // what makes them inert rather than trusting it: they sit on `ant-select-input`
  // combobox text inputs - never password-type fields, which is what a password manager
  // counts when it classifies a form. An input of ours picking up `new-password` would
  // be a claim nobody meant to make, and this is what notices.
  const newPasswordInputs = [...userFormHtml.matchAll(/<input[^>]*>/g)]
    .map((m) => m[0])
    .filter((tag) => tag.includes('autoComplete="new-password"'))
  check(
    'the add form is one the browser will offer to generate a password for',
    /<form[^>]*autoComplete="on"/.test(userFormHtml) &&
      newPasswordInputs.some((tag) => tag.includes('type="password"')) &&
      newPasswordInputs
        .filter((tag) => !tag.includes('type="password"'))
        .every((tag) => tag.includes('type="text"') && tag.includes('class="ant-select-input"')),
    `${newPasswordInputs.length} inputs claim new-password, one of them the password field`,
  )

  // The explicit button beside that offer, with the length interpolated rather than
  // written twice: the label the visitor reads and the constant the generator obeys are
  // built from the same number here, so one cannot drift from the other.
  check(
    'the add form offers the explicit generate button under the password field',
    userFormHtml.includes(
      `Generate password (${GENERATED_PASSWORD_LENGTH} letters and digits)`,
    ),
    `rendered ${userFormHtml.length} bytes of the add form`,
  )

  // The credit field: always drawn, disabled until the chosen group is a reseller's, with the
  // hint saying why. The static render has no group list, so `chosenGroup` is undefined and
  // the first paint is exactly the disabled state the rule exists for. The typed value is not
  // asserted here - a static render has no form input to type into - so the "not sent while
  // disabled" half is read off the page's source below.
  const userFormSource = readFileSync('src/pages/admin/UserFormPage.tsx', 'utf8')
  check(
    'the add form draws the balance field disabled with its hint while no reseller group is chosen',
    userFormHtml.includes('Credits') &&
      userFormHtml.includes('Credits apply only to accounts in a reseller group.') &&
      /<input[^>]*ant-input-number-input[^>]*disabled/.test(userFormHtml) &&
      userFormSource.includes("name=\"credits\"") &&
      /chosenGroup\?\.role === 'Reseller'/.test(userFormSource) &&
      /creditsEnabled \? values\.credits : undefined/.test(userFormSource) &&
      // The server keys a refusal by this camelCase name, so the `Form.Item` must carry it.
      i18n.getResource('en', 'translation', 'users.creditsLabel') === 'Credits' &&
      i18n.getResource('ru', 'translation', 'users.creditsLabel') === 'Кредиты' &&
      i18n.getResource('en', 'translation', 'validation.creditsInvalid') ===
        'Enter a whole number of 0 or more.',
    `rendered ${userFormHtml.length} bytes of the add form`,
  )

  check(
    'the balance input is a non-negative whole number, 200px wide, after the group field',
    /min=\{0\}[\s\S]{0,80}?precision=\{0\}/.test(userFormSource) &&
      /maxWidth: 200/.test(userFormSource) &&
      userFormSource.indexOf('name="groupId"') < userFormSource.indexOf('name="credits"') &&
      userFormSource.indexOf('name="credits"') < userFormSource.indexOf('name="status"'),
    'min 0, precision 0, a 200px control, drawn between the group and the status',
  )

  // The edit address needs a real route rather than a bare render: `useParams` reads the
  // router's match, and without one there is no `:id` to be found - the page would quietly
  // render its *add* mode at a URL whose whole meaning is "this one, by id".
  const userEditHtml = renderToStaticMarkup(
    withRoutes('/admin/users/11111111-1111-1111-1111-111111111111', [
      {
        path: '/admin/users/:id',
        element: withQuery(
          <AntApp>
            <UserFormPage />
          </AntApp>,
        ),
      },
    ]),
  )
  check(
    'the edit address waits for the account instead of drawing fields against nothing',
    userEditHtml.includes('Edit user') &&
      userEditHtml.includes('ant-spin') &&
      !userEditHtml.includes('Create'),
    `rendered ${userEditHtml.length} bytes under the edit title with the spinner up`,
  )

  // The group table, which is the accounts table's twin by design and its own page in
  // every other respect: it hangs off a different permission (`viewUserGroups` rather than
  // any of the per-role `view*` permissions), so a page that rendered the wrong one would be
  // a page answering a question the visitor never asked and would keep answering it while
  // every check above stayed green.
  const groupsHtml = renderToStaticMarkup(
    withRouter('/admin/user-groups', withQuery(
      <AntApp>
        <UserGroupsPage sectionKey="nav.items.userGroups" />
      </AntApp>,
    )),
  )

  // The heading is the page's own title rather than the menu item's name, for the reason
  // the users table's is: `Groups` says how the visitor got there, `User groups` says what
  // they are looking at. Both halves are asserted, because a card that went back to
  // `t(sectionKey)` would still show a sensible-looking sentence.
  check(
    'the groups table draws its own heading and its table before any rows arrive',
    groupsHtml.includes('>User groups<') &&
      !groupsHtml.includes('>Groups<') &&
      groupsHtml.includes('ant-table') &&
      !groupsHtml.includes('nav.items.userGroups'),
    `rendered ${groupsHtml.length} bytes with the list still being fetched`,
  )

  check(
    'the groups toolbar offers the search, both filters, the width reset and the way to add',
    groupsHtml.includes('Search by group name') &&
      groupsHtml.includes('Any role') &&
      groupsHtml.includes('Any status') &&
      groupsHtml.includes('aria-label="Reset widths"') &&
      groupsHtml.includes('Add group'),
    groupsHtml.includes('Add group')
      ? `rendered ${groupsHtml.length} bytes`
      : 'the way to add a group is missing from the toolbar',
  )

  // The same control the accounts table offers, in the same place, with the count folded
  // into it - two tables with different rules for a selection would be two places for one
  // rule to be wrong. Matched on the button for the reason the accounts table's is: "Actions"
  // is also the title of this table's last column.
  const groupsBulkButtons = [...groupsHtml.matchAll(/<button[^>]*>[\s\S]*?<\/button>/g)]
    .map((m) => m[0])
    .filter((tag) => /<span[^>]*>Actions<\/span>/.test(tag))
  check(
    'the groups toolbar draws the bulk menu, greyed while nothing is selected',
    groupsBulkButtons.length === 1 && groupsBulkButtons[0].includes('disabled'),
    `${groupsBulkButtons.length} button(s) labelled "Actions": ${groupsBulkButtons[0]?.slice(0, 180) ?? 'none'}`,
  )

  // The same desktop tie-break as the accounts table's, and the more load-bearing of the two
  // here: `Группы пользователей` is the longest heading either table carries, sitting beside
  // what is now the wider of the two toolbars. The compact rules below 1200px are the other
  // half of the answer, and the `page-card` class they key on is asserted with the other
  // toolbar cards.
  check(
    'the groups card pins its title to one line on the desktop so the toolbar is what yields',
    /<div[^>]*class="ant-card-head-title"[^>]*style="white-space:nowrap"/.test(groupsHtml),
    `head title: ${
      /<div[^>]*class="ant-card-head-title"[^>]*>/.exec(groupsHtml)?.[0] ?? 'not found'
    }`,
  )

  // Seven columns, and the same 1230px the accounts table holds on one Full HD screen: the
  // two tables sit under one sidebar and a visitor who can see one of them whole should be
  // able to see the other. The widths differ because the content does - the name column is
  // 420 rather than 170 because a group name is the one thing on this page nobody truncates
  // into a tooltip, and it is also where the room the actions column gave up went. The first
  // `<col>` is the 40px checkbox column the selection adds, which neither this table's six
  // widths nor the hook's memory know anything about.
  const groupsCols = [...groupsHtml.matchAll(/<col\s[^>]*>/g)].map((m) => m[0])
  const groupsWidths = groupsCols.map((col) => /width:(\d+)px/.exec(col)?.[1])
  const groupsTotal = groupsWidths.reduce((sum, width) => sum + (Number(width) || 0), 0)
  check(
    'the groups table is fixed-layout with a selection column and its six deliberate widths, in order',
    /table-layout:\s*fixed/.test(groupsHtml) &&
      groupsWidths.join(',') === '40,110,420,180,200,140,140' &&
      groupsTotal === 1230,
    `${groupsCols.length} columns: [${groupsWidths.join(', ')}] = ${groupsTotal}px`,
  )

  // `id` and `actions` are the two columns that must never produce a sorter event - the
  // server's `UserGroupSortField` has no member for either, and a header that offered one
  // would answer with `validation_failed`. Four sortable headers is the number that has to
  // come out of this, and the handles are the same six the accounts table draws.
  const groupsHandles = [...groupsHtml.matchAll(/<span[^>]*table-col-resizer[^>]*>/g)].map(
    (m) => m[0],
  )
  const groupsHeaderCells = [...groupsHtml.matchAll(/<th\b[^>]*>/g)].map((m) => m[0])
  check(
    'every groups column carries a handle, and four of the six headers sort',
    groupsHandles.length === 6 &&
      groupsHeaderCells.filter((th) => th.includes('aria-description="sortable"')).length === 4 &&
      groupsHeaderCells.some(
        (th) => th.includes('aria-label="Name"') && th.includes('tabindex="0"'),
      ),
    `${groupsHandles.length} handles, ${groupsHeaderCells.filter((th) => th.includes('aria-description="sortable"')).length} sortable headers`,
  )

  const groupFormHtml = renderToStaticMarkup(
    withRouter('/admin/user-groups/add', withQuery(
      <AntApp>
        <UserGroupFormPage sectionKey="nav.items.userGroups" />
      </AntApp>,
    )),
  )

  // The add form opens at the least privileged level on purpose, and the visible
  // consequence is asserted rather than reasoned about: a `Client` group owns no
  // permissions at all, so the permission section draws its explanation instead of eight
  // boxes that would all be refused - and the two bulk buttons go with the boxes, since
  // "select all" over nothing is a control that cannot be obeyed. A default that had crept
  // up to `Admin` would show up here as checkboxes, and a bulk row that stayed behind them
  // going away would show up here too.
  check(
    'the group add form draws its fields at once, and starts with nothing granted',
    groupFormHtml.includes('Add group') &&
      groupFormHtml.includes('Create') &&
      groupFormHtml.includes('Permissions') &&
      groupFormHtml.includes('owns no permissions') &&
      !groupFormHtml.includes('Edit group') &&
      // No id row and no copy affordance: the identifier does not exist until the row does.
      !groupFormHtml.includes('anticon-copy') &&
      // No base-group notice either - that sentence is a fact about a row, and this form
      // has not made one.
      !groupFormHtml.includes('one of the three base groups') &&
      !groupFormHtml.includes('ant-checkbox-wrapper') &&
      !groupFormHtml.includes('Select all'),
    `rendered ${groupFormHtml.length} bytes of the add form`,
  )

  // The group filter arrives in the address (`?groupId=`), which is how the groups table's
  // "show accounts" button sends a visitor over. Asserted because a filter kept in the
  // component's state instead would be gone by the time this page mounted, and the button
  // would land on the unfiltered list looking like it had done nothing - which no other
  // check here could see, since every render of this page also arrives with no rows.
  const groupArrivalHtml = renderToStaticMarkup(
    withRouter('/admin/users?groupId=22222222-2222-2222-2222-222222222222', withQuery(
      <AntApp>
        <UsersPage sectionKey="nav.items.usersManage" />
      </AntApp>,
    )),
  )
  check(
    'the accounts page picks its group filter up from the address it was opened at',
    groupArrivalHtml.includes('22222222-2222-2222-2222-222222222222'),
    groupArrivalHtml.includes('22222222-2222-2222-2222-222222222222')
      ? `the arriving group is on the filter of ${groupArrivalHtml.length} bytes`
      : 'THE ARRIVING GROUP WAS DROPPED - the button would open an unfiltered list',
  )

  // The table's own state arrives the same way the filters do: `page`, `pageSize`, `search`,
  // `sortBy` and `sortOrder` are read back from the address rather than kept beside it, so a
  // reload keeps the view and the address can be sent to somebody else. Three of the five are
  // visible before the first answer arrives, and each of them would silently draw its default
  // if the read were dropped: the search box's text, the filter's selection, and the sorted
  // header's arrow. The pager's own page number is the fourth, and it is not asserted here
  // because antd draws it from `total`, which is 0 in every render this probe makes.
  const stateArrivalHtml = renderToStaticMarkup(
    withRouter('/admin/users?search=ann&status=Blocked&sortBy=email&sortOrder=desc', withQuery(
      <AntApp>
        <UsersPage sectionKey="nav.items.usersManage" />
      </AntApp>,
    )),
  )
  const arrivalSorters = [
    ...stateArrivalHtml.matchAll(/<span[^>]*ant-table-column-sorter-[a-z]+[^>]*>/g),
  ].map((m) => m[0])
  check(
    'the accounts page picks its search, filter and order up from the address it was opened at',
    stateArrivalHtml.includes('value="ann"') &&
      stateArrivalHtml.includes('title="Blocked"') &&
      arrivalSorters.some((s) => s.includes('-down') && s.includes('active')),
    `rendered ${stateArrivalHtml.length} bytes; sorters: ${arrivalSorters.join(' ') || 'none'}`,
  )

  // The two bulk buttons are drawn exactly when the checkbox group is, which means they are
  // unreachable in every state this probe can render: the add form opens at `Client`, and
  // `renderToStaticMarkup` never runs the click that would move it to `Admin`. So the
  // precondition is asserted as a function instead - the same reasoning the two display
  // helpers in the visit history and the password generator above rest on. Without it, a
  // `permissionsForRole` that answered `[]` for every level would leave a perfectly tidy
  // form with no boxes and no bulk row anywhere in the application, and every check above
  // would still pass.
  check(
    'a level that owns permissions offers all of them, and the others none',
    permissionsForRole('Admin').length === ALL_PERMISSIONS.length &&
      permissionsForRole('Client').length === 0 &&
      permissionsForRole('Reseller').length === 0,
    `Admin ${permissionsForRole('Admin').length}/${ALL_PERMISSIONS.length}, Client ${permissionsForRole('Client').length}, Reseller ${permissionsForRole('Reseller').length}`,
  )

  // The split that replaced `viewUsers`/`editUsers`, asserted as a function for the same
  // reason the row above is: the accounts table only ever draws rows the server already
  // filtered to the roles this operator may read, and every render here arrives before the
  // profile answer - so no render in this probe can put a refused button on screen. Without
  // this, a `canEdit` that answered `true` for everything would leave every button live and
  // this file would still be green, which is exactly the state the old two permissions were
  // in: an operator allowed to run the clients could open, patch and delete an administrator.
  check(
    'view and edit are asked per target role, and the two groups permissions are neither',
    canView(['viewClients'], 'Client') &&
      !canView(['viewClients'], 'Admin') &&
      canView(['viewAdmins', 'editAdmins'], 'Admin') &&
      !canView(['viewAdmins'], 'Reseller') &&
      canEdit(['editResellers'], 'Reseller') &&
      !canEdit(['editResellers'], 'Client') &&
      canViewAny(['viewAdmins']) &&
      !canViewAny(['editAdmins']) &&
      !canViewAny(['viewUserGroups', 'editUserGroups']) &&
      canEditAny(['editClients']) &&
      !canEditAny(['viewUserGroups', 'editUserGroups']),
    'editClients alone must not read administrators, and neither groups permission may stand in for a view or an edit',
  )

  // The edit address of a group, with the same rule the accounts edit address follows: it
  // waits. `useParams` needs a real route match, so the router is handed the same `:id`
  // route the application's own table declares.
  const groupEditHtml = renderToStaticMarkup(
    withRoutes('/admin/user-groups/11111111-1111-1111-1111-111111111111', [
      {
        path: '/admin/user-groups/:id',
        element: withQuery(
          <AntApp>
            <UserGroupFormPage sectionKey="nav.items.userGroups" />
          </AntApp>,
        ),
      },
    ]),
  )
  check(
    'the group edit address waits for the group instead of drawing fields against nothing',
    groupEditHtml.includes('Edit group') &&
      groupEditHtml.includes('ant-spin') &&
      !groupEditHtml.includes('Create'),
    `rendered ${groupEditHtml.length} bytes under the edit title with the spinner up`,
  )

  // --- The content categories page --------------------------------------------
  //
  // One component behind four menu entries, and the type of content it lists is read from
  // the very key the router passes it - so the facts the renders pin are the ones nothing
  // else can see: the heading names the kind the key maps to (and a *different* key names
  // a different kind, which is the mapping itself asserted), the add action and the
  // unsaved-order controls draw before any row arrives, and every render here lands in
  // the state where the list has no rows yet and the drag handles therefore none.
  const categoriesHtml = renderToStaticMarkup(
    withRouter('/admin/video-streams/categories', withQuery(
      <AntApp>
        <ContentCategoriesPage sectionKey="nav.items.videoStreamsCategories" />
      </AntApp>,
    )),
  )
  check(
    'the video stream categories page draws its typed heading, its add action and its table before any rows arrive',
    categoriesHtml.includes('Video stream categories') &&
      !categoriesHtml.includes('Content categories') &&
      categoriesHtml.includes('Add category') &&
      categoriesHtml.includes('ant-table') &&
      !categoriesHtml.includes('nav.items.videoStreamsCategories'),
    `rendered ${categoriesHtml.length} bytes with the list still being fetched`,
  )

  // The save and discard controls of the unsaved arrangement, drawn while disabled - the
  // first paint of a page whose order on screen is still the order on the server. Matched
  // on the button rather than on the words, which also appear inside a modal or a toast
  // elsewhere; the disabled state is the assertion, because a save button enabled over an
  // arrangement nobody has made would send a request that changes nothing and call it a
  // save.
  const saveOrderLabelAt = categoriesHtml.indexOf('Save order')
  const saveOrderButtonAt = categoriesHtml.lastIndexOf('<button', saveOrderLabelAt)
  const saveOrderButtonTag = categoriesHtml.slice(saveOrderButtonAt, saveOrderLabelAt)
  check(
    'the content categories page offers the order save, greyed until the arrangement differs',
    saveOrderLabelAt !== -1 &&
      categoriesHtml.includes('Discard changes') &&
      saveOrderButtonTag.includes('disabled'),
    `save button tag: ${saveOrderButtonTag.slice(0, 160) || 'NOT FOUND'}`,
  )

  // A second address of the same component, and the one assertion that would catch the
  // section key renamed on the menu side only: a key the page does not know falls back to
  // the video stream kind silently, so only reading what a *different* key renders tells
  // the two apart.
  const seriesCategoriesHtml = renderToStaticMarkup(
    withRouter('/admin/series/categories', withQuery(
      <AntApp>
        <ContentCategoriesPage sectionKey="nav.items.seriesCategories" />
      </AntApp>,
    )),
  )
  check(
    'a different section key renders the same page for its own kind of content',
    seriesCategoriesHtml.includes('Series categories') &&
      !seriesCategoriesHtml.includes('Video stream categories'),
    `rendered ${seriesCategoriesHtml.length} bytes with the series heading`,
  )

  // The four menu entries have to name the page the visitor came for. `sectionRoutes`
  // falls back to `SectionPage` when an item carries no `page`, and a menu key renamed on
  // one side only would leave the page reading the wrong key - which this cannot see in
  // the render above, because the wrong key still renders *a* page. Asserted the way the
  // users-group items below are: each loader has to resolve to this page's own module.
  const categoryModules = await Promise.all(
    ['videoStreamsCategories', 'audioStreamsCategories', 'videoOnDemandCategories', 'seriesCategories'].map(
      async (key) => await flatItems('Admin').find((item) => item.key === key)?.page?.(),
    ),
  )
  const allCategoriesOpen = categoryModules.every(
    (module) => module?.default === ContentCategoriesPage,
  )
  check(
    'the four content sections point their categories items at the real page rather than the placeholder',
    allCategoriesOpen,
    allCategoriesOpen
      ? 'all four open ContentCategoriesPage'
      : `one opened something else: ${categoryModules.map((m) => m?.default?.name ?? 'nothing').join(', ')}`,
  )

  // --- The video streams pages -----------------------------------------------
  //
  // The first content section whose `add` and `manage` entries open something real, and
  // the first table here that is neither the accounts' nor the groups'. The facts below
  // are the ones all three build checks are satisfied by while broken:
  //
  // - the heading is the page's own title (`Video streams`) rather than the menu item's
  //   name, which is the verb `Manage` - a card titled "Manage" over a table would name
  //   neither the page nor its subject;
  // - the toolbar offers the search and all three filters, and the auto-refresh is an
  //   **icon-only** button: the row beside the card title is already the widest toolbar in
  //   the application, and a Russian `Продолжить автообновление` in it pushed the table
  //   sideways for a control one glyph wide. Its whole sentence lives in the aria-label
  //   instead, which is what a keyboard or a screen reader reads - so the assertion is that
  //   the label is *absent as text* and present as the accessible name;
  // - all eight columns keep their own width *in order*, summing to the same 1230 the
  //   other two tables hold beside the sidebar;
  // - every column carries a keyboard-reachable resize handle, and exactly three headers
  //   sort - `type` is a sort field the endpoint honours but this table
  //   draws no column for, and the rest are never fields it orders by.
  const streamsHtml = renderToStaticMarkup(
    withRouter('/admin/video-streams', withQuery(
      <AntApp>
        <VideoStreamsPage sectionKey="nav.items.videoStreamsManage" />
      </AntApp>,
    )),
  )
  check(
    'the streams table draws its own heading, its toolbar and its table before any rows arrive',
    streamsHtml.includes('>Video streams<') &&
      streamsHtml.includes('ant-table') &&
      !streamsHtml.includes('nav.items.videoStreamsManage'),
    `rendered ${streamsHtml.length} bytes with the list still being fetched`,
  )

  check(
    'the streams toolbar offers the search, all three filters, the width reset and the way to add',
    streamsHtml.includes('Search by name') &&
      streamsHtml.includes('Any category') &&
      streamsHtml.includes('Any status') &&
      streamsHtml.includes('Any type') &&
      streamsHtml.includes('aria-label="Reset widths"') &&
      streamsHtml.includes('Add stream'),
    streamsHtml.includes('Add stream') ? `rendered ${streamsHtml.length} bytes` : 'the add button went missing',
  )

  // The selection's control, greyed while nothing is selected for the reason the accounts
  // and groups tables draw theirs that way: a control that appears and disappears reads as
  // a broken one. This page also gates it on `editStreams`, and the probe has no profile
  // answer, so the first paint is the *permission-less* one - the button is drawn and
  // disabled, which is exactly what an operator who may read but not change streams sees.
  const streamsBulkButtons = [...streamsHtml.matchAll(/<button[^>]*>[\s\S]*?<\/button>/g)]
    .map((m) => m[0])
    .filter((tag) => /<span[^>]*>Actions<\/span>/.test(tag))
  check(
    'the streams toolbar draws the bulk menu, greyed while nothing is selected',
    streamsBulkButtons.length === 1 && streamsBulkButtons[0].includes('disabled'),
    `${streamsBulkButtons.length} button(s) labelled "Actions": ${streamsBulkButtons[0]?.slice(0, 180) ?? 'none'}`,
  )

  // The auto-refresh, as an icon carrying its sentence rather than a word in the toolbar.
  // Both halves matter: the state is still readable (through the accessible name, which
  // names the action *and* the interval), and the row is one glyph narrower for it. A
  // button that put the label back would push the table sideways again in Russian.
  //
  // Matched on the *start* of the name and on the interval rather than on the whole
  // sentence: the sentence is copy and will be reworded, while the two facts being pinned
  // are that a name exists at all and that it still says which state it acts on. The last
  // condition is the one that catches the regression this check is written for - the label
  // reappearing as *text*, which is what `>Pause auto-refresh<` looks like in the markup
  // once antd has wrapped a button's children in their own span.
  const refreshNameAt = streamsHtml.indexOf('aria-label="Pause auto-refresh')
  const refreshButtonAt = streamsHtml.lastIndexOf('<button', refreshNameAt)
  const refreshButtonEnd = streamsHtml.indexOf('</button>', refreshNameAt)
  const refreshButtonTag = streamsHtml.slice(refreshButtonAt, refreshButtonEnd)
  check(
    'the auto-refresh is an icon whose accessible name carries the state and the interval',
    refreshNameAt !== -1 &&
      refreshNameAt > refreshButtonAt &&
      streamsHtml.includes('30 seconds') &&
      !streamsHtml.includes('>Pause auto-refresh<'),
    refreshNameAt === -1
      ? 'THE AUTO-REFRESH BUTTON HAS NO ACCESSIBLE NAME'
      : `button: ${streamsHtml.slice(refreshButtonAt, refreshButtonAt + 160)} (${refreshButtonTag.length} bytes to its close)`,
  )

  // The order is a decision: the refresh describes the whole table, while the bulk menu acts
  // on a selection that is empty on first paint, so the refresh leads the toolbar. Pinned here
  // because both controls are present either way and no other checker can see their order.
  const streamsBulkButtonAt = streamsHtml.indexOf(streamsBulkButtons[0] ?? '')
  check(
    'the auto-refresh sits before the bulk menu in the streams toolbar',
    streamsBulkButtonAt !== -1 && refreshButtonAt !== -1 && refreshButtonAt < streamsBulkButtonAt,
    `auto-refresh at ${refreshButtonAt}, bulk menu at ${streamsBulkButtonAt}`,
  )

  const streamsCols = [...streamsHtml.matchAll(/<col\s[^>]*>/g)].map((m) => m[0])
  const streamsWidths = streamsCols.map((col) => /width:(\d+)px/.exec(col)?.[1])
  const streamsTotal = streamsWidths.reduce((sum, width) => sum + (Number(width) || 0), 0)
  // Nine columns: the 40px selection column the bulk toolbar acts through, then the eight
  // this table has always had. The eight add up to 1190 and the checkbox takes them to the
  // same 1230 the accounts and the groups tables reach, so all three fit beside the sidebar
  // on one Full HD screen. `name` is 262 rather than the 170 beside it because it is the one
  // column nothing truncates a second time, and because it is where the room the actions
  // column gave up went; `icon` is 70 because a logo is drawn at a fixed 28px and a column
  // sized by its widest image changes width when a logo is replaced. `status` is 150 because
  // it carries two lines - the state and the kind of stream under it - and the longest of
  // the six kinds in Russian has to fit without being cut. The actions column is the one
  // width that is not chosen: it is what the five buttons of a row need and not a pixel more
  // (`actionsColumnWidth`).
  check(
    'the streams table is fixed-layout with a selection column and its eight deliberate widths, in order',
    /table-layout:\s*fixed/.test(streamsHtml) &&
      streamsWidths.join(',') === '40,100,70,262,170,180,150,90,168' &&
      streamsTotal === 1230,
    `${streamsCols.length} columns: [${streamsWidths.join(', ')}] = ${streamsTotal}px`,
  )

  const streamsHandles = [...streamsHtml.matchAll(/<span[^>]*table-col-resizer[^>]*>/g)].map(
    (m) => m[0],
  )
  check(
    'every streams column carries a keyboard-reachable resize handle, named for resizing',
    streamsHandles.length === 8 &&
      streamsHandles.every(
        (handle) =>
          handle.includes('role="separator"') &&
          handle.includes('tabindex="0"') &&
          handle.includes('aria-label="Resize the '),
      ),
    `${streamsHandles.length} handles: ${
      streamsHandles.map((h) => /aria-label="[^"]*"/.exec(h)?.[0]).join(' ') || 'none'
    }`,
  )

  const streamsHeaders = [...streamsHtml.matchAll(/<th\b[^>]*>/g)].map((m) => m[0])
  check(
    'a sortable streams header keeps its own access, and exactly three of the eight sort',
    streamsHeaders.length === 9 &&
      streamsHeaders.filter((th) => th.includes('aria-description="sortable"')).length === 3 &&
      streamsHeaders.some((th) => th.includes('aria-label="Name"') && th.includes('tabindex="0"')),
    `${streamsHeaders.length} header cells, ${
      streamsHeaders.filter((th) => th.includes('aria-description="sortable"')).length
    } sortable`,
  )

  // The state cell, which no page render reaches: it is drawn by a column's `render`, which
  // runs only once a row exists, and every table render above lands before the first answer
  // arrives. A cell that drew the state and forgot the kind of stream under it - or drew the
  // kind and forgot the state - would look exactly like a healthy one.
  //
  // The order is asserted as well as the two words: the state is the line a visitor scans
  // the column for, and the kind is the quieter line under it, so a cell that put them the
  // other way round would still contain both.
  const streamStateHtml = renderToStaticMarkup(
    <AntApp>
      <StreamStateCell status="Running" type="Permanent" />
    </AntApp>,
  )
  check(
    'the state cell draws the status and the kind of stream under it',
    streamStateHtml.includes('Active') &&
      streamStateHtml.includes('Permanent') &&
      streamStateHtml.indexOf('Active') < streamStateHtml.indexOf('Permanent'),
    `rendered ${streamStateHtml.length} bytes: ${streamStateHtml.slice(0, 220)}`,
  )

  // The add form, which draws at once under its own title - and the shared ordered-list
  // editor with it, which is the control the whole payload half of this form rests on. A
  // form that rendered without it would still compile: `SourceListEditor` is a component
  // behind a conditional on the chosen kind, and the default kind is the one that draws it.
  const streamFormHtml = renderToStaticMarkup(
    withRouter('/admin/video-streams/add', withQuery(
      <AntApp>
        <VideoStreamFormPage sectionKey="nav.items.videoStreamsAdd" />
      </AntApp>,
    )),
  )
  check(
    'the stream add form draws its fields at once, with the ordered source list and without an id field',
    streamFormHtml.includes('Add stream') &&
      streamFormHtml.includes('Create') &&
      streamFormHtml.includes('What it plays') &&
      streamFormHtml.includes('Sources, in relay order') &&
      streamFormHtml.includes('Add a source') &&
      streamFormHtml.includes('Delay (minutes)') &&
      streamFormHtml.includes('Delay and archive') &&
      streamFormHtml.includes('Programme guide') &&
      streamFormHtml.includes('Restreaming') &&
      !streamFormHtml.includes('Edit stream') &&
      // No id field: the identifier does not exist until the row does, and the copy
      // affordance is the one thing on this form that would give it away.
      !streamFormHtml.includes('anticon-copy'),
    `rendered ${streamFormHtml.length} bytes of the add form`,
  )

  // The category is the broader fact - where the stream belongs - so it comes before the
  // kind, which only says what it does once it is there. The two labels are compared by
  // their `title` attributes because that is the markup antd puts on a field label, and a
  // plain text search would match the hint or a placeholder as well.
  const categoryLabelAt = streamFormHtml.indexOf('title="Category"')
  const typeLabelAt = streamFormHtml.indexOf('title="Type"')
  check(
    'the category field sits above the stream type',
    categoryLabelAt !== -1 && typeLabelAt !== -1 && categoryLabelAt < typeLabelAt,
    `category label at ${categoryLabelAt}, type label at ${typeLabelAt}`,
  )

  // The state picker sits above the kind, and the add form opens at Active. That default is
  // the form's rather than the endpoint's: the API keeps `Stopped` for a create that names
  // no status, so a form that sent nothing would create a disabled stream. Rendered as the
  // select's own selection item, which is the only place "Active" appears before the menu
  // is opened.
  const statusLabelAt = streamFormHtml.indexOf('title="Status"')
  check(
    'the stream add form draws the status picker above the type, starting at Active',
    statusLabelAt !== -1 &&
      typeLabelAt !== -1 &&
      statusLabelAt < typeLabelAt &&
      streamFormHtml.includes('>Active<'),
    `status label at ${statusLabelAt}, type label at ${typeLabelAt}`,
  )

  // The import form. The playlist is the first field on it, because it is the one thing the
  // page cannot work without and everything below describes what to do with the streams it
  // names. The create form's payload half must not be here: the name and the source list are
  // exactly what the playlist supplies, so a form that drew either would be asking the
  // operator to type what it is about to read out of the file.
  const streamImportHtml = renderToStaticMarkup(
    withRouter('/admin/video-streams/import', withQuery(
      <AntApp>
        <VideoStreamImportPage sectionKey="nav.items.videoStreamsImport" />
      </AntApp>,
    )),
  )
  const playlistLabelAt = streamImportHtml.indexOf('title="M3U playlist"')
  const importCategoryAt = streamImportHtml.indexOf('title="Category"')
  check(
    'the stream import form opens with the playlist and draws no name or source list',
    streamImportHtml.includes('Import streams') &&
      playlistLabelAt !== -1 &&
      importCategoryAt !== -1 &&
      playlistLabelAt < importCategoryAt &&
      streamImportHtml.includes('title="Type"') &&
      streamImportHtml.includes('Delay and archive') &&
      streamImportHtml.includes('Restreaming') &&
      streamImportHtml.includes('>Import<') &&
      !streamImportHtml.includes('title="Name"') &&
      !streamImportHtml.includes('Sources, in relay order') &&
      !streamImportHtml.includes('Add a source') &&
      !streamImportHtml.includes('What it plays'),
    `playlist label at ${playlistLabelAt}, category label at ${importCategoryAt}, rendered ${streamImportHtml.length} bytes`,
  )

  // The edit address, which waits. `useParams` needs a real route match, so the router is
  // handed the same `:id` route the two forms above use.
  const streamEditHtml = renderToStaticMarkup(
    withRoutes('/admin/video-streams/11111111-1111-1111-1111-111111111111', [
      {
        path: '/admin/video-streams/:id',
        element: withQuery(
          <AntApp>
            <VideoStreamFormPage sectionKey="nav.items.videoStreamsAdd" />
          </AntApp>,
        ),
      },
    ]),
  )
  check(
    'the stream edit address waits for the stream instead of drawing fields against nothing',
    streamEditHtml.includes('Edit stream') &&
      streamEditHtml.includes('ant-spin') &&
      !streamEditHtml.includes('Create'),
    `rendered ${streamEditHtml.length} bytes under the edit title with the spinner up`,
  )

  // --- The audio streams ---------------------------------------------------------
  //
  // The second medium's own pages, asserted over the same facts the video ones are. The
  // audio resource is deliberately a **separate** one - separate tables, separate
  // permissions, separate routes - and the only way to keep two resources from quietly
  // becoming one is to assert the differences rather than to trust them.
  //
  // The state cell first, for the same reason the video one is rendered directly: a
  // column's `render` runs only once a row exists, and every table render below lands
  // before the first answer arrives, so nothing else in this repository would notice the
  // cell rendering nothing at all. The two words are the state and the kind, and the
  // order is pinned - the state is the line a visitor scans for, the kind is the quieter
  // line under it.
  const audioStateHtml = renderToStaticMarkup(
    <AntApp>
      <AudioStreamStateCell status="Running" type="Redirect" />
    </AntApp>,
  )
  check(
    'the audio state cell draws the status and the kind of stream under it',
    audioStateHtml.includes('Active') &&
      audioStateHtml.includes('Redirect') &&
      audioStateHtml.indexOf('Active') < audioStateHtml.indexOf('Redirect'),
    `rendered ${audioStateHtml.length} bytes: ${audioStateHtml.slice(0, 220)}`,
  )

  // The audio table, which is the video table with one column fewer and a shorter bulk
  // menu. The heading is its own rather than the menu item's, for the same reason the
  // video one is: the menu says "Manage audio streams" and the card says "Audio streams",
  // one naming how the visitor got there and one naming what is looked at.
  const audioStreamsHtml = renderToStaticMarkup(
    withRouter('/admin/audio-streams', withQuery(
      <AntApp>
        <AudioStreamsPage sectionKey="nav.items.audioStreamsManage" />
      </AntApp>,
    )),
  )
  check(
    'the audio streams table draws its own heading, its toolbar and its table before any rows arrive',
    audioStreamsHtml.includes('>Audio streams<') &&
      audioStreamsHtml.includes('ant-table') &&
      !audioStreamsHtml.includes('nav.items.audioStreamsManage'),
    `rendered ${audioStreamsHtml.length} bytes with the list still being fetched`,
  )

  check(
    'the audio streams toolbar offers the search, all three filters, the width reset and the way to add',
    audioStreamsHtml.includes('Search by name') &&
      audioStreamsHtml.includes('Any category') &&
      audioStreamsHtml.includes('Any status') &&
      audioStreamsHtml.includes('Any type') &&
      audioStreamsHtml.includes('aria-label="Reset widths"') &&
      audioStreamsHtml.includes('Add stream'),
    audioStreamsHtml.includes('Add stream')
      ? `rendered ${audioStreamsHtml.length} bytes`
      : 'the add button went missing',
  )

  // The bulk menu, greyed while nothing is selected - and greyed for a second reason here:
  // the probe has no profile answer, so the first paint is the *permission-less* one, which
  // is exactly what an operator whose group holds no `editAudioStreams` sees.
  const audioBulkButtons = [...audioStreamsHtml.matchAll(/<button[^>]*>[\s\S]*?<\/button>/g)]
    .map((m) => m[0])
    .filter((tag) => /<span[^>]*>Actions<\/span>/.test(tag))
  check(
    'the audio streams toolbar draws the bulk menu, greyed while nothing is selected',
    audioBulkButtons.length === 1 && audioBulkButtons[0].includes('disabled'),
    `${audioBulkButtons.length} button(s) labelled "Actions": ${
      audioBulkButtons[0]?.slice(0, 180) ?? 'none'
    }`,
  )

  // The auto-refresh, as an icon carrying its sentence rather than a word in the toolbar -
  // the same decision as the video table's, and pinned for the same reason: a button that
  // put the label back would push the table sideways in Russian.
  const audioRefreshNameAt = audioStreamsHtml.indexOf('aria-label="Pause auto-refresh')
  const audioRefreshButtonAt = audioStreamsHtml.lastIndexOf('<button', audioRefreshNameAt)
  check(
    'the audio auto-refresh is an icon whose accessible name carries the state and the interval',
    audioRefreshNameAt !== -1 &&
      audioRefreshNameAt > audioRefreshButtonAt &&
      audioStreamsHtml.includes('30 seconds') &&
      !audioStreamsHtml.includes('>Pause auto-refresh<'),
    audioRefreshNameAt === -1
      ? 'THE AUTO-REFRESH BUTTON HAS NO ACCESSIBLE NAME'
      : `button at ${audioRefreshButtonAt}, name at ${audioRefreshNameAt}`,
  )

  // Eight columns: the 40px selection column the bulk toolbar acts through, then the seven
  // this table has. The seven add up to 1190 and the checkbox takes them to the same 1230
  // the accounts, the groups and the video streams tables reach, so all four fit beside the
  // sidebar on one Full HD screen. `name` is 352 rather than the video table's 262 because
  // the audio table has one column fewer and the room the EPG column gave up went to the
  // one column nothing truncates a second time. The actions column is the one width that is
  // not chosen: it is what the five buttons of a row need and not a pixel more.
  const audioCols = [...audioStreamsHtml.matchAll(/<col\s[^>]*>/g)].map((m) => m[0])
  const audioWidths = audioCols.map((col) => /width:(\d+)px/.exec(col)?.[1])
  const audioTotal = audioWidths.reduce((sum, width) => sum + (Number(width) || 0), 0)
  check(
    'the audio streams table is fixed-layout with a selection column and its seven deliberate widths, in order',
    /table-layout:\s*fixed/.test(audioStreamsHtml) &&
      audioWidths.join(',') === '40,100,70,352,170,180,150,168' &&
      audioTotal === 1230,
    `${audioCols.length} columns: [${audioWidths.join(', ')}] = ${audioTotal}px`,
  )

  const audioHandles = [...audioStreamsHtml.matchAll(/<span[^>]*table-col-resizer[^>]*>/g)].map(
    (m) => m[0],
  )
  check(
    'every audio streams column carries a keyboard-reachable resize handle, named for resizing',
    audioHandles.length === 7 &&
      audioHandles.every(
        (handle) =>
          handle.includes('role="separator"') &&
          handle.includes('tabindex="0"') &&
          handle.includes('aria-label="Resize the '),
      ),
    `${audioHandles.length} handles: ${
      audioHandles.map((h) => /aria-label="[^"]*"/.exec(h)?.[0]).join(' ') || 'none'
    }`,
  )

  const audioHeaders = [...audioStreamsHtml.matchAll(/<th\b[^>]*>/g)].map((m) => m[0])
  check(
    'a sortable audio streams header keeps its own access, and exactly three of the seven sort',
    audioHeaders.length === 8 &&
      audioHeaders.filter((th) => th.includes('aria-description="sortable"')).length === 3 &&
      audioHeaders.some((th) => th.includes('aria-label="Name"') && th.includes('tabindex="0"')),
    `${audioHeaders.length} header cells, ${
      audioHeaders.filter((th) => th.includes('aria-description="sortable"')).length
    } sortable`,
  )

  // The audio form. The differences from the video form are the point of the assertion:
  // an audio stream has no delay, no archive, no programme guide and no reference to a
  // stored item - and a form that drew any of them would be asking the operator for
  // something the server refuses, which is the one kind of wrong a field can be.
  const audioFormHtml = renderToStaticMarkup(
    withRouter('/admin/audio-streams/add', withQuery(
      <AntApp>
        <AudioStreamFormPage sectionKey="nav.items.audioStreamsAdd" />
      </AntApp>,
    )),
  )
  check(
    'the audio stream add form draws its own fields and none of the video-only ones',
    audioFormHtml.includes('Add stream') &&
      audioFormHtml.includes('Create') &&
      audioFormHtml.includes('What it plays') &&
      audioFormHtml.includes('Sources, in relay order') &&
      audioFormHtml.includes('Add a source') &&
      audioFormHtml.includes('Identity') &&
      audioFormHtml.includes('Restreaming') &&
      audioFormHtml.includes('Icon address') &&
      audioFormHtml.includes('Enigma2 service id') &&
      !audioFormHtml.includes('Delay (minutes)') &&
      !audioFormHtml.includes('Delay and archive') &&
      !audioFormHtml.includes('Programme guide') &&
      !audioFormHtml.includes('EPG') &&
      !audioFormHtml.includes('Archive') &&
      !audioFormHtml.includes('Edit stream') &&
      // No id field: the identifier does not exist until the row does, and the copy
      // affordance is the one thing on this form that would give it away.
      !audioFormHtml.includes('anticon-copy'),
    `rendered ${audioFormHtml.length} bytes of the audio add form`,
  )

  // The category is the broader fact and the kind the narrower one, and the state sits
  // between them - the same order as the video form (name, category, state, kind), asserted
  // by the labels' own `title` attributes because that is the markup antd puts on a field
  // label. A plain text search would match the hint or a placeholder as well.
  const audioCategoryLabelAt = audioFormHtml.indexOf('title="Category"')
  const audioTypeLabelAt = audioFormHtml.indexOf('title="Type"')
  const audioStatusLabelAt = audioFormHtml.indexOf('title="Status"')
  check(
    'the audio form draws the category above the type, and the state above the type too',
    audioCategoryLabelAt !== -1 &&
      audioTypeLabelAt !== -1 &&
      audioStatusLabelAt !== -1 &&
      audioCategoryLabelAt < audioTypeLabelAt &&
      audioStatusLabelAt < audioTypeLabelAt &&
      audioFormHtml.includes('>Active<'),
    `status label at ${audioStatusLabelAt}, category label at ${audioCategoryLabelAt}, type label at ${audioTypeLabelAt}`,
  )

  // The two kinds and their sentences. An audio stream has exactly two, and the form opens
  // on the on-demand one - the kind that has a name and an address list, which is what this
  // form exists to collect. The closed picker shows the chosen kind and the hint under it
  // says what the choice means, so those two are what the render can see; the menu itself
  // opens on a click, which no render here performs.
  check(
    'the audio form opens on the on-demand kind and draws its sentence, and none of the four video kinds',
    audioFormHtml.includes('>On demand<') &&
      audioFormHtml.includes('Relays from a list of addresses') &&
      !audioFormHtml.includes('Permanent') &&
      !audioFormHtml.includes('Video on demand') &&
      !audioFormHtml.includes('Series') &&
      !audioFormHtml.includes('File playlist'),
    'the kind picker was checked for the two audio kinds and the absence of the four video ones',
  )

  // The import form. The playlist is the first field on it, for the same reason the video
  // import's is: it is the one thing the page cannot work without. The create form's
  // payload half must not be here - the name and the source list are exactly what the
  // playlist supplies.
  const audioImportHtml = renderToStaticMarkup(
    withRouter('/admin/audio-streams/import', withQuery(
      <AntApp>
        <AudioStreamImportPage sectionKey="nav.items.audioStreamsImport" />
      </AntApp>,
    )),
  )
  const audioPlaylistLabelAt = audioImportHtml.indexOf('title="M3U playlist"')
  const audioImportCategoryAt = audioImportHtml.indexOf('title="Category"')
  check(
    'the audio stream import form opens with the playlist and draws no name or source list',
    audioImportHtml.includes('Import audio streams') &&
      audioPlaylistLabelAt !== -1 &&
      audioImportCategoryAt !== -1 &&
      audioPlaylistLabelAt < audioImportCategoryAt &&
      audioImportHtml.includes('title="Type"') &&
      audioImportHtml.includes('Restreaming') &&
      audioImportHtml.includes('>Import<') &&
      !audioImportHtml.includes('title="Name"') &&
      !audioImportHtml.includes('Sources, in relay order') &&
      !audioImportHtml.includes('Add a source') &&
      !audioImportHtml.includes('What it plays') &&
      !audioImportHtml.includes('Delay and archive') &&
      !audioImportHtml.includes('Permanent'),
    `playlist label at ${audioPlaylistLabelAt}, category label at ${audioImportCategoryAt}, rendered ${audioImportHtml.length} bytes`,
  )

  // The edit address, which waits - the same rule as the video form: a form painted from
  // whatever the previous screen happened to hold would edit the wrong row after a reload.
  const audioEditHtml = renderToStaticMarkup(
    withRoutes('/admin/audio-streams/11111111-1111-1111-1111-111111111111', [
      {
        path: '/admin/audio-streams/:id',
        element: withQuery(
          <AntApp>
            <AudioStreamFormPage sectionKey="nav.items.audioStreamsAdd" />
          </AntApp>,
        ),
      },
    ]),
  )
  check(
    'the audio stream edit address waits for the stream instead of drawing fields against nothing',
    audioEditHtml.includes('Edit stream') &&
      audioEditHtml.includes('ant-spin') &&
      !audioEditHtml.includes('Create'),
    `rendered ${audioEditHtml.length} bytes under the edit title with the spinner up`,
  )

  // The raw-key failure, over the audio form this time. A key missing from **both** locale
  // files is invisible to `tsc` - `en.ts` is what `Translation` is built from, so a key
  // absent from it is absent from the type, and `ru.ts` being typed `: Translation` cannot
  // complain either. A raw key is unmistakable in markup, so the check is a substring.
  const audioNamePlaceholder = /<input[^>]*placeholder="([^"]*)"[^>]*>/.exec(audioFormHtml)?.[1]
  check(
    'the audio stream add form draws translated text rather than raw locale keys',
    !audioFormHtml.includes('audioStreamsForm.') &&
      !audioFormHtml.includes('audioStreams.') &&
      Boolean(audioNamePlaceholder),
    audioNamePlaceholder
      ? `the name field asks for "${audioNamePlaceholder}" in ${audioFormHtml.length} bytes`
      : 'NO PLACEHOLDER ON THE NAME FIELD - a key is being drawn instead',
  )

  // All three stream items have to open the real pages, and the other three sections must
  // still open the placeholder - the rail deliberately offers real pages for this section
  // alone, and a `managePage` added to `series` by accident would be a table of video
  // streams at a series address, which every check above would stay green about.
  const [streamsAddModule, streamsImportModule, streamsManageModule] = await Promise.all([
    flatItems('Admin').find((item) => item.key === 'videoStreamsAdd')?.page?.(),
    flatItems('Admin').find((item) => item.key === 'videoStreamsImport')?.page?.(),
    flatItems('Admin').find((item) => item.key === 'videoStreamsManage')?.page?.(),
  ])
  check(
    'the admin menu points its three stream items at the real pages rather than the placeholder',
    streamsAddModule?.default === VideoStreamFormPage &&
      streamsImportModule?.default === VideoStreamImportPage &&
      streamsManageModule?.default === VideoStreamsPage,
    `videoStreamsAdd opens ${streamsAddModule?.default?.name ?? 'nothing'}, videoStreamsImport opens ${
      streamsImportModule?.default?.name ?? 'nothing'
    }, videoStreamsManage opens ${streamsManageModule?.default?.name ?? 'nothing'}`,
  )

  // The audio section's own three items, and the same question asked of them: do they open
  // the audio pages rather than the video ones? A loader copied from the video section by
  // accident would open a video-streams table at an audio-streams address, and every check
  // above would stay green about it - the rail would look right and the page would be
  // somebody else's.
  const [audioAddModule, audioImportModule, audioManageModule] = await Promise.all([
    flatItems('Admin').find((item) => item.key === 'audioStreamsAdd')?.page?.(),
    flatItems('Admin').find((item) => item.key === 'audioStreamsImport')?.page?.(),
    flatItems('Admin').find((item) => item.key === 'audioStreamsManage')?.page?.(),
  ])
  check(
    'the admin menu points its three audio stream items at the audio pages rather than the placeholder',
    audioAddModule?.default === AudioStreamFormPage &&
      audioImportModule?.default === AudioStreamImportPage &&
      audioManageModule?.default === AudioStreamsPage,
    `audioStreamsAdd opens ${audioAddModule?.default?.name ?? 'nothing'}, audioStreamsImport opens ${
      audioImportModule?.default?.name ?? 'nothing'
    }, audioStreamsManage opens ${audioManageModule?.default?.name ?? 'nothing'}`,
  )

  // Import is the other way to add streams, so it sits immediately after the add form. A
  // rail that moved it below the table would describe it as a thing to look at rather than
  // a thing to do. The two sections that have an importer carry it in the same place; the
  // other two must not grow an import item at all: their playlists have no importer, and
  // one generated from the table would open a placeholder where these have a real page.
  const streamItemKeys = flatItems('Admin')
    .filter((item) => item.key.startsWith('videoStreams'))
    .map((item) => item.key)
    .join(',')
  const audioStreamItemKeys = flatItems('Admin')
    .filter((item) => item.key.startsWith('audioStreams'))
    .map((item) => item.key)
    .join(',')
  const vodItemKeys = flatItems('Admin')
    .filter((item) => item.key.startsWith('videoOnDemand'))
    .map((item) => item.key)
    .join(',')
  const otherSectionsHaveNoImport = ['series'].every(
    (sectionKey) => !flatItems('Admin').some((item) => item.key === `${sectionKey}Import`),
  )
  check(
    'the playlist import sits immediately after the add form in every built content section and nowhere else',
    streamItemKeys ===
      'videoStreamsAdd,videoStreamsImport,videoStreamsManage,videoStreamsCategories' &&
      audioStreamItemKeys ===
        'audioStreamsAdd,audioStreamsImport,audioStreamsManage,audioStreamsCategories' &&
      vodItemKeys ===
        'videoOnDemandAdd,videoOnDemandImport,videoOnDemandManage,videoOnDemandCategories' &&
      otherSectionsHaveNoImport,
    `${streamItemKeys}; ${audioStreamItemKeys}; ${vodItemKeys}; other sections import: ${
      otherSectionsHaveNoImport ? 'no' : 'YES'
    }`,
  )

  // The series section is now built: its add form and table have to resolve to the series
  // pages rather than to the placeholder. Asserted the way the other three sections are,
  // because a loader copied from the on-demand section would open a video table at a series
  // address and every check above would stay green about it.
  const [seriesAddModule, seriesManageModule] = await Promise.all([
    flatItems('Admin').find((item) => item.key === 'seriesAdd')?.page?.(),
    flatItems('Admin').find((item) => item.key === 'seriesManage')?.page?.(),
  ])
  check(
    'the series section opens its own add form and table rather than the placeholder',
    seriesAddModule?.default === SeriesFormPage &&
      seriesManageModule?.default === SeriesPage,
    `seriesAdd opens ${seriesAddModule?.default?.name ?? 'nothing'}, seriesManage opens ${
      seriesManageModule?.default?.name ?? 'nothing'
    }`,
  )

  // --- The programme guide sources -------------------------------------------
  //
  // The section the owner asked for: two menu entries under one heading, a table with no
  // selection column, a form of three fields and a read-only channels page reached only from
  // a row. The "Reload EPG" button is a deliberate stub, so what is asserted is that it is
  // drawn *and disabled* rather than missing - a button that vanished would look like the
  // feature had been dropped.
  const epgSourcesHtml = renderToStaticMarkup(
    withRouter('/admin/epg-sources', withQuery(
      <AntApp>
        <EpgSourcesPage sectionKey="nav.items.epgManage" />
      </AntApp>,
    )),
  )
  check(
    'the guide source table draws its own heading and its table before any rows arrive',
    epgSourcesHtml.includes('>Guide sources<') &&
      epgSourcesHtml.includes('ant-table') &&
      !epgSourcesHtml.includes('nav.items.epgManage'),
    `rendered ${epgSourcesHtml.length} bytes with the sources still being fetched`,
  )

  check(
    'the guide source table offers its columns, the search and the disabled reload stub',
    epgSourcesHtml.includes('>Days to keep<') &&
      epgSourcesHtml.includes('>Last refreshed<') &&
      epgSourcesHtml.includes('Search by name or address') &&
      epgSourcesHtml.includes('Reload EPG') &&
      epgSourcesHtml.includes('disabled'),
    `rendered ${epgSourcesHtml.length} bytes`,
  )

  check(
    'the guide source table carries no selection column, because it has no bulk actions',
    !epgSourcesHtml.includes('ant-table-selection-column'),
    `rendered ${epgSourcesHtml.length} bytes`,
  )

  const epgSourceFormHtml = renderToStaticMarkup(
    withRouter('/admin/epg-sources/add', withQuery(
      <AntApp>
        <EpgSourceFormPage sectionKey="nav.items.epgAdd" />
      </AntApp>,
    )),
  )
  check(
    'the guide source form asks for the three fields an operator owns and nothing else',
    epgSourceFormHtml.includes('Add source') &&
      epgSourceFormHtml.includes('Address') &&
      epgSourceFormHtml.includes('Days to keep') &&
      !epgSourceFormHtml.includes('Raw data') &&
      !epgSourceFormHtml.includes('epgSourcesForm.'),
    `rendered ${epgSourceFormHtml.length} bytes`,
  )

  const epgSourceChannelsHtml = renderToStaticMarkup(
    withRouter('/admin/epg-sources/00000000-0000-0000-0000-000000000001/channels', withQuery(
      <AntApp>
        <EpgSourceChannelsPage sectionKey="nav.items.epgManage" />
      </AntApp>,
    )),
  )
  check(
    'the channels page is a read-only table of the stream and its guide key',
    epgSourceChannelsHtml.includes('Channels of the guide source') &&
      epgSourceChannelsHtml.includes('>Stream<') &&
      epgSourceChannelsHtml.includes('>Channel key<') &&
      !epgSourceChannelsHtml.includes('ant-table-selection-column') &&
      !epgSourceChannelsHtml.includes('epgSourceChannels.'),
    `rendered ${epgSourceChannelsHtml.length} bytes`,
  )

  // --- The three access-control blacklists -----------------------------------
  //
  // One component behind three menu entries, so the thing worth asserting is that each of the
  // three renderings is *its own*: its heading, the heading of its value column, and the `type`
  // the section key maps to. A key renamed on the menu side only falls back to the IP list
  // silently, which is exactly the drift this section exists to catch.
  const renderBlacklist = (sectionKey, path) =>
    renderToStaticMarkup(
      withRouter(path, withQuery(
        <AntApp>
          <BlacklistEntriesPage sectionKey={sectionKey} />
        </AntApp>,
      )),
    )

  const blacklistIpHtml = renderBlacklist('nav.items.blacklistIp', '/admin/blacklists/ip')
  const blacklistIspHtml = renderBlacklist('nav.items.blacklistIsp', '/admin/blacklists/isp')
  const blacklistUaHtml = renderBlacklist('nav.items.blacklistUa', '/admin/blacklists/ua')

  check(
    'the IP blacklist draws its own heading and the IP heading over the value column',
    blacklistIpHtml.includes('>IP blacklist<') &&
      blacklistIpHtml.includes('>IP address<') &&
      !blacklistIpHtml.includes('>ISP blacklist<') &&
      !blacklistIpHtml.includes('>Provider<') &&
      !blacklistIpHtml.includes('nav.items.blacklistIp'),
    `rendered ${blacklistIpHtml.length} bytes with the entries still being fetched`,
  )

  check(
    'the ISP blacklist draws its own heading and the provider heading over the value column',
    blacklistIspHtml.includes('>ISP blacklist<') &&
      blacklistIspHtml.includes('>Provider<') &&
      !blacklistIspHtml.includes('>IP address<') &&
      !blacklistIspHtml.includes('nav.items.blacklistIsp'),
    `rendered ${blacklistIspHtml.length} bytes with the entries still being fetched`,
  )

  check(
    'the user-agent blacklist draws its own heading and the agent heading over the value column',
    blacklistUaHtml.includes('>User-agent blacklist<') &&
      blacklistUaHtml.includes('>User agent<') &&
      !blacklistUaHtml.includes('>IP address<') &&
      !blacklistUaHtml.includes('nav.items.blacklistUa'),
    `rendered ${blacklistUaHtml.length} bytes with the entries still being fetched`,
  )

  // The six columns, in order, at the widths the page declares - and no seventh: a selection
  // column would double the table's width in a way typecheck cannot see.
  const blacklistCols = [...blacklistIpHtml.matchAll(/<col\s[^>]*>/g)].map((m) => m[0])
  const blacklistWidths = blacklistCols.map((col) => /width:(\d+)px/.exec(col)?.[1])
  check(
    'the blacklist table draws its six columns at 110/340/160/180/260 and the actions width',
    blacklistCols.length === 6 &&
      blacklistWidths.slice(0, 5).join(',') === '110,340,160,180,260' &&
      blacklistWidths[5] === '84',
    `${blacklistCols.length} columns at ${blacklistWidths.join('/')}`,
  )

  check(
    'the blacklist table carries no selection column, because it has no bulk actions',
    !blacklistIpHtml.includes('ant-table-selection-column'),
    `rendered ${blacklistIpHtml.length} bytes`,
  )

  check(
    'the blacklist table offers its search, its width reset, the clear-all command and the add button',
    blacklistIpHtml.includes('Search by value or note') &&
      blacklistIpHtml.includes('Reset widths') &&
      blacklistIpHtml.includes('Clear all') &&
      blacklistIpHtml.includes('Add entry') &&
      !/\bblacklists\.[a-zA-Z]/.test(blacklistIpHtml),
    `rendered ${blacklistIpHtml.length} bytes`,
  )

  // The toolbar order the owner asked for: search, width reset, clear-all, add. Asserted by the
  // index of the text rather than by the markup around it, because antd renders the `extra`
  // through its own wrapper and a structural check would pin antd's nesting instead of this
  // page's order. `Clear all` must sit strictly left of `Add entry`.
  const blacklistToolbar = [
    ['search', blacklistIpHtml.indexOf('Search by value or note')],
    ['width reset', blacklistIpHtml.indexOf('Reset widths')],
    ['clear all', blacklistIpHtml.indexOf('Clear all')],
    ['add', blacklistIpHtml.indexOf('Add entry')],
  ]
  const blacklistToolbarOrdered = blacklistToolbar.every(
    ([, position], index) => position >= 0 && (index === 0 || position > blacklistToolbar[index - 1][1]),
  )
  check(
    'the clear-all button sits between the width reset and the add button',
    blacklistToolbarOrdered,
    blacklistToolbar.map(([name, position]) => `${name}@${position}`).join(', '),
  )

  // The dialog is portalled, so no static render of the page reaches it. The two fields are a
  // component of their own for that reason, and drawing them here is what asserts the dialog
  // asks for the value and the note and nothing else - no type picker, no counter, no date.
  const blacklistFieldsHtml = renderToStaticMarkup(
    <AntApp>
      <Form>
        <BlacklistEntryFields type="user-agent" />
      </Form>
    </AntApp>,
  )
  const blacklistFieldLabels = [...blacklistFieldsHtml.matchAll(/<label[^>]*>([^<]*)</g)]
    .map((match) => match[1])
  check(
    'the add/edit dialog carries the value and the note fields only',
    blacklistFieldLabels.length === 2 &&
      blacklistFieldLabels[0] === 'User agent' &&
      blacklistFieldLabels[1] === 'Note' &&
      !blacklistFieldsHtml.includes('ISP') &&
      !blacklistFieldsHtml.includes('Blocked') &&
      !blacklistFieldsHtml.includes('blacklistsForm.'),
    `${blacklistFieldLabels.length} fields: ${blacklistFieldLabels.join(', ')}`,
  )

  // The hint rule: the IP value input carries one, and nothing else on any of the three lists
  // does. Rendered through the component rather than the dialog for the reason above, and
  // asserted per type rather than by counting - a hint that leaked onto the note field would
  // pass a count of "one" on the ISP list while breaking the rule the spec states.
  const blacklistHintOf = (type) =>
    renderToStaticMarkup(
      <AntApp>
        <Form>
          <BlacklistEntryFields type={type} />
        </Form>
      </AntApp>,
    )
  const blacklistIpFieldsHtml = blacklistHintOf('ip')
  const blacklistIspFieldsHtml = blacklistHintOf('isp')
  const ipHintText = 'A single IPv4 or IPv6 address'

  check(
    'the IP dialog shows its value hint, and only there',
    blacklistIpFieldsHtml.includes(ipHintText) &&
      blacklistIpFieldsHtml.split(ipHintText).length - 1 === 1,
    `${blacklistIpFieldsHtml.split(ipHintText).length - 1} occurrence(s) in ${blacklistIpFieldsHtml.length} bytes`,
  )

  check(
    'the ISP and user-agent dialogs carry no hint under any field',
    !blacklistIspFieldsHtml.includes(ipHintText) &&
      !blacklistFieldsHtml.includes(ipHintText) &&
      // antd draws an `extra` as `ant-form-item-extra`; none of the three ISP/UA fields has one.
      !blacklistIspFieldsHtml.includes('ant-form-item-extra') &&
      !blacklistFieldsHtml.includes('ant-form-item-extra'),
    `ISP extra blocks: ${blacklistIspFieldsHtml.includes('ant-form-item-extra')}, UA extra blocks: ${blacklistFieldsHtml.includes('ant-form-item-extra')}`,
  )

  check(
    'the note field carries no hint on any list',
    // The note's own `extra` would appear after the note label; the guard is the count of the
    // wrapper class across the IP render, which may only hold the value field's one.
    blacklistIpFieldsHtml.split('ant-form-item-extra').length - 1 === 1,
    `${blacklistIpFieldsHtml.split('ant-form-item-extra').length - 1} extra block(s) on the IP dialog`,
  )

  check(
    'the value placeholders stay on all three lists',
    blacklistIpFieldsHtml.includes('203.0.113.5') &&
      blacklistIspFieldsHtml.includes('Provider name') &&
      blacklistFieldsHtml.includes('curl/8.0'),
    `IP/ISP/UA placeholders present: ${blacklistIpFieldsHtml.includes('203.0.113.5')}/${
      blacklistIspFieldsHtml.includes('Provider name')
    }/${blacklistFieldsHtml.includes('curl/8.0')}`,
  )

  const adminGroupKeysForBlacklists = NAVIGATION.Admin.map((group) => group.key)
  const blacklistGroup = NAVIGATION.Admin.find((group) => group.key === 'blacklists')
  const blacklistItemKeys = blacklistGroup?.items.map((item) => item.key) ?? []
  check(
    'the blacklists group sits directly under the programme guide, with its four items in order',
    adminGroupKeysForBlacklists.indexOf('blacklists') ===
      adminGroupKeysForBlacklists.indexOf('epg') + 1 &&
      blacklistItemKeys.join(',') ===
        'blacklistIp,blacklistIsp,blacklistUa,apiRequestFailures',
    `the rail reads ${adminGroupKeysForBlacklists.join(', ')}; the group reads ${blacklistItemKeys.join(', ')}`,
  )

  check(
    'each of the four blacklists-group entries opens the page, and only the three lists nest under the group',
    blacklistGroup?.items.every((item) => item.page !== undefined) === true &&
      blacklistGroup?.items
        .filter((item) => item.key !== 'apiRequestFailures')
        .every((item) => item.path.startsWith('blacklists/')) === true &&
      // The refused-request log mirrors `admin/api-request-failures` on the API rather than
      // nesting under the group's prefix - the credit-logs/tariff-plans treatment. The heading
      // is still «Чёрные списки»: the group is a rail grouping, not a path segment.
      blacklistGroup?.items.find((item) => item.key === 'apiRequestFailures')?.path ===
        'api-request-failures',
    `paths: ${blacklistGroup?.items.map((item) => item.path).join(', ') ?? 'none'}`,
  )

  // --- The subscriptions section and its tariff plans -------------------------
  //
  // The section the owner asked for: five entries under one heading, four of them
  // placeholders, and the price list as the only real screen so far. What is asserted here is
  // that the rail shows the whole shape of it (a placeholder that vanished would read as an
  // entry the owner never asked for), that the tariff table carries no selection column and no
  // drag, and that the form draws every setting the domain states - including the two relations
  // whose pickers are the reason it is one form rather than three screens.
  const subscriptionsGroup = NAVIGATION.Admin.find((group) => group.key === 'subscriptions')
  const subscriptionItemKeys = subscriptionsGroup?.items.map((item) => item.key) ?? []
  const adminGroupKeys = NAVIGATION.Admin.map((group) => group.key)
  check(
    'the subscriptions section sits directly under the accounts section the owner named',
    adminGroupKeys.indexOf('subscriptions') === adminGroupKeys.indexOf('users') + 1,
    `the rail reads ${adminGroupKeys.join(', ')}`,
  )

  check(
    'the subscriptions section carries the add form, the management list, the price list and the three log entries',
    subscriptionItemKeys.join(',') ===
      'subscriptionsAdd,subscriptionsManage,tariffPlans,liveConnections,subscriptionActivity,magEvents',
    `the section reads ${subscriptionItemKeys.join(', ')}`,
  )

  check(
    'the three placeholder entries are gone and every remaining entry opens a page',
    !subscriptionItemKeys.includes('subscriptionsPlaylists') &&
      !subscriptionItemKeys.includes('subscriptionsMagDevices') &&
      !subscriptionItemKeys.includes('subscriptionsEnigma2Devices') &&
      subscriptionsGroup?.items.every((item) => item.page !== undefined) === true,
    `pages behind the section: ${
      subscriptionsGroup?.items
        .filter((item) => item.page !== undefined)
        .map((item) => item.key)
        .join(', ') ?? 'none'
    }`,
  )

  check(
    'the management list lives at subscriptions and the add form beside it',
    menuPath('Admin', 'subscriptions') === '/admin/subscriptions' &&
      menuPath('Admin', 'subscriptions/add') === '/admin/subscriptions/add',
    `the entries open ${menuPath('Admin', 'subscriptions')} and ${menuPath('Admin', 'subscriptions/add')}`,
  )

  check(
    'the tariff plans entry lives at the address the backend serves, not under subscriptions',
    // The owner's call: the page's address mirrors `admin/tariff-plans` on the API, so one
    // resource is named the same way by the menu, the page and the endpoint. The menu heading
    // is still "Subscriptions" - the group is a rail grouping, not a path segment.
    menuPath('Admin', 'tariff-plans') === '/admin/tariff-plans',
    `the entry opens ${menuPath('Admin', 'tariff-plans')}`,
  )

  const tariffPlansHtml = renderToStaticMarkup(
    withRouter('/admin/tariff-plans', withQuery(
      <AntApp>
        <TariffPlansPage sectionKey="nav.items.tariffPlans" />
      </AntApp>,
    )),
  )
  check(
    'the tariff plan table draws its own heading and its table before any rows arrive',
    tariffPlansHtml.includes('>Tariff plans<') &&
      tariffPlansHtml.includes('ant-table') &&
      !tariffPlansHtml.includes('nav.items.tariffPlans'),
    `rendered ${tariffPlansHtml.length} bytes with the plans still being fetched`,
  )

  check(
    'the tariff plan table offers its columns and the search, and no selection column',
    // The set the owner settled on: no trial column (the tag rides beside the name), one
    // combined services column, and the two count columns. Asserted by heading rather than by
    // width, because a heading renamed on one side only is invisible to typecheck.
    tariffPlansHtml.includes('>Cost<') &&
      tariffPlansHtml.includes('>Validity<') &&
      tariffPlansHtml.includes('>Services and formats<') &&
      tariffPlansHtml.includes('>Max connections<') &&
      tariffPlansHtml.includes('>Bouquets<') &&
      !tariffPlansHtml.includes('>Delivery<') &&
      !tariffPlansHtml.includes('>Formats<') &&
      !tariffPlansHtml.includes('>Trial<') &&
      tariffPlansHtml.includes('Search by name') &&
      !tariffPlansHtml.includes('ant-table-selection-column') &&
      !tariffPlansHtml.includes('ant-table-row-drag'),
    `rendered ${tariffPlansHtml.length} bytes`,
  )

  check(
    'the tariff plan table carries the page-card class the compact rules key on',
    tariffPlansHtml.includes('page-card'),
    `rendered ${tariffPlansHtml.length} bytes`,
  )

  const tariffPlanFormHtml = renderToStaticMarkup(
    withRouter('/admin/tariff-plans/add', withQuery(
      <AntApp>
        <TariffPlanFormPage sectionKey="nav.items.tariffPlans" />
      </AntApp>,
    )),
  )
  check(
    'the tariff plan form draws every setting, both relations and no raw key',
    // The headings the owner settled on: one field per row, no heading over the checkboxes, and
    // the relations split into access and contents.
    tariffPlanFormHtml.includes('Add tariff plan') &&
      tariffPlanFormHtml.includes('Trial plan') &&
      tariffPlanFormHtml.includes('Cost') &&
      tariffPlanFormHtml.includes('Validity') &&
      tariffPlanFormHtml.includes('Services') &&
      tariffPlanFormHtml.includes('Allowed video stream formats') &&
      tariffPlanFormHtml.includes('Limits') &&
      tariffPlanFormHtml.includes('Access to the plan') &&
      tariffPlanFormHtml.includes('Plan contents') &&
      // The boxes, each named once, with no heading repeating the name over it.
      tariffPlanFormHtml.includes('M3U playlist') &&
      tariffPlanFormHtml.includes('MAG') &&
      tariffPlanFormHtml.includes('Enigma2') &&
      tariffPlanFormHtml.includes('MPEG-TS') &&
      tariffPlanFormHtml.includes('HLS') &&
      tariffPlanFormHtml.includes('Enforce the STB device id strictly') &&
      tariffPlanFormHtml.includes('Allow restreaming') &&
      tariffPlanFormHtml.includes('Allow connections from other countries') &&
      tariffPlanFormHtml.includes('Maximum connections') &&
      tariffPlanFormHtml.includes('User groups with access') &&
      tariffPlanFormHtml.includes('Bouquets in the plan') &&
      // The two selection helpers each picker carries.
      tariffPlanFormHtml.includes('Select all') &&
      tariffPlanFormHtml.includes('Clear') &&
      !tariffPlanFormHtml.includes('tariffPlansForm.'),
    `rendered ${tariffPlanFormHtml.length} bytes`,
  )

  check(
    'the strict STB switch is drawn disabled while MAG support is off in the new plan',
    // `CREATE_VALUES` opens with MAG on, so the switch is enabled on the add address and the
    // reason it *can* be disabled is the watched flag rather than a constant - the sentence is
    // asserted instead of the attribute, because the attribute is a fact about the defaults.
    tariffPlanFormHtml.includes('Only a MAG device carries an STB identifier'),
    `rendered ${tariffPlanFormHtml.length} bytes`,
  )

  const [tariffPlanAddModule, tariffPlanEditModule] = await Promise.all([
    flatItems('Admin').find((item) => item.key === 'tariffPlans')?.page?.(),
    import('../src/pages/admin/TariffPlanFormPage'),
  ])
  check(
    'the tariff plans entry opens its table, and the form is a module of its own',
    tariffPlanAddModule?.default === TariffPlansPage &&
      tariffPlanEditModule?.default === TariffPlanFormPage,
    `the entry opens ${tariffPlanAddModule?.default?.name ?? 'nothing'}, the form imports ${
      tariffPlanEditModule?.default?.name ?? 'nothing'
    }`,
  )

  const epgItemKeys = flatItems('Admin')
    .filter((item) => item.key.startsWith('epg'))
    .map((item) => item.key)
    .join(',')
  const [epgAddModule, epgManageModule] = await Promise.all([
    flatItems('Admin').find((item) => item.key === 'epgAdd')?.page?.(),
    flatItems('Admin').find((item) => item.key === 'epgManage')?.page?.(),
  ])
  check(
    'the programme guide section has two entries, in that order, and each opens its own page',
    epgItemKeys === 'epgAdd,epgManage' &&
      epgAddModule?.default === EpgSourceFormPage &&
      epgManageModule?.default === EpgSourcesPage,
    `${epgItemKeys}; epgAdd opens ${epgAddModule?.default?.name ?? 'nothing'}, epgManage opens ${
      epgManageModule?.default?.name ?? 'nothing'
    }`,
  )

  // The bouquets section: one entry under its own heading, a table whose rows carry the
  // counts the delete rule reads, and a content manager behind a row. The manager is the
  // screen that carries the domain's one order rule - what the operator arranges is what is
  // saved - so what is asserted here is that both screens draw before any answer arrives and
  // that neither leaks a raw locale key.
  const bouquetsHtml = renderToStaticMarkup(
    withRouter('/admin/bouquets', withQuery(
      <AntApp>
        <BouquetsPage sectionKey="nav.items.bouquetsManage" />
      </AntApp>,
    )),
  )
  check(
    'the bouquets table draws its own heading, its toolbar and its table before any rows arrive',
    bouquetsHtml.includes('>Bouquets<') &&
      bouquetsHtml.includes('>Add bouquet<') &&
      bouquetsHtml.includes('Save order') &&
      bouquetsHtml.includes('Discard changes') &&
      bouquetsHtml.includes('ant-table') &&
      !bouquetsHtml.includes('nav.items.bouquetsManage') &&
      !/\bbouquets\.[a-zA-Z]/.test(bouquetsHtml),
    `rendered ${bouquetsHtml.length} bytes with the bouquets still being fetched`,
  )

  check(
    'the bouquets table offers the four content counts, so its delete button can tell the truth',
    bouquetsHtml.includes('>Video streams<') &&
      bouquetsHtml.includes('>Audio streams<') &&
      bouquetsHtml.includes('>On demand<') &&
      bouquetsHtml.includes('>Series<'),
    `rendered ${bouquetsHtml.length} bytes`,
  )

  const bouquetContentHtml = renderToStaticMarkup(
    withRouter('/admin/bouquets/00000000-0000-0000-0000-000000000001/content', withQuery(
      <AntApp>
        <BouquetContentPage sectionKey="nav.items.bouquetsManage" />
      </AntApp>,
    )),
  )
  check(
    'the bouquet content manager draws its four tabs and its save pair before any answer arrives',
    bouquetContentHtml.includes('Video streams') &&
      bouquetContentHtml.includes('Audio streams') &&
      bouquetContentHtml.includes('>On demand<') &&
      bouquetContentHtml.includes('>Series<') &&
      bouquetContentHtml.includes('Back to the list') &&
      bouquetContentHtml.includes('Save') &&
      !/\bbouquets\.[a-zA-Z]/.test(bouquetContentHtml),
    `rendered ${bouquetContentHtml.length} bytes with the content still being fetched`,
  )

  const bouquetsItemKeys = flatItems('Admin')
    .filter((item) => item.key.startsWith('bouquets'))
    .map((item) => item.key)
    .join(',')
  const bouquetsModule = await flatItems('Admin')
    .find((item) => item.key === 'bouquetsManage')
    ?.page?.()
  check(
    'the bouquets section has one entry, and it opens the real page',
    bouquetsItemKeys === 'bouquetsManage' && bouquetsModule?.default === BouquetsPage,
    `${bouquetsItemKeys}; bouquetsManage opens ${bouquetsModule?.default?.name ?? 'nothing'}`,
  )

  // The failure this whole script exists for, reached from the other end: a key missing from
  // **both** locale files is invisible to `tsc`, because `en.ts` is what `Translation` is
  // built from - a key absent from it is absent from the type, so `ru.ts` being typed
  // `: Translation` cannot complain either. `t()` keys are deliberately untyped, so nothing
  // else checks them; lint does not, the build does not. `videoStreamsForm.namePlaceholder`
  // shipped exactly that way and the form drew the raw key in its input.
  //
  // `scripts/i18n-audit.mjs` walks every literal key in the source and is the thorough
  // answer; this is the same fact asserted where it actually hurts, over the one render
  // that carries the most of them. A raw key is unmistakable in markup - it arrives as the
  // untranslated text - so the check is a substring rather than a pattern. The second
  // condition is deliberately about *a* placeholder rather than this form's wording: the
  // copy is free to change, and a check that pinned it would fail on an edit nobody meant.
  const namePlaceholder = /<input[^>]*placeholder="([^"]*)"[^>]*>/
    .exec(streamFormHtml)?.[1]
  check(
    'the stream add form draws translated text rather than raw locale keys',
    !streamFormHtml.includes('videoStreamsForm.') &&
      !streamFormHtml.includes('videoStreams.') &&
      Boolean(namePlaceholder),
    namePlaceholder
      ? `the name field asks for "${namePlaceholder}" in ${streamFormHtml.length} bytes`
      : 'NO PLACEHOLDER ON THE NAME FIELD - a key is being drawn instead',
  )

  // --- The administrator's visit history --------------------------------------
  //
  // The account's own history answered over every account the operator may read, so it gets
  // the same three assertions that one does - a heading of its own, a table drawn before any
  // row arrives, five widths that keep it the same size as the two tables beside it in the
  // rail - plus the two this page adds: the account filter, which is read out of the address
  // on the way in, and the account column, which is the whole difference between the two.
  const adminSessionsHtml = renderToStaticMarkup(
    withRouter('/admin/user-sessions', withQuery(
      <AntApp>
        <AdminSessionsPage sectionKey="nav.items.adminSessions" />
      </AntApp>,
    )),
  )
  check(
    'the installation visit history draws its own heading and its table before any rows arrive',
    adminSessionsHtml.includes('>Visit history<') &&
      adminSessionsHtml.includes('ant-table') &&
      !adminSessionsHtml.includes('User sessions') &&
      !adminSessionsHtml.includes('nav.items.adminSessions'),
    `rendered ${adminSessionsHtml.length} bytes with the visits still being fetched`,
  )

  check(
    'the visit-history toolbar offers the search, the account filter and the width reset',
    adminSessionsHtml.includes('Search by IP address or user agent') &&
      adminSessionsHtml.includes('Any user') &&
      adminSessionsHtml.includes('aria-label="Reset widths"'),
    `rendered ${adminSessionsHtml.length} bytes`,
  )

  const adminSessionsCols = [...adminSessionsHtml.matchAll(/<col\s[^>]*>/g)].map((m) => m[0])
  const adminSessionsWidths = adminSessionsCols.map((col) => /width:(\d+)px/.exec(col)?.[1])
  const adminSessionsTotal = adminSessionsWidths.reduce(
    (sum, width) => sum + (Number(width) || 0),
    0,
  )
  // Five columns and no selection column in front of them - there is nothing here to
  // select - so the five alone are the 1230 that the accounts table and the group table
  // reach only with their checkbox. All three then fit the same space beside the sidebar.
  //
  // Five on the round-3 correction, where there were seven: ip, country and network became one
  // cell drawn by the same component the connection logs use, and its room went to that cell and
  // to the user agent.
  check(
    'the visit-history table is fixed-layout with its five deliberate widths, in order',
    /table-layout:\s*fixed/.test(adminSessionsHtml) &&
      adminSessionsWidths.join(',') === '170,250,340,350,120' &&
      adminSessionsTotal === 1230,
    `${adminSessionsCols.length} columns: [${adminSessionsWidths.join(', ')}] = ${adminSessionsTotal}px`,
  )

  check(
    'the installation visit history draws one network column instead of separate IP, Country and Network ones',
    adminSessionsHtml.includes('>Network<') &&
      adminSessionsHtml.includes('>User agent<') &&
      !adminSessionsHtml.includes('>IP address<') &&
      !adminSessionsHtml.includes('>Country<'),
    `rendered ${adminSessionsHtml.length} bytes`,
  )

  const adminSessionsHandles = [
    ...adminSessionsHtml.matchAll(/<span[^>]*table-col-resizer[^>]*>/g),
  ].map((m) => m[0])
  check(
    'every visit-history column carries a keyboard-reachable resize handle, named for resizing',
    adminSessionsHandles.length === 5 &&
      adminSessionsHandles.every(
        (handle) =>
          handle.includes('role="separator"') &&
          handle.includes('tabindex="0"') &&
          handle.includes('aria-label="Resize the '),
      ),
    `${adminSessionsHandles.length} handles: ${
      adminSessionsHandles.map((h) => /aria-label="[^"]*"/.exec(h)?.[0]).join(' ') || 'none'
    }`,
  )

  // Only "When" and the network cell carry a sorter, for the same reason the account's own
  // history offers those two and no more: they are the stored columns - the network cell because
  // its first line is the address. The other geo fields are resolved from GeoIP while a page is
  // built and the account column is a join, so there is nothing in the row for the endpoint to
  // order by.
  const adminSessionsHeaders = [...adminSessionsHtml.matchAll(/<th\b[^>]*>/g)].map((m) => m[0])
  check(
    'a sortable visit-history header keeps its own access to sorting',
    adminSessionsHeaders.length === 5 &&
      adminSessionsHeaders.some(
        (th) => th.includes('aria-label="Network"') && th.includes('tabindex="0"'),
      ),
    `${adminSessionsHeaders.length} header cells`,
  )

  // The account filter lives in the address, the way the accounts table's group filter does -
  // and this is the page the "Last visit" link in that table lands on, so a filter kept in
  // this component's state instead would be gone by the time it mounted and the link would
  // open an unfiltered list, looking as if it had done nothing.
  const adminSessionsArrivalHtml = renderToStaticMarkup(
    withRouter('/admin/user-sessions?userId=44444444-4444-4444-4444-444444444444', withQuery(
      <AntApp>
        <AdminSessionsPage sectionKey="nav.items.adminSessions" />
      </AntApp>,
    )),
  )
  check(
    'the visit-history account filter picks its user up from the address it was opened at',
    adminSessionsArrivalHtml.includes('44444444-4444-4444-4444-444444444444'),
    adminSessionsArrivalHtml.includes('44444444-4444-4444-4444-444444444444')
      ? `the arriving account is on the filter of ${adminSessionsArrivalHtml.length} bytes`
      : 'THE ARRIVING ACCOUNT WAS DROPPED - the link would open an unfiltered list',
  )

  // The compact stylesheet keys every title-row and stacked-toolbar rule on `.page-card`,
  // because those rules must not reach into a card that has no toolbar (the profile cards,
  // the session cards, the auth card). A page that forgets the class keeps the desktop
  // header on a phone, where the toolbar squeezes the title to width 0 - and nothing in
  // `typecheck`, `lint` or `build` would say so. The breakpoints themselves are checked
  // against `index.css` below, beside the rail rule; this is the half that proves the
  // pages are the ones the stylesheet is looking for.
  const videoOnDemandHtml = renderToStaticMarkup(
    withRouter('/admin/video-on-demand', withQuery(
      <AntApp>
        <VideoOnDemandPage sectionKey="nav.items.videoOnDemandManage" />
      </AntApp>,
    )),
  )
  check(
    'the on-demand table draws its own heading, its toolbar and its table before any rows arrive',
    videoOnDemandHtml.includes('>On-demand videos<') &&
      videoOnDemandHtml.includes('ant-table') &&
      !videoOnDemandHtml.includes('nav.items.videoOnDemandManage'),
    `rendered ${videoOnDemandHtml.length} bytes with the list still being fetched`,
  )
  check(
    'the on-demand table draws the cover, release and rating columns and its own filters',
    videoOnDemandHtml.includes('>Cover<') &&
      videoOnDemandHtml.includes('>Released<') &&
      videoOnDemandHtml.includes('>Rating<') &&
      videoOnDemandHtml.includes('Search by name') &&
      videoOnDemandHtml.includes('Any category') &&
      videoOnDemandHtml.includes('Any status') &&
      videoOnDemandHtml.includes('Any type'),
    `rendered ${videoOnDemandHtml.length} bytes`,
  )

  const videoOnDemandFormHtml = renderToStaticMarkup(
    withRouter('/admin/video-on-demand/add', withQuery(
      <AntApp>
        <VideoOnDemandFormPage sectionKey="nav.items.videoOnDemandAdd" />
      </AntApp>,
    )),
  )
  check(
    'the on-demand add form draws the media and metadata sections',
    videoOnDemandFormHtml.includes('Add video') &&
      videoOnDemandFormHtml.includes('Where it plays from') &&
      videoOnDemandFormHtml.includes('Metadata') &&
      videoOnDemandFormHtml.includes('Source') &&
      videoOnDemandFormHtml.includes('IMDb identifier') &&
      videoOnDemandFormHtml.includes('Cast'),
    `rendered ${videoOnDemandFormHtml.length} bytes`,
  )

  const videoOnDemandImportHtml = renderToStaticMarkup(
    withRouter('/admin/video-on-demand/import', withQuery(
      <AntApp>
        <VideoOnDemandImportPage sectionKey="nav.items.videoOnDemandImport" />
      </AntApp>,
    )),
  )
  check(
    'the on-demand import opens with the playlist and draws no name or source field',
    videoOnDemandImportHtml.includes('Import on-demand videos') &&
      videoOnDemandImportHtml.includes('M3U playlist') &&
      !videoOnDemandImportHtml.includes('title="Name"') &&
      !videoOnDemandImportHtml.includes('title="Source"'),
    `rendered ${videoOnDemandImportHtml.length} bytes`,
  )

  // --- The series pages --------------------------------------------------------
  //
  // The catalogue's own table, its form, the episode table and the two forms that add an
  // episode or a season. The episode and season addresses are `extra` routes with no menu
  // item, so each render uses the same parameterised route the application declares.
  const seriesHtml = renderToStaticMarkup(
    withRouter('/admin/series', withQuery(
      <AntApp>
        <SeriesPage sectionKey="nav.items.seriesManage" />
      </AntApp>,
    )),
  )
  check(
    'the series table draws its heading, its counts and its filters before any rows arrive',
    seriesHtml.includes('>Series<') &&
      seriesHtml.includes('>Seasons<') &&
      seriesHtml.includes('>Episodes<') &&
      seriesHtml.includes('Any category') &&
      seriesHtml.includes('Any status') &&
      seriesHtml.includes('ant-table'),
    `rendered ${seriesHtml.length} bytes with the list still being fetched`,
  )

  const seriesFormHtml = renderToStaticMarkup(
    withRouter('/admin/series/add', withQuery(
      <AntApp>
        <SeriesFormPage sectionKey="nav.items.seriesAdd" />
      </AntApp>,
    )),
  )
  check(
    'the series add form draws its metadata section and no computed-count field',
    seriesFormHtml.includes('Add series') &&
      seriesFormHtml.includes('Metadata') &&
      seriesFormHtml.includes('IMDb identifier') &&
      seriesFormHtml.includes('Cast') &&
      !seriesFormHtml.includes('name="seasonsCount"'),
    `rendered ${seriesFormHtml.length} bytes`,
  )

  const seriesEpisodesHtml = renderToStaticMarkup(
    withRoutes('/admin/series/11111111-1111-1111-1111-111111111111/episodes', [
      {
        path: '/admin/series/:id/episodes',
        element: withQuery(
          <AntApp>
            <SeriesEpisodesPage sectionKey="nav.items.seriesManage" />
          </AntApp>,
        ),
      },
    ]),
  )
  check(
    'the episode table draws its series heading, its toolbar and its season filter',
    seriesEpisodesHtml.includes('>Episodes<') &&
      seriesEpisodesHtml.includes('Back to series') &&
      seriesEpisodesHtml.includes('Add season') &&
      seriesEpisodesHtml.includes('Any season') &&
      seriesEpisodesHtml.includes('ant-table'),
    `rendered ${seriesEpisodesHtml.length} bytes`,
  )

  const episodeFormHtml = renderToStaticMarkup(
    withRoutes(
      '/admin/series/11111111-1111-1111-1111-111111111111/episodes/22222222-2222-2222-2222-222222222222',
      [
        {
          path: '/admin/series/:id/episodes/:episodeId',
          element: withQuery(
            <AntApp>
              <EpisodeFormPage sectionKey="nav.items.seriesAdd" />
            </AntApp>,
          ),
        },
      ],
    ),
  )
  check(
    'the episode edit address waits for the episode instead of drawing fields against nothing',
    episodeFormHtml.includes('Edit episode') &&
      episodeFormHtml.includes('ant-spin') &&
      !episodeFormHtml.includes('Create'),
    `rendered ${episodeFormHtml.length} bytes under the edit title with the spinner up`,
  )

  const seasonFormHtml = renderToStaticMarkup(
    withRoutes('/admin/series/11111111-1111-1111-1111-111111111111/seasons/add', [
      {
        path: '/admin/series/:id/seasons/add',
        element: withQuery(
          <AntApp>
            <SeasonFormPage sectionKey="nav.items.seriesAdd" />
          </AntApp>,
        ),
      },
    ]),
  )
  check(
    'the add-season form waits for the series before drawing the folder field',
    seasonFormHtml.includes('Add a season') &&
      seasonFormHtml.includes('ant-spin') &&
      !seasonFormHtml.includes('Folder with the episodes'),
    `rendered ${seasonFormHtml.length} bytes with the spinner up`,
  )

  // --- The subscriptions management list and its form ------------------------
  //
  // The list is the screen the three placeholders collapsed into: one row per subscription with
  // its service strips drawn as forced-open expanded rows, the five columns the owner named, and
  // the same toolbar (auto-refresh, bulk menu, search, three filters) the streams table draws.
  const subscriptionsHtml = renderToStaticMarkup(
    withRouter('/admin/subscriptions', withQuery(
      <AntApp>
        <SubscriptionsPage sectionKey="nav.items.subscriptionsManage" />
      </AntApp>,
    )),
  )

  // The page's own source, read once here because two separate checks below assert things no
  // static render can draw: a row action's order, the drill-down it opens, and the link on the
  // active-connections count. Every render on this page lands before the first row arrives.
  const subscriptionsPageSource = readFileSync('src/pages/admin/SubscriptionsPage.tsx', 'utf8')

  check(
    'the subscriptions table draws its own heading and its table before any rows arrive',
    subscriptionsHtml.includes('>Subscriptions<') &&
      subscriptionsHtml.includes('ant-table') &&
      !subscriptionsHtml.includes('nav.items.subscriptionsManage'),
    `rendered ${subscriptionsHtml.length} bytes with the subscriptions still being fetched`,
  )

  check(
    'the subscriptions table offers its six columns, the three filters and the search',
    subscriptionsHtml.includes('>Expires<') &&
      subscriptionsHtml.includes('>Active connections<') &&
      subscriptionsHtml.includes('>Max connections<') &&
      subscriptionsHtml.includes('>Issued by<') &&
      subscriptionsHtml.includes('Search by playlist id, MAG MAC or Enigma2 MAC') &&
      subscriptionsHtml.includes('Any status') &&
      subscriptionsHtml.includes('Any owner') &&
      subscriptionsHtml.includes('Any issuer') &&
      // The auto-refresh toggle leads the toolbar, the same place the streams page puts it.
      subscriptionsHtml.includes('Pause auto refresh') &&
      // The bulk menu uses the same label as every other table (owner item 1).
      subscriptionsHtml.includes('Actions') &&
      !subscriptionsHtml.includes('Bulk actions'),
    `rendered ${subscriptionsHtml.length} bytes`,
  )

  // The active-connections column sits **before** the ceiling, on the owner's correction: the
  // two are read together ("3 of 5"), and the count is the half that moves. Asserted by heading
  // order because a column moved on the page side only is invisible to every other checker.
  check(
    'the active-connections column is drawn before the max-connections column',
    subscriptionsHtml.indexOf('>Active connections<') >= 0 &&
      subscriptionsHtml.indexOf('>Active connections<') <
        subscriptionsHtml.indexOf('>Max connections<'),
    `active@${subscriptionsHtml.indexOf('>Active connections<')}, max@${subscriptionsHtml.indexOf('>Max connections<')}`,
  )

  // The six columns, in order, at the widths the page declares - and the sum still 1190, which is
  // the contract every table in this administration shares. The selection column takes them to
  // 1230 and is not one of the page's own.
  const subscriptionCols = [...subscriptionsHtml.matchAll(/<col\s[^>]*>/g)].map((m) => m[0])
  const subscriptionWidths = subscriptionCols.map((col) => /width:(\d+)px/.exec(col)?.[1])
  const subscriptionTotal = subscriptionWidths.reduce((sum, width) => sum + (Number(width) || 0), 0)
  check(
    'the subscriptions table draws seven columns (six plus the selection) totalling 1230',
    subscriptionCols.length === 7 && subscriptionTotal === 1230,
    `${subscriptionCols.length} columns: [${subscriptionWidths.join(', ')}] = ${subscriptionTotal}px`,
  )

  // The actions column is measured rather than chosen: exactly as wide as the six buttons a
  // row draws. The count is the formula's whole input, and it has been left behind once before
  // (a five-button width under a six-button row clipped "Show activity"), so both halves are
  // pinned: the page's own source names six, and the rendered last column is that width.
  check(
    'the subscriptions actions column is exactly as wide as the six buttons a row draws',
    /actionsColumnWidth\(6\)/.test(subscriptionsPageSource) &&
      subscriptionWidths[6] === String(actionsColumnWidth(6)),
    `actions column ${subscriptionWidths[6]}, actionsColumnWidth(6) = ${actionsColumnWidth(6)}`,
  )

  // The two connection columns are read as one sentence ("3 of 5"), so the owner asked for them
  // to be the same width. Their positions are the 3rd and 4th of the page's own six (the
  // selection column shifts them to 4 and 5 in the rendered `<colgroup>`), and a pair that
  // drifts apart by a few pixels compiles and renders perfectly.
  check(
    'the active-connections and max-connections columns have the same width',
    subscriptionWidths[3] === subscriptionWidths[4] && subscriptionWidths[3] !== undefined,
    `active ${subscriptionWidths[3]}, max ${subscriptionWidths[4]}`,
  )

  // The active count is a **link** into the live page filtered to that subscription, not just a
  // number. No row is drawn in a static render, so this half is read off the page's own source
  // - the same treatment the row-action order gets - and pins both the target and the param.
  // (The source is read where the row-action check reads it, further down this section.)
  check(
    'the active-connections count links into the live page filtered to that subscription',
    /live-connections\?subscriptionId=\$\{subscription\.id\}/.test(subscriptionsPageSource) &&
      /activeConnectionCount[\s\S]{0,900}?noActiveConnections/.test(subscriptionsPageSource),
    'the count is not wired to the live page, or its zero state lost its accessible name',
  )

  check(
    'the playlist regeneration asks first only when an id would be replaced',
    // Both minting paths - the row strip's regenerate and the form's generate button - branch on
    // `replacesPlaylistId` before opening the question, so the first generation stays one click.
    // A static render draws neither dialog, so this reads the two sources the way the row-action
    // order is read.
    /if \(!replacesPlaylistId\(currentPlaylistId\)\) \{[\s\S]{0,120}?return/.test(
      subscriptionsPageSource,
    ) &&
      /modal\.confirm\(\{[\s\S]{0,200}?playlistRegenerateConfirmTitle/.test(
        subscriptionsPageSource,
      ) &&
      /if \(!replacesPlaylistId\(current\)\) \{[\s\S]{0,80}?return/.test(
        readFileSync('src/pages/admin/SubscriptionFormPage.tsx', 'utf8'),
      ),
    'a mint path does not branch on replacesPlaylistId, or its question is not a modal.confirm',
  )

  check(
    'the subscriptions table has no expand column and its name header sorts the creation date',
    // The empty chevron column was both the wide left gap and the width that pushed the table past
    // the card (design decision 17). Both sortable headers are present.
    !subscriptionsHtml.includes('ant-table-row-expand-icon-cell') &&
      !subscriptionsHtml.includes('ant-table-row-expand-icon') &&
      subscriptionsHtml.includes('>Expires<'),
    `rendered ${subscriptionsHtml.length} bytes`,
  )

  check(
    'the bulk menu carries an icon on every entry',
    // Six items rendered through the menu's markup carry an antd icon class; a bare label item
    // renders none. This is the owner's item 2, and it is invisible to typecheck.
    (subscriptionsHtml.match(/anticon/g) ?? []).length >= 7,
    `icons on the page: ${(subscriptionsHtml.match(/anticon/g) ?? []).length}`,
  )

  check(
    'the subscriptions table carries the page-card class the compact rules key on',
    subscriptionsHtml.includes('page-card'),
    `rendered ${subscriptionsHtml.length} bytes`,
  )

  check(
    'the subscription card carries its own scope class for the strip merge rules',
    // The merge rules live in `index.css` and must not reach any other table's expanded row, so
    // they are scoped by this class. Without it on the card the style block matches nothing and
    // the strips silently go back to a grey band under every row - invisible to tsc, lint and the
    // build, and invisible to every render assertion above.
    subscriptionsHtml.includes('page-card subscriptions-table'),
    subscriptionsHtml.match(/class="[^"]*page-card[^"]*"/)?.[0] ?? 'no page-card class',
  )

  const subscriptionFormHtml = renderToStaticMarkup(
    withRouter('/admin/subscriptions/add', withQuery(
      <AntApp>
        <SubscriptionFormPage sectionKey="nav.items.subscriptionsAdd" />
      </AntApp>,
    )),
  )

  check(
    'the subscription form draws every section, the plan-owned hint and no raw key',
    subscriptionFormHtml.includes('Add subscription') &&
      subscriptionFormHtml.includes('Owner') &&
      subscriptionFormHtml.includes('Tariff plan') &&
      subscriptionFormHtml.includes('No tariff plan') &&
      subscriptionFormHtml.includes('Services') &&
      subscriptionFormHtml.includes('Output formats') &&
      subscriptionFormHtml.includes('Limits') &&
      subscriptionFormHtml.includes('Access') &&
      subscriptionFormHtml.includes('Bouquets') &&
      subscriptionFormHtml.includes('Generate') &&
      subscriptionFormHtml.includes('Select all') &&
      subscriptionFormHtml.includes('Clear') &&
      // The add form opens with MAG and Enigma2 support available, so both MAC inputs are drawn.
      subscriptionFormHtml.includes('MAG MAC address') &&
      subscriptionFormHtml.includes('Enigma2 MAC address') &&
      !subscriptionFormHtml.includes('subscriptionsForm.'),
    `rendered ${subscriptionFormHtml.length} bytes`,
  )

  check(
    'the access lock hints read as sentences about watching a subscription, in both languages',
    // Round 3 replaced a garbled Russian construction ("Может ли подписка смотреться ...") that
    // named the subscription as the thing doing the watching. The bug lived only in `ru.ts`, so
    // the render alone would not catch it coming back: this asserts the English wording as drawn
    // *and* the Russian wording the bundle holds, plus the absence of the old construction in
    // either bundle. Keys are read through i18next rather than the module so a renamed key fails
    // here instead of silently passing.
    // Around the apostrophe rather than through it: `renderToStaticMarkup` escapes it, so the
    // literal "owner's" never appears in the markup.
    /may only be watched from the owner(?:&#x27;|')s country/.test(subscriptionFormHtml) &&
      /may only be watched from the owner(?:&#x27;|')s provider/.test(subscriptionFormHtml) &&
      i18n.t('subscriptionsForm.countryLockHint', { lng: 'ru' }) ===
        'Можно ли смотреть подписку только из страны владельца.' &&
      i18n.t('subscriptionsForm.ispLockHint', { lng: 'ru' }) ===
        'Можно ли смотреть подписку только из сети владельца.' &&
      !['en', 'ru'].some((lng) =>
        /смотреться|may be (only )?watched by the subscription/i.test(
          i18n.t('subscriptionsForm.countryLockHint', { lng }) +
            i18n.t('subscriptionsForm.ispLockHint', { lng }),
        ),
      ),
    `en drawn, ru reads "${i18n.t('subscriptionsForm.countryLockHint', { lng: 'ru' })}"`,
  )

  // The create form's own defaults, read through the rendered controls. `renderToStaticMarkup`
  // runs no effects, so this shows what `initialValues` opens with - which is exactly the state
  // the operator sees before touching anything.
  const maxConnectionsValue = /id="maxConnections"[^>]*value="([^"]*)"/.exec(subscriptionFormHtml)

  check(
    'the create form opens max connections at one, not empty',
    // The domain's floor is one, so a form that opened blank left the field *unset* rather than
    // start at the smallest legal value: an operator who never touched it created a subscription
    // with no ceiling at all.
    maxConnectionsValue?.[1] === '1',
    `the field opens at ${maxConnectionsValue?.[1] ?? 'nothing'}`,
  )

  check(
    'the playlist field is bound to the form rather than wrapped in a layout child',
    // The bug this catches: a named `Form.Item` whose child was `Space.Compact` injected
    // `value`/`onChange` into a `div` and the `Input` inside never received either, so the id the
    // server returned was written to a store no control read. A directly-bound input carries a
    // `value` attribute in the static markup and a compact wrapper's does not.
    /<input[^>]*id="playlistId"[^>]*value=""/.test(subscriptionFormHtml) ||
      /<input[^>]*value=""[^>]*id="playlistId"/.test(subscriptionFormHtml),
    `rendered ${subscriptionFormHtml.length} bytes`,
  )

  // Every write button on a service strip is gated on `editSubscriptions`, and the two reads -
  // copy and the MAG journal drill-down - are not: a view-only operator must see a greyed
  // control with the reason rather than a live button whose request the server refuses.
  // `probe:render` lands before `getProfile` answers, so `canEdit` is false here - which is
  // exactly the read-only state this asserts.
  const subrows = renderToStaticMarkup(
    withQuery(
      <AntApp>
        <SubscriptionSubrows
          subscription={{
            id: '11111111-1111-1111-1111-111111111111',
            userId: '22222222-2222-2222-2222-222222222222',
            username: 'subscriber',
            ownerBlocked: false,
            creatorUsername: 'operator',
            tariffPlanId: null,
            tariffPlanName: null,
            // The two fields the prolongation dialog projects from; null because this fixture
            // has no plan, which is the state whose button is disabled.
            tariffPlanDurationValue: null,
            tariffPlanDurationUnit: null,
            createdAt: '2026-01-01T00:00:00+00:00',
            expireAt: null,
            isTrial: false,
            status: 'active',
            maxConnections: 2,
            activeConnectionCount: 1,
            playlistId: '0123456789abcdef0123456789abcdef',
            magMac: 'AA:BB:CC:DD:EE:FF',
            enigma2Mac: null,
            supportsPlaylist: true,
            supportsMag: true,
            supportsEnigma2: true,
          }}
        />
      </AntApp>,
    ),
  )

  check(
    'the service strips draw every attached service, its copy and its gated write actions',
    subrows.includes('Playlist') &&
      subrows.includes('0123456789abcdef0123456789abcdef') &&
      subrows.includes('MAG') &&
      subrows.includes('AA:BB:CC:DD:EE:FF') &&
      // The MAG strip carries the two device actions between Change and Delete, drawn from
      // their own glyphs: the journal's drill-down and the send-event trigger.
      subrows.includes('anticon-usb') &&
      subrows.includes('anticon-send') &&
      // The add line offers the missing Enigma2 device and nothing else, because the playlist and
      // the MAG are attached.
      subrows.includes('Add Enigma2') &&
      !subrows.includes('Generate a playlist') &&
      !subrows.includes('Add MAG'),
    `rendered ${subrows.length} bytes`,
  )

  check(
    'every write button on a strip is disabled for a view-only operator',
    // Two services are attached, so exactly three buttons stay live: the two copy buttons,
    // which change nothing, and the MAG strip's journal drill-down, which is a read like them.
    // Every regenerate, change, send, delete and add button carries `disabled`.
    [...subrows.matchAll(/<button[^>]*>/g)].filter((m) => !m[0].includes('disabled')).length === 3,
    `live buttons: ${[...subrows.matchAll(/<button[^>]*>/g)].filter((m) => !m[0].includes('disabled')).length}`,
  )

  // The MAG strip's two device actions sit **between "Change" and "Delete"**, in that order:
  // the journal first, the send second. The labels live in tooltips, which a static render
  // never opens, so the order is read off the strip's own source - the keys are the only
  // stable spelling of an icon-only button.
  const subscriptionSubrowsSource = readFileSync(
    'src/features/subscriptions/components/SubscriptionSubrows.tsx',
    'utf8',
  )
  const magStripBlock =
    /key="mag"([\s\S]*?)key="enigma2"/.exec(subscriptionSubrowsSource)?.[1] ?? ''
  const magStripActionKeys = [...magStripBlock.matchAll(/key: '([A-Za-z-]+)',/g)].map(
    (match) => match[1],
  )
  const sendEventBlock =
    /key: 'sendEvent'([\s\S]*?)key: 'delete'/.exec(magStripBlock)?.[1] ?? ''
  check(
    'the MAG subrow runs Change, MAG events, Send event, Delete in that order',
    magStripActionKeys.join(',') === 'change,magEvents,sendEvent,delete' &&
      // Both buttons carry the row-action label keys, and only the journal's is ungated: it is
      // a read of the record of what was sent, while sending is a change to the subscription.
      magStripBlock.includes("t('subscriptions.actions.magEvents')") &&
      magStripBlock.includes("t('subscriptions.actions.sendEvent')") &&
      /key: 'magEvents',[\s\S]{0,220}?gated: false/.test(magStripBlock) &&
      sendEventBlock.includes('run: () => actions.sendEvent(subscription)') &&
      !sendEventBlock.includes('gated: false') &&
      // The page owns the dialog state and refuses a row whose device vanished between the
      // read and the click - the defence the server's `mag_device_missing` also states.
      /sendEvent: \(subscription\) => \{[\s\S]{0,240}?subscription\.magMac === null[\s\S]{0,240}?setSendEventTarget/.test(
        subscriptionsPageSource,
      ),
    `MAG strip actions: ${magStripActionKeys.join(', ') || 'none found'}`,
  )

  // The expiry field, rendered with a **populated** value. This is the regression check for the
  // crash a populated edit form had: `DatePicker` requires a `Dayjs` and calls `.isValid()` on
  // whatever it is handed, so a raw ISO string passed straight through threw
  // `getUDayjs(...).isValid is not a function` and took the form down. Seeding through
  // `initialValues` puts the stored string where the real page puts it, so this render fails on
  // the buggy wiring rather than only on a page that never had data.
  const expiryFieldHtml = renderToStaticMarkup(
    withQuery(
      <AntApp>
        <Form initialValues={{ expireAt: '2027-03-04T15:30:00+00:00' }}>
          <SubscriptionExpiryField planed={false} neverExpires={false} onNeverExpiresChange={() => {}} />
        </Form>
      </AntApp>,
    ),
  )

  check(
    'the expiry field renders a stored ISO moment through the picker without throwing',
    // The value is asserted as "populated with the stored day", not as an exact string: the picker
    // renders in the machine's zone, so 15:30Z is 18:30 on a UTC+3 box. What matters is that the
    // stored moment arrived - a raw string handed to `DatePicker` threw here rather than drawing
    // anything at all.
    expiryFieldHtml.includes('ant-picker') &&
      /value="2027-03-04 \d{2}:\d{2}"/.test(expiryFieldHtml) &&
      expiryFieldHtml.includes('Never expires'),
    `rendered ${expiryFieldHtml.length} bytes; value=${
      /value="([^"]*)"/.exec(expiryFieldHtml)?.[1] ?? 'none'
    }`,
  )

  // The same field with no expiry: an empty picker, which is the create form's state.
  const expiryFieldEmptyHtml = renderToStaticMarkup(
    withQuery(
      <AntApp>
        <Form initialValues={{ expireAt: null }}>
          <SubscriptionExpiryField planed={false} neverExpires onNeverExpiresChange={() => {}} />
        </Form>
      </AntApp>,
    ),
  )

  check(
    'the expiry field draws an empty picker when nothing is stored and the never switch is on',
    expiryFieldEmptyHtml.includes('ant-picker') &&
      expiryFieldEmptyHtml.includes('ant-picker-disabled') &&
      !expiryFieldEmptyHtml.includes('ant-picker-input-placeholder'),
    `rendered ${expiryFieldEmptyHtml.length} bytes with no expiry`,
  )

  const subscriptionEditFormHtml = renderToStaticMarkup(
    withRoutes('/admin/subscriptions/11111111-1111-1111-1111-111111111111', [
      {
        path: '/admin/subscriptions/:id',
        element: withQuery(
          <AntApp>
            <SubscriptionFormPage sectionKey="nav.items.subscriptionsAdd" />
          </AntApp>,
        ),
      },
    ]),
  )

  // The loaded state, from a seeded cache: the row has an expiry, a plan and two services.
  const subscriptionPopulatedHtml = renderToStaticMarkup(
    withRoutes('/admin/subscriptions/11111111-1111-1111-1111-111111111111', [
      {
        path: '/admin/subscriptions/:id',
        element: withSeededQuery(
          ['subscription', '11111111-1111-1111-1111-111111111111'],
          {
            id: '11111111-1111-1111-1111-111111111111',
            userId: '22222222-2222-2222-2222-222222222222',
            username: 'subscriber',
            resellerId: null,
            creatorUsername: 'operator',
            playlistId: '0123456789abcdef0123456789abcdef',
            expireAt: '2027-03-04T15:30:00+00:00',
            tariffPlanId: null,
            tariffPlanName: null,
            status: 'active',
            maxConnections: 2,
            isRestreamer: false,
            ipWhitelist: [],
            userAgentWhitelist: [],
            isIspLock: false,
            isCountryLock: false,
            isTrial: false,
            outputFormats: ['mpegTs'],
            magMac: null,
            isStbLock: false,
            enigma2Mac: null,
            bouquetIds: [],
            createdAt: '2026-01-01T10:00:00+00:00',
            derived: null,
          },
          <AntApp>
            <SubscriptionFormPage sectionKey="nav.items.subscriptionsAdd" />
          </AntApp>,
        ),
      },
    ]),
  )

  check(
    'a populated subscription edit form draws its fields without throwing on the stored expiry',
    // The regression this pins: `DatePicker` was handed the raw ISO string and threw
    // `getUDayjs(...).isValid is not a function`, which took the whole form down. A form that
    // renders its sections *and* draws the expiry control is the proof the conversion happens.
    // What this proves: the page draws its sections and its controls from a row that carries a
    // plan, two services and an expiry, rather than failing on the way. What it does **not** prove
    // is the expiry conversion - `renderToStaticMarkup` runs no effects, so the `setFieldsValue`
    // that fills the form never runs and the picker is drawn empty either way. That half is the
    // `SubscriptionExpiryField` render above (seeded through `initialValues`, which the effect is
    // not needed for) and `expireAtPickerProps`' unit test.
    subscriptionPopulatedHtml.includes('Edit subscription') &&
      subscriptionPopulatedHtml.includes('MAG MAC address') &&
      subscriptionPopulatedHtml.includes('ant-picker') &&
      !subscriptionPopulatedHtml.includes('subscriptionsForm.'),
    `rendered ${subscriptionPopulatedHtml.length} bytes from a populated row`,
  )

  check(
    'the subscription edit form waits for the row before drawing the fields',
    // The same loading state every edit form here draws: a spinner in a bare card, and no field
    // painted from whatever the previous screen happened to hold.
    subscriptionEditFormHtml.includes('ant-spin') &&
      !subscriptionEditFormHtml.includes('MAG MAC address') &&
      !subscriptionEditFormHtml.includes('Tariff plan'),
    `rendered ${subscriptionEditFormHtml.length} bytes with the spinner up`,
  )

  // --- The connection log: one component behind two menu entries --------------
  //
  // The two addresses differ in the slice of the log they read and in the columns they draw,
  // and both arrive through `sectionKey` - so the thing worth asserting is that each of the
  // two renderings is *its own*: its heading, its column set, its widths, and the two facts
  // that separate the modes (an end column and no actions column on the activity view; no end
  // column and a disabled kill on the live one).
  //
  // `?subscriptionId=` is in the live render because that is how the subscriptions table's
  // "Show activity" row action opens the page, and the drill-down must arrive filtered rather
  // than looking as if the click had done nothing.
  const liveLogsHtml = renderToStaticMarkup(
    withRouter(
      '/admin/live-connections?subscriptionId=11111111-1111-1111-1111-111111111111',
      withQuery(
        <AntApp>
          <ConnectionLogsPage sectionKey="nav.items.liveConnections" />
        </AntApp>,
      ),
    ),
  )
  const activityLogsHtml = renderToStaticMarkup(
    withRouter('/admin/connection-logs', withQuery(
      <AntApp>
        <ConnectionLogsPage sectionKey="nav.items.subscriptionActivity" />
      </AntApp>,
    )),
  )

  check(
    'the live connection log draws its own heading and its table before any rows arrive',
    liveLogsHtml.includes('>Live connections<') &&
      liveLogsHtml.includes('ant-table') &&
      !liveLogsHtml.includes('>Connection logs<') &&
      !liveLogsHtml.includes('nav.items.liveConnections'),
    `rendered ${liveLogsHtml.length} bytes with the connections still being fetched`,
  )

  check(
    'the activity log draws its own heading and its table before any rows arrive',
    // The heading is "Connection logs"/"Журнал подключений" on the owner's rename, and the *menu
    // item* now says the same words - the rail and the card name one resource. Two keys remain
    // so either can still be worded alone.
    activityLogsHtml.includes('>Connection logs<') &&
      activityLogsHtml.includes('ant-table') &&
      !activityLogsHtml.includes('>Live connections<') &&
      !activityLogsHtml.includes('nav.items.subscriptionActivity'),
    `rendered ${activityLogsHtml.length} bytes with the connections still being fetched`,
  )

  // The seven columns of the live view and the seven of the activity view, in order, from the
  // `<colgroup>` antd draws - and the sums that keep both tables the same width as every other
  // administration table. The live view is the only one of the two that draws an actions column;
  // the activity view ends at the end time, and the widths it keeps sum to the same 1190.
  //
  // Seven on the round-3 corrections: the log's own identifier went, the subscription and the
  // user became two columns, and the user agent joined beside the network cell.
  const liveLogCols = [...liveLogsHtml.matchAll(/<col\s[^>]*>/g)].map((m) => m[0])
  const liveLogWidths = liveLogCols.map((col) => /width:(\d+)px/.exec(col)?.[1])
  const liveLogTotal = liveLogWidths.reduce((sum, width) => sum + (Number(width) || 0), 0)
  check(
    'the live log draws seven columns totalling 1190, with no end column and its actions cell',
    liveLogCols.length === 7 && liveLogTotal === 1190,
    `${liveLogCols.length} columns: [${liveLogWidths.join(', ')}] = ${liveLogTotal}px`,
  )

  const activityLogCols = [...activityLogsHtml.matchAll(/<col\s[^>]*>/g)].map((m) => m[0])
  const activityLogWidths = activityLogCols.map((col) => /width:(\d+)px/.exec(col)?.[1])
  const activityLogTotal = activityLogWidths.reduce((sum, width) => sum + (Number(width) || 0), 0)
  check(
    'the activity log draws seven columns totalling 1190, with the end column and no actions column',
    activityLogCols.length === 7 && activityLogTotal === 1190,
    `${activityLogCols.length} columns: [${activityLogWidths.join(', ')}] = ${activityLogTotal}px`,
  )

  check(
    'the live log ends with its actions cell and the activity log ends with the end time',
    // The last column of each table by width: the live one is the kill stub's own measurement
    // plus the owner's 45 (`actionsColumnWidth(1) + 45` = 101) and the activity one is the end
    // column.
    liveLogWidths[6] === '101' && activityLogWidths[6] === '200',
    `live last ${liveLogWidths[6]}, activity last ${activityLogWidths[6]}`,
  )

  // The round-3 column corrections, asserted as the headers and as the arithmetic: the id column
  // is gone, the subscription and the user are two columns of their own, and the user agent has
  // one of its own - in *both* modes, because the two screens read the same row.
  check(
    'neither log mode draws the log identifier, and both draw Subscription, User and User agent',
    !liveLogsHtml.includes('>ID<') &&
      !activityLogsHtml.includes('>ID<') &&
      liveLogsHtml.includes('>Subscription<') &&
      liveLogsHtml.includes('>User<') &&
      liveLogsHtml.includes('>User agent<') &&
      activityLogsHtml.includes('>Subscription<') &&
      activityLogsHtml.includes('>User<') &&
      activityLogsHtml.includes('>User agent<'),
    `live id ${liveLogsHtml.includes('>ID<')}, activity id ${activityLogsHtml.includes('>ID<')}`,
  )

  check(
    "the actions column grew by the owner's 45 and the stream column gave it up",
    // 101 = `actionsColumnWidth(1)` (56) + 45, and 299 = the 344 the title had - 45.
    liveLogWidths[6] === '101' && liveLogWidths[2] === '299',
    `actions ${liveLogWidths[6]}, stream ${liveLogWidths[2]}`,
  )

  // The owner's rebalancing, asserted as the relationship rather than as seven magic numbers:
  // `startedAt` and `endedAt` are the 6th and 7th `<col>` of the activity table and must be
  // **equal**, and the address/user-agent pair (the 4th and 5th of either) carries the owner's
  // round-4 split. Both are invisible to typecheck, lint and the build - seven valid numbers
  // that no longer mean what the owner asked for compile perfectly.
  check(
    'the activity log gives the start and end columns equal widths',
    activityLogWidths[5] === activityLogWidths[6] && activityLogWidths[5] !== undefined,
    `started ${activityLogWidths[5]}, ended ${activityLogWidths[6]}`,
  )

  check(
    'the address column starts at 160 and the user agent at 170, the pair unchanged in both modes',
    // The owner's round-4 correction as two numbers and a sum: the address took 40 px from the
    // user agent, and the pair still spends 330 in both tables because the two screens read the
    // same row. A render is the only place this is visible - seven valid numbers compile fine.
    liveLogWidths[3] === '160' &&
      liveLogWidths[4] === '170' &&
      activityLogWidths[3] === '160' &&
      activityLogWidths[4] === '170',
    `live ${liveLogWidths[3]}/${liveLogWidths[4]}, activity ${activityLogWidths[3]}/${activityLogWidths[4]}`,
  )

  check(
    'the activity log draws no actions column and no kill stub at all',
    activityLogsHtml.includes('>Ended<') &&
      !activityLogsHtml.includes('>Actions<') &&
      !activityLogsHtml.includes('Kill active connections (not available yet)') &&
      liveLogsHtml.includes('>Actions<'),
    `activity actions: ${activityLogsHtml.includes('>Actions<')}, live actions: ${liveLogsHtml.includes('>Actions<')}`,
  )

  check(
    'the live log has no end column and the activity log has one',
    liveLogsHtml.includes('>Started<') &&
      !liveLogsHtml.includes('>Ended<') &&
      activityLogsHtml.includes('>Started<') &&
      activityLogsHtml.includes('>Ended<'),
    `live ends: ${liveLogsHtml.includes('>Ended<')}, activity ends: ${activityLogsHtml.includes('>Ended<')}`,
  )

  // The kill stub lives on the live view and nowhere else. A static render draws no cell `render`
  // at all - antd calls one only when a row exists, and every render here lands before the first
  // answer - so this half is read off the page's own source, exactly as the subscriptions
  // row-action order is. What it pins is the rule rather than the markup: the guard that draws
  // the stub, the reason the stub carries, and the `disabled` that makes the button tell the
  // truth before the click.
  const connectionLogsPageSource = readFileSync(
    'src/pages/admin/ConnectionLogsPage.tsx',
    'utf8',
  )
  check(
    'the live log draws the kill stub disabled with its reason, and the activity log has no actions column',
    /spec\.showsEnd\s*\n?\s*\?\s*\[\]/.test(connectionLogsPageSource) &&
      connectionLogsPageSource.includes("label: t('subscriptions.actions.killConnections')") &&
      /key: 'kill',[\s\S]{0,200}?disabled: true/.test(connectionLogsPageSource),
    'the mode gate, the reason or the disabled state is missing from the actions column',
  )

  // The "Live" tag: a row whose end time is null must not draw a dash in the end cell. No row is
  // rendered here, so this half is read off the source too - the guard, and the locale key it
  // draws - plus the key's presence in both bundles through i18next.
  check(
    "an open session's end cell draws the Live tag rather than a dash",
    /if \(value === null\)[\s\S]{0,300}?t\('connectionLogs\.live'\)/.test(connectionLogsPageSource) &&
      i18n.getResource('en', 'translation', 'connectionLogs.live') === 'Live' &&
      i18n.getResource('ru', 'translation', 'connectionLogs.live') === 'Live',
    `en ${i18n.getResource('en', 'translation', 'connectionLogs.live')}, ru ${i18n.getResource('ru', 'translation', 'connectionLogs.live')}`,
  )

  // The sorting regression, caught at the seam where it happened. A column whose default order is
  // descending could never leave it: antd's two-entry cycle answers `undefined` after `descend`,
  // the table reads that as "no order" and writes the default back, so every click on the
  // connection log's Started header asked for the page already on screen. The cycle itself is
  // asserted by `tests/utils/tableSort.test.ts`; what a render cannot see, and this pins, is that
  // `ServerTable` actually hands the cycle to antd for every table it draws. Without this line the
  // constant could be perfect and unused.
  const serverTableSource = readFileSync('src/components/table/ServerTable.tsx', 'utf8')
  check(
    'the table frame gives every sortable header the three-entry cycle, so a descending default can toggle',
    /sortDirections=\{SORT_DIRECTIONS\}/.test(serverTableSource) &&
      /from '\.\.\/\.\.\/utils\/tableSort'/.test(serverTableSource),
    'ServerTable does not pass the shared sort cycle - every column falls back to the two-entry pair',
  )

  check(
    'the connection log keeps its sortable headers controlled, so the arrow matches the order the server applied',
    /sortOrder: table\.orderOf\('startedAt'\)/.test(connectionLogsPageSource) &&
      /sortOrder: table\.orderOf\('endedAt'\)/.test(connectionLogsPageSource),
    'a sortable header lost its controlled order and would show no arrow over ordered rows',
  )

  check(
    'the live log offers the auto-refresh, the owner and stream pickers, the medium switch, the search and the clear command',
    liveLogsHtml.includes('Pause auto refresh') &&
      liveLogsHtml.includes('Any owner') &&
      liveLogsHtml.includes('Any stream') &&
      liveLogsHtml.includes('>Video<') &&
      liveLogsHtml.includes('>Audio<') &&
      liveLogsHtml.includes('Search by IP address, user agent or provider') &&
      liveLogsHtml.includes('Reset widths') &&
      liveLogsHtml.includes('Clear all logs'),
    `rendered ${liveLogsHtml.length} bytes`,
  )

  // The range is the activity view's alone: the live page is "every session still open", so a
  // start bound there would narrow the list with no control on screen saying so. Both the picker
  // and its tag must be absent - and the page must not send the two parameters either, which
  // `startRangeForMode` pins in `tests/features/connection-logs/startRange.test.ts` because a
  // render never sees the request.
  check(
    'the live log draws no start-time range control and no range tag',
    !liveLogsHtml.includes('Started from') &&
      !liveLogsHtml.includes('Started until') &&
      !liveLogsHtml.includes('Started:') &&
      !liveLogsHtml.includes('ant-picker-range'),
    `rendered ${liveLogsHtml.length} bytes with no range control`,
  )

  // A stale or hand-edited live address carrying the two bounds must be **ignored**, not honoured
  // and not stripped: no control is drawn, no tag appears, and - because the page reads the range
  // through `startRangeForMode` - the request is the plain live one.
  const staleLiveRangeHtml = renderToStaticMarkup(
    withRouter(
      '/admin/live-connections?startedFrom=2026-05-01T00%3A00%3A00.000Z&startedTo=2026-05-03T23%3A59%3A59.999Z',
      withQuery(<AntApp>
        <ConnectionLogsPage sectionKey="nav.items.liveConnections" />
      </AntApp>),
    ),
  )
  check(
    'a stale range in a live address draws no control and no tag',
    staleLiveRangeHtml.includes('>Live connections<') &&
      !staleLiveRangeHtml.includes('Started from') &&
      !staleLiveRangeHtml.includes('Started:') &&
      /startRangeForMode\(searchParams, spec\.showsStartRange\)/.test(connectionLogsPageSource),
    `rendered ${staleLiveRangeHtml.length} bytes with a stale range in the address`,
  )

  // The toolbar order the specification names, asserted by the index of the text: auto-refresh,
  // the search box, the owner and stream pickers, the medium switch, the width reset, and the
  // clear command. The start-time range is **not** in this row any more - it is the secondary
  // row below, and the next check pins that placement.
  const logToolbar = [
    ['auto-refresh', liveLogsHtml.indexOf('Pause auto refresh')],
    ['search', liveLogsHtml.indexOf('Search by IP address, user agent or provider')],
    ['owner', liveLogsHtml.indexOf('Any owner')],
    ['stream', liveLogsHtml.indexOf('Any stream')],
    ['medium', liveLogsHtml.indexOf('>Video<')],
    ['width reset', liveLogsHtml.indexOf('Reset widths')],
    ['clear', liveLogsHtml.indexOf('Clear all logs')],
  ]
  const logToolbarOrdered = logToolbar.every(
    ([, position], index) => position >= 0 && (index === 0 || position > logToolbar[index - 1][1]),
  )
  check(
    'the connection-log toolbar runs auto-refresh, search, owner, stream, medium, width reset, clear',
    logToolbarOrdered,
    logToolbar.map(([name, position]) => `${name}@${position}`).join(', '),
  )

  // The range's own row: after the card head (whose last control is "Clear all logs") and inside
  // the card body before the table, which is the owner's "separate line above the table" rather
  // than a control squeezed into the heading. Index order is enough: the head, the body's first
  // child (the picker), then the tag row and the table.
  check(
    'the activity log draws the start-time range on its own row between the card head and the table',
    activityLogsHtml.includes('ant-card-head') &&
      activityLogsHtml.indexOf('Started from') > activityLogsHtml.indexOf('Clear all logs') &&
      activityLogsHtml.indexOf('Started from') > activityLogsHtml.indexOf('ant-card-body') &&
      activityLogsHtml.indexOf('Started from') < activityLogsHtml.indexOf('ant-table'),
    `range@${activityLogsHtml.indexOf('Started from')}, head clear@${activityLogsHtml.indexOf('Clear all logs')}, body@${activityLogsHtml.indexOf('ant-card-body')}, table@${activityLogsHtml.indexOf('ant-table')}`,
  )

  // The range is a filter like every other one: a range in the address must be drawn back into
  // the picker and shown as a closable tag. `?startedFrom=` is in the render because that is
  // what a hand-edited link or a saved address carries, and the control must not look empty over
  // a filtered list.
  const rangedLogsHtml = renderToStaticMarkup(
    withRouter(
      '/admin/connection-logs?startedFrom=2026-05-01T00%3A00%3A00.000Z&startedTo=2026-05-03T23%3A59%3A59.999Z',
      withQuery(
        <AntApp>
          <ConnectionLogsPage sectionKey="nav.items.subscriptionActivity" />
        </AntApp>,
      ),
    ),
  )
  check(
    'a start-time range in the address is drawn as a closable filter tag',
    rangedLogsHtml.includes('Started:') &&
      rangedLogsHtml.includes('ant-tag-close-icon'),
    `rendered ${rangedLogsHtml.length} bytes with the range filter`,
  )

  // Both bounds are one control over two parameters, so closing its tag must clear both. The
  // clear is one `chooseRange(null)` call, which writes both keys: two separate `choose` calls
  // would read the half-written range in between. No click runs under `renderToStaticMarkup`, so
  // this half is read off the page's source - the same treatment the kill stub gets.
  check(
    'the range tag clears both bounds in one navigation',
    /key: 'startedRange',[\s\S]{0,400}?clear: \(\) => chooseRange\(null\)/.test(
      connectionLogsPageSource,
    ) &&
      /const chooseRange = \(value[\s\S]{0,600}?withFilter\(searchParams, STARTED_FROM_KEY[\s\S]{0,300}?withFilter\(withFrom, STARTED_TO_KEY/.test(
        connectionLogsPageSource,
      ),
    'the range tag and the single-write range update are both present',
  )

  // The `subscriptionId` the address arrived with is drawn as a closable tag - it is a filter
  // the page was opened with, and a visitor who cannot see it cannot remove it.
  check(
    'the subscription the page was opened at arrives as a closable filter tag',
    liveLogsHtml.includes('Subscription: 11111111') &&
      liveLogsHtml.includes('ant-tag-close-icon'),
    `rendered ${liveLogsHtml.length} bytes with the drill-down filter`,
  )

  // A key renamed on the menu side only must fail loudly. `renderToStaticMarkup` throws out of
  // a throwing component, so the presence of an error is the assertion.
  let unknownSectionKeyThrew = false
  try {
    renderToStaticMarkup(
      withRouter('/admin/live-connections', withQuery(
        <AntApp>
          <ConnectionLogsPage sectionKey="nav.items.notAConnectionLog" />
        </AntApp>,
      )),
    )
  } catch {
    unknownSectionKeyThrew = true
  }
  check(
    'an unknown section key fails the render rather than falling back to one of the two modes',
    unknownSectionKeyThrew,
    unknownSectionKeyThrew ? 'the render threw' : 'THE RENDER SUCCEEDED - a key is being guessed',
  )

  // **The two modes must not share a component instance.** Both menu entries render this one
  // module, so react-router changes the props of a mounted component rather than remounting it:
  // every `useState` inside survived the switch, and the width state did too - which is how the
  // activity table came to be drawn with the live mode's column list and then persist it under
  // its own key. The page splits into a shell that resolves the key and a screen keyed by the
  // mode; these two facts are what a static render *can* see about that, and are asserted as
  // source because the failure is a mounting behaviour no markup shows.
  check(
    'the two modes are two component instances, so no width or refresh state can leak between them',
    /<ConnectionLogScreen key=\{mode\} mode=\{mode\} \/>/.test(connectionLogsPageSource) &&
      /function ConnectionLogScreen\(\{ mode \}/.test(connectionLogsPageSource),
    'the page draws one instance for both modes - state (widths, auto-refresh) survives the switch',
  )

  // The width hook is given the mode's own three inputs. A call site that reached for
  // `spec.showsEnd ? … : …` was exactly the shape that could read one mode's column list with
  // the other's defaults, so the assertion is that all three come from the one spec entry.
  check(
    'each mode hands the width hook its own columns, defaults and storage key',
    /keys: spec\.columns/.test(connectionLogsPageSource) &&
      /defaults: spec\.widths/.test(connectionLogsPageSource) &&
      /storageKey: spec\.storageKey/.test(connectionLogsPageSource),
    'the width hook is reading from more than one mode at once',
  )

  check(
    'the activity menu item carries the renamed label, in both bundles',
    // The owner renamed the heading and then the rail entry to match it: one screen wearing two
    // names was the complaint. Both are asserted as strings because a missing translation would
    // degrade to the raw key in the rail, and nothing in typecheck, lint or the build sees it.
    i18n.getResource('en', 'translation', 'nav.items.subscriptionActivity') ===
      'Connection logs' &&
      i18n.getResource('ru', 'translation', 'nav.items.subscriptionActivity') ===
        'Журнал подключений' &&
      i18n.getResource('en', 'translation', 'connectionLogs.titles.activity') ===
        'Connection logs' &&
      i18n.getResource('ru', 'translation', 'connectionLogs.titles.activity') ===
        'Журнал подключений',
    `en ${i18n.getResource('en', 'translation', 'nav.items.subscriptionActivity')}, ru ${i18n.getResource('ru', 'translation', 'nav.items.subscriptionActivity')}`,
  )

  check(
    'the two modes store their widths under different keys',
    COLUMN_WIDTHS_KEYS.live !== COLUMN_WIDTHS_KEYS.activity &&
      COLUMN_WIDTHS_KEYS.live.includes('live') &&
      COLUMN_WIDTHS_KEYS.activity.includes('activity') &&
      COLUMN_WIDTHS_KEYS.live.endsWith('.v2') &&
      COLUMN_WIDTHS_KEYS.activity.endsWith('.v2'),
    `${COLUMN_WIDTHS_KEYS.live} vs ${COLUMN_WIDTHS_KEYS.activity}`,
  )

  // The two menu entries sit in the subscriptions group, and the owner's order is: the list,
  // the price list, then the two log entries - tariffs above live connections.
  const logGroup = NAVIGATION.Admin.find((group) => group.key === 'subscriptions')
  const connectionLogItems = (logGroup?.items ?? []).map((item) => item.key)
  check(
    'the subscriptions group carries the price list above the two connection-log entries',
    connectionLogItems.indexOf('tariffPlans') ===
      connectionLogItems.indexOf('subscriptionsManage') + 1 &&
      connectionLogItems.indexOf('liveConnections') ===
        connectionLogItems.indexOf('tariffPlans') + 1 &&
      connectionLogItems.indexOf('subscriptionActivity') ===
        connectionLogItems.indexOf('liveConnections') + 1,
    `items: ${connectionLogItems.join(', ')}`,
  )

  check(
    'the live log and the activity log have addresses of their own',
    menuPath('Admin', 'live-connections') === '/admin/live-connections' &&
      menuPath('Admin', 'connection-logs') === '/admin/connection-logs',
    `entries open ${menuPath('Admin', 'live-connections')} and ${menuPath('Admin', 'connection-logs')}`,
  )

  // The row actions of the subscriptions table, in the order the specification names them, and
  // the drill-down the "Show activity" one opens. No row exists in a static render, so the
  // order is read off the page's own source - the one thing a probe can see about a `render`
  // that runs only for a row. (`subscriptionsPageSource` above is the same read.)
  const rowActionsBlock =
    /const actions: RowAction\[\] = \[([\s\S]*?)return <RowActions actions=\{actions\} \/>/
      .exec(subscriptionsPageSource)?.[1] ?? ''
  const subscriptionActionKeys = [
    ...rowActionsBlock.matchAll(/key: '([a-z-]+)',/g),
  ].map((match) => match[1])
  check(
    'the subscription row actions run edit, prolong, show-activity, kill, block-owner, delete',
    subscriptionActionKeys.join(',') ===
      'edit,prolong,show-activity,kill,block-owner,delete',
    `actions: ${subscriptionActionKeys.join(', ') || 'none found'}`,
  )

  check(
    'the Show activity action opens the activity page filtered by that subscription',
    subscriptionsPageSource.includes('/connection-logs?subscriptionId=${subscription.id}'),
    'the drill-down target is missing from the subscriptions page',
  )

  check(
    'the MAG events action opens the journal filtered by that subscription',
    // The journal's own address mirrors the backend's route, so the drill-down is a link into
    // `/admin/mag-events?subscriptionId=…` rather than a second filter on this table.
    subscriptionsPageSource.includes('/mag-events?subscriptionId=${subscription.id}'),
    `drill-down ${
      subscriptionsPageSource.includes('/mag-events?subscriptionId=${subscription.id}')
        ? 'drawn'
        : 'MISSING'
    } on the subscriptions page`,
  )

  // --- The reseller credit log ------------------------------------------------
  //
  // Its own page behind `nav.items.creditLogs`, right after the visit history in the admin's
  // `users` group (the user audit log follows it).
  // The heading, the five column headers, the toolbar controls and the clear command all
  // appear before the first answer - the table itself is drawn empty under a spinner, so the
  // static render is exactly the state to assert them in. The row cells are unreachable here
  // (no row exists), so the actor and tone rules are asserted as functions below.
  const creditLogsHtml = renderToStaticMarkup(
    withRouter('/admin/credit-logs', withQuery(
      <AntApp>
        <CreditLogsPage sectionKey="nav.items.creditLogs" />
      </AntApp>,
    )),
  )

  check(
    'the credit log draws its heading and its table before any entries arrive',
    creditLogsHtml.includes('>Credit logs<') &&
      creditLogsHtml.includes('ant-table') &&
      !creditLogsHtml.includes('nav.items.creditLogs'),
    `rendered ${creditLogsHtml.length} bytes with the log still being fetched`,
  )

  check(
    'the credit log draws its five column headers in order',
    ['Reseller', 'Credits', 'Issued by', 'Description', 'Date'].every((heading) =>
      creditLogsHtml.includes(`>${heading}<`),
    ) &&
      creditLogsHtml.indexOf('>Reseller<') < creditLogsHtml.indexOf('>Credits<') &&
      creditLogsHtml.indexOf('>Credits<') < creditLogsHtml.indexOf('>Issued by<') &&
      creditLogsHtml.indexOf('>Issued by<') < creditLogsHtml.indexOf('>Description<') &&
      creditLogsHtml.indexOf('>Description<') < creditLogsHtml.indexOf('>Date<'),
    `five headings: ${['Reseller', 'Credits', 'Issued by', 'Description', 'Date']
      .map((heading) => `${heading}@${creditLogsHtml.indexOf(`>${heading}<`)}`)
      .join(', ')}`,
  )

  const creditLogCols = [...creditLogsHtml.matchAll(/<col\s[^>]*>/g)].map((m) => m[0])
  const creditLogWidths = creditLogCols.map((col) => /width:(\d+)px/.exec(col)?.[1])
  const creditLogTotal = creditLogWidths.reduce((sum, width) => sum + (Number(width) || 0), 0)
  check(
    'the credit log is fixed-layout with five columns totalling 1190, in order',
    /table-layout:\s*fixed/.test(creditLogsHtml) &&
      creditLogWidths.join(',') === '170,110,170,540,200' &&
      creditLogTotal === 1190,
    `${creditLogCols.length} columns: [${creditLogWidths.join(', ')}] = ${creditLogTotal}px`,
  )

  check(
    'the credit log toolbar runs search, the reseller picker, the width reset and the clear command',
    creditLogsHtml.includes('Search credit logs') &&
      creditLogsHtml.includes('Any reseller') &&
      creditLogsHtml.includes('aria-label="Reset widths"') &&
      creditLogsHtml.includes('Clear all logs') &&
      creditLogsHtml.indexOf('Search credit logs') < creditLogsHtml.indexOf('Any reseller') &&
      creditLogsHtml.indexOf('Any reseller') < creditLogsHtml.indexOf('aria-label="Reset widths"') &&
      creditLogsHtml.indexOf('aria-label="Reset widths"') <
        creditLogsHtml.indexOf('Clear all logs'),
    `rendered ${creditLogsHtml.length} bytes`,
  )

  // The date range is the long control, so it is a row of its own in the card body - after
  // the card head (whose last control is "Clear all logs") and before the table. Index order
  // is enough: head, then the picker, then the table.
  check(
    'the credit log draws the date range on its own row between the card head and the table',
    creditLogsHtml.includes('ant-picker-range') &&
      creditLogsHtml.indexOf('Date from') > creditLogsHtml.indexOf('Clear all logs') &&
      creditLogsHtml.indexOf('Date from') > creditLogsHtml.indexOf('ant-card-body') &&
      creditLogsHtml.indexOf('Date from') < creditLogsHtml.indexOf('ant-table'),
    `range@${creditLogsHtml.indexOf('Date from')}, clear@${creditLogsHtml.indexOf('Clear all logs')}, body@${creditLogsHtml.indexOf('ant-card-body')}, table@${creditLogsHtml.indexOf('ant-table')}`,
  )

  // A range and a reseller in the address are drawn back into the controls and as two closable
  // tags, in that order: the range first, then the reseller. The search is deliberately not a
  // tag - its own box is where it is read and cleared.
  const filteredCreditLogsHtml = renderToStaticMarkup(
    withRouter(
      '/admin/credit-logs?resellerId=44444444-4444-4444-4444-444444444444&dateFrom=2026-05-01T00%3A00%3A00.000Z&dateTo=2026-05-03T23%3A59%3A59.999Z&search=alice',
      withQuery(
        <AntApp>
          <CreditLogsPage sectionKey="nav.items.creditLogs" />
        </AntApp>,
      ),
    ),
  )
  check(
    'an address-borne range and reseller arrive as two closable tags, range first',
    filteredCreditLogsHtml.includes('Date:') &&
      filteredCreditLogsHtml.includes('Reseller:') &&
      filteredCreditLogsHtml.includes('ant-tag-close-icon') &&
      filteredCreditLogsHtml.indexOf('Date:') < filteredCreditLogsHtml.indexOf('Reseller:'),
    `rendered ${filteredCreditLogsHtml.length} bytes with the filters in the address`,
  )

  // The five widths, the storage key and the one key-to-field mapping are pinned by
  // `tests/features/credit-logs/layout.test.ts`; what a render can add is that the table
  // actually reads them, and that the reseller's own link opens that reseller's edit form -
  // the same `${area}/users/${id}` target the accounts table's row action uses - rather than
  // filtering the log again, which a static render cannot click, so the source is read.
  const creditLogsPageSource = readFileSync('src/pages/admin/CreditLogsPage.tsx', 'utf8')
  check(
    'the credit log hands the width hook its own columns, defaults and storage key',
    creditLogsPageSource.includes('keys: COLUMN_KEYS') &&
      creditLogsPageSource.includes('defaults: DEFAULT_COLUMN_WIDTHS') &&
      creditLogsPageSource.includes('storageKey: COLUMN_WIDTHS_KEY') &&
      CREDIT_LOG_WIDTHS_KEY === 'fluxy.creditLogs.columnWidths' &&
      CREDIT_LOG_COLUMN_KEYS.length === 5,
    `${CREDIT_LOG_COLUMN_KEYS.length} columns under ${CREDIT_LOG_WIDTHS_KEY}`,
  )

  check(
    'the reseller cell navigates to that reseller\'s edit form, the same target the users row edit uses',
    /onClick=\{\(\) => navigate\(`\$\{area\}\/users\/\$\{log\.resellerId\}`\)\}/.test(
      creditLogsPageSource,
    ) &&
      /const navigate = useNavigate\(\)/.test(creditLogsPageSource) &&
      /const area = session \? areaForRole\(session\.role\) : ''/.test(creditLogsPageSource) &&
      !/choose\('resellerId', log\.resellerId\)/.test(creditLogsPageSource) &&
      Object.values(CREDIT_LOG_WIDTHS).reduce((sum, width) => sum + width, 0) === 1190,
    'the reseller link does not open /users/{id}',
  )

  check(
    'the credits header sorts by amount while its key stays the word the table draws',
    /key: 'credits'[\s\S]{0,200}?orderOf\('amount'\)/.test(creditLogsPageSource) &&
      creditLogSortFieldOf('credits') === 'amount' &&
      creditLogSortFieldOf('createdAt') === 'createdAt',
    `credits -> ${creditLogSortFieldOf('credits')}, createdAt -> ${creditLogSortFieldOf('createdAt')}`,
  )

  // The clear command is never disabled by data state - only by the permission - because its
  // words are a promise about the log, not about the rows the filters admit. The static render
  // has no profile answer, so it lands in the permission-less branch: the button exists, is
  // disabled, and carries the reason.
  check(
    'the credit log clear command is gated on editResellers and never on the data',
    /granted\.includes\('editResellers'\)/.test(creditLogsPageSource) &&
      /loading=\{clearLogs\.isPending\}/.test(creditLogsPageSource) &&
      /modal\.confirm\(\{/.test(creditLogsPageSource) &&
      creditLogsPageSource.includes("t('creditLogs.clearConfirmBody')") &&
      !/disabled=\{[^}]*logs/.test(creditLogsPageSource),
    creditLogsHtml.includes('Clear all logs')
      ? 'the button is drawn in the permission-less state, disabled with its reason'
      : 'THE CLEAR BUTTON IS MISSING',
  )

  // --- The user audit log ------------------------------------------------------
  //
  // Its own page behind `nav.items.userLogs`, appended after the credit log so the admin's
  // `users` group ends with it. The heading, the five column headers, the toolbar controls and
  // the clear command all appear before the first answer - the table itself is drawn empty
  // under a spinner, so the static render is exactly the state to assert them in. No row
  // exists in a static render, so the record tone is asserted as a function below, and the two
  // source-shape checks (the username link, the gated clear) read the page's own file.
  const userLogsHtml = renderToStaticMarkup(
    withRouter('/admin/user-logs', withQuery(
      <AntApp>
        <UserLogsPage sectionKey="nav.items.userLogs" />
      </AntApp>,
    )),
  )

  check(
    'the user log draws its heading and its table before any entries arrive',
    userLogsHtml.includes('>User logs<') &&
      userLogsHtml.includes('ant-table') &&
      !userLogsHtml.includes('nav.items.userLogs'),
    `rendered ${userLogsHtml.length} bytes with the log still being fetched`,
  )

  check(
    'the user log draws its five column headers in order',
    ['User', 'Event', 'Description', 'Network', 'Date'].every((heading) =>
      userLogsHtml.includes(`>${heading}<`),
    ) &&
      userLogsHtml.indexOf('>User<') < userLogsHtml.indexOf('>Event<') &&
      userLogsHtml.indexOf('>Event<') < userLogsHtml.indexOf('>Description<') &&
      userLogsHtml.indexOf('>Description<') < userLogsHtml.indexOf('>Network<') &&
      userLogsHtml.indexOf('>Network<') < userLogsHtml.indexOf('>Date<'),
    `five headings: ${['User', 'Event', 'Description', 'Network', 'Date']
      .map((heading) => `${heading}@${userLogsHtml.indexOf(`>${heading}<`)}`)
      .join(', ')}`,
  )

  const userLogCols = [...userLogsHtml.matchAll(/<col\s[^>]*>/g)].map((m) => m[0])
  const userLogWidths = userLogCols.map((col) => /width:(\d+)px/.exec(col)?.[1])
  const userLogTotal = userLogWidths.reduce((sum, width) => sum + (Number(width) || 0), 0)
  check(
    'the user log is fixed-layout with five columns totalling 1190, in order',
    /table-layout:\s*fixed/.test(userLogsHtml) &&
      userLogWidths.join(',') === '210,170,400,220,190' &&
      userLogTotal === USER_LOG_TABLE_WIDTH &&
      userLogColumnWidthTotal(USER_LOG_WIDTHS) === USER_LOG_TABLE_WIDTH,
    `${userLogCols.length} columns: [${userLogWidths.join(', ')}] = ${userLogTotal}px`,
  )

  // The toolbar order the specification names: auto-refresh, search, the three pickers, the
  // width reset, the clear command. The auto-refresh control is icon-only, so its position is
  // read from its own accessible name.
  const refreshTipIndex = userLogsHtml.indexOf('Resume auto refresh')
  check(
    'the user log toolbar runs auto-refresh, search, the three pickers, the width reset and the clear command',
    refreshTipIndex !== -1 &&
      userLogsHtml.includes('Search user logs') &&
      userLogsHtml.includes('Any user') &&
      userLogsHtml.includes('Any group') &&
      userLogsHtml.includes('Any event') &&
      userLogsHtml.includes('aria-label="Reset widths"') &&
      userLogsHtml.includes('Clear all logs') &&
      refreshTipIndex < userLogsHtml.indexOf('Search user logs') &&
      userLogsHtml.indexOf('Search user logs') < userLogsHtml.indexOf('Any user') &&
      userLogsHtml.indexOf('Any user') < userLogsHtml.indexOf('Any group') &&
      userLogsHtml.indexOf('Any group') < userLogsHtml.indexOf('Any event') &&
      userLogsHtml.indexOf('Any event') < userLogsHtml.indexOf('aria-label="Reset widths"') &&
      userLogsHtml.indexOf('aria-label="Reset widths"') < userLogsHtml.indexOf('Clear all logs'),
    `refresh@${refreshTipIndex}, search@${userLogsHtml.indexOf('Search user logs')}, clear@${userLogsHtml.indexOf('Clear all logs')}`,
  )

  // The date range is the long control, so it is a row of its own in the card body - after
  // the card head (whose last control is "Clear all logs") and before the table. Index order
  // is enough: head, then the picker, then the table.
  check(
    'the user log draws the date range on its own row between the card head and the table',
    userLogsHtml.includes('ant-picker-range') &&
      userLogsHtml.indexOf('Date from') > userLogsHtml.indexOf('Clear all logs') &&
      userLogsHtml.indexOf('Date from') > userLogsHtml.indexOf('ant-card-body') &&
      userLogsHtml.indexOf('Date from') < userLogsHtml.indexOf('ant-table'),
    `range@${userLogsHtml.indexOf('Date from')}, clear@${userLogsHtml.indexOf('Clear all logs')}, body@${userLogsHtml.indexOf('ant-card-body')}, table@${userLogsHtml.indexOf('ant-table')}`,
  )

  // A range, one of each list filter and the three network filters in the address are drawn
  // back into the controls and as closable tags, in that order: the range first, then the
  // network filters (country, ip, isp - the connection log's tag order), then the user, the
  // group and the event. The three list filters are repeated parameters, so this also proves
  // the read-back handles more than one element; the network ones are the values a click on the
  // shared cell writes.
  const filteredUserLogsHtml = renderToStaticMarkup(
    withRouter(
      '/admin/user-logs?userIds=44444444-4444-4444-4444-444444444444&groupIds=22222222-2222-2222-2222-222222222222&recordTypes=login&recordTypes=logout&ip=203.0.113.9&country=DE&isp=Example%20ISP&dateFrom=2026-05-01T00%3A00%3A00.000Z&dateTo=2026-05-03T23%3A59%3A59.999Z&search=alice',
      withQuery(
        <AntApp>
          <UserLogsPage sectionKey="nav.items.userLogs" />
        </AntApp>,
      ),
    ),
  )
  check(
    'an address-borne range, three network filters and three list filters arrive as closable tags, range first',
    filteredUserLogsHtml.includes('Date:') &&
      filteredUserLogsHtml.includes('Country:') &&
      filteredUserLogsHtml.includes('IP:') &&
      filteredUserLogsHtml.includes('Provider:') &&
      filteredUserLogsHtml.includes('User:') &&
      filteredUserLogsHtml.includes('Group:') &&
      filteredUserLogsHtml.includes('Event:') &&
      filteredUserLogsHtml.includes('ant-tag-close-icon') &&
      filteredUserLogsHtml.indexOf('Date:') < filteredUserLogsHtml.indexOf('Country:') &&
      filteredUserLogsHtml.indexOf('Country:') < filteredUserLogsHtml.indexOf('IP:') &&
      filteredUserLogsHtml.indexOf('IP:') < filteredUserLogsHtml.indexOf('Provider:') &&
      filteredUserLogsHtml.indexOf('Provider:') < filteredUserLogsHtml.indexOf('User:') &&
      filteredUserLogsHtml.indexOf('User:') < filteredUserLogsHtml.indexOf('Group:') &&
      filteredUserLogsHtml.indexOf('Group:') < filteredUserLogsHtml.indexOf('Event:'),
    `rendered ${filteredUserLogsHtml.length} bytes with the filters in the address: ${[
      'Date:',
      'Country:',
      'IP:',
      'Provider:',
      'User:',
      'Group:',
      'Event:',
    ]
      .map((tag) => `${tag}@${filteredUserLogsHtml.indexOf(tag)}`)
      .join(', ')}`,
  )

  const userLogsPageSource = readFileSync('src/pages/admin/UserLogsPage.tsx', 'utf8')
  check(
    'the user log hands the width hook its own columns, defaults and storage key',
    userLogsPageSource.includes('keys: COLUMN_KEYS') &&
      userLogsPageSource.includes('defaults: DEFAULT_COLUMN_WIDTHS') &&
      userLogsPageSource.includes('storageKey: COLUMN_WIDTHS_KEY') &&
      USER_LOG_WIDTHS_KEY === 'fluxy.userLogs.columnWidths' &&
      USER_LOG_COLUMN_KEYS.length === 5,
    `${USER_LOG_COLUMN_KEYS.length} columns under ${USER_LOG_WIDTHS_KEY}`,
  )

  // The username cell has two branches and no row reaches either in a static render, so the
  // shape is read from the page: a link to `${area}/users/${row.accountId}` when the row names
  // an account, and plain text otherwise. Building a link from a null id would open the form
  // at `/users/null` for exactly the rows the fallback exists for.
  //
  // The network cell is read the same way, and now with its click-through: this endpoint takes
  // the same `ip`/`country`/`isp` parameters as the connection log, so the cell receives
  // `onFilter={choose}` - the one writer that drops `page` - exactly as `ConnectionLogsPage`
  // hands it over. Without the callback the three facts would render as plain text on a screen
  // whose server does accept the filter, which is the regression this pins.
  check(
    'the user-log username cell links only when the row names an account',
    /if \(row\.accountId\)/.test(userLogsPageSource) &&
      /onClick=\{\(\) => navigate\(`\$\{area\}\/users\/\$\{row\.accountId\}`\)\}/.test(
        userLogsPageSource,
      ) &&
      /<Typography\.Text>\{row\.username \?\? DASH\}<\/Typography\.Text>/.test(userLogsPageSource) &&
      /const area = session \? areaForRole\(session\.role\) : ''/.test(userLogsPageSource),
    'the username cell draws a link for an account and plain text otherwise',
  )

  check(
    'the user-log network cell is the click-through: it writes its three filters through choose',
    /<ConnectionNetworkCell log=\{row\} locale=\{locale\} onFilter=\{choose\} \/>/.test(
      userLogsPageSource,
    ) &&
      /const choose = \(key: string, value: string \| null \| undefined\) =>[\s\S]{0,120}?withFilter\(searchParams, key, value\), \{ replace: true \}\)/.test(
        userLogsPageSource,
      ) &&
      /const \{ ip, country, isp \} = networkFilterParams\(searchParams\)/.test(userLogsPageSource) &&
      // The wire param is `country`, not the response field `countryCode`: a reader that sent
      // the response spelling would be ignored by the endpoint and the table would stay wide.
      /country: params\.get\('country'\)/.test(
        readFileSync('src/features/user-logs/networkFilters.ts', 'utf8'),
      ),
    'the network cell is not wired to choose, or the three filters are not read from the address',
  )

  check(
    'the record-type header sorts by recordType while its key stays the word the table draws',
    /key: 'recordType'[\s\S]{0,400}?orderOf\('recordType'\)/.test(userLogsPageSource) &&
      userLogSortFieldOf('recordType') === 'recordType' &&
      userLogSortFieldOf('createdAt') === 'createdAt',
    `recordType -> ${userLogSortFieldOf('recordType')}, createdAt -> ${userLogSortFieldOf('createdAt')}`,
  )

  // The clear command is never disabled by data state - only by the permission - because its
  // words are a promise about the log, not about the rows the filters admit. The static render
  // has no profile answer, so it lands in the permission-less branch: the button exists, is
  // disabled, and carries the reason.
  check(
    'the user log clear command is gated on viewUserLogs and never on the data',
    /granted\.includes\('viewUserLogs'\)/.test(userLogsPageSource) &&
      /loading=\{clearLogs\.isPending\}/.test(userLogsPageSource) &&
      /modal\.confirm\(\{/.test(userLogsPageSource) &&
      userLogsPageSource.includes("t('userLogs.clearConfirmBody')") &&
      !/disabled=\{[^}]*logs/.test(userLogsPageSource),
    userLogsHtml.includes('Clear all logs')
      ? 'the button is drawn in the permission-less state, disabled with its reason'
      : 'THE CLEAR BUTTON IS MISSING',
  )

  // The refusal is answered only after the profile answers: an unanswered profile is "not
  // known yet" and draws the ordinary shell, which is why every check above (rendered from a
  // fresh client with no profile answer) sees the toolbar. Seeding a profile **without** the
  // grant is the one state that reaches the refusal branch, and it must draw the card, its
  // heading and the sentence - and nothing else: no toolbar, no filters, no table.
  const refusedUserLogsHtml = renderToStaticMarkup(
    withRouter('/admin/user-logs', withSeededQuery(
      ['profile'],
      {
        username: 'probeadmin',
        email: 'probeadmin@localhost',
        role: 'Admin',
        permissions: [],
        credits: 0,
        timeZone: null,
        geoProtectionEnabled: false,
        bindSessionToIp: false,
        allowedIps: [],
        allowedCountry: null,
        allowedAutonomousSystemNumber: null,
      },
      <AntApp>
        <UserLogsPage sectionKey="nav.items.userLogs" />
      </AntApp>,
    )),
  )
  check(
    'a profile without viewUserLogs draws the refusal alert alone, under the card title',
    refusedUserLogsHtml.includes('>User logs<') &&
      refusedUserLogsHtml.includes('Your group does not allow this.') &&
      refusedUserLogsHtml.includes('ant-alert') &&
      refusedUserLogsHtml.includes('page-card') &&
      !refusedUserLogsHtml.includes('ant-table') &&
      !refusedUserLogsHtml.includes('Search user logs') &&
      !refusedUserLogsHtml.includes('ant-picker-range') &&
      !refusedUserLogsHtml.includes('Clear all logs'),
    refusedUserLogsHtml.includes('ant-alert')
      ? `rendered ${refusedUserLogsHtml.length} bytes of the refusal`
      : 'THE REFUSAL ALERT IS MISSING',
  )

  // Translations, before the menu block below reads the group's order: a card title, a rail
  // label and all ten record-type words have to exist in both bundles, and a key present in
  // neither is the one failure no type or render can see.
  check(
    'the user-log menu entry, card title and record types are all translated',
    i18n.getResource('en', 'translation', 'nav.items.userLogs') === 'User logs' &&
      i18n.getResource('ru', 'translation', 'nav.items.userLogs') === 'Журнал действий' &&
      i18n.getResource('en', 'translation', 'userLogs.title') === 'User logs' &&
      i18n.getResource('ru', 'translation', 'userLogs.title') === 'Журнал действий' &&
      USER_LOG_RECORD_TYPES.every(
        (type) =>
          i18n.getResource('en', 'translation', `userLogs.types.${type}`) &&
          i18n.getResource('ru', 'translation', `userLogs.types.${type}`),
      ) &&
      USER_LOG_RECORD_TYPES.length === 10,
    `en ${i18n.getResource('en', 'translation', 'nav.items.userLogs')}, ru ${i18n.getResource('ru', 'translation', 'nav.items.userLogs')}, ${USER_LOG_RECORD_TYPES.length} types`,
  )

  // The two row rules, as functions: a static render draws no cell, so the green/red tag and
  // the ten-value vocabulary are only reachable here.
  check(
    'a record tag is green for a successful attempt and red for a refused one',
    recordTone({ isSuccess: true }) === 'success' &&
      recordTone({ isSuccess: false }) === 'error',
    `success ${recordTone({ isSuccess: true })}, failure ${recordTone({ isSuccess: false })}`,
  )

  // The menu entry: right after the visit history, an address of its own, and a real page
  // behind the loader rather than the placeholder.
  const creditLogGroup = NAVIGATION.Admin.find((group) => group.key === 'users')
  const creditLogItemKeys = (creditLogGroup?.items ?? []).map((item) => item.key)
  const creditLogItem = creditLogGroup?.items.find((item) => item.key === 'creditLogs')
  const creditLogModule = creditLogItem?.page ? await creditLogItem.page() : null
  check(
    'the admin users group carries the credit-log entry right after the sessions, pointing at the real page',
    creditLogItemKeys.indexOf('creditLogs') === creditLogItemKeys.indexOf('adminSessions') + 1 &&
      menuPath('Admin', 'credit-logs') === '/admin/credit-logs' &&
      creditLogModule?.default === CreditLogsPage,
    `items: ${creditLogItemKeys.join(', ')}; opens ${menuPath('Admin', 'credit-logs')}`,
  )

  // The user-log entry: appended after the credit log, so the group ends with the wider record.
  const userLogGroup = NAVIGATION.Admin.find((group) => group.key === 'users')
  const userLogItemKeys = (userLogGroup?.items ?? []).map((item) => item.key)
  const userLogItem = userLogGroup?.items.find((item) => item.key === 'userLogs')
  const userLogModule = userLogItem?.page ? await userLogItem.page() : null
  check(
    'the admin users group ends with the user-log entry, right after the credit log',
    userLogItemKeys[userLogItemKeys.length - 1] === 'userLogs' &&
      userLogItemKeys.indexOf('userLogs') === userLogItemKeys.indexOf('creditLogs') + 1 &&
      userLogItemKeys.indexOf('creditLogs') === userLogItemKeys.indexOf('adminSessions') + 1 &&
      menuPath('Admin', 'user-logs') === '/admin/user-logs' &&
      userLogModule?.default === UserLogsPage,
    `items: ${userLogItemKeys.join(', ')}; opens ${menuPath('Admin', 'user-logs')}`,
  )

  check(
    'the credit-log menu entry and card title are both translated',
    i18n.getResource('en', 'translation', 'nav.items.creditLogs') === 'Credit logs' &&
      i18n.getResource('ru', 'translation', 'nav.items.creditLogs') === 'Журнал кредитов' &&
      i18n.getResource('en', 'translation', 'creditLogs.title') === 'Credit logs' &&
      i18n.getResource('ru', 'translation', 'creditLogs.title') === 'Журнал кредитов' &&
      i18n.getResource('en', 'translation', 'users.creditsLabel') === 'Credits' &&
      i18n.getResource('ru', 'translation', 'users.creditsLabel') === 'Кредиты',
    `en ${i18n.getResource('en', 'translation', 'nav.items.creditLogs')}, ru ${i18n.getResource('ru', 'translation', 'nav.items.creditLogs')}`,
  )

  // The two row rules, as functions: a static render draws no cell, so the "Issued by" name
  // and the green/red tone are only reachable here. An issuer on the row wins, whatever the
  // sign; a negative row with no issuer names the reseller (the future reseller-spent row);
  // a non-negative row with no issuer is an em dash (the deleted-issuer case).
  const probeCreditLog = {
    id: '55555555-5555-5555-5555-555555555555',
    resellerId: '44444444-4444-4444-4444-444444444444',
    resellerUsername: 'reseller',
    issuedById: '66666666-6666-6666-6666-666666666666',
    issuedByUsername: 'root',
    amount: 10,
    description: 'Credits changed: 0 → 10',
    createdAt: '2026-05-01T10:00:00.000Z',
  }
  check(
    'the Issued by cell names the issuer on either sign and the reseller only on an issuer-less expense',
    creditLogActorName(probeCreditLog) === 'root' &&
      creditLogActorName({ ...probeCreditLog, amount: -5 }) === 'root' &&
      creditLogActorName({
        ...probeCreditLog,
        amount: -5,
        issuedById: null,
        issuedByUsername: null,
      }) === 'reseller' &&
      creditLogActorName({
        ...probeCreditLog,
        amount: 0,
      }) === 'root' &&
      creditLogActorName({
        ...probeCreditLog,
        issuedById: null,
        issuedByUsername: null,
      }) === '—',
    `issued ${creditLogActorName(probeCreditLog)}, decreased ${creditLogActorName({
      ...probeCreditLog,
      amount: -5,
    })}, spent ${creditLogActorName({
      ...probeCreditLog,
      amount: -5,
      issuedById: null,
      issuedByUsername: null,
    })}, deleted issuer ${creditLogActorName({
      ...probeCreditLog,
      issuedById: null,
      issuedByUsername: null,
    })}`,
  )

  check(
    'the amount is green only when positive, and red for spent and for zero',
    creditLogTone(10) === 'success' &&
      creditLogTone(-10) === 'danger' &&
      creditLogTone(0) === 'danger',
    `+10 ${creditLogTone(10)}, -10 ${creditLogTone(-10)}, 0 ${creditLogTone(0)}`,
  )

  // --- The failed API request log ----------------------------------------------
  //
  // Its own page behind `nav.items.apiRequestFailures`, last in the `blacklists` group. The
  // heading, the five column headers and the toolbar all appear before the first answer - the
  // table is drawn empty under a spinner - so a static render is exactly the state to assert
  // them in. No row exists in a static render, so the reason tag's colour is asserted as a
  // function, and the source-shape checks read the page's own file.
  const apiRequestFailuresHtml = renderToStaticMarkup(
    withRouter('/admin/api-request-failures', withQuery(
      <AntApp>
        <ApiRequestFailuresPage sectionKey="nav.items.apiRequestFailures" />
      </AntApp>,
    )),
  )

  check(
    'the failed-request log draws its heading and its table before any entries arrive',
    apiRequestFailuresHtml.includes('>Failed API requests<') &&
      apiRequestFailuresHtml.includes('ant-table') &&
      !apiRequestFailuresHtml.includes('nav.items.apiRequestFailures'),
    `rendered ${apiRequestFailuresHtml.length} bytes with the log still being fetched`,
  )

  check(
    'the failed-request log draws its five column headers in order',
    ['Subscription', 'Content', 'Reason', 'Network', 'Date'].every((heading) =>
      apiRequestFailuresHtml.includes(`>${heading}<`),
    ) &&
      apiRequestFailuresHtml.indexOf('>Subscription<') <
        apiRequestFailuresHtml.indexOf('>Content<') &&
      apiRequestFailuresHtml.indexOf('>Content<') <
        apiRequestFailuresHtml.indexOf('>Reason<') &&
      apiRequestFailuresHtml.indexOf('>Reason<') <
        apiRequestFailuresHtml.indexOf('>Network<') &&
      apiRequestFailuresHtml.indexOf('>Network<') <
        apiRequestFailuresHtml.indexOf('>Date<'),
    `five headings: ${['Subscription', 'Content', 'Reason', 'Network', 'Date']
      .map((heading) => `${heading}@${apiRequestFailuresHtml.indexOf(`>${heading}<`)}`)
      .join(', ')}`,
  )

  const apiRequestFailureCols = [...apiRequestFailuresHtml.matchAll(/<col\s[^>]*>/g)].map(
    (m) => m[0],
  )
  const apiRequestFailureWidths = apiRequestFailureCols.map(
    (col) => /width:(\d+)px/.exec(col)?.[1],
  )
  const apiRequestFailureTotal = apiRequestFailureWidths.reduce(
    (sum, width) => sum + (Number(width) || 0),
    0,
  )
  check(
    'the failed-request log is fixed-layout with five columns totalling 1190, in order',
    /table-layout:\s*fixed/.test(apiRequestFailuresHtml) &&
      apiRequestFailureWidths.join(',') === '220,320,200,250,200' &&
      apiRequestFailureTotal === API_REQUEST_FAILURE_TABLE_WIDTH &&
      apiRequestFailureColumnWidthTotal(API_REQUEST_FAILURE_WIDTHS) ===
        API_REQUEST_FAILURE_TABLE_WIDTH &&
      API_REQUEST_FAILURE_TABLE_WIDTH === 1190,
    `${apiRequestFailureCols.length} columns: [${apiRequestFailureWidths.join(', ')}] = ${apiRequestFailureTotal}px`,
  )

  // The toolbar order the specification names: auto-refresh, search, the reason picker, the
  // width reset, the clear command. Auto-refresh is ON by default, so the icon-only control's
  // accessible name in a fresh render is "Pause ...".
  const apiRefreshTipIndex = apiRequestFailuresHtml.indexOf('Pause auto refresh')
  check(
    'the failed-request log toolbar runs auto-refresh (on by default), search, the reason picker, the width reset and the clear command',
    apiRefreshTipIndex !== -1 &&
      apiRequestFailuresHtml.includes('Search failed API requests') &&
      apiRequestFailuresHtml.includes('Any reason') &&
      apiRequestFailuresHtml.includes('aria-label="Reset widths"') &&
      apiRequestFailuresHtml.includes('Clear all logs') &&
      apiRefreshTipIndex < apiRequestFailuresHtml.indexOf('Search failed API requests') &&
      apiRequestFailuresHtml.indexOf('Search failed API requests') <
        apiRequestFailuresHtml.indexOf('Any reason') &&
      apiRequestFailuresHtml.indexOf('Any reason') <
        apiRequestFailuresHtml.indexOf('aria-label="Reset widths"') &&
      apiRequestFailuresHtml.indexOf('aria-label="Reset widths"') <
        apiRequestFailuresHtml.indexOf('Clear all logs'),
    `refresh@${apiRefreshTipIndex}, search@${apiRequestFailuresHtml.indexOf('Search failed API requests')}, picker@${apiRequestFailuresHtml.indexOf('Any reason')}, clear@${apiRequestFailuresHtml.indexOf('Clear all logs')}`,
  )

  // The date range is the long control, so it is a row of its own in the card body - after
  // the card head (whose last control is "Clear all logs") and before the tag row and the
  // table. Index order is enough: head, then the picker, then the table.
  check(
    'the failed-request log draws the date range on its own row between the card head and the table',
    apiRequestFailuresHtml.includes('ant-picker-range') &&
      apiRequestFailuresHtml.indexOf('Date from') > apiRequestFailuresHtml.indexOf('Clear all logs') &&
      apiRequestFailuresHtml.indexOf('Date from') > apiRequestFailuresHtml.indexOf('ant-card-body') &&
      apiRequestFailuresHtml.indexOf('Date from') < apiRequestFailuresHtml.indexOf('ant-table'),
    `range@${apiRequestFailuresHtml.indexOf('Date from')}, clear@${apiRequestFailuresHtml.indexOf('Clear all logs')}, body@${apiRequestFailuresHtml.indexOf('ant-card-body')}, table@${apiRequestFailuresHtml.indexOf('ant-table')}`,
  )

  const apiRequestFailuresPageSource = readFileSync(
    'src/pages/admin/ApiRequestFailuresPage.tsx',
    'utf8',
  )

  // The reason picker is a multi-select: nothing is a tag until something is chosen, and one
  // picked reason contributes one tag. The date range leads the tag row - the connection and
  // user logs' order - and the three network filters follow it, ahead of the picker's tag.
  const filteredApiRequestFailuresHtml = renderToStaticMarkup(
    withRouter(
      '/admin/api-request-failures?dateFrom=2026-05-01T00%3A00%3A00.000Z&dateTo=2026-05-03T23%3A59%3A59.999Z&reasons=auth_failed&reasons=user_blocked&ip=203.0.113.9&country=DE&isp=Example%20ISP&search=token',
      withQuery(
        <AntApp>
          <ApiRequestFailuresPage sectionKey="nav.items.apiRequestFailures" />
        </AntApp>,
      ),
    ),
  )
  check(
    'an address-borne range, the three network filters and the reason filter arrive as closable tags, range first',
    filteredApiRequestFailuresHtml.includes('Date:') &&
      filteredApiRequestFailuresHtml.includes('Country:') &&
      filteredApiRequestFailuresHtml.includes('IP:') &&
      filteredApiRequestFailuresHtml.includes('Provider:') &&
      filteredApiRequestFailuresHtml.includes('Reason:') &&
      filteredApiRequestFailuresHtml.includes('Authentication failed') &&
      filteredApiRequestFailuresHtml.includes('User blocked') &&
      filteredApiRequestFailuresHtml.includes('ant-tag-close-icon') &&
      // The picker is its own row above the tag row, and the tag row sits above the table.
      filteredApiRequestFailuresHtml.indexOf('ant-picker-range') <
        filteredApiRequestFailuresHtml.indexOf('Date:') &&
      filteredApiRequestFailuresHtml.indexOf('Date:') <
        filteredApiRequestFailuresHtml.indexOf('ant-table') &&
      filteredApiRequestFailuresHtml.indexOf('Date:') <
        filteredApiRequestFailuresHtml.indexOf('Country:') &&
      filteredApiRequestFailuresHtml.indexOf('Country:') <
        filteredApiRequestFailuresHtml.indexOf('IP:') &&
      filteredApiRequestFailuresHtml.indexOf('IP:') <
        filteredApiRequestFailuresHtml.indexOf('Provider:') &&
      filteredApiRequestFailuresHtml.indexOf('Provider:') <
        filteredApiRequestFailuresHtml.indexOf('Reason:'),
    `rendered ${filteredApiRequestFailuresHtml.length} bytes with the filters in the address: ${[
      'Date:',
      'Country:',
      'IP:',
      'Provider:',
      'Reason:',
    ]
      .map((tag) => `${tag}@${filteredApiRequestFailuresHtml.indexOf(tag)}`)
      .join(', ')}`,
  )

  // Both bounds are one control over two parameters, so closing its tag must clear both. The
  // clear is one `chooseRange(null)` call, which writes both keys: two separate `choose` calls
  // would read the half-written range in between. No click runs under `renderToStaticMarkup`,
  // so this half is read off the page's source - the same treatment the connection log gets.
  check(
    'the failed-request range tag clears both bounds in one navigation',
    /key: 'dateRange',[\s\S]{0,400}?clear: \(\) => chooseRange\(null\)/.test(
      apiRequestFailuresPageSource,
    ) &&
      /const chooseRange = \(value[\s\S]{0,600}?withFilter\(searchParams, DATE_FROM_KEY[\s\S]{0,300}?withFilter\(withFrom, DATE_TO_KEY/.test(
        apiRequestFailuresPageSource,
      ) &&
      /dateRangeParams\(searchParams, DATE_FROM_KEY, DATE_TO_KEY\)/.test(
        apiRequestFailuresPageSource,
      ),
    'the range tag, the single-write range update and the address read are not all present',
  )

  check(
    'the failed-request log hands the width hook its own columns, defaults and storage key',
    apiRequestFailuresPageSource.includes('keys: COLUMN_KEYS') &&
      apiRequestFailuresPageSource.includes('defaults: DEFAULT_COLUMN_WIDTHS') &&
      apiRequestFailuresPageSource.includes('storageKey: COLUMN_WIDTHS_KEY') &&
      API_REQUEST_FAILURE_COLUMN_KEYS.length === 5,
    `${API_REQUEST_FAILURE_COLUMN_KEYS.length} columns`,
  )

  // The content cell is a stored snapshot: a primary name with its opaque identifier muted
  // underneath, and one dash when the group is null. No row reaches either branch in a static
  // render, so the shape is read from the page.
  check(
    'the failed-request content cell draws the stored name over its muted identifier, or one dash',
    /const contentCell = \(row: ApiRequestFailureRow\)/.test(apiRequestFailuresPageSource) &&
      /if \(!row\.contentName && !row\.contentId\)/.test(apiRequestFailuresPageSource) &&
      /<Typography\.Text type="secondary">\{DASH\}<\/Typography\.Text>/.test(
        apiRequestFailuresPageSource,
      ) &&
      /row\.contentName \?\? DASH/.test(apiRequestFailuresPageSource) &&
      /row\.contentId/.test(apiRequestFailuresPageSource),
    'the content cell is not the name-over-id snapshot',
  )

  check(
    'the failed-request network cell is the click-through: it writes its three filters through choose',
    /<ConnectionNetworkCell log=\{row\} locale=\{locale\} onFilter=\{choose\} \/>/.test(
      apiRequestFailuresPageSource,
    ) &&
      /const choose = \(key: string, value: string \| null \| undefined\) =>[\s\S]{0,120}?withFilter\(searchParams, key, value\), \{ replace: true \}\)/.test(
        apiRequestFailuresPageSource,
      ) &&
      /const \{ ip, country, isp \} = networkFilterParams\(searchParams\)/.test(
        apiRequestFailuresPageSource,
      ) &&
      // The cell and its read-back live in other features, imported at PAGE level: a page may
      // import across features, a feature may not.
      !/from '\.\.\/connection-logs\//.test(
        readFileSync('src/features/api-request-failures/api.ts', 'utf8'),
      ) &&
      !/from '\.\.\/user-logs\//.test(
        readFileSync('src/features/api-request-failures/api.ts', 'utf8'),
      ),
    'the network cell is not wired to choose, or the three filters are not read from the address',
  )

  check(
    'the reason header sorts by reason while its key stays the word the table draws',
    /key: 'reason'[\s\S]{0,400}?orderOf\('reason'\)/.test(apiRequestFailuresPageSource) &&
      apiRequestFailureSortFieldOf('reason') === 'reason' &&
      apiRequestFailureSortFieldOf('createdAt') === 'createdAt',
    `reason -> ${apiRequestFailureSortFieldOf('reason')}, createdAt -> ${apiRequestFailureSortFieldOf('createdAt')}`,
  )

  // The clear command is never disabled by data state - only by the permission. The static
  // render has no profile answer, so it lands in the permission-less branch: the button exists,
  // is disabled, and carries the reason.
  check(
    'the failed-request clear command is gated on viewApiRequestFailures and never on the data',
    /granted\.includes\('viewApiRequestFailures'\)/.test(apiRequestFailuresPageSource) &&
      /loading=\{clearLog\.isPending\}/.test(apiRequestFailuresPageSource) &&
      /modal\.confirm\(\{/.test(apiRequestFailuresPageSource) &&
      apiRequestFailuresPageSource.includes("t('apiRequestFailures.clearConfirmBody')") &&
      !/disabled=\{[^}]*log/.test(apiRequestFailuresPageSource),
    apiRequestFailuresHtml.includes('Clear all logs')
      ? 'the button is drawn in the permission-less state, disabled with its reason'
      : 'THE CLEAR BUTTON IS MISSING',
  )

  // Auto-refresh is ON by default here, unlike the user log, and the interval is 30 seconds.
  check(
    'the failed-request log starts with auto-refresh on at thirty seconds',
    /const AUTO_REFRESH_MS = 30_000/.test(apiRequestFailuresPageSource) &&
      /useState\(true\)/.test(apiRequestFailuresPageSource) &&
      /const refreshRef = useRef<\(\) => void>\(\(\) => \{\}\)/.test(
        apiRequestFailuresPageSource,
      ) &&
      apiRequestFailuresHtml.includes('Refreshes every 30 seconds'),
    'auto-refresh is not on by default at 30 s',
  )

  // The six reasons as a runtime vocabulary, and the tag tone as a function: no row reaches a
  // static render, so a wrong colour would keep compiling.
  check(
    'the failed-request reason vocabulary is all six wire names, in server order',
    API_REQUEST_FAILURE_REASONS.join(',') ===
      'unknown_id,geo_ip_violation,auth_failed,too_many_connections,subscription_expired,user_blocked',
    API_REQUEST_FAILURE_REASONS.join(', '),
  )

  check(
    'a reason tag is toned by the refusal kind, and every member answers one of antd’s status words',
    API_REQUEST_FAILURE_REASONS.every((reason) =>
      ['error', 'warning', 'processing', 'default'].includes(reasonTone(reason)),
    ) &&
      reasonTone('auth_failed') === 'error' &&
      reasonTone('geo_ip_violation') === 'warning' &&
      reasonTone('too_many_connections') === 'processing' &&
      reasonTone('user_blocked') === 'default',
    API_REQUEST_FAILURE_REASONS.map((reason) => `${reason}:${reasonTone(reason)}`).join(', '),
  )

  // The refusal is answered only after the profile answers, so seeding a profile **without**
  // the grant is the one state that reaches it: the card, its heading and the sentence, and
  // nothing else.
  const refusedApiRequestFailuresHtml = renderToStaticMarkup(
    withRouter('/admin/api-request-failures', withSeededQuery(
      ['profile'],
      {
        username: 'probeadmin',
        email: 'probeadmin@localhost',
        role: 'Admin',
        permissions: [],
        credits: 0,
        timeZone: null,
        geoProtectionEnabled: false,
        bindSessionToIp: false,
        allowedIps: [],
        allowedCountry: null,
        allowedAutonomousSystemNumber: null,
      },
      <AntApp>
        <ApiRequestFailuresPage sectionKey="nav.items.apiRequestFailures" />
      </AntApp>,
    )),
  )
  check(
    'a profile without viewApiRequestFailures draws the refusal alert alone, under the card title',
    refusedApiRequestFailuresHtml.includes('>Failed API requests<') &&
      refusedApiRequestFailuresHtml.includes('Your group does not allow this.') &&
      refusedApiRequestFailuresHtml.includes('ant-alert') &&
      refusedApiRequestFailuresHtml.includes('page-card') &&
      !refusedApiRequestFailuresHtml.includes('ant-table') &&
      !refusedApiRequestFailuresHtml.includes('Search failed API requests') &&
      !refusedApiRequestFailuresHtml.includes('Clear all logs') &&
      !refusedApiRequestFailuresHtml.includes('nav.items.apiRequestFailures'),
    refusedApiRequestFailuresHtml.includes('ant-alert')
      ? `rendered ${refusedApiRequestFailuresHtml.length} bytes of the refusal`
      : 'THE REFUSAL ALERT IS MISSING',
  )

  // Translations of the rail label, the card title, all six reasons and the one permission: a
  // key present in neither bundle is the one failure no type or render can see.
  check(
    'the failed-request menu entry, card title, reasons and permission are all translated',
    i18n.getResource('en', 'translation', 'nav.items.apiRequestFailures') ===
      'Failed API requests' &&
      i18n.getResource('ru', 'translation', 'nav.items.apiRequestFailures') ===
        'Неудачные запросы' &&
      i18n.getResource('en', 'translation', 'apiRequestFailures.title') ===
        'Failed API requests' &&
      i18n.getResource('ru', 'translation', 'apiRequestFailures.title') ===
        'Неудачные запросы API' &&
      i18n.getResource('en', 'translation', 'permissions.viewApiRequestFailures') &&
      i18n.getResource('ru', 'translation', 'permissions.viewApiRequestFailures') &&
      // The date range's accessible name, the two placeholders and their tag labels are the
      // literals the page calls, so a missing key here is the raw key drawn on screen.
      i18n.getResource('en', 'translation', 'apiRequestFailures.filterDateRange') ===
        'Date range' &&
      i18n.getResource('ru', 'translation', 'apiRequestFailures.filterDateRange') ===
        'Интервал дат' &&
      i18n.getResource('en', 'translation', 'apiRequestFailures.filterDateFrom') === 'Date from' &&
      i18n.getResource('ru', 'translation', 'apiRequestFailures.filterDateFrom') === 'Дата с' &&
      i18n.getResource('en', 'translation', 'apiRequestFailures.filterDateTo') === 'Date to' &&
      i18n.getResource('ru', 'translation', 'apiRequestFailures.filterDateTo') === 'Дата по' &&
      i18n.getResource('en', 'translation', 'apiRequestFailures.filters.dateRange') ===
        'Date: {{from}} – {{to}}' &&
      i18n.getResource('ru', 'translation', 'apiRequestFailures.filters.dateRange') ===
        'Дата: {{from}} – {{to}}' &&
      API_REQUEST_FAILURE_REASONS.every(
        (reason) =>
          i18n.getResource('en', 'translation', `apiRequestFailures.reasons.${reason}`) &&
          i18n.getResource('ru', 'translation', `apiRequestFailures.reasons.${reason}`),
      ) &&
      API_REQUEST_FAILURE_REASONS.length === 6,
    `en ${i18n.getResource('en', 'translation', 'nav.items.apiRequestFailures')}, ru ${i18n.getResource('ru', 'translation', 'nav.items.apiRequestFailures')}, ${API_REQUEST_FAILURE_REASONS.length} reasons`,
  )

  // The menu entry: last in the `blacklists` group, the derived address, and a real page behind
  // the loader rather than the placeholder.
  const apiRequestFailureGroup = NAVIGATION.Admin.find(
    (group) => group.key === 'blacklists',
  )
  const apiRequestFailureItemKeys = (apiRequestFailureGroup?.items ?? []).map(
    (item) => item.key,
  )
  const apiRequestFailureItem = apiRequestFailureGroup?.items.find(
    (item) => item.key === 'apiRequestFailures',
  )
  const apiRequestFailureModule = apiRequestFailureItem?.page
    ? await apiRequestFailureItem.page()
    : null
  const adminItemKeys = flatItems('Admin').map((item) => item.key)
  check(
    'the blacklists group ends with the failed-request entry, opening the real page at its mirrored address',
    apiRequestFailureItemKeys[apiRequestFailureItemKeys.length - 1] ===
      'apiRequestFailures' &&
      // Mirroring `admin/api-request-failures` rather than nesting under `blacklists/` - the
      // credit-logs/tariff-plans precedent: one resource named the same way by menu, page and
      // endpoint, while the «Чёрные списки» heading stays a rail grouping only.
      menuPath('Admin', 'api-request-failures') === '/admin/api-request-failures' &&
      apiRequestFailureModule?.default === ApiRequestFailuresPage &&
      adminItemKeys.includes('apiRequestFailures'),
    `items: ${apiRequestFailureItemKeys.join(', ')}; opens ${menuPath('Admin', 'api-request-failures')}`,
  )

  // --- The mass source replacement ---------------------------------------------
  //
  // A form, not a table: the screen rewrites every playback address and shows no rows. The
  // item key is deliberately not prefixed with the section's (`sourceReplacement`, not
  // `videoStreamsSourceReplacement`), so the assertion the section's four generated entries
  // rest on - the `videoStreams*` prefix check above - stays exactly as it is.
  //
  // What a fresh render can assert: the card title is drawn before the profile answers, both
  // fields are there with their labels, the replacement's rule is stated, and the submit is
  // disabled without any accusation while the grants are unknown. The refusal and the
  // permitted states need a seeded profile, so they are seeded below the way the stream
  // problem log seeds one.
  const sourceReplacementHtml = renderToStaticMarkup(
    withRouter('/admin/source-replacement', withQuery(
      <AntApp>
        <SourceReplacementPage sectionKey="nav.items.sourceReplacement" />
      </AntApp>,
    )),
  )

  check(
    'the source replacement draws its card title and both fields before the profile answers',
    sourceReplacementHtml.includes('>Source replacement<') &&
      sourceReplacementHtml.includes('>Find<') &&
      sourceReplacementHtml.includes('>Replace with<') &&
      sourceReplacementHtml.includes('id="find"') &&
      sourceReplacementHtml.includes('id="replace"') &&
      !sourceReplacementHtml.includes('nav.items.sourceReplacement'),
    `rendered ${sourceReplacementHtml.length} bytes with the profile still being fetched`,
  )

  // The two fields are one per row: antd's `ant-form-item-row` wraps each, and the form must
  // not carry a `ant-row` grid of its own around them.
  check(
    'the source replacement form puts one field per row and states the empty-replacement rule',
    (sourceReplacementHtml.match(/ant-form-item-row/g) ?? []).length >= 2 &&
      sourceReplacementHtml.includes('Leave empty to delete every occurrence') &&
      !sourceReplacementHtml.includes('placeholder="Find"'),
    `form rows: ${(sourceReplacementHtml.match(/ant-form-item-row/g) ?? []).length}`,
  )

  // The submit is disabled while the grants are unknown, and the "not known yet" state must
  // not accuse: no refusal tooltip, no hinted wrapper. Only the answered refused state speaks;
  // that one is asserted below with a seeded profile.
  check(
    'the source replacement submit is disabled and silent while the grants are unknown',
    sourceReplacementHtml.includes('type="submit"') &&
      /<button[^>]*type="submit"[^>]*disabled/.test(sourceReplacementHtml) &&
      !sourceReplacementHtml.includes('Your group does not allow this.') &&
      !sourceReplacementHtml.includes('display:inline-flex') &&
      i18n.getResource('en', 'translation', 'sourceReplacement.noPermission') ===
        'Your group does not allow this.',
    `disabled submit present: ${/<button[^>]*type="submit"[^>]*disabled/.test(sourceReplacementHtml)}, accused while loading: ${sourceReplacementHtml.includes('Your group does not allow this.')}`,
  )

  // The permitted and refused states come from a seeded profile: the query key is the one the
  // page asks with (`['profile']`), and the same seeded render is used twice so the only
  // difference is the grants.
  const renderSourceReplacementSeeded = (permissions) => {
    const client = createQueryClient()

    client.setQueryData(['profile'], {
      username: 'probeadmin',
      email: 'probeadmin@localhost',
      role: 'Admin',
      permissions,
      credits: 0,
      timeZone: null,
      geoProtectionEnabled: false,
      bindSessionToIp: false,
      allowedIps: [],
      allowedCountry: null,
      allowedAutonomousSystemNumber: null,
    })

    return renderToStaticMarkup(
      withRouter(
        '/admin/source-replacement',
        <QueryClientProvider client={client}>
          <AntApp>
            <SourceReplacementPage sectionKey="nav.items.sourceReplacement" />
          </AntApp>
        </QueryClientProvider>,
      ),
    )
  }

  const permittedSourceReplacementHtml = renderSourceReplacementSeeded([
    'editStreams',
    'editAudioStreams',
    'editVideoOnDemand',
    'editSeries',
  ])
  const refusedSourceReplacementHtml = renderSourceReplacementSeeded([
    'editStreams',
    'editAudioStreams',
    'editVideoOnDemand',
  ])

  // The four domain labels the refusal dialog builds its lines from are template keys
  // (`t(\`sourceReplacement.domains.${domain}\`)`), so the literal-key audit never sees them.
  // They are checked here against both bundles, the way the MAG event vocabulary is.
  const sourceReplacementDomains = [
    'videoStreams',
    'audioStreams',
    'videoOnDemand',
    'seriesEpisodes',
  ]
  check(
    'the four source replacement domain labels and the two refusal codes resolve in both bundles',
    sourceReplacementDomains.every((domain) =>
      ['en', 'ru'].every((lng) =>
        Boolean(
          i18n.getResource(lng, 'translation', `sourceReplacement.domains.${domain}`),
        ),
      ),
    ) &&
      ['en', 'ru'].every(
        (lng) =>
          Boolean(
            i18n.getResource(lng, 'translation', 'messages.api.source_replacement_blank'),
          ) &&
          Boolean(
            i18n.getResource(lng, 'translation', 'messages.api.source_replacement_too_long'),
          ) &&
          Boolean(
            i18n.getResource(lng, 'translation', 'messages.api.source_replacement_completed'),
          ),
      ),
    sourceReplacementDomains
      .map((domain) => `${domain}:${i18n.getResource('ru', 'translation', `sourceReplacement.domains.${domain}`)}`)
      .join(', '),
  )

  check(
    'the source replacement is submittable with all four grants and refuses with any one missing',
    permittedSourceReplacementHtml.includes('>Replace sources<') &&
      !/<button[^>]*disabled/.test(permittedSourceReplacementHtml) &&
      refusedSourceReplacementHtml.includes('Your group does not allow this.') &&
      /<button[^>]*disabled/.test(refusedSourceReplacementHtml),
    `permitted disabled: ${/<button[^>]*disabled/.test(permittedSourceReplacementHtml)}, refused disabled: ${/<button[^>]*disabled/.test(refusedSourceReplacementHtml)}`,
  )

  // The menu entry: after the categories item and before the problem log, at its mirrored
  // address, with a real page behind the loader. The order between the two endings is the
  // owner's: the replacement acts on the set, the journal records what the set reported.
  const sourceReplacementGroup = NAVIGATION.Admin.find((group) => group.key === 'videoStreams')
  const sourceReplacementItem = sourceReplacementGroup?.items.find(
    (item) => item.key === 'sourceReplacement',
  )
  const sourceReplacementModule = sourceReplacementItem?.page
    ? await sourceReplacementItem.page()
    : null
  const sourceReplacementMatch = matchRoutes(routeTable, '/admin/source-replacement')
  const sourceReplacementLeaf =
    sourceReplacementMatch?.[sourceReplacementMatch.length - 1]?.route
  check(
    'the source replacement sits after categories and before the problem log, opening the real page',
    sourceReplacementItem?.path === 'source-replacement' &&
      menuPath('Admin', 'source-replacement') === '/admin/source-replacement' &&
      sourceReplacementItem?.labelKey === 'nav.items.sourceReplacement' &&
      Boolean(i18n.getResource('en', 'translation', 'nav.items.sourceReplacement')) &&
      i18n.getResource('ru', 'translation', 'nav.items.sourceReplacement') ===
        'Замена источников' &&
      sourceReplacementModule?.default === SourceReplacementPage &&
      sourceReplacementLeaf?.path === 'source-replacement' &&
      sourceReplacementLeaf?.path !== '*',
    `items: ${(sourceReplacementGroup?.items ?? []).map((item) => item.key).join(', ')}; opens ${menuPath('Admin', 'source-replacement')}, route ${sourceReplacementLeaf?.path ?? 'nothing'}, module ${sourceReplacementModule?.default?.name ?? 'nothing'}`,
  )

  // --- The stream problem log ---------------------------------------------------
  //
  // Its own page behind `nav.items.streamProblems`, last in the `videoStreams` group but at
  // `/admin/stream-problems` rather than under the section's own prefix: the address mirrors
  // the backend's route, the credit-logs/api-request-failures treatment. The item key is
  // deliberately not prefixed with the section's (`streamProblems`, not `videoStreamsProblems`),
  // so the assertion the section's four generated entries rest on - the `videoStreams*` prefix
  // check above - stays exactly as it is.
  //
  // The heading, the three column headers and the toolbar all appear before the first answer -
  // the table is drawn empty under a spinner - so a static render is exactly the state to
  // assert them in. No row exists in a static render, so the table's sort mapping is asserted
  // through the layout module.
  const streamProblemsHtml = renderToStaticMarkup(
    withRouter('/admin/stream-problems', withQuery(
      <AntApp>
        <StreamProblemsPage sectionKey="nav.items.streamProblems" />
      </AntApp>,
    )),
  )

  check(
    'the stream problem log draws its heading and its table before any entries arrive',
    streamProblemsHtml.includes('>Stream problem logs<') &&
      streamProblemsHtml.includes('ant-table') &&
      !streamProblemsHtml.includes('nav.items.streamProblems'),
    `rendered ${streamProblemsHtml.length} bytes with the log still being fetched`,
  )

  // The three headers in the order they are drawn, and the two Russian words behind them: the
  // render runs in `en`, so the `ru` bundle is read through `i18n.getResource` - a key present
  // in neither file is the one failure no type or render can see.
  check(
    'the stream problem log draws Stream, Error and Date in that order',
    ['Stream', 'Error', 'Date'].every((heading) =>
      streamProblemsHtml.includes(`>${heading}<`),
    ) &&
      streamProblemsHtml.indexOf('>Stream<') < streamProblemsHtml.indexOf('>Error<') &&
      streamProblemsHtml.indexOf('>Error<') < streamProblemsHtml.indexOf('>Date<') &&
      i18n.getResource('en', 'translation', 'streamProblems.title') ===
        'Stream problem logs' &&
      i18n.getResource('ru', 'translation', 'streamProblems.title') ===
        'Проблемы потоков' &&
      i18n.getResource('ru', 'translation', 'streamProblems.columns.stream') === 'Поток' &&
      i18n.getResource('ru', 'translation', 'streamProblems.columns.error') === 'Ошибка' &&
      i18n.getResource('ru', 'translation', 'streamProblems.columns.createdAt') === 'Дата',
    `three headings: ${['Stream', 'Error', 'Date']
      .map((heading) => `${heading}@${streamProblemsHtml.indexOf(`>${heading}<`)}`)
      .join(', ')}`,
  )

  const streamProblemCols = [...streamProblemsHtml.matchAll(/<col\s[^>]*>/g)].map((m) => m[0])
  const streamProblemWidths = streamProblemCols.map((col) => /width:(\d+)px/.exec(col)?.[1])
  const streamProblemTotal = streamProblemWidths.reduce(
    (sum, width) => sum + (Number(width) || 0),
    0,
  )
  check(
    'the stream problem log is fixed-layout with three columns totalling 1190, in order',
    /table-layout:\s*fixed/.test(streamProblemsHtml) &&
      streamProblemWidths.join(',') === '320,570,300' &&
      streamProblemTotal === STREAM_PROBLEM_TABLE_WIDTH &&
      streamProblemColumnWidthTotal(STREAM_PROBLEM_WIDTHS) === STREAM_PROBLEM_TABLE_WIDTH &&
      STREAM_PROBLEM_TABLE_WIDTH === 1190 &&
      STREAM_PROBLEM_COLUMN_KEYS.join(',') === 'stream,error,createdAt' &&
      // The endpoint orders by one field alone, and every column key maps to it - a header that
      // sorted by a value the server never orders on would order the page, not the list.
      streamProblemSortFieldOf('createdAt') === 'createdAt' &&
      streamProblemSortFieldOf('stream') === 'createdAt',
    `${streamProblemCols.length} columns: [${streamProblemWidths.join(', ')}] = ${streamProblemTotal}px`,
  )

  // The stream cell is the one thing on this page a column `render` writes, and a render is
  // reached only when a row exists - so it is rendered from a seeded cache, the subscription
  // form's treatment. Two grants are the whole difference: `editStreams` turns the joined name
  // into a link to that stream's edit form, and no grant leaves the same name as plain text.
  //
  // The link is built from `videoStreamsManage`'s menu item rather than a copied segment, and
  // the seed below is the query key the page actually asks with - the table's five parameters
  // plus its three filters - so a key change here is a failed assertion rather than a silently
  // empty table.
  const streamProblemProbeRow = {
    id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    streamId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    streamName: 'Stream Alfa',
    errorMessage: 'Connection refused',
    createdAt: '2026-05-01T10:00:00Z',
  }
  // The session `RequireAuth` would have published for the admin area; the page reads only its
  // role, which is what turns the streams menu item into an address.
  const streamProblemsSession = {
    userId: '1',
    username: 'root',
    role: 'Admin',
    refresh: async () => null,
  }
  const renderStreamProblemsSeeded = (permissions) => {
    const client = createQueryClient()

    client.setQueryData(['profile'], {
      username: 'probeadmin',
      email: 'probeadmin@localhost',
      role: 'Admin',
      permissions,
      credits: 0,
      timeZone: null,
      geoProtectionEnabled: false,
      bindSessionToIp: false,
      allowedIps: [],
      allowedCountry: null,
      allowedAutonomousSystemNumber: null,
    })
    client.setQueryData(
      [
        'streamProblems',
        {
          page: 1,
          pageSize: 20,
          search: '',
          sortBy: 'createdAt',
          sortOrder: 'desc',
          streamId: null,
          dateFrom: null,
          dateTo: null,
        },
      ],
      { items: [streamProblemProbeRow], total: 1, page: 1, pageSize: 20 },
    )

    return renderToStaticMarkup(
      withRouter(
        '/admin/stream-problems',
        <QueryClientProvider client={client}>
          <AntApp>
            <SessionContext.Provider value={streamProblemsSession}>
              <StreamProblemsPage sectionKey="nav.items.streamProblems" />
            </SessionContext.Provider>
          </AntApp>
        </QueryClientProvider>,
      ),
    )
  }
  const editableStreamProblemsHtml = renderStreamProblemsSeeded(['viewStreams', 'editStreams'])
  const readableStreamProblemsHtml = renderStreamProblemsSeeded(['viewStreams'])
  const streamProblemEditHref = `${menuItemPath('Admin', 'videoStreamsManage')}/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa`
  check(
    'the stream cell opens that stream’s edit form for an operator holding editStreams',
    streamProblemEditHref ===
      '/admin/video-streams/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' &&
      editableStreamProblemsHtml.includes(`href="${streamProblemEditHref}"`) &&
      editableStreamProblemsHtml.includes('>Stream Alfa</a>') &&
      // The ellipsis tooltip is antd's own and still names the whole value on the cell.
      editableStreamProblemsHtml.includes('title="Stream Alfa"'),
    `link ${editableStreamProblemsHtml.includes('>Stream Alfa</a>') ? 'drawn' : 'MISSING'}, href ${
      /href="([^"]*video-streams[^"]*)"/.exec(editableStreamProblemsHtml)?.[1] ?? 'none'
    }`,
  )
  check(
    'a visitor without editStreams reads the stream name as plain text, with no dead link',
    readableStreamProblemsHtml.includes('Stream Alfa') &&
      !readableStreamProblemsHtml.includes('video-streams/aaaaaaaa') &&
      !readableStreamProblemsHtml.includes('>Stream Alfa</a>'),
    readableStreamProblemsHtml.includes('Stream Alfa')
      ? 'name drawn, no link'
      : 'THE STREAM NAME IS MISSING',
  )

  // The toolbar order: auto-refresh (an icon whose accessible name says which action it
  // performs, ON by default so the fresh render says "Pause"), search, the stream picker, the
  // width reset, the clear command. Both the clear button's greyed state and its reason are the
  // permission-less branch of a fresh render - the profile has not answered yet.
  const streamProblemRefreshAt = streamProblemsHtml.indexOf('Pause auto refresh')
  check(
    'the stream problem log toolbar runs auto-refresh (on by default), search, stream picker, width reset and clear',
    streamProblemRefreshAt !== -1 &&
      streamProblemsHtml.includes('Search the stream problem logs') &&
      streamProblemsHtml.includes('Any stream') &&
      streamProblemsHtml.includes('aria-label="Reset widths"') &&
      streamProblemsHtml.includes('Clear the logs') &&
      streamProblemRefreshAt <
        streamProblemsHtml.indexOf('Search the stream problem logs') &&
      streamProblemsHtml.indexOf('Search the stream problem logs') <
        streamProblemsHtml.indexOf('Any stream') &&
      streamProblemsHtml.indexOf('Any stream') <
        streamProblemsHtml.indexOf('aria-label="Reset widths"') &&
      streamProblemsHtml.indexOf('aria-label="Reset widths"') <
        streamProblemsHtml.indexOf('Clear the logs'),
    `refresh@${streamProblemRefreshAt}, search@${streamProblemsHtml.indexOf('Search the stream problem logs')}, picker@${streamProblemsHtml.indexOf('Any stream')}, clear@${streamProblemsHtml.indexOf('Clear the logs')}`,
  )

  // The date range is the one control too wide for the card head, so it is a row of its own in
  // the card body - after the card head (whose last control is "Clear the logs") and before
  // the table. Index order is enough: head, then the picker, then the table.
  check(
    'the stream problem log draws the date range on its own row between the card head and the table',
    streamProblemsHtml.includes('ant-picker-range') &&
      streamProblemsHtml.indexOf('Date from') >
        streamProblemsHtml.indexOf('Clear the logs') &&
      streamProblemsHtml.indexOf('Date from') >
        streamProblemsHtml.indexOf('ant-card-body') &&
      streamProblemsHtml.indexOf('Date from') < streamProblemsHtml.indexOf('ant-table'),
    `range@${streamProblemsHtml.indexOf('Date from')}, clear@${streamProblemsHtml.indexOf('Clear the logs')}, body@${streamProblemsHtml.indexOf('ant-card-body')}, table@${streamProblemsHtml.indexOf('ant-table')}`,
  )

  // The menu entry: last in the `videoStreams` group, one past the categories item, the derived
  // address mirroring the backend's route, and a real page behind the loader rather than the
  // placeholder. The midpoint of the group also pins the *order*: the section's four generated
  // entries first, then the log.
  const streamProblemGroup = NAVIGATION.Admin.find((group) => group.key === 'videoStreams')
  const streamProblemItemKeys = (streamProblemGroup?.items ?? []).map((item) => item.key)
  const streamProblemItem = streamProblemGroup?.items.find(
    (item) => item.key === 'streamProblems',
  )
  const streamProblemModule = streamProblemItem?.page ? await streamProblemItem.page() : null
  const streamProblemMatch = matchRoutes(routeTable, '/admin/stream-problems')
  const streamProblemLeaf = streamProblemMatch?.[streamProblemMatch.length - 1]?.route
  check(
    'the video streams group ends with the problem log entry, opening the real page at its mirrored address',
    streamProblemItemKeys.join(',') ===
      'videoStreamsAdd,videoStreamsImport,videoStreamsManage,videoStreamsCategories,sourceReplacement,streamProblems' &&
      streamProblemItem?.path === 'stream-problems' &&
      menuPath('Admin', 'stream-problems') === '/admin/stream-problems' &&
      streamProblemItem?.labelKey === 'nav.items.streamProblems' &&
      Boolean(i18n.getResource('en', 'translation', 'nav.items.streamProblems')) &&
      Boolean(i18n.getResource('ru', 'translation', 'nav.items.streamProblems')) &&
      streamProblemModule?.default === StreamProblemsPage &&
      // The route table resolves the address to the section's own child rather than the
      // catch-all - a menu item without a route is a click that lands on the 404.
      streamProblemLeaf?.path === 'stream-problems' &&
      streamProblemLeaf?.path !== '*',
    `items: ${streamProblemItemKeys.join(', ')}; opens ${menuPath('Admin', 'stream-problems')}, route ${streamProblemLeaf?.path ?? 'nothing'}, module ${streamProblemModule?.default?.name ?? 'nothing'}`,
  )

  // --- The MAG event journal ---------------------------------------------------
  //
  // Its own page behind `nav.items.magEvents`, last in the `subscriptions` group and at
  // `/admin/mag-events` rather than under the section's prefix: the address mirrors the
  // backend's route, the credit-logs/api-request-failures/stream-problems treatment.
  //
  // The heading, the five column headers and the toolbar all appear before the first answer -
  // the table is drawn empty under a spinner - so a static render is exactly the state to
  // assert them in. A row's own cells need the answer, so they are rendered from a seeded
  // cache below; the send dialog itself is a portal, which `renderToStaticMarkup` never opens,
  // so its mount and its "no flag controls" shape are read off the two sources instead.
  const magEventsHtml = renderToStaticMarkup(
    withRouter('/admin/mag-events', withQuery(
      <AntApp>
        <MagEventsPage sectionKey="nav.items.magEvents" />
      </AntApp>,
    )),
  )

  check(
    'the MAG event journal draws its heading and its table before any entries arrive',
    magEventsHtml.includes('>MAG events<') &&
      magEventsHtml.includes('ant-table') &&
      !magEventsHtml.includes('nav.items.magEvents'),
    `rendered ${magEventsHtml.length} bytes with the journal still being fetched`,
  )

  // The page's own source, read once here because two checks below assert things no static
  // render can draw: the send dialog's mount condition and the clear command's gate.
  const magEventsPageSource = readFileSync('src/pages/admin/MagEventsPage.tsx', 'utf8')

  // The five headers in the order the row reads, and the two bundles behind them: the render
  // runs in `en`, so `ru` is read through `i18n.getResource` - a key present in neither file is
  // the one failure no type or render can see.
  check(
    'the MAG event journal draws MAC, Event, Message, Status and Date in that order',
    ['MAG MAC', 'Event', 'Message', 'Status', 'Date'].every((heading) =>
      magEventsHtml.includes(`>${heading}<`),
    ) &&
      magEventsHtml.indexOf('>MAG MAC<') < magEventsHtml.indexOf('>Event<') &&
      magEventsHtml.indexOf('>Event<') < magEventsHtml.indexOf('>Message<') &&
      magEventsHtml.indexOf('>Message<') < magEventsHtml.indexOf('>Status<') &&
      magEventsHtml.indexOf('>Status<') < magEventsHtml.indexOf('>Date<') &&
      i18n.getResource('en', 'translation', 'magEvents.title') === 'MAG events' &&
      i18n.getResource('ru', 'translation', 'magEvents.title') === 'События MAG' &&
      i18n.getResource('ru', 'translation', 'magEvents.columns.mac') === 'MAC-адрес MAG' &&
      i18n.getResource('ru', 'translation', 'magEvents.columns.event') === 'Событие' &&
      i18n.getResource('ru', 'translation', 'magEvents.columns.message') === 'Сообщение' &&
      i18n.getResource('ru', 'translation', 'magEvents.columns.status') === 'Статус' &&
      i18n.getResource('ru', 'translation', 'magEvents.columns.date') === 'Дата',
    `five headings: ${['MAG MAC', 'Event', 'Message', 'Status', 'Date']
      .map((heading) => `${heading}@${magEventsHtml.indexOf(`>${heading}<`)}`)
      .join(', ')}`,
  )

  // The five widths, in order, from the `<colgroup>` antd draws - and the sum that keeps this
  // table beside every other administration table. The actions column this page does not have
  // is why the width reset is the last measurement-shaped control instead.
  const magEventCols = [...magEventsHtml.matchAll(/<col\s[^>]*>/g)].map((m) => m[0])
  const magEventWidths = magEventCols.map((col) => /width:(\d+)px/.exec(col)?.[1])
  const magEventTotal = magEventWidths.reduce((sum, width) => sum + (Number(width) || 0), 0)
  check(
    'the MAG event journal is fixed-layout with five columns totalling 1190, in order',
    /table-layout:\s*fixed/.test(magEventsHtml) &&
      magEventWidths.join(',') === '180,170,520,120,200' &&
      magEventTotal === MAG_EVENT_TABLE_WIDTH &&
      magEventColumnWidthTotal(MAG_EVENT_WIDTHS) === MAG_EVENT_TABLE_WIDTH &&
      MAG_EVENT_TABLE_WIDTH === 1190 &&
      MAG_EVENT_COLUMN_KEYS.join(',') === 'mac,event,message,status,date' &&
      // The endpoint orders by one field alone, and every column key maps to it.
      magEventSortFieldOf('createdAt') === 'createdAt' &&
      magEventSortFieldOf('mac') === 'createdAt',
    `${magEventCols.length} columns: [${magEventWidths.join(', ')}] = ${magEventTotal}px`,
  )

  // The toolbar order, the way the other logs read it: auto-refresh (an icon whose accessible
  // name says which action it performs, ON by default so the fresh render says "Pause"),
  // search, the operation picker, the width reset and the clear command. Both the clear
  // button's greyed state and its reason are the permission-less branch of a fresh render -
  // the profile has not answered yet.
  const magEventRefreshAt = magEventsHtml.indexOf('Pause auto refresh')
  check(
    'the MAG event journal toolbar runs auto-refresh (on by default), search, the event picker, width reset and clear',
    magEventRefreshAt !== -1 &&
      magEventsHtml.includes('Search by message or MAC address') &&
      magEventsHtml.includes('Any event') &&
      magEventsHtml.includes('aria-label="Reset widths"') &&
      magEventsHtml.includes('Clear the logs') &&
      magEventRefreshAt < magEventsHtml.indexOf('Search by message or MAC address') &&
      magEventsHtml.indexOf('Search by message or MAC address') <
        magEventsHtml.indexOf('Any event') &&
      magEventsHtml.indexOf('Any event') < magEventsHtml.indexOf('aria-label="Reset widths"') &&
      magEventsHtml.indexOf('aria-label="Reset widths"') <
        magEventsHtml.indexOf('Clear the logs'),
    `refresh@${magEventRefreshAt}, search@${magEventsHtml.indexOf('Search by message or MAC address')}, picker@${magEventsHtml.indexOf('Any event')}, clear@${magEventsHtml.indexOf('Clear the logs')}`,
  )

  // The date range is the one control too wide for the card head, so it is a row of its own in
  // the card body - after the card head (whose last control is "Clear the logs") and before
  // the table. Index order is enough: head, then the picker, then the table.
  check(
    'the MAG event journal draws the date range on its own row between the card head and the table',
    magEventsHtml.includes('ant-picker-range') &&
      magEventsHtml.indexOf('Date from') > magEventsHtml.indexOf('Clear the logs') &&
      magEventsHtml.indexOf('Date from') > magEventsHtml.indexOf('ant-card-body') &&
      magEventsHtml.indexOf('Date from') < magEventsHtml.indexOf('ant-table'),
    `range@${magEventsHtml.indexOf('Date from')}, clear@${magEventsHtml.indexOf('Clear the logs')}, body@${magEventsHtml.indexOf('ant-card-body')}, table@${magEventsHtml.indexOf('ant-table')}`,
  )

  // The row's own cells - a MAC link, the localized operation, the payload, the tone of the
  // status tag - are drawn by column `render`s, which run only when a row exists. Two renders
  // from a seeded cache, the stream problem log's treatment: one row with a device and one
  // without, because the dash branch is the one a null join produces.
  const magEventProbeRow = {
    id: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
    subscriptionId: '11111111-1111-1111-1111-111111111111',
    magMac: 'AA:BB:CC:DD:EE:FF',
    eventType: 'sendMessage',
    message: 'Hello from the operator',
    status: 'notConfirmed',
    createdAt: '2026-05-01T10:00:00Z',
  }
  const magEventNoDeviceRow = {
    ...magEventProbeRow,
    id: 'dddddddd-dddd-dddd-dddd-dddddddddddd',
    magMac: null,
    eventType: 'rebootDevice',
    message: null,
    status: 'confirmed',
  }
  const magEventsSession = {
    userId: '1',
    username: 'root',
    role: 'Admin',
    refresh: async () => null,
  }
  const renderMagEventsSeeded = (permissions) => {
    const client = createQueryClient()

    client.setQueryData(['profile'], {
      username: 'probeadmin',
      email: 'probeadmin@localhost',
      role: 'Admin',
      permissions,
      credits: 0,
      timeZone: null,
      geoProtectionEnabled: false,
      bindSessionToIp: false,
      allowedIps: [],
      allowedCountry: null,
      allowedAutonomousSystemNumber: null,
    })
    client.setQueryData(
      [
        'magEvents',
        {
          page: 1,
          pageSize: 20,
          search: '',
          sortBy: 'createdAt',
          sortOrder: 'desc',
          event: null,
          subscriptionId: null,
          dateFrom: null,
          dateTo: null,
        },
      ],
      { items: [magEventProbeRow, magEventNoDeviceRow], total: 2, page: 1, pageSize: 20 },
    )

    return renderToStaticMarkup(
      withRouter(
        '/admin/mag-events',
        <QueryClientProvider client={client}>
          <AntApp>
            <SessionContext.Provider value={magEventsSession}>
              <MagEventsPage sectionKey="nav.items.magEvents" />
            </SessionContext.Provider>
          </AntApp>
        </QueryClientProvider>,
      ),
    )
  }
  const readableMagEventsHtml = renderMagEventsSeeded(['viewSubscriptions'])
  const writableMagEventsHtml = renderMagEventsSeeded([
    'viewSubscriptions',
    'editSubscriptions',
  ])
  const magEventSubscriptionHref = `${menuItemPath('Admin', 'subscriptionsManage')}/11111111-1111-1111-1111-111111111111`
  check(
    'the MAC cell links the device back to its subscription and draws the address in monospace',
    // The link is a `Typography.Link` with an `onClick`, the same click-through the connection
    // logs draw - it is not an `href`, because the address is built from the session's role at
    // click time rather than from a copied segment. What a static render can see is the
    // monospace address inside a link, and what the page's source names is the target.
    /<a[^>]*>[\s\S]{0,200}?<code>AA:BB:CC:DD:EE:FF<\/code>/.test(readableMagEventsHtml) &&
      magEventsPageSource.includes(
        '`${area}/subscriptions/${row.subscriptionId}`',
      ) &&
      // The row with no device draws a dash rather than a link to nowhere.
      !readableMagEventsHtml.includes('dddddddd-dddd-dddd-dddd-dddddddddddd</a>') &&
      magEventsPageSource.includes("return <Typography.Text type=\"secondary\">{DASH}</Typography.Text>"),
    `link ${readableMagEventsHtml.includes('AA:BB:CC:DD:EE:FF') ? 'drawn' : 'MISSING'}, target ${
      magEventSubscriptionHref
    }`,
  )
  check(
    'the event and status cells read as localized words and the tag carries the state’s tone',
    readableMagEventsHtml.includes('Send a message') &&
      readableMagEventsHtml.includes('Reboot the device') &&
      readableMagEventsHtml.includes('Hello from the operator') &&
      readableMagEventsHtml.includes('Not confirmed') &&
      readableMagEventsHtml.includes('Confirmed') &&
      // antd renders a semantic tag colour as its own class: success for confirmed, warning
      // for not confirmed. A pair of tags that silently swapped would read perfectly otherwise.
      readableMagEventsHtml.includes('ant-tag-success') &&
      readableMagEventsHtml.includes('ant-tag-warning'),
    `rendered ${readableMagEventsHtml.length} bytes with both rows`,
  )
  check(
    'the clear command is gated on editSubscriptions and never on the data',
    // The read-only render carries the greyed command with its reason; the writable one carries
    // the live danger button. Neither branch mentions `total` or `items` - the button is a
    // promise about the journal, not about the rows the filters admit - so the gate is read off
    // the page's source as well, where the two facts are stated once.
    readableMagEventsHtml.includes('Clear the logs') &&
      writableMagEventsHtml.includes('Clear the logs') &&
      /canClear \? \(/.test(magEventsPageSource) &&
      /const canClear = granted\.includes\('editSubscriptions'\)/.test(magEventsPageSource) &&
      !/disabled=\{[^}]*(total|items|journal)/.test(magEventsPageSource),
    `readable button drawn: ${readableMagEventsHtml.includes('Clear the logs')}`,
  )

  // A profile without `viewSubscriptions` gets the refusal alert alone, under the card title:
  // the address still names the page, and an empty table would describe a journal that exists.
  const refusedMagEventsHtml = renderToStaticMarkup(
    withRouter(
      '/admin/mag-events',
      withSeededQuery(
        ['profile'],
        {
          username: 'probeadmin',
          email: 'probeadmin@localhost',
          role: 'Admin',
          permissions: [],
          credits: 0,
          timeZone: null,
          geoProtectionEnabled: false,
          bindSessionToIp: false,
          allowedIps: [],
          allowedCountry: null,
          allowedAutonomousSystemNumber: null,
        },
        <AntApp>
          <MagEventsPage sectionKey="nav.items.magEvents" />
        </AntApp>,
      ),
    ),
  )
  check(
    'a profile without viewSubscriptions draws the refusal alert alone, under the card title',
    refusedMagEventsHtml.includes('>MAG events<') &&
      refusedMagEventsHtml.includes('Your group does not allow this.') &&
      !refusedMagEventsHtml.includes('ant-table') &&
      !refusedMagEventsHtml.includes('Any event'),
    `rendered ${refusedMagEventsHtml.length} bytes with the read refused`,
  )

  // The send dialog is a `Modal`, whose portal `renderToStaticMarkup` never opens - so what is
  // asserted is what a static render *can* see of it: the page mounts it only for an open row,
  // and neither source invents a flag the server resolves.
  const sendEventModalSource = readFileSync(
    'src/features/mag-events/components/SendEventModal.tsx',
    'utf8',
  )
  check(
    'the send dialog is mounted only while a row is chosen, and offers no flag controls',
    /sendEventTarget !== null &&[\s\S]{0,240}?<SendEventModal/.test(subscriptionsPageSource) &&
      /const \[sendEventTarget, setSendEventTarget\] = useState<SendEventTarget \| null>\(null\)/.test(
        subscriptionsPageSource,
      ) &&
      sendEventModalSource.includes("t('subscriptions.sendEvent.title')") &&
      sendEventModalSource.includes('name="event"') &&
      sendEventModalSource.includes('name="message"') &&
      // The six wire operations are the picker's options, with no default: an operation is a
      // decision, and a preselected one would let "Send" record something nobody chose.
      sendEventModalSource.includes('MAG_EVENT_TYPES.map') &&
      // No flag is a control: the words may appear in the module's own explanation of why they
      // are absent, but no `Form.Item`, switch or input may name one.
      !/name="(needConfirm|rebootAfterOk|autoHideTimeout|additionalServicesOn|anec|vclub)"/.test(
        sendEventModalSource,
      ) &&
      !/Form\.Item[^\n]*(needConfirm|rebootAfterOk|autoHideTimeout|additionalServicesOn|anec|vclub)/.test(
        sendEventModalSource,
      ),
    'a dialog that offers a server-resolved flag, or one that mounts without a row',
  )

  // The menu entry: last in the `subscriptions` group, one past the activity log, the address
  // mirroring the backend's route, and a real page behind the loader. The order is the owner's
  // - the journal is the record of what was sent, so it follows the two logs of what was then
  // watched.
  const magEventGroup = NAVIGATION.Admin.find((group) => group.key === 'subscriptions')
  const magEventItemKeys = (magEventGroup?.items ?? []).map((item) => item.key)
  const magEventItem = magEventGroup?.items.find((item) => item.key === 'magEvents')
  const magEventModule = magEventItem?.page ? await magEventItem.page() : null
  const magEventMatch = matchRoutes(routeTable, '/admin/mag-events')
  const magEventLeaf = magEventMatch?.[magEventMatch.length - 1]?.route
  check(
    'the subscriptions group ends with the MAG event journal, opening the real page at its mirrored address',
    magEventItemKeys.join(',') ===
      'subscriptionsAdd,subscriptionsManage,tariffPlans,liveConnections,subscriptionActivity,magEvents' &&
      magEventItem?.path === 'mag-events' &&
      menuPath('Admin', 'mag-events') === '/admin/mag-events' &&
      magEventItem?.labelKey === 'nav.items.magEvents' &&
      Boolean(i18n.getResource('en', 'translation', 'nav.items.magEvents')) &&
      Boolean(i18n.getResource('ru', 'translation', 'nav.items.magEvents')) &&
      magEventModule?.default === MagEventsPage &&
      magEventLeaf?.path === 'mag-events' &&
      magEventLeaf?.path !== '*',
    `items: ${magEventItemKeys.join(', ')}; opens ${menuPath('Admin', 'mag-events')}, route ${magEventLeaf?.path ?? 'nothing'}, module ${magEventModule?.default?.name ?? 'nothing'}`,
  )

  // The operation vocabulary, the send rules and the three server codes, asserted as values
  // because no render reaches a refusal that has not been asked for yet.
  check(
    'the MAG event vocabulary and the send rules mirror the server, and all three codes are translated',
    MAG_EVENT_TYPES.join(',') ===
      'playChannel,closePortal,reloadPortal,rebootDevice,sendMessage,resetStbLock' &&
      ['en', 'ru'].every((lng) =>
        MAG_EVENT_TYPES.every((event) =>
          Boolean(i18n.getResource(lng, 'translation', `magEvents.events.${event}`)),
        ),
      ) &&
      i18n.getResource('en', 'translation', 'magEvents.events.playChannel') === 'Play a channel' &&
      i18n.getResource('ru', 'translation', 'magEvents.events.resetStbLock') ===
        'Сбросить STB-блокировку' &&
      i18n.getResource('en', 'translation', 'magEvents.statuses.confirmed') === 'Confirmed' &&
      i18n.getResource('ru', 'translation', 'magEvents.statuses.notConfirmed') ===
        'Не подтверждено' &&
      i18n.getResource('en', 'translation', 'messages.api.mag_event_sent') ===
        'The event has been sent to the MAG device.' &&
      Boolean(i18n.getResource('ru', 'translation', 'messages.api.mag_event_sent')) &&
      Boolean(i18n.getResource('en', 'translation', 'messages.api.mag_events_cleared')) &&
      Boolean(i18n.getResource('ru', 'translation', 'messages.api.mag_events_cleared')) &&
      Boolean(i18n.getResource('en', 'translation', 'messages.api.mag_device_missing')) &&
      Boolean(i18n.getResource('ru', 'translation', 'messages.api.mag_device_missing')),
    `${MAG_EVENT_TYPES.length} operations, three codes`,
  )

  // --- Feature isolation -------------------------------------------------------
  //
  // A feature imports neither another feature nor a page; the shared `components/`, `hooks/`
  // and `utils/` are what it may reach. The new feature's own files are the first thing this
  // probe can read as text, and a stray cross-feature import is exactly what no type catches.
  const apiRequestFailureFeatureFiles = [
    'src/features/api-request-failures/api.ts',
    'src/features/api-request-failures/reasons.ts',
    'src/features/api-request-failures/reasonTone.ts',
    'src/features/api-request-failures/layout.ts',
    'src/features/api-request-failures/dateRange.ts',
    'src/features/api-request-failures/hooks/queries.ts',
    'src/features/api-request-failures/hooks/mutations.ts',
    'src/features/api-request-failures/types/index.ts',
    'src/features/api-request-failures/types/ApiRequestFailureRow.ts',
    'src/features/api-request-failures/types/ApiRequestFailureRowList.ts',
    'src/features/api-request-failures/types/ApiRequestFailureReason.ts',
    'src/features/api-request-failures/types/ApiRequestFailureContentKind.ts',
  ]
  const apiRequestFailureCrossImports = apiRequestFailureFeatureFiles.flatMap((file) =>
    [...readFileSync(file, 'utf8').matchAll(/from '([^']+)'/g)]
      .map((match) => match[1])
      .filter((specifier) => specifier.includes('/features/'))
      .filter((specifier) => !specifier.includes('features/api-request-failures/')),
  )
  check(
    'nothing inside the api-request-failures feature imports another feature',
    apiRequestFailureCrossImports.length === 0,
    apiRequestFailureCrossImports.length === 0
      ? `${apiRequestFailureFeatureFiles.length} feature files, no cross-feature imports`
      : `CROSS-FEATURE IMPORTS: ${apiRequestFailureCrossImports.join(', ')}`,
  )

  // The same fence over the stream problem journal's own files, listed explicitly for the reason
  // above: a feature that reached sideways into `video-streams` would still typecheck, and the
  // page-level import is the only one allowed - the page, not the feature, names both domains.
  const streamProblemFeatureFiles = [
    'src/features/stream-problems/api.ts',
    'src/features/stream-problems/dateRange.ts',
    'src/features/stream-problems/layout.ts',
    'src/features/stream-problems/hooks/queries.ts',
    'src/features/stream-problems/hooks/mutations.ts',
    'src/features/stream-problems/types/index.ts',
    'src/features/stream-problems/types/StreamProblemRow.ts',
    'src/features/stream-problems/types/StreamProblemRowList.ts',
  ]
  const streamProblemCrossImports = streamProblemFeatureFiles.flatMap((file) =>
    [...readFileSync(file, 'utf8').matchAll(/from '([^']+)'/g)]
      .map((match) => match[1])
      .filter((specifier) => specifier.includes('/features/'))
      .filter((specifier) => !specifier.includes('features/stream-problems/')),
  )
  check(
    'nothing inside the stream-problems feature imports another feature',
    streamProblemCrossImports.length === 0,
    streamProblemCrossImports.length === 0
      ? `${streamProblemFeatureFiles.length} feature files, no cross-feature imports`
      : `CROSS-FEATURE IMPORTS: ${streamProblemCrossImports.join(', ')}`,
  )

  // The same fence over the MAG event journal's files. `SendEventModal` is the first component
  // in this feature, and the temptation it creates is exactly the forbidden one: reading the
  // video streams itself instead of taking the page's options.
  const magEventFeatureFiles = [
    'src/features/mag-events/api.ts',
    'src/features/mag-events/events.ts',
    'src/features/mag-events/eventRules.ts',
    'src/features/mag-events/dateRange.ts',
    'src/features/mag-events/layout.ts',
    'src/features/mag-events/hooks/queries.ts',
    'src/features/mag-events/hooks/mutations.ts',
    'src/features/mag-events/components/SendEventModal.tsx',
    'src/features/mag-events/types/index.ts',
    'src/features/mag-events/types/MagEventRow.ts',
    'src/features/mag-events/types/MagEventRowList.ts',
    'src/features/mag-events/types/MagEventType.ts',
    'src/features/mag-events/types/MagEventStatus.ts',
  ]
  const magEventCrossImports = magEventFeatureFiles.flatMap((file) =>
    [...readFileSync(file, 'utf8').matchAll(/from '([^']+)'/g)]
      .map((match) => match[1])
      .filter((specifier) => specifier.includes('/features/'))
      .filter((specifier) => !specifier.includes('features/mag-events/')),
  )
  check(
    'nothing inside the mag-events feature imports another feature',
    magEventCrossImports.length === 0,
    magEventCrossImports.length === 0
      ? `${magEventFeatureFiles.length} feature files, no cross-feature imports`
      : `CROSS-FEATURE IMPORTS: ${magEventCrossImports.join(', ')}`,
  )

  const toolbarCards = [
    sessionsHtml,
    activeSessionsHtml,
    usersHtml,
    groupsHtml,
    categoriesHtml,
    streamsHtml,
    audioStreamsHtml,
    videoOnDemandHtml,
    seriesHtml,
    seriesEpisodesHtml,
    adminSessionsHtml,
    epgSourcesHtml,
    epgSourceChannelsHtml,
    blacklistIpHtml,
    blacklistIspHtml,
    blacklistUaHtml,
    bouquetsHtml,
    bouquetContentHtml,
    subscriptionsHtml,
    liveLogsHtml,
    activityLogsHtml,
    creditLogsHtml,
    userLogsHtml,
    apiRequestFailuresHtml,
    streamProblemsHtml,
    magEventsHtml,
  ]
  const cardWithoutClass = toolbarCards.findIndex((html) => !html.includes('page-card'))
  check(
    'every page with a toolbar card carries the class the compact stylesheet keys on',
    cardWithoutClass === -1,
    cardWithoutClass === -1
      ? `${toolbarCards.length} toolbar cards carry page-card`
      : `CARD ${cardWithoutClass + 1} HAS NO page-card CLASS`,
  )

  // The one thing on the accounts table a column `render` is the only place to write, and a
  // `render` is reached only when a row exists - while every render of that page above also
  // arrives before the first answer does. So the cell is a component and is rendered here
  // directly, for the same reason `ConfirmCodeForm` and the two display helpers are: nothing
  // else in this repository would notice it rendering nothing at all.
  //
  // The area is passed exactly as `UsersPage` passes it - `areaForRole(role)`, which is
  // `/admin` with the leading slash, not the bare segment. That is load-bearing rather than
  // cosmetic: the link is built from this string, and a value without the slash would be a
  // *relative* address whose resolution depends on the route rendering it - the one input
  // the page would never produce.
  const probeVisit = '2026-10-11T10:46:52Z'
  const probeVisitText = formatTimestamp(probeVisit, 'en', null)
  const lastSeenHtml = renderToStaticMarkup(
    withRouter(
      '/admin/users',
      <AntApp>
        <LastSeenCell
          area="/admin"
          user={{ id: '33333333-3333-3333-3333-333333333333', username: 'probeuser' }}
          value={probeVisit}
          format={(v) => formatTimestamp(v, 'en', null)}
        />
      </AntApp>,
    ),
  )
  const lastSeenLinkText = /<a[^>]*user-sessions[^>]*>([^<]*)</.exec(lastSeenHtml)?.[1]
  check(
    'a last-visit cell draws the moment itself as the way to the visit history of that account',
    lastSeenLinkText === probeVisitText &&
      lastSeenHtml.includes(
        'href="/admin/user-sessions?userId=33333333-3333-3333-3333-333333333333"',
      ) &&
      lastSeenHtml.includes('title="Visit history for probeuser"'),
    `link text ${JSON.stringify(lastSeenLinkText)} (expected ${JSON.stringify(probeVisitText)}), href ${
      /href="([^"]*user-sessions[^"]*)"/.exec(lastSeenHtml)?.[1] ?? 'none'
    }`,
  )

  // And the account that has never signed in: a dash, and nothing to click. A link over a
  // dash would name a history the server has none of.
  const lastSeenEmptyHtml = renderToStaticMarkup(
    withRouter(
      '/admin/users',
      <AntApp>
        <LastSeenCell
          area="/admin"
          user={{ id: '33333333-3333-3333-3333-333333333333', username: 'probeuser' }}
          value={null}
          format={(v) => formatTimestamp(v, 'en', null)}
        />
      </AntApp>,
    ),
  )
  check(
    'an account that has never signed in draws a dash where the link would be',
    lastSeenEmptyHtml.includes('—') && !lastSeenEmptyHtml.includes('user-sessions'),
    `${lastSeenEmptyHtml.length} bytes: ${lastSeenEmptyHtml.slice(0, 120)}`,
  )

  // The accounts table's group cell draws a reseller's balance as a second line and nothing
  // at all for the two other roles - a value that does not apply to an account is not an
  // unknown one, so it is not a dash either. `0` is a fact and is drawn. The cell is rendered
  // directly because a column `render` runs only once a row exists, and every render of the
  // table above lands before the first answer.
  const probeGroupUser = {
    id: '77777777-7777-7777-7777-777777777777',
    username: 'reseller',
    email: 'reseller@localhost',
    role: 'Reseller',
    groupId: '00000000-0000-0000-0000-000000000002',
    groupName: 'Resellers',
    status: 'Registered',
    credits: 0,
    lastSeenAt: null,
    lastIp: null,
  }
  const resellerGroupCellHtml = renderToStaticMarkup(
    <AntApp>
      <UserGroupCell user={probeGroupUser} />
    </AntApp>,
  )
  const clientGroupCellHtml = renderToStaticMarkup(
    <AntApp>
      <UserGroupCell user={{ ...probeGroupUser, role: 'Client', credits: 0 }} />
    </AntApp>,
  )
  check(
    "the accounts table's group cell carries a reseller's balance as a second line, zero included",
    resellerGroupCellHtml.includes('Resellers') &&
      resellerGroupCellHtml.includes('users-credits') &&
      resellerGroupCellHtml.includes('aria-label="Credits: 0"') &&
      resellerGroupCellHtml.includes('anticon-wallet') &&
      // The zero is drawn, and it is a number rather than a placeholder.
      /users-credits[^>]*>[\s\S]*?<span>0<\/span>/.test(resellerGroupCellHtml),
    `${resellerGroupCellHtml.length} bytes: ${resellerGroupCellHtml.slice(0, 200)}`,
  )
  check(
    'the group cell draws no balance at all for a client, not even a dash',
    !clientGroupCellHtml.includes('users-credits') &&
      !clientGroupCellHtml.includes('Credits:') &&
      !clientGroupCellHtml.includes('anticon-wallet') &&
      clientGroupCellHtml.includes('Resellers'),
    `${clientGroupCellHtml.length} bytes: ${clientGroupCellHtml.slice(0, 200)}`,
  )

  // The other render no page reach in this probe gets to: a row-action cluster is drawn by a
  // column's `render`, which runs only once a row exists, and every table render above lands
  // before the first answer arrives - so a cluster that rendered nothing at all, or one that
  // quietly stopped passing `disabled` through, would look exactly like a healthy one.
  //
  // What cannot be asserted here is the tooltip text: a `Tooltip` keeps its title in a popup
  // that exists only after a hover, so the label an icon button lives by is invisible to
  // static markup. What can be asserted is what the cluster draws: one button per action, the
  // disabled state the `hinted` wrapper exists for, and the danger styling of the one
  // destructive move.
  const rowActionsHtml = renderToStaticMarkup(
    <AntApp>
      <RowActions
        actions={[
          { key: 'edit', label: 'Edit', icon: <EditOutlined />, onClick: () => undefined },
          { key: 'block', label: 'Refused for this row', icon: <LockOutlined />, disabled: true },
          { key: 'delete', label: 'Delete', icon: <DeleteOutlined />, danger: true },
        ]}
      />
    </AntApp>,
  )
  const rowActionItems = (rowActionsHtml.match(/ant-space-item/g) ?? []).length
  const rowActionDisabled = /<button[^>]*disabled/.test(rowActionsHtml)
  const rowActionDanger = rowActionsHtml.includes('ant-btn-dangerous')
  check(
    'a row-action cluster draws one icon-only button per action, disabled and danger kept',
    rowActionItems === 3 && rowActionDisabled && rowActionDanger,
    `${rowActionItems} item(s), disabled ${rowActionDisabled}, danger ${rowActionDanger}: ${rowActionsHtml.slice(0, 200)}`,
  )

  // The network cell is the same kind of unreachable render - its three facts are drawn by a
  // column `render`, which needs a row. It is rendered here directly for two reasons the owner
  // reported as bugs: the flag has a URL that must be the working one (the old host answers a
  // Cloudflare challenge for every image, so every cell silently fell back to two letters), and
  // only the ISP is meant to be set smaller while the flag and the address keep the cell's size.
  //
  // The stylesheet is read here rather than borrowed from the later `railCss`, because the rule
  // that keeps the address at the cell's size lives there and is half of what this asserts.
  const networkCellHtml = renderToStaticMarkup(
    <AntApp>
      <ConnectionNetworkCell
        log={{
          id: '11111111-1111-1111-1111-111111111111',
          subscriptionId: '22222222-2222-2222-2222-222222222222',
          ownerId: '33333333-3333-3333-3333-333333333333',
          username: 'subscriber',
          streamKind: 'video',
          streamId: '44444444-4444-4444-4444-444444444444',
          streamTitle: 'News',
          ip: '203.0.113.9',
          userAgent: null,
          countryCode: 'DE',
          isp: 'Example ISP',
          startedAt: '2026-05-01T10:00:00+00:00',
          endedAt: null,
          broadcastPid: null,
        }}
        locale="en"
        onFilter={() => undefined}
      />
    </AntApp>,
  )
  const flagSrc = /<img[^>]*src="([^"]*)"/.exec(networkCellHtml)?.[1] ?? ''
  check(
    'the network cell points its flag at the host that answers a PNG, with the code in lower case',
    // The host was measured: the old one answers 403 with a Cloudflare page, this one 200 image/png.
    flagSrc === 'https://www.iptrackeronline.com/flags/de.png',
    `the flag loads ${flagSrc || 'nothing'}`,
  )
  check(
    'only the provider is set smaller: the address and the flag inherit the cell size',
    // The cell states **one** font size - the provider's 12.
    /font-size:12px[^>]*>[\s\S]{0,200}?Example ISP/.test(networkCellHtml) &&
      // The address is plain text on the owner's round-3 correction: no `<code>` wrapper and no
      // size of its own, so it reads like the subscription link and the start time beside it.
      // It is still the click target that writes the `ip` filter, which is why it is a link.
      /<a [^>]*aria-label="Filter by 203\.0\.113\.9"[^>]*>203\.0\.113\.9<\/a>/.test(
        networkCellHtml,
      ) &&
      !/<code[^>]*>203\.0\.113\.9/.test(networkCellHtml) &&
      !/font-size:1[01](\.\d+)?px[^>]*>[\s\S]{0,120}?203\.0\.113\.9/.test(networkCellHtml),
    `rendered ${networkCellHtml.length} bytes`,
  )

  // The shared cell is the same rendering in the three tables, and the visit histories cannot
  // filter by any of its three facts: their endpoint resolves geo fields while a page is built.
  // So the cell has to degrade to plain text there rather than offer a click that writes nothing.
  const networkCellUnfilteredHtml = renderToStaticMarkup(
    <AntApp>
      <ConnectionNetworkCell
        log={{
          ip: '203.0.113.9',
          countryCode: 'DE',
          organization: 'Example ISP',
          autonomousSystemNumber: 15169,
        }}
        locale="en"
      />
    </AntApp>,
  )
  check(
    'without a filter callback the network cell draws the same three facts and no click targets',
    networkCellUnfilteredHtml.includes('203.0.113.9') &&
      networkCellUnfilteredHtml.includes('Example ISP · AS15169') &&
      networkCellUnfilteredHtml.includes('flags/de.png') &&
      !networkCellUnfilteredHtml.includes('<button') &&
      !networkCellUnfilteredHtml.includes('<a ') &&
      !networkCellUnfilteredHtml.includes('Filter by'),
    `rendered ${networkCellUnfilteredHtml.length} bytes`,
  )

  // The user-agent cell is the fourth unreachable render: three tables draw it, one of them as
  // the way to narrow the log by that exact client, and a static page render draws no cell at
  // all. It is rendered directly here so the summary, the raw tooltip and the click are visible.
  const agentCellHtml = renderToStaticMarkup(
    <AntApp>
      <UserAgentCell
        value="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36"
        onFilter={() => undefined}
      />
    </AntApp>,
  )
  check(
    'the user-agent cell summarises the string, keeps it whole, and links it as a filter',
    agentCellHtml.includes('Chrome 140 (Windows 10, x64)') &&
      /<a [^>]*aria-label="Filter by Mozilla\/5\.0/.test(agentCellHtml),
    `rendered ${agentCellHtml.length} bytes: ${agentCellHtml.slice(0, 200)}`,
  )

  check(
    'the network cell puts the flag and the address on one line and the provider on the next',
    // Asserted structurally rather than visually: the cell is a column of exactly two children,
    // the first a row holding the flag button and the address, the second the provider. A
    // provider left on the first line is the layout the owner asked to change, and it is
    // invisible to every other checker.
    /flex-direction:column[^>]*>[\s\S]{0,400}?<button[\s\S]{0,600}?203\.0\.113\.9[\s\S]{0,600}?Example ISP/.test(
      networkCellHtml,
    ) &&
      // Exactly one flex row inside the cell - the first line - so the provider cannot be in it.
      (networkCellHtml.match(/display:flex;align-items:center/g) ?? []).length === 1,
    `rendered ${networkCellHtml.length} bytes`,
  )

  // The same failure the profile item's check below exists for: `sectionRoutes` falls back
  // to `SectionPage` when an item carries no `page`, so a loader dropped from any of these
  // four items would open the placeholder while the menu, lint and the build all stayed
  // green. Four rather than three since the visit history arrived, and it is the one worth
  // watching: it is the newest, and like the group table its page the menu cannot reach by
  // any other route.
  const [usersAddModule, usersManageModule, userGroupsModule, adminSessionsModule] =
    await Promise.all([
      flatItems('Admin').find((item) => item.key === 'usersAdd')?.page?.(),
      flatItems('Admin').find((item) => item.key === 'usersManage')?.page?.(),
      flatItems('Admin').find((item) => item.key === 'userGroups')?.page?.(),
      flatItems('Admin').find((item) => item.key === 'adminSessions')?.page?.(),
    ])
  check(
    'the admin menu points its four users-group items at the real pages rather than the placeholder',
    usersAddModule?.default === UserFormPage &&
      usersManageModule?.default === UsersPage &&
      userGroupsModule?.default === UserGroupsPage &&
      adminSessionsModule?.default === AdminSessionsPage,
    `usersAdd opens ${usersAddModule?.default?.name ?? 'nothing'}, usersManage opens ${
      usersManageModule?.default?.name ?? 'nothing'
    }, userGroups opens ${userGroupsModule?.default?.name ?? 'nothing'}, adminSessions opens ${
      adminSessionsModule?.default?.name ?? 'nothing'
    }`,
  )

  // The failure this has to catch is silent: `sectionRoutes` falls back to `SectionPage`
  // when an item carries no `page`, so a profile menu entry with the loader dropped from it
  // would render the placeholder, lint would pass, the build would pass, and the only sign
  // would be a section that says it is under construction.
  //
  // The item holds a *loader* rather than the component now that the pages are route-level
  // chunks, so the question is asked after the answer arrives. Testing the loader itself
  // would prove nothing - an arrow function is never equal to `ProfilePage` - and awaiting
  // it keeps this check exactly as strong as it was: the module that opens still has to be
  // this page's module, or the placeholder is what the visitor would have got.
  const profileModules = await Promise.all(
    ROLES.map(async (role) => {
      const item = flatItems(role).find((candidate) => candidate.key === 'profile')
      return item?.page ? await item.page() : null
    }),
  )
  const opensProfilePage = profileModules.every((module) => module?.default === ProfilePage)
  check(
    'every role menu points its profile item at the real page rather than the placeholder',
    opensProfilePage,
    opensProfilePage
      ? 'Client, Reseller and Admin all use ProfilePage'
      : 'A ROLE WOULD OPEN THE PLACEHOLDER INSTEAD OF THE PROFILE',
  )

  const areaRouteFor = (role) =>
    (routeTable ?? []).find(
      (candidate) => candidate.element?.type === RequireAuth && candidate.element.props.role === role,
    )

  check(
    'the router declares a guarded area for each of the three roles',
    ROLES.every((role) => areaRouteFor(role)),
    ROLES.filter((role) => !areaRouteFor(role)).join(', ') || 'all three found',
  )

  // The loop hazard: `GuestOnly` sends a visitor to `homeForRole(role)` on the way in, and
  // `RequireAuth` refuses anybody whose role the route was not built for. If those two ever
  // disagreed, a sign-in would land somebody on a route that immediately refuses them with
  // a 404 - bounced between two guards, each doing exactly what it was told. The two sides
  // come from different files (`utils/session.ts` and `router/routes.tsx`), which is the only
  // reason this check is worth having.
  //
  // "Lands on" is a question for the matcher rather than for string equality now: the
  // area's own path is the prefix (`/admin`) while the landing path is a page inside it
  // (`/admin/dashboard`), so equality stopped meaning anything the moment the two were
  // separated. What has to hold is that the address opens under *this* role's area - and
  // not under the catch-all, which matches anything and would therefore never fail.
  for (const role of ROLES) {
    const route = areaRouteFor(role)
    if (!route) continue

    const landing = homeForRole(role)
    const opened = matchRoutes(routeTable, landing)?.[0]?.route

    check(
      `${role}: homeForRole lands inside the route guarded for ${role}`,
      opened === route,
      `homeForRole=${landing} opens under ${
        opened ? routePath(opened.path) : 'nothing'
      } (expected ${routePath(route.path)})`,
    )
  }

  // The same comparison one level down, for the menu. The sidebar builds its keys from
  // `areaForRole` and the router builds its children from `routePath(route.path)` - two
  // sources again - so a divergence would show up as a menu that clicks to the catch-all.
  for (const role of ROLES) {
    const route = areaRouteFor(role)
    if (!route) continue

    const root = routePath(route.path)
    // A parameterised child is a page the menu deliberately does not name - `users/:id`
    // and `user-groups/:id` are the edit forms, addressed from a row of the table the menu
    // *does* offer, and listing them would demand a second menu entry for a page that edits
    // the row you just picked. `user-groups/add` is the same exception in its other shape:
    // it has no `:` in it, but it is reached the same way - from the add button above the
    // group table - and a menu item for it would be a second way to open a form the "Add
    // group" button already opens. The tariff plan form's two addresses are the same case
    // again: the rail offers the plans table alone, and both the create button in its toolbar
    // and the edit action on its rows open the one form. Only the children a visitor could
    // click to are compared.
    const EXTRA_ROUTES = [
      'user-groups/add',
      'tariff-plans/add',
      'tariff-plans/:id',
    ]
    const fromRoutes = new Set(
      (route.children ?? [])
        .filter((child) => !child.path?.includes(':') && !EXTRA_ROUTES.includes(child.path ?? ''))
        .map((child) => (child.index ? root : `${root}/${child.path}`)),
    )
    const fromMenu = new Set(
      flatItems(role).map((item) =>
        item.path ? `${areaForRole(role)}/${item.path}` : homeForRole(role),
      ),
    )

    const menuWithoutRoute = [...fromMenu].filter((key) => !fromRoutes.has(key))
    const routeWithoutMenu = [...fromRoutes].filter((key) => !fromMenu.has(key))

    check(
      `${role}: every menu item has a route and every route has a menu item`,
      (NAVIGATION[role] ?? []).length > 0 && menuWithoutRoute.length === 0 && routeWithoutMenu.length === 0,
      [
        menuWithoutRoute.length ? `menu without route: ${menuWithoutRoute.join(', ')}` : '',
        routeWithoutMenu.length ? `route without menu: ${routeWithoutMenu.join(', ')}` : '',
      ]
        .filter(Boolean)
        .join('; ') || `${fromMenu.size} items matched`,
    )
  }

  // Both loops above compare strings with strings, so they agree with each other even when
  // both are wrong; this one asks react-router itself. A path rule the table can break
  // without a word - a child that does not sit under its parent, a segment the matcher
  // reads differently than the string that built it - is invisible to a set comparison and
  // to `lint`, and this probe has no browser to notice it with. Without this the only
  // witness to a bad nesting is a visitor getting a 404 on a page the menu offers them.
  for (const role of ROLES) {
    const route = areaRouteFor(role)
    if (!route) continue

    const keys = flatItems(role).map((item) =>
      item.path ? `${areaForRole(role)}/${item.path}` : homeForRole(role),
    )
    const unresolved = keys.filter((key) => matchRoutes(routeTable, key)?.[0]?.route !== route)

    check(
      `${role}: every address the menu offers resolves to this role's area`,
      keys.length > 0 && unresolved.length === 0,
      unresolved.length
        ? `not under ${routePath(route.path)}: ${unresolved.join(', ')}`
        : `${keys.length} addresses resolved`,
    )
  }

  // The four addresses the admin area answers without a menu item beside them. The loops
  // above deliberately exclude them, which leaves them unchecked by anything: a typo in
  // `user-groups/add` would break the button that navigates to it and every other check in
  // this file would stay green. (`video-streams/add` is a *menu* item - `videoStreamsAdd`
  // - so it is covered by those loops; only its sibling behind a row is not.)
  const EXTRA_ADMIN_ADDRESSES = [
    `/admin/users/11111111-1111-1111-1111-111111111111`,
    `/admin/user-groups/add`,
    `/admin/user-groups/11111111-1111-1111-1111-111111111111`,
    `/admin/video-streams/11111111-1111-1111-1111-111111111111`,
    `/admin/series/11111111-1111-1111-1111-111111111111`,
    `/admin/series/11111111-1111-1111-1111-111111111111/episodes`,
    `/admin/series/11111111-1111-1111-1111-111111111111/episodes/add`,
    `/admin/series/11111111-1111-1111-1111-111111111111/episodes/22222222-2222-2222-2222-222222222222`,
    `/admin/series/11111111-1111-1111-1111-111111111111/seasons/add`,
  ]
  const extraLandedOnNotFound = EXTRA_ADMIN_ADDRESSES.filter((address) => {
    const matched = matchRoutes(routeTable, address)?.[0]?.route
    return !matched || matched.path === '*'
  })
  check(
    'every address the admin area answers without a menu item still lands on its own page',
    extraLandedOnNotFound.length === 0,
    extraLandedOnNotFound.length
      ? `would open the 404: ${extraLandedOnNotFound.join(', ')}`
      : `${EXTRA_ADMIN_ADDRESSES.length} extra addresses resolved`,
  )

  // The screen a page that threw would draw, and the table that hands it out. Rendering
  // the fallback itself is the only way to see that its three strings resolve - a key
  // missing from *both* bundles is invisible to `tsc`, to `lint` and to the build - and it
  // is rendered with no router and no query client around it on purpose: that is exactly
  // how the class boundary in `main.tsx` draws it, so this call failing would mean the
  // last-resort screen itself throws.
  const errorHtml = renderToStaticMarkup(
    <AntApp>
      <ErrorFallback />
    </AntApp>,
  )
  check(
    'the error fallback offers a reload and a door to the sign-in form, translated',
    errorHtml.includes('Something went wrong') &&
      errorHtml.includes('Reload the page') &&
      errorHtml.includes('href="/login"') &&
      !errorHtml.includes('errors.'),
    errorHtml.includes('errors.')
      ? 'THE RAW KEY REACHED THE PAGE'
      : `rendered ${errorHtml.length} bytes`,
  )

  // Every route carries that screen. A route added later that forgets the property would
  // be answered by the data router's own unlocalized page - a gap nothing else here can
  // see, because it is a property of the table rather than of any page it renders.
  const routesWithoutFallback = routeTable.filter((route) => !route.errorElement)
  check(
    'every top-level route hands the same screen to a page that threw',
    routesWithoutFallback.length === 0 && routeTable.length > 0,
    routesWithoutFallback.length
      ? `routes without a fallback: ${routesWithoutFallback
          .map((route) => route.path ?? String(route.index))
          .join(', ')}`
      : `${routeTable.length} routes carry the fallback`,
  )

  // The expiry reaction's choice of door. The session is gone by the time it runs, so the
  // path being read is the only evidence of the area it belonged to - and an admin-era
  // address has to open the admin form rather than the public one, which is the same
  // by-role-never-by-entrance rule `homeForRole` answers from the other side.
  const loginFor = [
    ['/admin/users', '/admin/login'],
    ['/admin/dashboard/profile', '/admin/login'],
    ['/reseller/dashboard', '/reseller/login'],
    ['/client/index', '/login'],
    ['/somewhere-else', '/login'],
    ['/', '/login'],
  ]
  const wrongDoor = loginFor.filter(([path, expected]) => loginRouteForPath(path) !== expected)
  check(
    'a session that ended sends each area to its own sign-in entrance',
    wrongDoor.length === 0,
    wrongDoor.length
      ? `wrong door: ${wrongDoor
          .map(([path, expected]) => `${path} -> ${loginRouteForPath(path)} (expected ${expected})`)
          .join('; ')}`
      : `${loginFor.length} paths resolved`,
  )

  // Keys the shell reads, in both languages. i18next falls back to `en`, so a string
  // added to one file only fails here and not in the build - and the raw key then shows on
  // every page in the other language.
  const shellKeys = new Set([
    'titles.clientArea',
    'titles.resellerArea',
    'titles.adminArea',
    'header.accountMenu',
    'header.collapseMenu',
    'header.expandMenu',
    'header.openMenu',
    'header.themeLight',
    'header.themeDark',
    'header.themeSystem',
    'actions.backHome',
    'actions.signOut',
    'messages.notFound',
    'messages.areaNotBuilt',
    'errors.title',
    'errors.description',
    'errors.reload',
    'unsavedChanges.title',
    'unsavedChanges.content',
    'unsavedChanges.leave',
    'unsavedChanges.stay',
    'dashboard.greeting',
    'dashboard.stats.today',
    'dashboard.stats.week',
    'dashboard.stats.month',
  ])
  for (const role of ROLES) {
    for (const group of NAVIGATION[role] ?? []) {
      shellKeys.add(group.labelKey)
      for (const item of group.items) shellKeys.add(item.labelKey)
    }
  }

  const keyExists = (lng, key) => {
    let node = i18n.getResourceBundle(lng, 'translation')
    for (const part of key.split('.')) {
      if (node === null || node === undefined || typeof node !== 'object') return false
      if (!(part in node)) return false
      node = node[part]
    }
    return typeof node === 'string' && node.trim() !== ''
  }

  const untranslated = [...shellKeys].filter((key) =>
    ['en', 'ru'].some((lng) => !keyExists(lng, key)),
  )
  check(
    'every key the shell uses is defined in both locales',
    untranslated.length === 0,
    untranslated.length === 0
      ? `${shellKeys.size} keys present in en and ru`
      : `MISSING OR EMPTY: ${untranslated.join(', ')}`,
  )

  // The refusal itself. Rendering `RequireAuth` cannot reach it - an effect never runs
  // under `renderToStaticMarkup`, so the guard is permanently "still asking" here - which
  // is why it is a predicate and why this is where the role rule is actually asserted.
  const clientSession = { userId: '2', username: 'alice', role: 'Client' }
  const adminSession = { userId: '1', username: 'root', role: 'Admin' }

  check(
    'a session is refused an area built for a different role',
    sessionOpensArea(clientSession, 'Admin') === false &&
      sessionOpensArea(adminSession, 'Client') === false,
    'client is kept out of the admin area and admin out of the client one',
  )
  check(
    'a session opens its own area, and any session answers when no role is asked',
    sessionOpensArea(clientSession, 'Client') === true &&
      sessionOpensArea(adminSession, 'Admin') === true &&
      sessionOpensArea(clientSession, undefined) === true,
    'exact match, and the catch-all only requires some session',
  )
  check(
    'a missing session opens nothing, however loose the question',
    sessionOpensArea(null, 'Client') === false && sessionOpensArea(null, undefined) === false,
    'null is an answer, and the answer is no',
  )

  // The guard while it is still asking. The opposite of `GuestOnly` above, on purpose: a
  // login form has no side effects until it is submitted, while an admin sidebar shown
  // before the role has been confirmed is the expensive way to be wrong.
  const inFlightHtml = renderToStaticMarkup(
    withRouter(
      '/client/index',
      <AntApp>
        <RequireAuth role="Client">
          <AccountLayout titleKey="titles.clientArea" />
        </RequireAuth>
      </AntApp>,
    ),
  )
  check(
    'the area guard withholds the shell while it is still asking the server',
    inFlightHtml.includes('ant-skeleton') && !inFlightHtml.includes('layout-shell__header'),
    inFlightHtml.includes('ant-skeleton')
      ? `skeleton instead of a shell (${inFlightHtml.length} bytes)`
      : 'GUARD RENDERED THE SHELL BEFORE THE ROLE WAS CONFIRMED',
  )

  // The shell itself, with the session `RequireAuth` would have published - the guard is
  // not involved because it has already decided above; what is under test here is that the
  // frame draws and that it shows this role's own menu. The router *is* involved, as it is
  // for every render in this script: the menu's links, the sidebar's own navigation and the
  // unsaved-changes guard all read the router, and `withRouter` gives them one at the
  // landing path this session would have opened.
  //
  // The theme is told at both ends, because neither end reads the other: `appTheme` is what
  // `ConfigProvider` paints with, and `setThemeChoice` is what `useIsDark` reads to decide
  // the `theme` prop of the `Sider` and the `Menu`. Setting only the first would render a
  // dark algorithm with a light menu inside it - precisely the half-set declaration the app
  // refuses to make - and the probe would then be asserting that it happens.
  const renderShell = (session, dark = false) => {
    setThemeChoice(dark ? ThemeChoice.Dark : ThemeChoice.Light)
    return renderToStaticMarkup(
      withRouter(
        homeForRole(session.role),
        <ConfigProvider theme={appTheme(dark).config}>
          {/* The header now reaches for the query client - it drops every cached row
              before a sign-out moves the page - so the probe has to hand it the same
              client `App.tsx` does, or it throws instead of rendering. */}
          {withQuery(
            <AntApp>
              <SessionContext.Provider value={session}>
                <AccountLayout titleKey={`titles.${session.role.toLowerCase()}Area`} />
              </SessionContext.Provider>
            </AntApp>,
          )}
        </ConfigProvider>,
      ),
    )
  }

  const clientShell = renderShell(clientSession)
  const adminShell = renderShell(adminSession, true)
  // Back to following the OS. The choice is module state, so leaving it pinned would make
  // every render after this one in the process - and anything importing this script later -
  // answer "dark" without being asked.
  setThemeChoice(ThemeChoice.System)

  check(
    'the shell renders the logo, the area title, the name and one account-menu trigger',
    [
      'layout-shell__logo',
      'Client area',
      clientSession.username,
      'aria-label="Account menu: alice"',
      'aria-haspopup="menu"',
      'layout-shell__header',
    ].every((needle) => clientShell.includes(needle)),
    `rendered ${clientShell.length} bytes`,
  )
  check(
    'the shell renders a scrolling sidebar carrying the role own menu',
    clientShell.includes('layout-shell__sider-scroll') &&
      ['Dashboard', 'Orders'].every((needle) => clientShell.includes(needle)),
    clientShell.includes('layout-shell__sider-scroll')
      ? "sider scrolls, and the open category's items are in it"
      : 'NO SCROLL CONTAINER IN THE SIDER',
  )
  // The categories are an accordion whose open one follows the page. Both shells are
  // rendered at their role's home, which belongs to `overview`; the account category is
  // therefore closed, and its items must not be in the markup. The static render is the
  // right place for this: it proves the *initial* state is one open category rather than
  // every category, which is exactly what `defaultOpenKeys` used to get wrong.
  check(
    'the sidebar opens only the category of the page being read',
    clientShell.includes('Dashboard') &&
      clientShell.includes('Orders') &&
      !clientShell.includes('Profile') &&
      adminShell.includes('Dashboard') &&
      !adminShell.includes('Manage users'),
    'the client overview is open and the account category is closed; the admin users list is closed',
  )
  check(
    "each role is shown its own menu and never another role's",
    clientShell.includes('Orders') &&
      !clientShell.includes('Users') &&
      adminShell.includes('Users') &&
      !adminShell.includes('Orders'),
    'orders for the client, users for the admin, and neither the other way round',
  )
  check(
    'the shell renders under the dark algorithm too',
    adminShell.includes('layout-shell__header') && adminShell.includes(adminSession.username),
    `rendered ${adminShell.length} bytes with appTheme(true).config`,
  )

  // The reseller's balance chip: drawn for a reseller and for nobody else. The render lands
  // before the profile answer, so the number it draws is its zero floor - what is asserted is
  // that the chip is *there* and carries the wallet glyph, the accessible tooltip and the
  // class the stylesheet keeps out of the ≤768px username rule. A client and an admin render
  // no chip at all, not a zero.
  const resellerSession = { userId: '3', username: 'bob', role: 'Reseller' }
  const resellerShell = renderShell(resellerSession)

  check(
    'the reseller shell draws a non-interactive credit chip beside the account menu',
    resellerShell.includes('layout-shell__credits') &&
      resellerShell.includes('anticon-wallet') &&
      /<span[^>]*class="layout-shell__credits"/.test(resellerShell) &&
      !/<button[^>]*layout-shell__credits/.test(resellerShell) &&
      resellerShell.indexOf('layout-shell__credits') < resellerShell.indexOf('Account menu: bob') &&
      // The tooltip's text lives in a popup that only exists after a hover, so the *key* is
      // asserted through i18next rather than through this markup - the same treatment every
      // icon-only control's label gets.
      i18n.getResource('en', 'translation', 'header.creditBalance') === 'Credit balance' &&
      i18n.getResource('ru', 'translation', 'header.creditBalance') === 'Баланс кредитов',
    `rendered ${resellerShell.length} bytes for the reseller shell`,
  )

  check(
    'the credit chip is drawn for a reseller only, never as a zero for the other two roles',
    !clientShell.includes('layout-shell__credits') &&
      !adminShell.includes('layout-shell__credits') &&
      !clientShell.includes('Credit balance') &&
      !adminShell.includes('Credit balance'),
    `client ${clientShell.includes('layout-shell__credits')}, admin ${adminShell.includes('layout-shell__credits')}`,
  )

  // The variant is declared in two places and both have to move together: `Sider` does not
  // pass its `theme` to the `Menu` inside it, so a half-set pair is not a mismatch antd
  // would notice or repair. It is the kind of bug that renders, styles nothing red and is
  // found by eye only after somebody has already shipped it.
  check(
    'each theme declares the same menu variant on both the rail and the menu',
    !clientShell.includes('ant-menu-dark') &&
      clientShell.includes('ant-menu-light') &&
      adminShell.includes('ant-menu-dark') &&
      adminShell.includes('ant-layout-sider-dark'),
    'light rail with a light menu, dark rail with a dark menu',
  )

  // The dark rail's selected *category* cannot be checked the way the row can be. rc-menu
  // records key paths in a `useEffect` (`@rc-component/menu/lib/SubMenu/index.js`), so a
  // `renderToStaticMarkup` pass has no paths to match, `ant-menu-submenu-selected` never
  // reaches static HTML, and the class exists only after mount, in a browser. That is also
  // why this one reached a human instead of this script: antd's rule for that class paints
  // the title with `--ant-menu-dark-item-selected-color`, which is `#141414` - the label
  // for the row, whose background is a primary fill - and the category has no background
  // of its own, so it became the rail's colour on the rail. What *is* checkable is the
  // split: the rule in `index.css` exists, names the class antd uses, and says this theme's
  // own primary - the very blue the selected row underneath it is filled with.
  const railCss = readFileSync('src/index.css', 'utf8')

  check(
    'the playlist regeneration question names the id that stops working, in both bundles',
    // The dialog cannot be opened by a static render, so the wording is read through i18next: the
    // body must carry the id placeholder (a question that does not say *what* disappears is not a
    // confirmation), must differ from the title, and the same three keys are asserted in Russian
    // because that bundle is the one a missing key silently falls back from.
    ['en', 'ru'].every((lng) => {
      const body = i18n.t('subscriptions.playlistRegenerateConfirmBody', { lng })
      const title = i18n.t('subscriptions.playlistRegenerateConfirmTitle', { lng })
      const ok = i18n.t('subscriptions.playlistRegenerateConfirmOk', { lng })

      return body.includes('{{playlistId}}') && body !== title && ok !== title
    }),
    `en: "${i18n.t('subscriptions.playlistRegenerateConfirmBody', { lng: 'en' })}"`,
  )

  check(
    'the prolongation questions name the date where one can be computed, and a sentence where it cannot',
    // The single-row body has to carry the two placeholders the dialog fills - the owner's name
    // and the projected date - or the operator is asked to confirm a change they cannot see. The
    // no-date branch gets its own sentence rather than repeating the title: a dialog whose body
    // restates its heading says nothing, and it is the only line that can explain *why* no date is
    // shown.
    ['en', 'ru'].every((lng) => {
      const body = i18n.t('subscriptions.prolongConfirmBody', { lng })
      const fallback = i18n.t('subscriptions.prolongNoDateBody', { lng })
      const title = i18n.t('subscriptions.prolongConfirmTitle', { lng })

      return (
        body.includes('{{name}}') &&
        body.includes('{{expiry}}') &&
        body.includes('{{plan}}') &&
        fallback !== title &&
        fallback.length > title.length &&
        // The bulk body names the rule rather than a date: a selection has as many answers as rows.
        !i18n.t('subscriptions.bulk.prolongBody', { lng }).includes('{{expiry}}')
      )
    }),
    `en: "${i18n.t('subscriptions.prolongConfirmBody', { lng: 'en' })}"`,
  )

  check(
    'the two device-clear bulk entries name devices rather than MAC addresses, in both bundles',
    // The owner's round-4 item 11.1. Read through i18next in both languages, because the wording
    // this replaces lived in `ru.ts` alone: the render above draws whichever language is active
    // and it would have passed while the Russian menu still said "Очистить MAC-адреса". The word
    // "MAC" must be gone from *both*, and the playlist entry is asserted beside them so the three
    // keep reading as one family.
    //
    // The **icons** for these two entries are deliberately not asserted: a `Dropdown` renders its
    // menu on open, and `renderToStaticMarkup` never opens one, so `anticon-usb`/`anticon-desktop`
    // appear in no markup this probe can reach. Reading the page's own source instead would only
    // assert that the text matches itself. That half was verified in a browser.
    i18n.t('subscriptions.bulk.deleteMag', { lng: 'en' }) === 'Clear the MAG devices' &&
      i18n.t('subscriptions.bulk.deleteMag', { lng: 'ru' }) === 'Очистить устройства MAG' &&
      !/MAC/i.test(i18n.t('subscriptions.bulk.deleteMag', { lng: 'en' })) &&
      !/MAC/i.test(i18n.t('subscriptions.bulk.deleteMag', { lng: 'ru' })) &&
      i18n.t('subscriptions.bulk.deletePlaylist', { lng: 'en' }) === 'Clear the playlist' &&
      i18n.t('subscriptions.bulk.deletePlaylist', { lng: 'ru' }) === 'Очистить плейлист' &&
      i18n.t('subscriptions.bulk.deleteEnigma2', { lng: 'en' }) === 'Clear the Enigma2 devices' &&
      i18n.t('subscriptions.bulk.deleteEnigma2', { lng: 'ru' }) ===
        'Очистить устройства Enigma2',
    `en=${i18n.t('subscriptions.bulk.deleteMag', { lng: 'en' })}; ru=${i18n.t('subscriptions.bulk.deleteMag', { lng: 'ru' })}`,
  )

  check(
    'the strip merge rules are scoped, drop the expanded surface, and answer the hover too',
    // Three facts the browser measurement in round 4 established, none of which any other checker
    // can see: the strip paints no surface of its own, the row/strip border goes while the strip
    // keeps its own, and the hover is driven by `:hover` rather than by
    // `ant-table-cell-row-hover` - rc-table adds that class only to cells that carry a record, so
    // an expanded row never receives it and the earlier hover rule matched nothing at all.
    /\.subscriptions-table[\s\S]{0,200}?tr\.ant-table-expanded-row > td \{[^}]*background: transparent;[^}]*padding-block: 4px;/.test(
      railCss,
    ) &&
      /\.subscriptions-table[\s\S]{0,300}?tr\.ant-table-row:has\(\+ tr\.ant-table-expanded-row\) > td \{[^}]*border-bottom-width: 0;/.test(
        railCss,
      ) &&
      /\.subscriptions-table[\s\S]{0,300}?tr\.ant-table-row:not\(\.subscription-row-selected\):hover \+ tr\.ant-table-expanded-row > td \{[^}]*var\(--ant-table-row-hover-bg\)/.test(
        railCss,
      ) &&
      /\[data-theme=['"]light['"]\] \.subscriptions-table[\s\S]{0,300}?tr\.subscription-row-selected:hover \+ tr\.ant-table-expanded-row > td \{[^}]*#add8ff/.test(
        railCss,
      ) &&
      /\[data-theme=['"]dark['"]\] \.subscriptions-table[\s\S]{0,300}?tr\.subscription-row-selected \+ tr\.ant-table-expanded-row > td \{[^}]*#213b5b/.test(
        railCss,
      ) &&
      // The dead hover selector must be gone, not merely superseded.
      !/tr\.ant-table-expanded-row > td\.ant-table-cell-row-hover/.test(railCss),
    'the subscriptions strip rules are present in index.css',
  )

  // The two halves of the compact card header, in the stylesheet that is the only thing that
  // can express them. At 1200 the wrapper turns into a column so the title gets a row of its
  // own; at 768 the toolbar's `Space` turns into a column too, so every control gets a full
  // width row. Neither is visible to `typecheck`, `lint` or the static markup above, and
  // removing either one restores the exact overflow the measurements named: a title of
  // width 0 beside a toolbar that fills the row.
  check(
    'the compact stylesheet gives the page title its own row and stacks the toolbar on a phone',
    /@media \(max-width: 1200px\)[\s\S]*?\.layout-shell \.page-card \.ant-card-head-wrapper \{[\s\S]*?flex-direction: column;/.test(
      railCss,
    ) &&
      /@media \(max-width: 768px\)[\s\S]*?\.layout-shell \.page-card \.ant-card-extra \.ant-space \{[\s\S]*?flex-direction: column;/.test(
        railCss,
      ),
    'the title row at 1200 and the stacked toolbar at 768 are both present',
  )

  // The user log's head is the one that does not fit between 1201px and 1500px: seven
  // controls and a long title in one row, and the title was measured at width 0 while the
  // toolbar kept the row. The rule is scoped to that card's own class so no other page's head
  // changes, and it is the half `probe:render` can see - the stylesheet side - because a
  // static render carries no layout. It must keep the title at its natural width and let the
  // head wrap, exactly as the ≤1200px rule does.
  check(
    'the user log head keeps its title at its natural width above 1200px and wraps instead of squeezing it',
    userLogsHtml.includes('user-logs-card') &&
      /@media \(min-width: 1201px\)[\s\S]*?\.layout-shell \.user-logs-card \.ant-card-head-wrapper \{[^}]*flex-wrap: wrap;/.test(
        railCss,
      ) &&
      /\.layout-shell \.user-logs-card \.ant-card-head-title \{[^}]*flex: 0 0 auto;[^}]*min-width: max-content;/.test(
        railCss,
      ),
    userLogsHtml.includes('user-logs-card')
      ? 'the card carries user-logs-card and index.css keeps its title at max-content'
      : 'THE USER LOG CARD LACKS THE user-logs-card CLASS',
  )

  // The reseller's balance chip is `flex: none` - every other control in that row has its own
  // min-width and a balance that shrank under a long username would lose a digit - and the
  // ≤768px rule that hides the username names the title and the username only. A balance that
  // disappeared on a phone would be the one number the owner checks, gone exactly where the
  // page title has already given way.
  const creditsChipRule =
    /\.layout-shell__credits \{[^}]*\}/.exec(railCss)?.[0] ?? ''
  const narrowHeaderRule =
    /@media \(max-width: 768px\) \{[^}]*?\.layout-shell \.layout-shell__title,\s*\.layout-shell__username \{[\s\S]*?\n {2}\}/.exec(
      railCss,
    )?.[0] ?? ''
  check(
    'the credit chip keeps its width and is not caught by the phone rule that hides the username',
    creditsChipRule.includes('flex: none') &&
      creditsChipRule.includes('white-space: nowrap') &&
      narrowHeaderRule.includes('layout-shell__username') &&
      !narrowHeaderRule.includes('layout-shell__credits'),
    creditsChipRule
      ? `chip rule present; the 768px rule reads ${narrowHeaderRule.replace(/\s+/g, ' ').slice(0, 120)}`
      : 'THE CHIP RULE IS MISSING FROM index.css',
  )

  // The selector is pinned whole, colour included, and not merely the colour: the leading
  // `.layout-shell__sider-scroll` is the fourth class that puts this rule above antd's own
  // `-submenu-selected` rule, which is three classes once `:where()` is taken for what it
  // costs - nothing. Drop it and two equal rules are left to be settled by whichever
  // stylesheet landed last, and antd injects its at runtime, after this one.
  const titleColor =
    /\.layout-shell__sider-scroll \.ant-menu-dark \.ant-menu-submenu-selected > \.ant-menu-submenu-title \{\s*color:\s*(#[0-9a-f]{6});/.exec(
      railCss,
    )?.[1]
  const darkTheme = appTheme(true)
  const lightTheme = appTheme(false)

  // A sorted column is told apart by its arrow and by nothing else. antd tints a sorted
  // header (`headerSortActiveBg` = `colorFillSecondary`) and every body cell of the column
  // (`bodySortBg` = `colorFillAlter`) unless it is told otherwise, so the theme hands the
  // sorted header the very token the resting header is painted from and clears the body
  // tint. The token, not a hex: `headerBg` is `colorFillAlter` flattened over
  // `colorBgContainer`, which is what the header sits on, so the two come out one colour in
  // either theme - and this check would fail if either half of that were ever unwound.
  check(
    'a sorted column is marked by its arrow, not by a tint in either theme',
    [lightTheme, darkTheme].every(
      (t) =>
        t.config.components.Table.headerSortActiveBg === t.derived.colorFillAlter &&
        t.config.components.Table.bodySortBg === 'transparent',
    ),
    [
      ['light', lightTheme],
      ['dark', darkTheme],
    ]
      .map(
        ([name, t]) =>
          `${name}: sorted header ${t.config.components.Table.headerSortActiveBg} vs resting ${t.derived.colorFillAlter}, body ${t.config.components.Table.bodySortBg}`,
      )
      .join('; '),
  )

  check(
    'the dark rail overrides the selected category with the primary it renders with',
    titleColor === darkTheme.derived.colorPrimary,
    `index.css says ${titleColor ?? 'NO RULE'}, theme says ${darkTheme.derived.colorPrimary}`,
  )

  // The plan-name link's dark colour, measured against every surface this table actually paints.
  //
  // The owner's round-5 item 12.3: antd's link colour is legible on the plain row and drops to
  // 2.8:1 on a selected one - and the link used to be drawn at 0.9 opacity inside a 0.75 line,
  // which took the plain row down to 2.8:1 as well. The replacement is pinned here rather than
  // merely present, and every one of the four backgrounds is checked: a colour chosen against the
  // plain row alone would pass on the row nobody is looking at and fail on the row they are.
  const planLinkDark =
    /\[data-theme=['"]dark['"]\] \.subscriptions-table \.ant-table-wrapper a\.subscription-plan-link \{\s*color:\s*(#[0-9a-f]{6});/.exec(
      railCss,
    )?.[1]

  const darkSurfaces = {
    row: '#141414',
    hover: '#1d1d1d',
    selected: '#213b5b',
    selectedHover: '#284f7e',
  }

  const planLinkRatios = planLinkDark
    ? Object.entries(darkSurfaces).map(([name, bg]) => [name, contrast(planLinkDark, bg)])
    : []

  check(
    'the plan link stays readable on every dark surface this table paints',
    planLinkDark !== undefined && planLinkRatios.every(([, ratio]) => ratio >= 4.5),
    planLinkDark === undefined
      ? 'index.css has no dark rule for .subscription-plan-link'
      : `${planLinkDark}: ${planLinkRatios.map(([n, r]) => `${n}=${r.toFixed(2)}`).join(', ')}`,
  )

  // The rows are tightened to 8px, and only in this table. The number is asserted rather than the
  // rule's existence: "it has a padding rule" would pass at the 16px this replaces.
  check(
    'the subscriptions rows carry 8px of vertical padding and no other table is touched',
    /\.subscriptions-table \.ant-table-wrapper \.ant-table-tbody > tr\.ant-table-row > td \{\s*padding-block: 8px;/.test(
      railCss,
    ) &&
      !/(?<!\.subscriptions-table )\.ant-table-wrapper \.ant-table-tbody > tr\.ant-table-row > td \{\s*padding-block:/.test(
        railCss,
      ),
    'the scoped 8px rule is present and unscoped',
  )

  const rowRatio = contrast(
    darkTheme.config.components.Menu.darkItemSelectedColor,
    darkTheme.derived.colorPrimary,
  )
  const darkTitleRatio = titleColor ? contrast(titleColor, darkTheme.derived.colorBgContainer) : 0
  const lightTitleRatio = contrast(
    lightTheme.derived.colorPrimary,
    lightTheme.derived.colorBgContainer,
  )
  check(
    'every menu selection label clears 4.5:1 on the surface it is painted on',
    rowRatio >= 4.5 && darkTitleRatio >= 4.5 && lightTitleRatio >= 4.5,
    `dark row on its fill ${rowRatio.toFixed(2)}, dark category on the rail ${darkTitleRatio.toFixed(
      2,
    )}, light category on the rail ${lightTitleRatio.toFixed(2)}`,
  )

  console.log(
    failed === 0 ? '\nall render checks passed' : `\n${failed} render check(s) failed`,
  )
  // An explicit exit, and it is load-bearing rather than tidy: every query the pages
  // created above keeps its garbage-collection timer (five minutes, TanStack Query's
  // default `gcTime`) alive after the process would otherwise have finished, because
  // `renderToStaticMarkup` never unmounts anything. Without this line the probe sits
  // through the whole five minutes after printing its verdict, which reads as a hang.
  process.exit(failed === 0 ? 0 : 1)
}

main()
