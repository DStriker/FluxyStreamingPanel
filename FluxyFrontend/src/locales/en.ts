const en = {
  fields: {
    username: 'Username',
    email: 'Email',
    password: 'Password',
    confirmPassword: 'Password confirmation',
    code: 'Confirmation code',
    usernamePlaceholder: 'user',
    emailPlaceholder: 'user@example.com',
    // The profile form. Separate from `password` because they are not the same field there:
    // the current one proves the session is the owner's, the new one is what the account
    // should have from now on, and one label over both would make the form unreadable.
    currentPassword: 'Current password',
    newPassword: 'New password',
    newUsername: 'New username',
    newEmail: 'New email address',
    role: 'Access level',
  },
  // Sentences that belong to no page in particular: what the shell writes before it knows
  // which page it is about to draw.
  common: {
    loading: 'Loading',
  },
  validation: {
    usernameRequired: 'Enter your username',
    usernameLength: 'Between 5 and 20 characters',
    emailRequired: 'Enter your email',
    emailInvalid: 'Invalid email address',
    emailMaxLength: 'Email address is too long - up to {{max}} characters',
    passwordRequired: 'Enter your password',
    passwordLength: 'Between 8 and 100 characters',
    passwordComplexity: 'Needs a lowercase letter, an uppercase letter and a digit',
    confirmRequired: 'Confirm your password',
    passwordMismatch: 'Passwords do not match',
    codeRequired: 'Enter the code from the email',
    codeLength: 'The code is 6 digits',
    currentPasswordRequired: 'Enter your current password',
  },
  titles: {
    register: 'Registration',
    confirmRegistration: 'Confirm your registration',
    forgotPassword: 'Password reset',
    confirmPasswordReset: 'Confirm the password reset',
    clientLogin: 'Client area',
    resellerLogin: 'Reseller panel',
    adminLogin: 'Admin panel',
    // The heading in the pinned header of each signed-in area. Separate from the `*Login`
    // titles above on purpose: those title a form on a page a visitor is not signed in on,
    // these title the area itself once they are.
    clientArea: 'Client area',
    resellerArea: 'Reseller panel',
    adminArea: 'Admin panel',
  },
  actions: {
    login: 'Sign in',
    register: 'Sign up',
    confirm: 'Confirm',
    back: 'Use a different account',
    haveAccount: 'Already have an account? Sign in',
    noAccount: "Don't have an account? Sign up",
    signOut: 'Sign out',
    backHome: 'Back to my page',
    // The third door out of the sign-in form: not "I have no account" but "I cannot get
    // into mine", which is a different visitor with a different problem.
    forgotPassword: 'Forgot your password?',
    resetPassword: 'Reset the password',
    change: 'Change',
    save: 'Save',
    cancel: 'Cancel',
    // The only way out of a failed load on the pages that read the server for their rows.
    retry: 'Try again',
  },
  nav: {
    // Categories are the sidebar's submenus; items are the routes beneath them. Both are
    // rendered from `src/lib/navigation.ts`, which decides where each item points - so a
    // label added here and nothing else shows up nowhere.
    groups: {
      overview: 'Overview',
      // The admin's own category, holding the two pages that make and mind accounts.
      users: 'Users',
      account: 'Account',
    },
    items: {
      dashboard: 'Dashboard',
      orders: 'Orders',
      clients: 'Clients',
      // The two halves of the category above, named for what each one does rather than for
      // what it shows: the first is a form that makes an account, the second is the table
      // of the ones that exist, and a visitor scanning the rail decides by the verb.
      usersAdd: 'Add user',
      usersManage: 'Manage users',
      profile: 'Profile',
      // The visit history. Named for what it lists rather than for "sessions", because the
      // rows are tokens - a visitor reading the menu should be told they will see every
      // sign-in and every rotation, not one row per login.
      sessions: 'Sign-in history',
      // The live sessions, next to the history that records them. Named for what it lists
      // rather than for "sessions", for the reason the entry above gives from the other side:
      // what this page holds is sessions - one per sign-in, still running - and a visitor
      // should be told that before they open it, not after.
      activeSessions: 'Active sessions',
    },
  },
  header: {
    settings: 'Settings',
    collapseMenu: 'Collapse the menu',
    expandMenu: 'Expand the menu',
    themeLight: 'Light theme',
    themeDark: 'Dark theme',
    themeSystem: 'Follow the system',
  },
  dashboard: {
    greeting: 'Welcome back, {{name}}.',
    stats: {
      today: 'Today',
      week: 'This week',
      month: 'This month',
    },
  },
  // The profile page. Everything on it is a sentence the server does not send: the server
  // answers in codes and the codes live under `messages.api`, so what is here is only the
  // copy the page itself writes - headings, hints and the names of the three things it can
  // change.
  profile: {
    currentTitle: 'Your account',
    changeTitle: 'Change',
    confirmTitle: 'Confirm the change',
    // Shown after the request: what the visitor should be waiting for, and what to do if
    // nothing arrives. The address is the one the code went to, which is not always the
    // address on the account - an address change is confirmed at the new one.
    codeSentTo: 'We sent a confirmation code to {{where}}.',
    codeSentHint:
      'The change takes effect only when the code is entered. Requesting another change replaces this code.',
    codeSent: 'Check your inbox for the confirmation code.',
    changed: 'Your profile has been updated.',
    cancelHint: 'Give up on this change. Requesting a new one replaces the code.',
    changeHint:
      'Your current password is required for every change. When a password changes, every other session of this account is signed out.',
    // The time zone card, which saves on selection rather than through the form below -
    // hence its own success sentence instead of `changed`.
    timezoneTitle: 'Time zone',
    timezoneHint: 'Dates and times are shown in this zone. "Automatic" follows the browser.',
    timezoneAuto: 'Automatic (from the browser)',
    timezoneSaved: 'Time zone saved.',
    // The login guard card: which networks may sign this account in, and whether a
    // session may move between IPs. The lists are stored even while the switch is off -
    // off means they are ignored, not deleted - so the inputs below are disabled rather
    // than taken away, and the hint says the lists are kept.
    guardTitle: 'Sign-in protection',
    guardProtection: 'Protect sign-in by network',
    guardProtectionHint:
      'When on, a sign-in from a network that is not allowed is refused exactly like a wrong password. When off, the lists below are kept but ignored.',
    guardIps: 'Allowed IP addresses (up to {{max}})',
    guardIpsPlaceholder: '203.0.113.0/24',
    guardIpsInvalid: 'Not an IP address or a CIDR network: {{entry}}',
    guardCountry: 'Allowed country',
    guardCountryPlaceholder: 'Select country',
    guardAsn: 'Allowed provider (AS number)',
    guardBind: 'Bind the session to one IP',
    guardBindHint:
      'When on, refreshing from another IP ends the session. Mobile networks and IP changes will sign you out.',
    guardUseCurrent: 'Use my current network',
    guardCurrentUnknown: 'Your current network could not be determined.',
    guardCurrentFilled: 'Filled in from your current network.',
    kinds: {
      username: 'Username',
      email: 'Email address',
      password: 'Password',
    },
    roles: {
      Client: 'Client',
      Reseller: 'Reseller',
      Admin: 'Administrator',
    },
  },
  // The visit history page. Everything here is copy the server does not send: it answers with
  // rows and with `total`, and what the rows are called, what an unknown value looks like and
  // what the page is for are decisions this application makes.
  sessions: {
    empty: 'This account has no recorded activity yet.',
    // Shown instead of `empty` while a search is running. The account does have history, it
    // simply has none matching - and "no recorded activity" would be a claim the search box on
    // the same card has just disproved.
    noMatches: 'No visits match this search.',
    // The pager's own sentence, which antd would otherwise write in its default locale.
    range: '{{from}}–{{to}} of {{total}}',
    current: 'This session',
    searchPlaceholder: 'Search by IP address or user agent',
    // For the screen reader: a placeholder is not a label, and it disappears the moment
    // anything is typed - which is exactly when the field needs naming.
    searchLabel: 'Search visits',
    // The handle at the right edge of every column header, which the visitor drags to change
    // the width. `{{column}}` is that column's own name - a separator a screen reader cannot
    // name is a separator it can only find by tabbing into it blindly.
    resizeColumn: 'Resize the {{column}} column',
    // Puts all five columns back to the widths they ship with. Offered next to the search
    // rather than in a menu, because the widths it undoes are set by hand in the first place.
    resetWidths: 'Reset widths',
    columns: {
      when: 'When',
      ip: 'IP address',
      country: 'Country',
      network: 'Network',
      agent: 'User agent',
    },
  },
  // The active sessions page. Everything here is copy the server does not send: it answers
  // with rows of its own shape, and what the rows are called, what an unknown value looks
  // like and what each button does are decisions this application makes.
  activeSessions: {
    // Shown when the account holds only the one session the page is being read with. The
    // button for it is disabled and the list would otherwise be one card of "this is you",
    // which the badge already says.
    empty: 'This account holds no other sessions.',
    current: 'This session',
    lastSeen: 'Last seen',
    end: 'End session',
    // The tooltip on the disabled button of the current session's card: naming the way out
    // rather than only the refusal, because "cannot end this one" without a "sign out
    // instead" is a dead end on a page whose whole purpose is ending sessions.
    endCurrentHint: 'The session you are reading with cannot be ended here. Sign out instead.',
    endAll: 'End all other sessions',
    endAllConfirmTitle: 'End all other sessions?',
    endAllConfirmBody:
      'Every session except this one will be signed out. Those devices will have to sign in again.',
    endAllConfirmOk: 'End them',
    revoked: 'The session has been ended.',
    revokedOthers: 'Ended {{count}} other session(s).',
    revokedOthersNone: 'This account held no other sessions.',
    agentUnknown: 'Unknown client',
    ipUnknown: 'Address not recorded',
    networkUnknown: 'Network not determined',
  },
  // The administrator's two pages over the accounts: the table and the form that adds and
  // edits one. Everything here is copy the server does not send - it answers with rows, with
  // codes and with `errors`, and what a column is called, what an account that has never
  // signed in looks like and what a destructive button warns about are decisions this
  // application makes.
  users: {
    // The form's heading. One component serves both addresses, so the title is chosen here
    // rather than read from `sectionKey`: the same page is this at `users/add` and the edit
    // title at `users/{id}`, and a key naming only the first would title the second a lie.
    addTitle: 'Add user',
    editTitle: 'Edit user',
    // The id is drawn as static text, never as a field: it is not editable and it is not
    // sent back, and an input the visitor cannot change reads as a form that is broken.
    idLabel: 'ID',
    // Under the password field while editing. An empty password means "keep the one that is
    // there" - the server's rule for that field, stated before the visitor wonders about it.
    passwordKeep: 'Leave empty to keep the current password',
    create: 'Create',
    backToList: 'Back to the list',
    // The table.
    searchPlaceholder: 'Search by username or email',
    // For the screen reader: a placeholder is not a label, and it disappears the moment
    // anything is typed - which is exactly when the field needs naming.
    searchLabel: 'Search users',
    empty: 'No accounts yet.',
    // Shown instead of `empty` when a search or a filter is on. The accounts do exist, they
    // simply are not in this answer - and "no accounts" would be a claim the toolbar above
    // the table has just disproved.
    noMatches: 'No accounts match this search.',
    // The pager's own sentence, which antd would otherwise write in its default locale.
    range: '{{from}}–{{to}} of {{total}}',
    // The handle at the right edge of every column header, which the visitor drags to change
    // the width. `{{column}}` is that column's own name - a separator a screen reader cannot
    // name is a separator it can only find by tabbing into it blindly.
    resizeColumn: 'Resize the {{column}} column',
    // Puts every column back to the widths they ship with. Offered next to the toolbar
    // rather than in a menu, because the widths it undoes are set by hand in the first place.
    resetWidths: 'Reset widths',
    // The two filters, as the choice they offer when nothing is chosen. The labels are the
    // column headers beside them - one name for one thing, however it is drawn.
    filterAnyRole: 'Any role',
    filterAnyStatus: 'Any status',
    columns: {
      id: 'ID',
      username: 'Username',
      email: 'Email',
      role: 'Role',
      status: 'Status',
      // `lastSeenAt` rather than `lastSeen` because the column's key *is* the server's own
      // sort field: a header click then hands its key straight to `getUsers`, and there is
      // no mapping table between the two that could get one name wrong. The label stays
      // what a reader sees; the key is what the endpoint is asked to order by.
      lastSeenAt: 'Last visit',
      ip: 'IP address',
      actions: 'Actions',
    },
    // The three states, as the server spells them - the enum member names are what comes
    // back, and this is where they become words. `UserStatus` is not an ordered scale, so
    // these are names rather than levels: nothing here compares one to another.
    statuses: {
      Unregistered: 'Unregistered',
      Registered: 'Registered',
      Blocked: 'Blocked',
    },
    // The four row actions, as tooltips. The icons carry no text of their own, so the name
    // each one gets here is the only thing that says what pressing it does.
    actions: {
      edit: 'Edit',
      confirm: 'Confirm registration',
      block: 'Block',
      unblock: 'Unblock',
      delete: 'Delete',
    },
    // Why the block and delete buttons on *your own* row are disabled. The server refuses
    // those two anyway (`cannot_block_self`, `cannot_delete_self`), so the disabled control
    // is the same fact told before the click - the pattern the current session's button
    // follows on the active sessions page.
    selfProtected: 'You cannot do this to the account you are signed in with.',
    // The one action that asks first. Only deletion is irreversible - blocking is an
    // unblock away and confirmation is a state the row can be put back into - so a
    // confirmation here is a question and not a habit.
    deleteConfirmTitle: 'Delete this account?',
    deleteConfirmBody:
      'The account "{{name}}" and everything attached to it — sessions, sign-in protection — will be removed. This cannot be undone.',
    deleteConfirmOk: 'Delete',
  },
  messages: {
    // Shown by the login forms, whose endpoints have no server code to translate yet.
    sent: 'Form submitted',
    sendFailed: 'Failed to submit the form',
    // The refusal a visitor gets when this installation cannot send mail at all, so there
    // is no code to confirm a reset with. Never a fault of what they typed - the form is
    // simply not offered on such a server.
    passwordResetUnavailable:
      'Password reset is not available on this server. Please contact support to regain access to your account.',
    // The shell's own placeholders. Neither is produced by the server: `areaNotBuilt` says
    // which section has no page behind it yet, and `notFound` is the catch-all route and
    // the refusal a role mismatch gets.
    areaNotBuilt: 'The "{{section}}" section is still under construction.',
    notFound: 'This page does not exist, or it belongs to another area.',
    // Every code the API can answer with. The keys are the server's own `code` values, so
    // a new one on the backend falls back to its English `message` until it is added here.
    api: {
      registration_submitted: 'Check your inbox — we sent you a confirmation code.',
      registration_confirmed: 'Registration confirmed. You can sign in now.',
      registration_not_configured: 'Registration is not available on this server right now.',
      validation_failed: 'Please correct the highlighted fields.',
      user_already_exists: 'That username or email address is already taken.',
      invalid_code: 'That confirmation code is not correct.',
      code_expired: 'The code has expired. Request a new one to get a fresh code.',
      captcha_invalid: 'Could not verify that a person sent this. Please try again.',
      csrf_invalid: 'Your session expired. Please try again.',
      registration_rate_limited: 'Too many registrations from your address. Please try again later.',
      confirmation_rate_limited: 'Too many attempts. Please try again later.',
      email_delivery_failed: 'The confirmation email could not be sent. Please try again later.',
      // Ending sessions: the two success codes and the two refusals only the revoke
      // endpoints can produce. `cannot_revoke_current` is refused before any database work,
      // and `session_not_found` is one answer for unknown, gone and somebody else's - the id
      // is guessable and the difference would turn the endpoint into a probe.
      session_revoked: 'The session has been ended.',
      other_sessions_revoked: 'Your other sessions have been ended.',
      cannot_revoke_current: 'The session you are using cannot be ended from here. Sign out instead.',
      session_not_found: 'No such session on this account.',
      // Profile changes: the same three shapes as registration (sent / applied / refused),
      // plus the two refusals only an authenticated caller can meet.
      profile_updated: 'Your profile has been updated.',
      profile_change_submitted: 'Check your inbox for the confirmation code.',
      profile_change_confirmed: 'Your profile has been updated.',
      invalid_current_password: 'The password you entered is not your current password.',
      account_not_active: 'This account cannot change its profile.',
      profile_rate_limited: 'Too many attempts to change your profile. Please try again later.',
      profile_confirmation_rate_limited: 'Too many attempts. Please try again later.',
      // The public password reset.
      password_reset_submitted: 'Check your inbox for the confirmation code.',
      password_reset_confirmed: 'Your password has been changed. You can sign in now.',
      password_reset_not_configured: 'Password reset is not available on this server.',
      credentials_mismatch: 'That username and email address do not belong to the same account.',
      password_reset_rate_limited: 'Too many attempts. Please try again later.',
      password_reset_confirmation_rate_limited: 'Too many attempts. Please try again later.',
      // The administrator's user endpoints: five successes, the absence of a row, and three
      // refusals that exist only because an administrator can otherwise reach their own
      // account with an action meant for somebody else's. Each is a state change the table
      // shows a row for, so the sentence names what happened to which kind of thing rather
      // than what the request was.
      user_created: 'The account has been created.',
      user_updated: 'The account has been updated.',
      user_deleted: 'The account has been deleted.',
      user_blocked: 'The account has been blocked.',
      user_unblocked: 'The account has been unblocked.',
      user_not_found: 'There is no such account.',
      // 409 rather than a silent no-op: `block` is idempotent by design, but confirming a
      // registration that is not waiting for one and unblocking an account that is not
      // blocked are contradictions between what the page shows and what the row holds, and
      // answering "done" would leave the table describing something that did not happen.
      invalid_status: 'That account is not in the state this action needs.',
      cannot_delete_self: 'You cannot delete the account you are signed in with.',
      cannot_block_self: 'You cannot block the account you are signed in with.',
      cannot_demote_self: 'You cannot lower the access level of the account you are signed in with.',
      // Never produced by the server - both mean the request did not reach it.
      network_error: 'Server unavailable. Check that the backend is running.',
      server_error: 'Server error ({{status}}).',
      authenticated: 'You have signed in.',
      invalid_credentials: 'The username or password is incorrect.',
      login_rate_limited: 'Too many sign-in attempts. Please try again later.',
      refresh_rate_limited: 'Too many refresh attempts. Please try again later.',
      session_expired: 'Your session has ended. Please sign in again.',
      auth_required: 'Please sign in to continue.',
      auth_role_changed: 'Your access level has changed. Please sign in again.',
      signed_out: 'You have signed out.',
    },
  },
}

/**
 * The shape of the English file, which is the shape every other locale has to have.
 *
 * It is derived from the object rather than written out, so it cannot drift from it: a key
 * added to `en` is automatically required by `ru`, and a key `en` does not have is an error
 * in `ru` rather than a string nothing ever reads. `ru.ts` annotates itself with this type.
 */
export type Translation = typeof en

export default en