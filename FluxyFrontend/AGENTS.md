# AGENTS.md

Single-package Vite + React 19 SPA (antd 6, react-router-dom 7, react-i18next). All source is `.jsx`/`.js` plain JS — no TypeScript, no test framework, no typecheck step.

## Commands

```bash
npm run dev      # vite on :5173, auto-opens the browser
npm run build    # vite build -> dist/
npm run preview  # serve dist/
npm run lint     # eslint . (only verification available; there are no tests)
npm run favicon  # regenerate public/favicon.ico + favicon.svg from public/logo.svg
```

`npm run dev` opens a browser by default; for automation/debugging use `npm run dev -- --no-open` (matches the `dev (debug)` VS Code task, which the `launch.json` Chrome/Edge configs depend on).

Verify changes with `npm run lint` (only `.js`/`.jsx` are linted) and `npm run build` (the only way to catch import/JSX errors — there is no typecheck).

## Structure

- `src/main.jsx` -> `src/App.jsx` -> `src/router/index.jsx`
- `src/App.jsx` owns the antd `ConfigProvider` (locale + dark algorithm). Components must be rendered under `<AntApp>` to use `App.useApp()`; `AuthForm` relies on that.
- `src/components/AuthForm.jsx` is the single shared form. All four pages in `src/pages/` are thin wrappers that pass `action` + `onSubmit`; add behavior there, not by copying the form.
- `src/config/index.js` holds all route paths and the reCAPTCHA key, read from `import.meta.env`. Never hardcode route strings in pages — use `config.X_ROUTE` and `routePath()`.
- `src/lib/api.js`, `csrf.js`, `recaptcha.js` are the network layer. `src/locales/{en,ru}.js` are plain nested objects used as i18next resources.

## Gotchas

- **No dev proxy exists.** `vite.config.js` has no `server.proxy` and API calls go to relative `/api/*` (e.g. `/api/auth/csrf`, `/api/auth/client-login`). Without a backend on the same origin, submits fail with `messages.serverUnavailable`. The `VITE_*` vars in `.env` only configure routes, not the API base.
- **Page `action` uses snake_case, the API path uses kebab-case.** `ClientLoginPage` passes `action="client_login"` (used for reCAPTCHA) but submits to `action: 'client-login'`. Keep both in sync when adding a flow.
- **reCAPTCHA is disabled in dev** (`getCaptchaToken` returns `null` when `import.meta.env.DEV`) and skipped entirely when the site key is empty. Captures in prod only.
- **CSRF is best-effort and cached in a module-level variable.** `getCsrfToken` reads the `XSRF-TOKEN` cookie, falls back to `GET /api/auth/csrf`, and silently returns `null` when the backend is absent. `AuthForm` calls it with `{ refresh: true }` on each submit.
- **`public/logo.jpg` is missing** but `AuthForm` renders `<Image src="/logo.jpg" />`. Only `logo.png` / `logo.svg` / `favicon.*` exist in `public/`, so the card logo 404s (including in `dist/`). Fix the reference rather than adding a new binary if you touch it.
- `index.html` hardcodes `lang="ru"`; the real language comes from i18next (`localStorage` key `fluxy-lang`, then `navigator.language`, default `en`).
- `.env` is gitignored; `.env.example` does not exist. Env vars are inlined at build time, so changing them requires a rebuild.
- `dist/` is committed/present locally and ignored by eslint. Don't edit build output.
- Comment and commit language in this repo is mixed Russian/English; `csrf.js` and `launch.json` contain Russian comments. Keep the surrounding style when editing existing lines.
