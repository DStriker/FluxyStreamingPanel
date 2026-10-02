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
import { MemoryRouter } from 'react-router-dom'
import { App as AntApp } from 'antd'
import i18n from '../src/i18n.js'
import RegisterPage from '../src/pages/RegisterPage.jsx'
import ClientLoginPage from '../src/pages/ClientLoginPage.jsx'
import ResellerLoginPage from '../src/pages/ResellerLoginPage.jsx'
import AdminLoginPage from '../src/pages/AdminLoginPage.jsx'
import ConfirmCodeForm from '../src/components/ConfirmCodeForm.jsx'
import GuestOnly from '../src/components/GuestOnly.jsx'

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

  // Only two of the four pages ever had a footer, and which two is a fact about the app
  // rather than an accident: `git log -S footer` over Reseller/Admin returns nothing, so
  // they have never offered a cross-link. Asserting one there would fail forever, and the
  // fix would be inventing a link nobody asked for.
  const cases = [
    {
      page: ClientLoginPage,
      path: '/login',
      mustContain: ["Don't have an account? Sign up", 'Sign in'],
      label: 'client login links to registration',
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
      page: ResellerLoginPage,
      path: '/reseller/login',
      mustContain: ['Sign in'],
      label: 'reseller login renders its form',
      hasFooter: false,
    },
    {
      page: AdminLoginPage,
      path: '/admin/login',
      mustContain: ['Sign in'],
      label: 'admin login renders its form',
      hasFooter: false,
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

  console.log(
    failed === 0 ? '\nall render checks passed' : `\n${failed} render check(s) failed`,
  )
  if (failed > 0) process.exitCode = 1
}

main()
