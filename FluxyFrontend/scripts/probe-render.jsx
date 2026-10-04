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

  // The failure this has to catch is silent: `sectionRoutes` falls back to `SectionPage`
  // when an item carries no `page`, so a profile menu entry with the component dropped from
  // it would render the placeholder, lint would pass, the build would pass, and the only
  // sign would be a section that says it is under construction.
  const profileItems = ROLES.map((role) =>
    flatItems(role).find((item) => item.key === 'profile'),
  )
  check(
    'every role menu points its profile item at the real page rather than the placeholder',
    profileItems.every((item) => item?.page === ProfilePage),
    profileItems.every((item) => item?.page === ProfilePage)
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

  console.log(
    failed === 0 ? '\nall render checks passed' : `\n${failed} render check(s) failed`,
  )
  if (failed > 0) process.exitCode = 1
}

main()
