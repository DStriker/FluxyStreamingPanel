> Part of the FluxyFrontend AGENTS.md instruction set — read on demand. Back to [AGENTS.md](../AGENTS.md).

## UI rules that govern every page

These are owner rules, not suggestions. They apply to a new page exactly as they apply to the
one it was copied from, and a screenshot is what proves them. The verification recipe lives in
[probes.md](probes.md); this file is the rule and the reason.

### 1. The page title is always visible in the page card

Every routed page draws a `Card` (or `AuthCard` for the five guest screens) and that card's
`title` is the page's heading. The loading state, the failed-read alert and the empty state keep
the same title, because the address still names the page even when its data does not arrive —
`UserFormPage` is the reference: its edit address keeps `users.editTitle` above an alert that
says the account is not there.

- The title is **never** moved into the body as a `Typography.Title`, never replaced by a
  breadcrumb or by the menu item, and never omitted because the menu already says something.
  The menu says how the visitor arrived; the card says what they are looking at. The two may
  legitimately differ in wording — `UsersPage` is headed `users.listTitle` while its menu item
  is `nav.items.usersManage` — but the card always has a title of its own.
- The title must stay **visible**, not merely present in the markup. It shares the head with the
  toolbar, so the `page-card` class (see [shell-and-layout.md](shell-and-layout.md)) and the
  pinned `styles={{ title: { whiteSpace: 'nowrap' } }}` are what stop antd from shrinking it to
  zero width when the toolbar is wide. A new toolbar card without `page-card` keeps the broken
  desktop header on a phone.
- A page that renders a card *inside* another card (the session cards of `ActiveSessionsPage`,
  the profile cards) follows the same rule for its own heading, but the routed page's outer card
  is the one this rule is about.

**Pages that predate this rule:** `DashboardPage` draws its heading as a body-level
`Typography.Title` in a title-less card, and `ProfilePage` is a stack of individually titled
cards (`profile.currentTitle`, `profile.timezoneTitle`, `profile.guardTitle`,
`profile.changeTitle`) with no page-level card at all. Both are the shapes already in the tree,
not precedents: a new page starts from a titled page card, and either page is brought in line if
and when it is reworked. Anything else found without a card title is a defect — add the title
rather than another exception here.

### 2. A long filter control gets its own row above the table, outside the card head

A control that is too wide to share the card head — `DatePicker.RangePicker` is the case this
rule was written for, and a long multi-select or a month picker is the same shape — is a
**filter row of its own**: one control per row, rendered in the **card body, above the tag row
and the table**, left-aligned.

- It is outside the card **head**, not outside the card. The head keeps only the title and the
  toolbar: the title row already nearly fills the width above 1200px, so a range picker in
  `extra` pushes the title out of the way or wraps the toolbar into a second line. The card
  itself still frames the whole page, filter included.
- The row comes before the active-filter tags and before `ServerTable`, with `marginBottom: 12`
  — the same gap the tag row uses.
- Do not put two wide controls side by side to save a row: a paired row is the first thing to
  break on a narrow viewport or under a longer translation.
- The value still lives in the address (`useServerTable` / `tableParams`), not in component
  state, and a drill-down link that already carries the filter arrives showing it.
- `ConnectionLogsPage` is the reference: its activity view renders the start-time
  `RangePicker` in the card body before the tag row, and it is the placement to copy.

### 3. A form says each thing once, one field per row

Forms in this repo are `layout="vertical"` with a bounded width — `maxWidth: 640` on the
account/group forms, `720` on the stream/subscription/tariff forms, `820` where the form carries
a list editor — and **one field per row**. Do not use `Row`/`Col` to put two fields side by side:
a narrow viewport and a longer translation both break a paired layout, and the form is not a
dashboard. A field that wants to be narrower than its form (the country selector, the AS number)
sets its own `maxWidth` on the control; it does not gain a second column beside a neighbour.

- **Never duplicate text on a form.** The label already names the field; a placeholder that
  repeats the label is noise, and a `Typography.Paragraph` explaining what a username is
  explains nothing. A placeholder is allowed only when it adds a format or an example the label
  does not carry (`admin@example.com`, `http://host:port/playlist.m3u`), not a synonym.
- **No help text for an obvious field.** Username, email, password, name, category, title and
  the like need no `extra` sentence. `extra` is for a rule the visitor cannot infer — "leave
  empty to keep the current password" on an edit form — and for nothing else.
- **No sections, dividers or sub-headings for the key fields of a form.** A name, a category or
  a title is not a section; a `Divider` above the first three inputs is decoration. A section is
  justified only when it separates genuinely different concerns the visitor must understand
  separately — the login-guard block on the profile/account form is the existing example — and
  never to group "General" fields.
- The server's field names stay the form item names (`allowedIps`, `startedFrom`), so
  `fieldErrors()` lands a refusal under its input with no translation step.

### 4. Every new page or UI element is checked in a browser, with a screenshot

Before closeout, any change that adds a page, a table, a form, a toolbar control or another
visible element is opened in a real browser through **Playwright** and captured as a
**screenshot**. Read the screenshot, not just the DOM: look for the page title, controls that
overlap or leave the viewport, clipped or wrapped headings, an empty card head, a form field
that is not full width, a wide filter in the wrong row, and text in the wrong language.

- Use the Playwright browser automation available to the agent (the `playwright` / `browser`
  tools) against the running dev server. **Do not add `@playwright/test` or any other browser
  runner to `package.json` for this check** without the owner's explicit approval; the probes
  in `package.json` are the committed suite.
- This is **in addition to** `probe:render`, `probe:i18n` and `probe:paths`, never a
  replacement. The probes assert the markup; the screenshot is the only check that sees layout.
- Save captures under `.tmp-runtime/` (gitignored) and reference the path in the report. Do not
  commit screenshots.
- If the browser automation is unavailable, say so explicitly in the report and fall back to a
  manual screenshot; a missing check is a reported gap, not a silent pass.

### 5. A check that needs a sign-in uses a throwaway account, not a password hunt

Never spend time looking for the seeded administrator's password or guessing an existing one.
Create a temporary account directly in PostgreSQL, in the **base group of the role the check
needs**, run the check, and delete the account when the check ends — including when it fails.

The recipe — base group identifiers, the `bcrypt` hash, the insert and the delete — is in
[probes.md](probes.md#a-throwaway-account-for-a-check-that-needs-a-sign-in). Prefer the
narrowest base group that can open the screen (a client for the client area, an administrator
only when the screen is admin-only), and never create a new group for the check.
