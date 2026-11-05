> Part of the FluxyFrontend AGENTS.md instruction set — read on demand. Back to [AGENTS.md](../AGENTS.md).

## Structure

> Moved from AGENTS.md — the core bullets stay in the index.

- `src/features/auth/components/AuthCard.tsx` is the shared frame (language switcher, logo, card, `Form`). Both forms render through it, so a second form looks like the first without copying the layout. It **must** render its `footer` prop — it is the last child inside the `<Form>`, and that is where the cross-link between the two flows has always lived. Dropping it is invisible to `typecheck`, `lint` and `build`, which is how it went missing once.
- `src/features/auth/components/AuthForm.tsx` is the shared credentials form, generic in the values its page collects (see [gotchas.md](gotchas.md)). The three login pages in `src/pages/auth/` are thin wrappers that pass `action` + `onSubmit`; add behavior there, not by copying the form.
- `src/features/auth/components/ConfirmCodeForm.tsx` is the second step of registration. It is separate from `AuthForm` because the two share no fields but differ in endpoint, captcha action and rate limit.
- `src/features/auth/components/GuestOnly.tsx` wraps the five guest routes (three sign-ins, registration, password reset) where they are declared in `src/router/routes.tsx`. A visitor who already holds a session is sent to their own area instead of being shown a form that would stack a second session over a first that nothing can end anymore. It renders its children while `GET /auth/me` is in flight — a login form must not sit behind a round trip — and the destination comes from `homeForRole` in `src/utils/session.ts`.

## Registration is two steps on one page

`RegisterPage` holds `step` and the address carried between the steps. `AuthForm` calls `onSuccess(result, values)` once the server answers 2xx, which is what advances to `ConfirmCodeForm`. There is **no `/register/confirm` route** — the visitor stays on `/register` and the page swaps the form underneath them.

The address is mirrored into `sessionStorage` (`src/features/auth/pendingRegistration.ts`) so a reload between the steps resumes instead of throwing the visitor back to the first form. It is a convenience, never a source of truth: the server keys confirmation by address alone, so a stale value costs a re-typed email. Cleared on `registration_confirmed` and on `code_expired` — the latter also drops back to step one, because an expired code cannot be revived by typing a better one.

`captchaAction` is a prop of `AuthForm` separate from `action`. The server fixes one captcha action per endpoint, so the confirm form passes `ConfirmCaptchaAction` from `src/features/auth/api.ts`, while the login pages still let `action` serve both purposes.

## The server code is the branch, not the text

Every response is `{ code, message, errors }`. The client branches on `code` and shows `messages.api.<code>` from the locale files; `message` is used only when a code has no translation yet. `network_error` and `server_error` are the two codes the client invents for failures that never reached the server.

Adding a code to `RegistrationResponses.cs` on the backend without adding it to `messages.api` in both locales breaks nothing — it degrades to the server's English sentence. It should still be added, because the point of the code is to be translated. Note that `probe:i18n` cannot see this class of key: `successTextFor`'s `messages.api.<code>` lookup and the group form's `t(\`permissions.${permission}\`)` build their keys at runtime, so a new outcome code or permission label must be cross-checked against the backend's response constants by hand — the subscriptions codes shipping untranslated once is how this was found.

`errors` is keyed by the camelCase JSON property name (`username`, `email`, `password`), which is deliberately identical to the antd `Form.Item` names, so `fieldErrors()` feeds `form.setFields` with no translation step.
