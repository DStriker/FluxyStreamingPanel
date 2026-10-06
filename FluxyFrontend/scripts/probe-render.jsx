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
import { matchRoutes, MemoryRouter } from 'react-router-dom'
import { App as AntApp, ConfigProvider } from 'antd'
import i18n from '../src/i18n'
import RegisterPage from '../src/pages/RegisterPage'
import ClientLoginPage from '../src/pages/ClientLoginPage'
import ResellerLoginPage from '../src/pages/ResellerLoginPage'
import AdminLoginPage from '../src/pages/AdminLoginPage'
import ForgotPasswordPage from '../src/pages/ForgotPasswordPage'
import ProfilePage from '../src/pages/ProfilePage'
import SessionsPage from '../src/pages/SessionsPage'
import TimeZoneCard from '../src/components/TimeZoneCard'
import LoginGuardCard from '../src/components/LoginGuardCard'
import ConfirmCodeForm from '../src/components/ConfirmCodeForm'
import GuestOnly from '../src/components/GuestOnly'
import RequireAuth from '../src/components/RequireAuth'
import AccountLayout from '../src/components/AccountLayout'
import { routes as routeTable } from '../src/router/routes'
import { SessionContext } from '../src/lib/sessionContext'
import { NAVIGATION, flatItems } from '../src/lib/navigation'
import { areaForRole, homeForRole, sessionOpensArea } from '../src/lib/session'
import { appTheme, setThemeChoice, ThemeChoice } from '../src/lib/theme'
import { routePath } from '../src/config/index'

const render = (Page, path) =>
  renderToStaticMarkup(
    <MemoryRouter initialEntries={[path]}>
      <AntApp>
        <Page />
      </AntApp>
    </MemoryRouter>,
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
    <MemoryRouter initialEntries={['/register']}>
      <AntApp>
        <ConfirmCodeForm email="probe@example.com" onSubmit={() => {}} onBack={() => {}} />
      </AntApp>
    </MemoryRouter>,
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
    <MemoryRouter initialEntries={['/login']}>
      <AntApp>
        <GuestOnly>
          <ClientLoginPage />
        </GuestOnly>
      </AntApp>
    </MemoryRouter>,
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
    <MemoryRouter initialEntries={['/client/sessions']}>
      <AntApp>
        <SessionsPage sectionKey="nav.items.sessions" />
      </AntApp>
    </MemoryRouter>,
  )
  check(
    'the visit history page draws its heading and its table before any rows arrive',
    sessionsHtml.includes('Sign-in history') &&
      sessionsHtml.includes('ant-table') &&
      !sessionsHtml.includes('nav.items.sessions'),
    `rendered ${sessionsHtml.length} bytes with the history still being fetched`,
  )

  // The time zone card cannot be asserted through the page above: the page is a spinner
  // until the server answers, and an effect never runs under `renderToStaticMarkup`, so
  // the loaded state that holds this card is unreachable from here. That is exactly why
  // the card is its own component - rendered directly, this is what proves it draws its
  // heading, its hint and the zone the account actually stored, rather than accepting the
  // props and putting nothing on the page.
  const timezoneHtml = renderToStaticMarkup(
    <MemoryRouter initialEntries={['/profile']}>
      <AntApp>
        <TimeZoneCard value="Europe/Moscow" saving={false} onChange={() => {}} />
      </AntApp>
    </MemoryRouter>,
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
    <MemoryRouter initialEntries={['/profile']}>
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
      </AntApp>
    </MemoryRouter>,
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
    <MemoryRouter initialEntries={['/profile']}>
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
      </AntApp>
    </MemoryRouter>,
  )
  const disabledCount = guardOffHtml.split('disabled').length - 1
  check(
    'the login guard disables its allow lists while protection is off, without hiding them',
    guardOffHtml.includes('198.51.100.7') && disabledCount >= 3,
    guardOffHtml.includes('198.51.100.7')
      ? `${disabledCount} occurrences of "disabled", the stored lists still on the card`
      : 'THE LISTS DISAPPEARED FROM THE CARD',
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
  // come from different files (`lib/session.ts` and `router/routes.tsx`), which is the only
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
    const fromRoutes = new Set(
      (route.children ?? []).map((child) => (child.index ? root : `${root}/${child.path}`)),
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

  // Keys the shell reads, in both languages. i18next falls back to `en`, so a string
  // added to one file only fails here and not in the build - and the raw key then shows on
  // every page in the other language.
  const shellKeys = new Set([
    'titles.clientArea',
    'titles.resellerArea',
    'titles.adminArea',
    'header.settings',
    'header.collapseMenu',
    'header.expandMenu',
    'header.themeLight',
    'header.themeDark',
    'header.themeSystem',
    'actions.backHome',
    'messages.notFound',
    'messages.areaNotBuilt',
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
    <MemoryRouter initialEntries={['/client/index']}>
      <AntApp>
        <RequireAuth role="Client">
          <AccountLayout titleKey="titles.clientArea" />
        </RequireAuth>
      </AntApp>
    </MemoryRouter>,
  )
  check(
    'the area guard withholds the shell while it is still asking the server',
    inFlightHtml.includes('ant-skeleton') && !inFlightHtml.includes('layout-shell__header'),
    inFlightHtml.includes('ant-skeleton')
      ? `skeleton instead of a shell (${inFlightHtml.length} bytes)`
      : 'GUARD RENDERED THE SHELL BEFORE THE ROLE WAS CONFIRMED',
  )

  // The shell itself, with the session `RequireAuth` would have published. The router is
  // not involved because the guard above has already decided; what is under test here is
  // that the frame draws and that it shows this role's own menu.
  //
  // The theme is told at both ends, because neither end reads the other: `appTheme` is what
  // `ConfigProvider` paints with, and `setThemeChoice` is what `useIsDark` reads to decide
  // the `theme` prop of the `Sider` and the `Menu`. Setting only the first would render a
  // dark algorithm with a light menu inside it - precisely the half-set declaration the app
  // refuses to make - and the probe would then be asserting that it happens.
  const renderShell = (session, dark = false) => {
    setThemeChoice(dark ? ThemeChoice.Dark : ThemeChoice.Light)
    return renderToStaticMarkup(
      <MemoryRouter initialEntries={[homeForRole(session.role)]}>
        <ConfigProvider theme={appTheme(dark).config}>
          <AntApp>
            <SessionContext.Provider value={session}>
              <AccountLayout titleKey={`titles.${session.role.toLowerCase()}Area`} />
            </SessionContext.Provider>
          </AntApp>
        </ConfigProvider>
      </MemoryRouter>,
    )
  }

  const clientShell = renderShell(clientSession)
  const adminShell = renderShell(adminSession, true)
  // Back to following the OS. The choice is module state, so leaving it pinned would make
  // every render after this one in the process - and anything importing this script later -
  // answer "dark" without being asked.
  setThemeChoice(ThemeChoice.System)

  check(
    'the shell renders the logo, the area title, the name and the way out',
    [
      'layout-shell__logo',
      'Client area',
      clientSession.username,
      'Sign out',
      'layout-shell__header',
    ].every((needle) => clientShell.includes(needle)),
    `rendered ${clientShell.length} bytes`,
  )
  check(
    'the shell renders a scrolling sidebar carrying the role own menu',
    clientShell.includes('layout-shell__sider-scroll') &&
      ['Dashboard', 'Orders', 'Profile'].every((needle) => clientShell.includes(needle)),
    clientShell.includes('layout-shell__sider-scroll')
      ? 'sider scrolls, and the client items are in it'
      : 'NO SCROLL CONTAINER IN THE SIDER',
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
  if (failed > 0) process.exitCode = 1
}

main()
