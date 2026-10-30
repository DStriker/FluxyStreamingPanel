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
import { App as AntApp, ConfigProvider } from 'antd'
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
import TariffPlansPage from '../src/pages/admin/TariffPlansPage'
import TariffPlanFormPage from '../src/pages/admin/TariffPlanFormPage'
import SubscriptionsPage from '../src/pages/admin/SubscriptionsPage'
import SubscriptionFormPage from '../src/pages/admin/SubscriptionFormPage'
import SubscriptionSubrows from '../src/features/subscriptions/components/SubscriptionSubrows'
import BouquetsPage from '../src/pages/admin/BouquetsPage'
import BouquetContentPage from '../src/pages/admin/BouquetContentPage'
import LastSeenCell from '../src/features/users/components/LastSeenCell'
import StreamStateCell from '../src/features/video-streams/components/StreamStateCell'
import AudioStreamStateCell from '../src/features/audio-streams/components/StreamStateCell'
import { RowActions } from '../src/components/table'
import TimeZoneCard from '../src/features/profile/components/TimeZoneCard'
import LoginGuardCard from '../src/features/profile/components/LoginGuardCard'
import ConfirmCodeForm from '../src/features/auth/components/ConfirmCodeForm'
import GuestOnly from '../src/features/auth/components/GuestOnly'
import RequireAuth from '../src/features/auth/components/RequireAuth'
import ErrorFallback from '../src/components/ErrorFallback'
import AccountLayout from '../src/layouts/AccountLayout'
import { routes as routeTable } from '../src/router/routes'
import { SessionContext } from '../src/utils/sessionContext'
import { NAVIGATION, flatItems, menuPath } from '../src/utils/navigation'
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
  // sixty characters. Fixed layout, a width on each of the five columns and a body that scrolls
  // instead of stretching are three separate facts, and dropping any one of them lets the
  // content set the width again. None of that is visible to typecheck, lint or the build.
  const sessionsCols = [...sessionsHtml.matchAll(/<col\s[^>]*>/g)].map((m) => m[0])
  check(
    'the visit history table is fixed-layout with a width on each column',
    /table-layout:\s*fixed/.test(sessionsHtml) &&
      sessionsCols.length === 5 &&
      ['300px', '170px', '150px', '240px', '280px'].every((w) =>
        sessionsCols.some((col) => col.includes(`width:${w}`)),
      ),
    `${sessionsCols.length} columns: ${sessionsCols.join(' ')}`,
  )

  // The resize handles, and the fact that they are the whole of the resizing feature. They are
  // produced by `ResizableHeaderCell` through `components.header.cell` from props supplied by
  // `onHeaderCell` on each of the five columns, and every link in that chain is invisible to
  // typecheck, lint and the build: break any one of them and the table still draws five perfect
  // columns with nothing on them to drag and nothing focusable for a keyboard to reach.
  //
  // The last condition is the one that caught a real bug. `aria-label` cannot simply be passed
  // through `onHeaderCell`, because `useSorter` writes that attribute onto a sortable header
  // cell itself, *after* what the column returned - so the two sortable columns came back
  // labelled "When" and "IP address" while the other three said "Resize the Country column",
  // and the table was perfectly well formed and named two of its handles wrong. Only the
  // rendered markup shows that; the types are satisfied either way.
  const sessionHandles = [...sessionsHtml.matchAll(/<span[^>]*table-col-resizer[^>]*>/g)].map(
    (m) => m[0],
  )
  check(
    'every visit history column carries a keyboard-reachable resize handle, named for resizing',
    sessionHandles.length === 5 &&
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
  const sessionHeaderCells = [...sessionsHtml.matchAll(/<th\b[^>]*>/g)].map((m) => m[0])
  check(
    'a sortable visit history header keeps its own access to sorting',
    sessionHeaderCells.some(
      (th) => th.includes('aria-label="When"') && th.includes('tabindex="0"'),
    ),
    `${sessionHeaderCells.length} header cells, sortable one: ${
      sessionHeaderCells.find((th) => th.includes('aria-label="When"'))?.slice(0, 220) ?? 'none'
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
    'the subscriptions section carries the add form, the management list and the price list',
    subscriptionItemKeys.join(',') === 'subscriptionsAdd,subscriptionsManage,tariffPlans',
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
  // row arrives, seven widths that keep it the same size as the two tables beside it in the
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
  // Seven columns and no selection column in front of them - there is nothing here to
  // select - so the seven alone are the 1230 that the accounts table and the group table
  // reach only with their checkbox. All three then fit the same space beside the sidebar.
  check(
    'the visit-history table is fixed-layout with its seven deliberate widths, in order',
    /table-layout:\s*fixed/.test(adminSessionsHtml) &&
      adminSessionsWidths.join(',') === '170,250,150,120,190,230,120' &&
      adminSessionsTotal === 1230,
    `${adminSessionsCols.length} columns: [${adminSessionsWidths.join(', ')}] = ${adminSessionsTotal}px`,
  )

  const adminSessionsHandles = [
    ...adminSessionsHtml.matchAll(/<span[^>]*table-col-resizer[^>]*>/g),
  ].map((m) => m[0])
  check(
    'every visit-history column carries a keyboard-reachable resize handle, named for resizing',
    adminSessionsHandles.length === 7 &&
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

  // Only "When" and "IP address" carry a sorter, for the same reason the account's own
  // history offers those two and no more: they are the stored columns. The three geo fields
  // are resolved from GeoIP while a page is built and the account column is a join, so there
  // is nothing in the row for the endpoint to order by.
  const adminSessionsHeaders = [...adminSessionsHtml.matchAll(/<th\b[^>]*>/g)].map((m) => m[0])
  check(
    'a sortable visit-history header keeps its own access to sorting',
    adminSessionsHeaders.length === 7 &&
      adminSessionsHeaders.some((th) => th.includes('aria-label="When"') && th.includes('tabindex="0"')),
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

  check(
    'the subscriptions table draws its own heading and its table before any rows arrive',
    subscriptionsHtml.includes('>Subscriptions<') &&
      subscriptionsHtml.includes('ant-table') &&
      !subscriptionsHtml.includes('nav.items.subscriptionsManage'),
    `rendered ${subscriptionsHtml.length} bytes with the subscriptions still being fetched`,
  )

  check(
    'the subscriptions table offers its five columns, the three filters and the search',
    subscriptionsHtml.includes('>Expires<') &&
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

  // Every write button on a service strip is gated on `editSubscriptions`, and copy is not: a
  // view-only operator must see a greyed control with the reason rather than a live button whose
  // request the server refuses. `probe:render` lands before `getProfile` answers, so `canEdit` is
  // false here - which is exactly the read-only state this asserts.
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
            createdAt: '2026-01-01T00:00:00+00:00',
            expireAt: null,
            isTrial: false,
            status: 'active',
            maxConnections: 2,
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
      // The add line offers the missing Enigma2 device and nothing else, because the playlist and
      // the MAG are attached.
      subrows.includes('Add Enigma2') &&
      !subrows.includes('Generate a playlist') &&
      !subrows.includes('Add MAG'),
    `rendered ${subrows.length} bytes`,
  )

  check(
    'every write button on a strip is disabled for a view-only operator',
    // Two services are attached, so exactly two buttons stay live - the two copy buttons, which
    // change nothing. Every regenerate, change, delete and add button carries `disabled`.
    [...subrows.matchAll(/<button[^>]*>/g)].filter((m) => !m[0].includes('disabled')).length === 2,
    `live buttons: ${[...subrows.matchAll(/<button[^>]*>/g)].filter((m) => !m[0].includes('disabled')).length}`,
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

  check(
    'the subscription edit form waits for the row before drawing the fields',
    // The same loading state every edit form here draws: a spinner in a bare card, and no field
    // painted from whatever the previous screen happened to hold.
    subscriptionEditFormHtml.includes('ant-spin') &&
      !subscriptionEditFormHtml.includes('MAG MAC address') &&
      !subscriptionEditFormHtml.includes('Tariff plan'),
    `rendered ${subscriptionEditFormHtml.length} bytes with the spinner up`,
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
    bouquetsHtml,
    bouquetContentHtml,
    subscriptionsHtml,
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

  check(
    'the dark rail overrides the selected category with the primary it renders with',
    titleColor === darkTheme.derived.colorPrimary,
    `index.css says ${titleColor ?? 'NO RULE'}, theme says ${darkTheme.derived.colorPrimary}`,
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
