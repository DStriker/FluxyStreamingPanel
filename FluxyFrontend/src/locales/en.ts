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
  // The shared wording of a bulk run's summary. It belongs to no page because every bulk
  // table reads its answer through the same hook: the title says how much landed, and the
  // two headings below it separate what was refused from what was changed with a caveat.
  // A warning is not a refusal - the row did what it was told and lost a setting it could
  // no longer carry - so the two lists are drawn under their own headings rather than
  // folded into one sentence about failure.
  bulk: {
    warningsTitle: 'Changed {{done}} of {{total}}. Some settings were cleared:',
    refused: 'Refused',
    warnings: 'Warnings',
  },
  // The last-resort screen of an error boundary - a route that threw while rendering, or a
  // failure above the router. Its own section rather than a page's, because the page it
  // would have belonged to is the one that just failed to draw.
  errors: {
    title: 'Something went wrong',
    description:
      'The page failed to render. Reloading usually brings it back вЂ” nothing that already reached the server was affected.',
    reload: 'Reload the page',
  },
  // The guard on every form that edits something: a move away from a page with unsaved
  // edits is asked about first. The reload/close half is answered by the browser's own
  // dialog, which no page can word; these strings cover the in-app dialog.
  unsavedChanges: {
    title: 'You have unsaved changes',
    content: 'Leaving this page will discard what you have edited.',
    leave: 'Leave',
    stay: 'Stay',
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
    // The acknowledge button of an informational dialog (the bulk run's partial-success
    // summary). Deliberately not `confirm`: nothing is being agreed to, the dialog is
    // reporting what already happened.
    ok: 'OK',
  },
  nav: {
    // Categories are the sidebar's submenus; items are the routes beneath them. Both are
    // rendered from `src/utils/navigation.ts`, which decides where each item points - so a
    // label added here and nothing else shows up nowhere.
    groups: {
      overview: 'Overview',
      // The admin's own category, holding the three pages that make, mind and group
      // accounts.
      users: 'Users',
      // The four content sections, in the order the domain names them. One group per kind
      // of content, each with the same three entries beneath it.
      videoStreams: 'Video streams',
      audioStreams: 'Audio streams',
      videoOnDemand: 'Video on demand',
      series: 'Series',
      // The programme guide: the sources a schedule is fetched from. It follows the four
      // content sections because a guide is what the streams are filed against rather than a
      // fifth catalogue of content.
      epg: 'Programme guide',
      // The bouquets: the packages the four content sections above are delivered in. It sits
      // between the content sections and the guide, and it is a frame around them rather
      // than a fifth catalogue - which is why it has one entry and the sections have four.
      bouquets: 'Bouquets',
      // The subscriptions: what a subscriber buys. It follows the accounts and the packages
      // because it is the price list of the second - a plan names the packages it delivers and
      // the groups it may be sold to. Four of its five entries are placeholders for now.
      subscriptions: 'Subscriptions',
      account: 'Account',
    },
    items: {
      dashboard: 'Dashboard',
      orders: 'Orders',
      clients: 'Clients',
      // The first two entries of the category above, named for what each one does rather
      // than for what it shows: the first is a form that makes an account, the second is the
      // table of the ones that exist, and a visitor scanning the rail decides by the verb.
      usersAdd: 'Add user',
      usersManage: 'Manage users',
      // The third, named for what it holds rather than for the verb: groups are not an
      // action to take but a list to read, and "Groups" beside "Manage users" is the one
      // word that says which table opens. It has no "add" sibling in the rail - the add
      // button lives above its own table, where the visitor already is.
      userGroups: 'Groups',
      // The visit history of every account this operator may read. Named for whose
      // sessions they are rather than for what the rows are, for one reason: the *account's
      // own* history is already called "Sign-in history" and sits in the Account group, and
      // two entries reading as variants of one word is how the wrong page gets opened
      // under pressure - which is exactly when this one is opened. "User sessions" rather
      // than "Users' sessions": the noun attributes, as in "user table" and "user group",
      // and an apostrophe on a plural possessive reads as a typo beside the four flat
      // labels above it. The card heading is "Visit history" instead, so the pair works
      // the way the two above it do: the menu names how you got there, the card names what
      // you are looking at.
      adminSessions: 'User sessions',
      // Each content section repeats one shape: `add`, `manage` and `categories`, plus an
      // `import` entry only where a playlist import has been built. Unlike the labels around
      // them, every entry names its object as well as its verb: a bare `Add` beside
      // `Add user` read as a different kind of control, and the group header alone is easy
      // to skip while scanning the rail. The two stream sections have real pages today.
      videoStreamsAdd: 'Add video stream',
      videoStreamsImport: 'Import video streams',
      videoStreamsManage: 'Manage video streams',
      videoStreamsCategories: 'Categories',
      audioStreamsAdd: 'Add audio stream',
      audioStreamsImport: 'Import audio streams',
      audioStreamsManage: 'Manage audio streams',
      audioStreamsCategories: 'Categories',
      videoOnDemandAdd: 'Add video',
      videoOnDemandImport: 'Import videos',
      videoOnDemandManage: 'Manage videos',
      videoOnDemandCategories: 'Categories',
      seriesAdd: 'Add series',
      seriesManage: 'Manage series',
      seriesCategories: 'Categories',
      // The guide section has two entries and no more: a source has no categories, and a
      // playlist import would be a second way to add one file rather than a second thing to
      // look at. The owner asked for these two words, so they are what the rail says.
      epgAdd: 'Add',
      epgManage: 'Manage',
      // The bouquets section's one entry. Named for the verb like the two guild entries
      // above it, and for its object as well, because "Manage" alone under a heading called
      // "Bouquets" would read as a heading with no door.
      bouquetsManage: 'Manage bouquets',
      // The subscriptions section. Four of the five are placeholders - the screens they name
      // have not been built - and they are named exactly as the owner described the section:
      // adding a subscription first, then the lists of what a subscriber holds, and the price
      // list last. The plan entry names its object as well as its list, because "Tariff plans"
      // under "Subscriptions" is one product described twice rather than a heading with no door.
      // The subscriptions section. The first is the form that issues a subscription, the
      // second is the list that draws every one of them with its service subrows and the price
      // list after them. The three placeholder entries the section used to carry - playlists,
      // MAG devices and Enigma2 devices - are gone: each of those is a field of a row on the
      // management list, and a second screen for one field would be a second place for the same
      // fact to disagree with itself.
      subscriptionsAdd: 'Add',
      subscriptionsManage: 'Manage',
      tariffPlans: 'Tariff plans',
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
    accountMenu: 'Account menu',
    collapseMenu: 'Collapse the menu',
    expandMenu: 'Expand the menu',
    openMenu: 'Open the menu',
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
    range: '{{from}}вЂ“{{to}} of {{total}}',
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
    // The list page's heading. Deliberately not `sectionKey`, which would take the menu
    // item's name: a page must not repeat in large letters what the menu already says
    // beside it - "Manage users" is how the visitor got here, the heading names the
    // subject. Sharing the group's name ("Users") is accepted: the page is that group.
    listTitle: 'Users',
    // The form's heading. One component serves both addresses, so the title is chosen here
    // rather than read from `sectionKey`: the same page is this at `users/add` and the edit
    // title at `users/{id}`, and a key naming only the first would title the second a lie.
    addTitle: 'Add user',
    editTitle: 'Edit user',
    // The id is drawn as static text, never as a field: it is not editable and it is not
    // sent back, and an input the visitor cannot change reads as a form that is broken.
    idLabel: 'ID',
    // Under the group field of the form. The level is the fact being decided and the
    // select's label only says "Group", so this is where the two are joined: what the
    // visitor is choosing, named as what it does.
    groupRequired: 'Choose a group for this account',
    groupLevel: 'Access level: {{role}}',
    // The note beside the status field: the account's own row and its group's, most
    // restrictive winning. It says *where* the restriction comes from, because the row the
    // form is editing may read `Registered` while the account nobody can sign in to is
    // being held by the group - and an "Unblock" pressed against that comes back
    // `409 user_blocked_by_group`, naming a control this form does not have.
    blockedByGroup: 'Blocked by the group {{group}}. The account stays blocked until that group is unblocked.',
    effectiveStatus: 'Its group makes this account {{status}} right now.',
    // Under the password field while editing. An empty password means "keep the one that is
    // there" - the server's rule for that field, stated before the visitor wonders about it.
    passwordKeep: 'Leave empty to keep the current password',
    generatePassword: 'Generate password ({{length}} letters and digits)',
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
    range: '{{from}}вЂ“{{to}} of {{total}}',
    // The handle at the right edge of every column header, which the visitor drags to change
    // the width. `{{column}}` is that column's own name - a separator a screen reader cannot
    // name is a separator it can only find by tabbing into it blindly.
    resizeColumn: 'Resize the {{column}} column',
    // Puts every column back to the widths they ship with. Offered next to the toolbar
    // rather than in a menu, because the widths it undoes are set by hand in the first place.
    resetWidths: 'Reset widths',
    // The two filters, as the choice they offer when nothing is chosen. The labels are the
    // column headers beside them - one name for one thing, however it is drawn. The group
    // filter replaced the level filter when the level stopped being a property of the
    // account: an identifier is the only thing that can name "the accounts of *that* group",
    // since a level filter would admit every group holding it at once.
    filterAnyGroup: 'Any group',
    filterAnyStatus: 'Any status',
    // The moment in the "Last visit" column, drawn as the link to that account's own visit
    // history. The date *is* the link rather than a label beside it: the cell stays one line,
    // the column header already says "Last visit", and the whole of what the column is for
    // becomes clickable. This is the link's `title` - the destination named for a hover and
    // for a screen reader, deliberately a title rather than an `aria-label`: an `aria-label`
    // replaces the visible date as the link's accessible name, and a name that does not
    // contain what the eye reads breaks Label in Name.
    lastSeenLinkTitle: 'Visit history for {{name}}',
    columns: {
      id: 'ID',
      username: 'Username',
      email: 'Email',
      // The group rather than the level it grants - and a name the form uses as well as the
      // table, so "Group" means the same thing in both places and there is one word for one
      // thing rather than a header and a label that could drift apart.
      group: 'Group',
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
    // Why a write button on *somebody else's* row is disabled: the operator's own group holds
    // no grant for that account's role. The server refuses the same move with 403
    // `permission_denied`, so this is the same fact told before the click - and it names the
    // group rather than the visitor, because that is what the refusal is about. A visitor
    // told "you are not allowed" would go looking for a different account; there is none to
    // find, only a grant their group does not hold.
    noPermission: 'Your group does not allow this.',
    // The one action that asks first. Only deletion is irreversible - blocking is an
    // unblock away and confirmation is a state the row can be put back into - so a
    // confirmation here is a question and not a habit.
    deleteConfirmTitle: 'Delete this account?',
    deleteConfirmBody:
      'The account "{{name}}" and everything attached to it вЂ” sessions, sign-in protection вЂ” will be removed. This cannot be undone.',
    deleteConfirmOk: 'Delete',
    // The bulk toolbar over the selection: one count and one menu, deliberately the smallest
    // thing that can carry five operations, because the toolbar shares its row with the
    // search and both filters and there is no room in it for a second line. Both are drawn
    // even with nothing selected - a control that appears and disappears reads as a broken
    // one, and the count is what says *why* the menu is greyed.
    bulk: {
      menu: 'Actions',
      // The count rides inside the button that the count enables, rather than beside it as
      // a label of its own. That is a width decision before it is a design one: `РЎР±СЂРѕСЃРёС‚СЊ
      // С€РёСЂРёРЅСѓ`, `Р”РѕР±Р°РІРёС‚СЊ РїРѕР»СЊР·РѕРІР°С‚РµР»СЏ` and `Р”РµР№СЃС‚РІРёСЏ` are already longer than their
      // English twins, and a separate `Selected: 0` costs a whole extra element of room in
      // the one row of the card that has no room to spare. Number and control in one place
      // is also the tighter truth - the count exists to say why the button is greyed.
      menuWithCount: 'Actions ({{count}})',
      // The move, which opens a modal rather than an inline select: the destination is a
      // decision about the whole selection, and a control dropped into the toolbar would be
      // answering it for a menu that has already closed. The other four run straight from
      // the menu and take the labels of the row actions - it is the same operation over more
      // than one row, and a second name for it would be a second name for one thing.
      assign: 'Move to group',
      assignTitle: 'Move these accounts to a group',
      assignPlaceholder: 'Choose a group',
      assignOk: 'Move',
      // Shown instead of a toast the moment at least one row was refused. Every line is the
      // server's own sentence for that row's code, already translated here, so the reason is
      // read rather than guessed at from a count - and this is where the refusal the row
      // buttons would have shown *before* a click arrives for a row the visitor did not
      // click (`cannot_block_self` above all).
      partialTitle: 'Changed {{done}} of {{total}}. The rest were refused:',
      // Only deletion asks first, for the reason only deletion asks first on a single row.
      deleteTitle: 'Delete these {{count}} accounts?',
      deleteBody:
        'Each account and everything attached to it вЂ” sessions, sign-in protection вЂ” will be removed. This cannot be undone.',
    },
  },
  // The ten permission keys, exactly as the catalog spells them. These strings are the
  // labels of the checkboxes on the group form and the values `src/features/user-groups/permissions.ts`
  // mirrors from the backend - one name for one thing, spelled the same way in both, so a
  // permission added on the server becomes a box an operator can tick without a translation
  // step in between.
  permissions: {
    viewClients: 'View clients',
    editClients: 'Edit clients',
    viewResellers: 'View resellers',
    editResellers: 'Edit resellers',
    viewAdmins: 'View administrators',
    editAdmins: 'Edit administrators',
    viewUserGroups: 'View groups',
    editUserGroups: 'Edit groups',
    // The streams pair is deliberately not a role pair like the six above: `view*Clients`,
    // `viewResellers` and friends each name the role they open, and these two name the
    // domain instead - they belong to every kind of stream, not to one audience of them.
    viewStreams: 'View video streams',
    editStreams: 'Edit video streams',
    // The audio pair is the same question asked about another medium, and it is deliberately
    // a separate pair: a group that may read the video streams may not read the audio ones.
    viewAudioStreams: 'View audio streams',
    editAudioStreams: 'Edit audio streams',
    // The on-demand pair asks about the video catalogue: its table, its form, its import and
    // the transcode request. It is a separate pair from the two stream pairs above because
    // it is a separate resource behind a separate screen.
    viewVideoOnDemand: 'View on-demand videos',
    editVideoOnDemand: 'Edit on-demand videos',
    // The series pair asks about the series catalogue: its table, its form, the episodes and
    // the season form. Separate from the on-demand pair for the same reason the audio pair is
    // separate from the video one: the screens and the rows are their own.
    viewSeries: 'View series',
    editSeries: 'Edit series',
    // The guide pair guards the sources a programme schedule is read from: the sources
    // table and the channels that use one source, or the writes that create, change and
    // delete a source. A guide source is its own resource, not a kind of stream.
    viewEpgSources: 'View guide sources',
    editEpgSources: 'Edit guide sources',
    // The bouquet pair guards the packages content is delivered in: the bouquets table, the
    // content manager behind one row, and the bouquet picker on the four content forms. It is
    // a pair of its own rather than a reuse of any content pair, because a bouquet is a
    // package *of* the content: a group may run the streams and still have no business
    // deciding which packages they are sold in.
    viewBouquets: 'View bouquets',
    editBouquets: 'Edit bouquets',
    // The tariff plan pair guards the price list a subscription is sold under: the plans
    // table and the plan form, where a plan's two relations - the groups it may be sold to and
    // the bouquets it delivers - are decided. It is a pair of its own because what a package
    // costs and what a package contains are decided by different jobs.
    viewTariffPlans: 'View tariff plans',
    editTariffPlans: 'Edit tariff plans',
    viewSubscriptions: 'View subscriptions',
    editSubscriptions: 'Edit subscriptions',
  },
  // The admin's group table and its form. Its own block rather than more keys under `users`,
  // because the two are behind different permissions (`view*Clients` vs. `viewUserGroups`) and
  // a page that fails must name which of the two it was asking for.
  userGroups: {
    // The list page's heading. Deliberately *not* `nav.items.userGroups`: a card repeating
    // the menu item's name in large letters would say the menu again instead of naming the
    // page. The pair here is `Groups` beside `User groups` - one naming how the visitor got
    // there, one naming what they are looking at.
    listTitle: 'User groups',
    // The form's heading. One component serves both addresses, so the title is chosen here
    // rather than read from `sectionKey`: the same page is this at `user-groups/add` and the
    // edit title at `user-groups/{id}`, and a key naming only the first would title the
    // second a lie. The add button above the table reuses the same key, because a button
    // reading "Groups" over the groups would say what the visitor is already looking at.
    addTitle: 'Add group',
    editTitle: 'Edit group',
    // The id is drawn as static text, never as a field: it is not editable and it is not
    // sent back, and an input the visitor cannot change reads as a form that is broken.
    idLabel: 'ID',
    nameRequired: 'Enter a group name',
    // The member count on the edit form, where the row is already loaded. The sentence the
    // delete dialog tells is the one this number is the evidence for.
    membersLabel: 'Members',
    members: '{{count}} accounts in this group',
    create: 'Create',
    backToList: 'Back to the list',
    // The table.
    searchPlaceholder: 'Search by group name',
    // For the screen reader: a placeholder is not a label, and it disappears the moment
    // anything is typed - which is exactly when the field needs naming.
    searchLabel: 'Search groups',
    empty: 'No groups yet.',
    // Shown instead of `empty` when a search or a filter is on: the groups exist, they are
    // just not in this answer - and "there are no groups" would be contradicted by the
    // filter panel right above the table.
    noMatches: 'No groups match this search.',
    // The pager's own row, which antd would otherwise write in its default language.
    range: '{{from}}вЂ“{{to}} of {{total}}',
    // The handle at the right edge of each header: it is dragged to change the width.
    // `{{column}}` is the column's own name - a separator a screen reader cannot name is one
    // it can only find by tabbing blindly through six headers.
    resizeColumn: 'Resize the {{column}} column',
    resetWidths: 'Reset widths',
    filterAnyRole: 'Any role',
    filterAnyStatus: 'Any status',
    columns: {
      id: 'ID',
      name: 'Name',
      // The level a group grants, as the same word the rest of the application uses for it.
      // It is drawn from the enum member the server sent, and this header hands its own key
      // to the sorter - `role` is both what the column says and what the endpoint orders by.
      role: 'Role',
      status: 'Status',
      // "Permissions" rather than "permission count": the header is read before the figures
      // under it are, and what a reader wants to know from the column is how much this
      // group can do - which the number answers.
      permissionsCount: 'Permissions',
      actions: 'Actions',
    },
    // The four row actions, as tooltips. The icons carry no text of their own, so the name
    // each one gets here is the only thing that says what pressing it does. Block and
    // unblock are one button meaning the opposite depending on the row's own state, exactly
    // as on the accounts table.
    actions: {
      edit: 'Edit',
      // The read among the four. It opens the accounts list with this group already on it -
      // the filter lives in the address, so there is nothing to hand over and the page that
      // comes out can be reloaded or sent to somebody else and still show the same list.
      showAccounts: 'Show accounts',
      block: 'Block',
      unblock: 'Unblock',
      delete: 'Delete',
    },
    // Why two of the four buttons are disabled on the three base groups. Each states what
    // the server would answer (`409 user_group_immutable`) as a rule rather than as an
    // error, because the visitor is being told before the click, not after it.
    baseImmutable: 'A base group can only be renamed.',
    baseUndeletable: 'The base groups cannot be deleted.',
    // The one sentence the edit form puts above three disabled controls, naming the whole
    // row rather than any one field: what a base group is, and which of the four properties
    // still belongs to whoever is editing it.
    baseLocked:
      'This is one of the three base groups. Only the name can be changed - its role, status and permissions are what this installation was set up with.',
    // The permission section of the form.
    permissionsTitle: 'Permissions',
    // Stated under the checkboxes rather than left to be discovered: a permission belongs to
    // exactly one role, and offering one the role does not own would be offering a choice
    // the server throws away on the way in.
    permissionsHint:
      'A permission decides which accounts a member of this group may see and change. It belongs to the groupвЂ™s own level, so a permission that level does not own cannot be granted.',
    // The section for a role that owns none of them - which, on this installation, is every
    // role but Admin. An empty space would read as a section that failed to load.
    permissionsNone: 'This role owns no permissions of its own, so there is nothing to grant here.',
    // The two bulk moves beside that hint. Eight permissions is eight clicks; forty is the
    // point where a form that offered only the boxes starts producing groups that are
    // nearly right, which is worse than a form that offered nothing. Both act on what the
    // chosen level may grant and never on the whole catalog - see the buttons' own comments.
    permissionsSelectAll: 'Select all',
    permissionsClear: 'Clear',
    // The one action that asks first. Only deletion is irreversible - a blocked group is an
    // unblock away - so a confirmation here is a question and not a habit.
    deleteConfirmTitle: 'Delete this group?',
    deleteConfirmBody:
      'The group "{{name}}" will be removed. Accounts in it are not deleted - the deletion is refused while any account still belongs to it.',
    deleteConfirmOk: 'Delete',
    // The bulk toolbar over the selection, mirroring the accounts table's for the reason that
    // table mirrors this one: two tables with different rules for a selection would be two
    // places for one of those rules to be wrong. There is no move here, because a group
    // cannot be moved into a group.
    bulk: {
      menu: 'Actions',
      // The count rides inside the button, for the reason the accounts table folds it in
      // there too: this row has no room to spare, and the Russian labels are the long ones.
      menuWithCount: 'Actions ({{count}})',
      // Where the three foundation rows are named. A run over a selection containing two of
      // them blocks the rest and reports the two - every line below is the server's own
      // sentence for that row's code, so `user_group_immutable` reads the same here as it
      // does when a single disabled button would have said it.
      partialTitle: 'Changed {{done}} of {{total}}. The rest were refused:',
      deleteTitle: 'Delete these {{count}} groups?',
      deleteBody:
        'Each group will be removed. Accounts in them are not deleted - a deletion is refused while any account still belongs to that group.',
    },
  },
  // The administrator's visit history over the accounts. Its own block rather than more keys
  // under `users` or `sessions`, for the reason the group table got one: the three are behind
  // different gates (`view*Clients` here, `viewUserGroups` there, no gate at all on the
  // account's own), and a page that fails must name which of them it was asking for. It also
  // answers a different question from both - not "which accounts are there" and not "where
  // did *I* sign in from" but "where are the accounts I mind signing in from".
  adminSessions: {
    // The heading. Deliberately *not* `nav.items.adminSessions`: a card repeating the menu
    // item's name in large letters would say the menu again instead of naming the page. The
    // pair is `User sessions` beside `Visit history` - one naming how the visitor got
    // there, one naming what they are looking at, and the second is what the page is for:
    // one row per token, so every sign-in and every rotation of it is on the list.
    listTitle: 'Visit history',
    // The table. The search matches what a row *stores* - the address and the user agent -
    // because the country and the provider are resolved from GeoIP while the page is built
    // and are not in the row for a filter to find.
    searchPlaceholder: 'Search by IP address or user agent',
    // For the screen reader: a placeholder is not a label, and it disappears the moment
    // anything is typed - which is exactly when the field needs naming.
    searchLabel: 'Search visits',
    // The per-account filter, which lives in the address (`?userId=`) the way the accounts
    // table's group filter does. Empty means "every account I may read".
    filterAnyUser: 'Any user',
    filterUserLabel: 'Filter by account',
    // Only ever drawn for an operator whose group owns no view permission at all - the
    // endpoint is behind `ViewAnyUsersPolicy`, so such a visitor reaches a `403` and never
    // a page. The alert carries that sentence; this is what the table itself says.
    empty: 'No recorded activity among the accounts you can see.',
    // Shown instead of `empty` while a search or the account filter is on. The rows exist,
    // they are simply not in this answer - and "there is no activity" would be contradicted
    // by the filters right above the table.
    noMatches: 'No visits match this search.',
    // The pager's own sentence, which antd would otherwise write in its default locale.
    range: '{{from}}вЂ“{{to}} of {{total}}',
    current: 'This session',
    // The row whose session has already been ended or has quietly expired. It stays in the
    // list rather than being dropped: a history is a record of what happened, and a row that
    // vanished the moment its session ended would take that moment with it. Its button is
    // greyed, which is the same fact told before the click rather than after it.
    ended: 'Ended',
    // The handle at the right edge of every column header, which the visitor drags to change
    // the width. `{{column}}` is that column's own name - a separator a screen reader cannot
    // name is a separator it can only find by tabbing into it blindly.
    resizeColumn: 'Resize the {{column}} column',
    // Puts every column back to the width it ships with, next to the search rather than in a
    // menu, because the widths it undoes are set by hand in the first place.
    resetWidths: 'Reset widths',
    columns: {
      // The account the visit belongs to. Its own name rather than "User": every other
      // account page in this application calls the subject a user, and the one word this
      // column must not be is the same word the *account's own* history uses for its
      // visitor.
      user: 'Account',
      when: 'When',
      ip: 'IP address',
      country: 'Country',
      network: 'Network',
      agent: 'User agent',
      actions: 'Actions',
    },
    // The one row action, as a verb on the button - the accounts table labels its column
    // "Actions" and lets the tooltip of each icon name it, but there is one button here and
    // a column of icons for a single action would be a row of identical pictures.
    end: 'End',
    // The two reasons the button may be greyed, each stated as the fact rather than as a
    // refusal. A greyed button that says nothing is a dead control, and the point of
    // disabling it rather than letting the refusal arrive as a toast was to say why before
    // the click.
    endHint: 'This session is already signed out.',
    // The success sentence, used only when the server's own code arrives without a
    // translation yet - `messages.api.session_revoked` is where it normally comes from, so
    // this is the floor beneath that lookup rather than a second place for the same fact.
    revoked: 'The session has been ended.',
    // Named with the way out rather than only the refusal, for the reason the active
    // sessions page gives: "cannot end this one" without "sign out instead" is a dead end
    // on a page whose whole purpose is ending sessions.
    endCurrentHint: 'The session you are reading with cannot be ended from here. Sign out instead.',
  },
  // The content categories table, shared by all four content sections: one component,
  // four addresses, and the kind of content each instance works on read from its own menu
  // key. Everything here is copy the server does not send.
  contentCategories: {
    // One heading per kind of content. The four addresses of the page differ in nothing
    // else, and a card that said only "Categories" on all four would leave the visitor
    // reading four identical screens with no way to tell which one they are on. Keyed by
    // the enum member, the way the account statuses are.
    titles: {
      VideoStream: 'Video stream categories',
      AudioStream: 'Audio stream categories',
      VideoOnDemand: 'On-demand video categories',
      Series: 'Series categories',
    },
    empty: 'No categories yet.',
    // The drag handle's own name - the only text it carries, for a visitor on a keyboard
    // or a screen reader, and the instruction for everybody else.
    dragHint: 'Drag to rearrange',
    // The two toolbar controls for the unsaved arrangement. Dragging changes the screen
    // only; this pair is what turns the arrangement into a request - one save for however
    // many rows the visitor moved.
    saveOrder: 'Save order',
    discardOrder: 'Discard changes',
    columns: {
      sort: 'Order',
      name: 'Name',
      actions: 'Actions',
    },
    addTitle: 'Add category',
    editTitle: 'Edit category',
    nameLabel: 'Name',
    nameRequired: 'Enter a category name',
    nameLength: 'Up to 100 characters',
    create: 'Create',
    actions: {
      members: 'Show content',
      rename: 'Rename',
      delete: 'Delete',
    },
    // The one action that asks first. Nothing is filed under categories yet, so the body
    // carries no warning about members - the confirmation is the whole of the guard until
    // the content tables arrive.
    deleteConfirmTitle: 'Delete this category?',
    deleteConfirmBody: 'The category "{{name}}" will be removed. This cannot be undone.',
    deleteConfirmOk: 'Delete',
  },
  // The bouquets: the packages content is delivered in. Two screens share this block - the
  // list, and the content manager one package opens - because they are two halves of one
  // domain and a second prefix would only hide that.
  bouquets: {
    // The list's heading. Deliberately *not* `nav.items.bouquetsManage`: the menu says how
    // the visitor got there, and the card says what they are looking at.
    listTitle: 'Bouquets',
    emptyList: 'No bouquets yet.',
    // The drag handle's own name - the only text it carries.
    dragHint: 'Drag to rearrange',
    // The two toolbar controls for the unsaved arrangement, the same pair the categories
    // table carries and for the same reason: a drag changes the screen only.
    saveOrder: 'Save order',
    discardOrder: 'Discard changes',
    // The word beside a row that holds nothing. It is the fact that decides whether the row
    // can be deleted, said in the row rather than inferred from four zeroes.
    empty: 'empty',
    columns: {
      sort: 'Order',
      name: 'Name',
      videoStreams: 'Video streams',
      audioStreams: 'Audio streams',
      videoOnDemand: 'On demand',
      series: 'Series',
      actions: 'Actions',
    },
    addTitle: 'Add bouquet',
    renameTitle: 'Rename bouquet',
    nameLabel: 'Name',
    nameRequired: 'Enter a bouquet name',
    nameLength: 'Up to 100 characters',
    // Said under the input, because the rule is not visible in the field: two names that
    // differ only in case are one bouquet.
    nameHint: 'The name must be unique, ignoring the case.',
    // The field the four content forms carry under their category selector. One key for all
    // four rather than one per form: the control asks the same question of every kind of
    // content, and four copies would be four places for the wording to drift.
    fieldLabel: 'Bouquets',
    fieldHint:
      'The packages this item belongs to. A bouquet it joins puts it at the end of its list; the order inside a bouquet is arranged on the bouquet screen.',
    fieldPlaceholder: 'No bouquet',
    actions: {
      content: 'Manage content',
      rename: 'Rename',
      delete: 'Delete',
    },
    // Why the delete button is disabled: the server refuses the same click with a count, and
    // the row says it before the click rather than after it.
    notEmpty: 'Empty the bouquet before deleting it.',
    // The reason every write on both bouquets screens is disabled: the visitor's group does
    // not hold `editBouquets`. One sentence for the group of controls rather than one per
    // button, because the answer is the same for all of them.
    noPermission: 'Your group may not change the bouquets.',
    deleteConfirmTitle: 'Delete this bouquet?',
    deleteConfirmBody: 'The bouquet "{{name}}" will be removed. This cannot be undone.',
    deleteConfirmOk: 'Delete',
    // The content manager. Its heading carries the package's name, because the visitor
    // arrived from a row and the page must say which row it was.
    content: {
      title: 'Content of "{{name}}"',
      back: 'Back to the list',
      save: 'Save',
      hint:
        'Move items between the two lists, arrange the bouquet in the order it should be delivered in, then save. Nothing is written until you do.',
      tabs: {
        videostream: 'Video streams',
        audiostream: 'Audio streams',
        videoondemand: 'On demand',
        series: 'Series',
      },
      members: 'In the bouquet',
      available: 'Not in the bouquet',
      emptyMembers: 'The bouquet holds nothing of this kind yet.',
      emptyAvailable: 'Everything of this kind is already in the bouquet.',
      // Shown when the lists hold items but the filters hide all of them - a different
      // fact from an empty list, and one the operator can act on by clearing a filter.
      emptyFiltered: 'Nothing matches the filter.',
      add: 'Add to the bouquet',
      remove: 'Remove from the bouquet',
      moveUp: 'Move the selected items up',
      moveDown: 'Move the selected items down',
      sortAz: 'Sort by name',
      filterMembers: 'Filter the bouquet',
      filterAvailable: 'Filter the other items',
      // The category filter beside the name filter. It exists for the bulk gesture: narrow
      // to one section, select all of it, and the whole category moves in one action.
      filterCategory: 'Any category',
      selectAll: 'Select all',
      clearSelection: 'Clear the selection',
    },
  },
  // The admin's on-demand catalogue: its own block rather than more keys under
  // `videoStreams`, because the two are separate resources behind separate permissions. The
  // page adds the two actions the stream table cannot offer: play, which goes through the
  // endpoint that owns the delivery rule, and transcode, whose menu states that this
  // installation has no encoder yet.
  videoOnDemand: {
    listTitle: 'On-demand videos',
    addTitle: 'Add video',
    editTitle: 'Edit video',
    create: 'Create',
    save: 'Save',
    backToList: 'Back to the list',
    columns: {
      id: 'ID',
      poster: 'Cover',
      name: 'Name',
      category: 'Category',
      status: 'Status',
      releaseDate: 'Released',
      rating: 'Rating',
      actions: 'Actions',
      type: 'Type',
    },
    // Two descriptions, not a ramp: `VodStatus` is not an ordered scale.
    statuses: {
      Active: 'Published',
      Inactive: 'Hidden',
    },
    // Two answers to "who carries the bytes", not two kinds of file.
    types: {
      Relay: 'Relay',
      Redirect: 'Redirect',
    },
    actions: {
      publish: 'Publish',
      hide: 'Hide',
      play: 'Play',
      transcode: 'Transcode',
      transcodeUnavailable: 'This server has no transcoder configured.',
      edit: 'Edit',
      delete: 'Delete',
    },
    noPermission: 'Your group does not allow this.',
    searchLabel: 'Search by name',
    searchPlaceholder: 'Search by name',
    filterAnyCategory: 'Any category',
    filterAnyStatus: 'Any status',
    filterAnyType: 'Any type',
    pauseRefresh: 'Pause auto-refresh',
    resumeRefresh: 'Resume auto-refresh',
    autoRefreshHint: 're-reads the table every {{seconds}} seconds',
    empty: 'No videos yet.',
    noMatches: 'No videos match these filters.',
    range: '{{from}}вЂ“{{to}} of {{total}}',
    resizeColumn: 'Resize the {{column}} column',
    resetWidths: 'Reset widths',
    // The one action on the table that asks first, because it cannot be undone.
    deleteConfirmTitle: 'Delete this video?',
    deleteConfirmBody: 'The video "{{name}}" will be removed. This cannot be undone.',
    deleteConfirmOk: 'Delete',
    bulk: {
      menu: 'Actions',
      menuWithCount: 'Actions ({{count}})',
      publish: 'Publish',
      hide: 'Hide',
      move: 'Move to category',
      type: 'Change type',
      delete: 'Delete',
      apply: 'Apply',
      moveTitle: 'Move the selected videos',
      movePlaceholder: 'Choose a category',
      typeTitle: 'Change the type of the selected videos',
      partialTitle: 'Changed {{done}} of {{total}}. The rest were refused:',
      deleteTitle: 'Delete these {{count}} videos?',
      deleteBody: 'Each video will be removed. This cannot be undone.',
    },
    transcode: {
      title: 'Transcode "{{name}}"',
      unavailable:
        'This server has no transcoder configured, so no format can be produced yet.',
    },
  },
  // The add and edit form for one on-demand video. Its own block rather than more keys under
  // `videoOnDemand`, because the table names what a row is and the form names what an
  // operator is being asked to type.
  videoOnDemandForm: {
    idLabel: 'ID',
    nameLabel: 'Name',
    nameRequired: 'Enter a video name',
    namePlaceholder: 'Dune',
    categoryRequired: 'Choose a category',
    statusRequired: 'Choose whether the video is published',
    statusHint: 'A published video is offered to clients; a hidden one is kept without being listed.',
    typeRequired: 'Choose how the video is delivered',
    typeHint:
      'Relay is served by this installation (a local file or a proxied address); redirect sends the client to the source.',
    mediaDivider: 'Where it plays from',
    metadataDivider: 'Metadata',
    sourceLabel: 'Source',
    sourceRequired: 'Enter a URL or a path to a file',
    sourcePlaceholder: 'https://example.com/film.mp4 or /media/film.mp4',
    sourceHint: 'One address: a URL or a path to a file on this server.',
    subtitleLabel: 'Subtitles address',
    urlPlaceholder: 'https://example.com/file',
    imdbLabel: 'IMDb identifier',
    posterLabel: 'Cover address',
    backdropLabel: 'Backdrop address',
    plotLabel: 'Synopsis',
    castLabel: 'Cast',
    directorLabel: 'Director',
    genresLabel: 'Genres',
    tagsHint: 'Press Enter after each entry. Up to {{max}}.',
    releaseDateLabel: 'Release date',
    releaseDatePlaceholder: 'Choose a date',
    ratingLabel: 'Rating',
    ratingInvalid: 'Enter a rating between 0 and 10.',
    countryLabel: 'Country',
    countryPlaceholder: 'Choose a country',
    maxLength: 'Up to {{max}} characters.',
  },
  // The admin's series catalogue. Its own block rather than more keys under `videoOnDemand`,
  // because the two are separate resources behind separate permissions. The table adds the two
  // counts over the episodes and the entries into the episode and season screens.
  series: {
    listTitle: 'Series',
    addTitle: 'Add series',
    editTitle: 'Edit series',
    create: 'Create',
    save: 'Save',
    backToList: 'Back to the list',
    columns: {
      id: 'ID',
      poster: 'Poster',
      name: 'Name',
      category: 'Category',
      status: 'Status',
      seasons: 'Seasons',
      episodes: 'Episodes',
      releaseDate: 'Released',
      rating: 'Rating',
      actions: 'Actions',
    },
    statuses: {
      Active: 'Published',
      Inactive: 'Hidden',
    },
    actions: {
      publish: 'Publish',
      hide: 'Hide',
      episodes: 'Episodes',
      addEpisode: 'Add episode',
      addSeason: 'Add season',
      edit: 'Edit',
      delete: 'Delete',
    },
    noPermission: 'Your group does not allow this.',
    searchLabel: 'Search by name',
    searchPlaceholder: 'Search by name',
    filterAnyCategory: 'Any category',
    filterAnyStatus: 'Any status',
    empty: 'No series yet.',
    noMatches: 'No series match these filters.',
    range: '{{from}}вЂ“{{to}} of {{total}}',
    resizeColumn: 'Resize the {{column}} column',
    resetWidths: 'Reset widths',
    deleteConfirmTitle: 'Delete this series?',
    deleteConfirmBody:
      'The series "{{name}}" and every episode filed under it will be removed. This cannot be undone.',
    deleteConfirmOk: 'Delete',
    bulk: {
      menu: 'Actions',
      menuWithCount: 'Actions ({{count}})',
      publish: 'Publish',
      hide: 'Hide',
      move: 'Move to category',
      delete: 'Delete',
      apply: 'Apply',
      moveTitle: 'Move the selected series',
      movePlaceholder: 'Choose a category',
      partialTitle: 'Changed {{done}} of {{total}}. The rest were refused:',
      deleteTitle: 'Delete these {{count}} series?',
      deleteBody:
        'Each series and every episode filed under it will be removed. This cannot be undone.',
    },
  },
  // The add and edit form for one series. Its own block because the table names what a row is
  // and the form names what an operator is being asked to type.
  seriesForm: {
    idLabel: 'ID',
    countsLabel: 'Episodes',
    counts: '{{seasons}} seasons, {{episodes}} episodes - computed from the episodes.',
    nameLabel: 'Name',
    nameRequired: 'Enter a series name',
    namePlaceholder: 'Breaking Bad',
    categoryRequired: 'Choose a category',
    statusRequired: 'Choose whether the series is published',
    statusHint:
      'A published series is offered to clients; a hidden one is kept without being listed.',
    metadataDivider: 'Metadata',
    imdbLabel: 'IMDb identifier',
    posterLabel: 'Poster address',
    backdropLabel: 'Backdrop address',
    urlPlaceholder: 'https://example.com/file',
    plotLabel: 'Synopsis',
    castLabel: 'Cast',
    directorLabel: 'Director',
    genresLabel: 'Genres',
    tagsHint: 'Press Enter after each entry. Up to {{max}}.',
    releaseDateLabel: 'Release date',
    releaseDatePlaceholder: 'Choose a date',
    ratingLabel: 'Rating',
    ratingInvalid: 'Enter a rating between 0 and 10.',
    maxLength: 'Up to {{max}} characters.',
  },
  // The admin's programme guide sources: where a schedule is fetched from and how much of it
  // is kept. Its own block rather than more keys under `series`, because a guide is a
  // separate resource behind its own permission pair. The table reads and the form writes;
  // the raw guide and the programmes parsed out of it belong to the updater, which does not
  // exist yet - which is why "Reload EPG" is drawn disabled.
  epgSources: {
    listTitle: 'Guide sources',
    addTitle: 'Add source',
    editTitle: 'Edit source',
    create: 'Create',
    save: 'Save',
    backToList: 'Back to the list',
    columns: {
      id: 'ID',
      name: 'Name',
      url: 'Address',
      daysKeep: 'Days to keep',
      lastEpgUpdatedAt: 'Last refreshed',
      actions: 'Actions',
    },
    actions: {
      // The row action that reads rather than writes: which streams file their schedule
      // under this source. It opens the channels page, which has no menu item.
      channels: 'Channels',
      edit: 'Edit',
      delete: 'Delete',
    },
    // A stub on purpose: the updater is a later pass, so the button is drawn with its reason
    // attached rather than wired to an endpoint that would do nothing.
    reload: 'Reload EPG',
    reloadUnavailable: 'The guide updater is not built yet, so no guide is fetched.',
    noPermission: 'Your group does not allow this.',
    searchLabel: 'Search by name or address',
    searchPlaceholder: 'Search by name or address',
    empty: 'No guide sources yet.',
    noMatches: 'No guide source matches this search.',
    range: '{{from}}вЂ“{{to}} of {{total}}',
    resizeColumn: 'Resize the {{column}} column',
    resetWidths: 'Reset widths',
    deleteConfirmTitle: 'Delete this guide source?',
    // The programmes go with it through the cascade, so the sentence says so rather than
    // leaving the visitor to discover it afterwards.
    deleteConfirmBody:
      'The source "{{name}}" and every programme parsed from it will be removed. This cannot be undone.',
    deleteConfirmOk: 'Delete',
  },
  // The add and edit form for one guide source. Its own block because the table names what a
  // row is and the form names what an operator is being asked to type.
  epgSourcesForm: {
    idLabel: 'ID',
    nameLabel: 'Name',
    nameRequired: 'Enter a source name',
    namePlaceholder: 'Main guide',
    urlLabel: 'Address',
    urlRequired: 'Enter the address the guide is fetched from',
    urlInvalid: 'The address must be an absolute http or https URL.',
    urlPlaceholder: 'https://example.com/guide.xml',
    urlHint: 'The absolute http(s) address of the guide file.',
    daysKeepLabel: 'Days to keep',
    daysKeepRequired: 'Enter how many days of programmes to keep.',
    daysKeepHint: 'How many days of programmes are kept, between {{min}} and {{max}}.',
    maxLength: 'Up to {{max}} characters.',
  },
  // The streams that read their guide from one source: read-only, reached from a row of the
  // sources table and from nowhere else.
  epgSourceChannels: {
    title: 'Channels of "{{name}}"',
    titlePlain: 'Channels of the guide source',
    back: 'Back to the sources',
    columns: {
      streamName: 'Stream',
      channelKey: 'Channel key',
    },
    searchLabel: 'Search by stream or channel key',
    searchPlaceholder: 'Search by stream or channel key',
    empty: 'No stream reads its guide from this source.',
    noMatches: 'No channel matches this search.',
    range: '{{from}}вЂ“{{to}} of {{total}}',
    resizeColumn: 'Resize the {{column}} column',
    resetWidths: 'Reset widths',
  },
  // The episodes of one series, reached from a row of the series table.
  seriesEpisodes: {
    title: 'Episodes of "{{name}}"',
    listTitle: 'Episodes',
    backToSeries: 'Back to series',
    backToList: 'Back to the episodes',
    addEpisode: 'Add episode',
    addSeason: 'Add season',
    columns: {
      id: 'ID',
      season: 'Season',
      episode: 'Episode',
      name: 'Name',
      status: 'Status',
      type: 'Type',
      source: 'Source',
      releaseDate: 'Released',
      rating: 'Rating',
      actions: 'Actions',
    },
    types: {
      Relay: 'Relay',
      Redirect: 'Redirect',
    },
    actions: {
      transcode: 'Transcode',
      transcodeUnavailable: 'This server has no transcoder configured.',
      publish: 'Publish',
      hide: 'Hide',
      edit: 'Edit',
      delete: 'Delete',
    },
    noPermission: 'Your group does not allow this.',
    searchLabel: 'Search by name or source',
    searchPlaceholder: 'Search by name or source',
    filterAnySeason: 'Any season',
    filterAnyStatus: 'Any status',
    filterAnyType: 'Any type',
    seasonOption: 'Season {{season}}',
    empty: 'No episodes yet.',
    noMatches: 'No episodes match these filters.',
    range: '{{from}}вЂ“{{to}} of {{total}}',
    resizeColumn: 'Resize the {{column}} column',
    resetWidths: 'Reset widths',
    deleteConfirmTitle: 'Delete this episode?',
    deleteConfirmBody: 'The episode "{{name}}" will be removed. This cannot be undone.',
    deleteConfirmOk: 'Delete',
    bulk: {
      menu: 'Actions',
      menuWithCount: 'Actions ({{count}})',
      publish: 'Publish',
      hide: 'Hide',
      type: 'Change type',
      delete: 'Delete',
      apply: 'Apply',
      typeTitle: 'Change the type of the selected episodes',
      partialTitle: 'Changed {{done}} of {{total}}. The rest were refused:',
      deleteTitle: 'Delete these {{count}} episodes?',
      deleteBody: 'Each episode will be removed. This cannot be undone.',
    },
    transcode: {
      title: 'Transcode "{{name}}"',
      unavailable:
        'This server has no transcoder configured, so no format can be produced yet.',
    },
  },
  // The add and edit form for one episode of one series.
  episodeForm: {
    addTitle: 'Add episode',
    editTitle: 'Edit episode',
    idLabel: 'ID',
    seriesLabel: 'Series',
    seasonRequired: 'Enter a season number',
    episodeRequired: 'Enter an episode number',
    nameLabel: 'Name',
    namePlaceholder: 'Pilot',
    maxLength: 'Up to {{max}} characters.',
    mediaDivider: 'Where it plays from',
    sourceLabel: 'Source',
    sourceRequired: 'Enter a URL or a path to a file',
    sourcePlaceholder: 'https://example.com/episode.mp4 or /media/episode.mp4',
    sourceHint: 'One address: a URL or a path to a file on this server.',
    statusRequired: 'Choose whether the episode is published',
    statusHint:
      'A published episode is offered to clients; a hidden one is kept without being listed.',
    typeRequired: 'Choose how the episode is delivered',
    typeHint:
      'Relay is served by this installation; redirect sends the client to the source.',
    metadataDivider: 'Metadata',
    imdbLabel: 'IMDb identifier',
    imageLabel: 'Image address',
    urlPlaceholder: 'https://example.com/file',
    plotLabel: 'Synopsis',
    releaseDateLabel: 'Release date',
    releaseDatePlaceholder: 'Choose a date',
    ratingLabel: 'Rating',
    ratingInvalid: 'Enter a rating between 0 and 10.',
    create: 'Create',
    save: 'Save',
  },
  // The add-season form: one folder in, a season's worth of episode rows out.
  seasonForm: {
    title: 'Add a season',
    seriesLabel: 'Series',
    seasonLabel: 'Season number',
    seasonRequired: 'Enter a season number',
    folderLabel: 'Folder with the episodes',
    folderRequired: 'Enter a path to a folder on this server',
    folderMax: 'Up to {{max}} characters.',
    folderHint:
      'Every video file directly in this folder becomes one episode, numbered in natural order.',
    folderPlaceholder: '/media/series/breaking-bad/season-1',
    addSeasonNumber: 'Add the season number to the episode name',
    seasonNameHint: 'The name becomes "Breaking Bad S01".',
    addEpisodeNumber: 'Add the episode number to the episode name',
    episodeNameHint: 'With both boxes ticked the name becomes "Breaking Bad S01E02".',
    statusRequired: 'Choose whether the episodes are published',
    statusHint:
      'A published episode is offered to clients; a hidden one is kept without being listed.',
    typeRequired: 'Choose how the episodes are delivered',
    typeHint:
      'Relay is served by this installation; redirect sends the client to the source.',
    create: 'Create the season',
    created: 'The season has been created with {{count}} episodes.',
  },
  // The admin's streams table: the first page behind the content rail, and the first of
  // the four content sections whose `add` and `manage` entries actually open something.
  // Everything here is copy the server does not send.
  videoStreams: {
    // The list page's heading. Deliberately *not* `nav.items.videoStreamsManage`, which
    // names the action (`Manage video streams`/`РЈРїСЂР°РІР»РµРЅРёРµ`) - a card titled after the menu
    // item would name neither the page nor its subject. The pair here is `Video streams`
    // beside `Manage video streams`: one naming what is looked at, one naming how the
    // visitor got there.
    listTitle: 'Video streams',
    // The form's heading, and the label of the add button above the table. One component
    // serves both addresses, so the title is chosen here rather than read from
    // `sectionKey`: the same page is this at `video-streams/add` and the edit title at
    // `video-streams/{id}`, and a key naming only the first would title the second a lie.
    addTitle: 'Add stream',
    editTitle: 'Edit stream',
    create: 'Create',
    backToList: 'Back to the list',
    // The eight columns of the table, and one more name. `type` has no column of its own -
    // it rides in the state column, under the status - but it is named here because the sort
    // field shares its name and the filter's own accessible label comes from this block too.
    columns: {
      id: 'ID',
      icon: 'Icon',
      name: 'Name',
      category: 'Category',
      activeSource: 'Active source',
      status: 'Status',
      epg: 'EPG',
      actions: 'Actions',
      type: 'Type',
    },
    // Two descriptions, not a ramp: `VideoStreamStatus` is not an ordered scale, so the
    // words say what the state is rather than how much of it there is. The form and the
    // table use the same pair: a stream is active or disabled.
    statuses: {
      Stopped: 'Disabled',
      Running: 'Active',
    },
    // The six kinds, keyed by the enum member the way every other enum in this file is.
    types: {
      Permanent: 'Permanent',
      OnDemand: 'On demand',
      Redirect: 'Redirect',
      OnlineSeries: 'Online series',
      VodPlaylist: 'Video playlist',
      FilePlaylist: 'File playlist',
    },
    // The EPG column answers yes or no about two fields; these are the two words it says.
    epgOn: 'On',
    epgOff: 'Off',
    actions: {
      start: 'Start',
      stop: 'Stop',
      restart: 'Restart',
      edit: 'Edit',
      delete: 'Delete',
      // Why play is greyed out. It is an operation on the relaying process, and that
      // process does not exist yet - so the reason is stated before the click, which is
      // the whole point of drawing the button disabled rather than live and letting a 404
      // explain it afterwards.
      notImplemented: 'Not implemented yet.',
    },
    searchLabel: 'Search by name',
    searchPlaceholder: 'Search by name',
    filterAnyCategory: 'Any category',
    filterAnyStatus: 'Any status',
    filterAnyType: 'Any type',
    resetWidths: 'Reset widths',
    // The auto-refresh, which is on by default and has a visible pause beside the search.
    // A page that silently re-reads under a visitor's cursor moves the row they were about
    // to press, and a pause they cannot find is not a pause.
    pauseRefresh: 'Pause auto-refresh',
    resumeRefresh: 'Resume auto-refresh',
    autoRefreshHint: 're-reads the table every {{seconds}} seconds',
    // The resize handle's own name, the only text it carries for a keyboard or a screen
    // reader: the column's own label lives in `columns`, and the handle says what it is
    // resizing rather than what the column is called.
    resizeColumn: 'Resize the {{column}} column',
    range: '{{from}}вЂ“{{to}} of {{total}}',
    empty: 'No streams yet.',
    noMatches: 'No streams match these filters.',
    // Why the bulk toolbar is greyed for an operator whose group holds no `editStreams`
    // grant. The server refuses the same run with 403 `permission_denied`, so this is the
    // same fact told before the click - and it names the group rather than the visitor,
    // because that is what the refusal is about.
    noPermission: 'Your group does not allow this.',
    // The bulk toolbar over the selection: one count and one menu, mirroring the accounts
    // and groups tables. Both are drawn even with nothing selected, for the reason those
    // two are: a control that appears and disappears reads as a broken one.
    bulk: {
      menu: 'Actions',
      menuWithCount: 'Actions ({{count}})',
      // The five transitions and the four edits, in one menu. The labels are the row
      // actions' own names, because the operation is the same one over more than one row,
      // and a second name would be a second name for one thing.
      start: 'Start',
      stop: 'Stop',
      restart: 'Restart',
      delete: 'Delete',
      move: 'Move to category',
      type: 'Change type',
      delay: 'Change delay',
      archive: 'Change archive length',
      restream: 'Change restream settings',
      // The move dialog. Its destination is a decision about the whole selection, so it is
      // asked for in a dialog rather than in the toolbar: an inline control would be
      // answering it for a menu that has already closed.
      moveTitle: 'Move these streams to a category',
      movePlaceholder: 'Choose a category',
      apply: 'Apply',
      // The type dialog. Only the three kinds a bulk change can keep whole are offered:
      // the other three need a payload only the single-row form can collect.
      typeTitle: 'Change the type of these streams',
      typePlaceholder: 'Choose a type',
      typeHint:
        'Only permanent, on-demand and redirect streams can be chosen here. A stream whose current settings the new type cannot keep is changed anyway - the settings it loses are reported as warnings.',
      // One number per dialog, and the same "zero means none" rule the single-row form
      // states in its hint.
      delayTitle: 'Change the delay of these streams',
      delayLabel: 'Delay (minutes)',
      delayHint: 'Zero removes the delay. Only permanent streams can carry one.',
      archiveTitle: 'Change the archive length of these streams',
      archiveLabel: 'Archive length (days)',
      archiveHint: 'Zero keeps no archive. Only permanent streams keep one.',
      // The four restream settings are replaced as a group, not patched field by field: a
      // blank input clears its setting, so "these are the values now" and "clear them" are
      // one request shape. The dialog says so once rather than beside each input.
      restreamTitle: 'Change the restream settings of these streams',
      restreamHint:
        'The four values replace the current ones on every selected stream. Leave a field empty to clear it. Only permanent and on-demand streams can carry them.',
      restreamUserAgent: 'User agent',
      restreamProxy: 'Proxy',
      restreamCookies: 'Cookies',
      restreamHeaders: 'Extra headers',
      // Shown instead of a toast the moment at least one row was refused. Every line is
      // the server's own sentence for that row's code, already translated here, so the
      // reason is read rather than guessed at from a count.
      partialTitle: 'Changed {{done}} of {{total}}. The rest were refused:',
      // Only deletion asks first, for the reason only deletion asks first on a single row.
      deleteTitle: 'Delete these {{count}} streams?',
      deleteBody:
        'Each stream will be removed with its sources and files. This cannot be undone.',
    },
    // The one action on the table that asks first. The body quotes the stream's own name,
    // because that is what the visitor is reading when they press it; what happens to the
    // *category* is the category's dialog to make - the server refuses to delete one a
    // stream still files under, and that is a question about the row's category.
    deleteConfirmTitle: 'Delete this stream?',
    deleteConfirmBody: 'The stream "{{name}}" will be removed with its sources and files. This cannot be undone.',
    deleteConfirmOk: 'Delete',
  },
  // The add and edit form for one stream. Its own block rather than more keys under
  // `videoStreams`, because the two are not the same copy: the table names what a row
  // *is*, and the form names what an operator is being asked to *type*.
  videoStreamsForm: {
    idLabel: 'ID',
    nameLabel: 'Name',
    nameRequired: 'Enter a stream name',
    // What the field asks for before anything is typed. `maxLength` is the only other hint
    // the name carries, and it appears once the visitor is already over the limit - so the
    // shape of a good name is stated up front, in the field itself.
    namePlaceholder: 'Morning news',
    typeRequired: 'Choose the kind of stream',
    categoryRequired: 'Choose a category',
    // The state picker, which the add form starts at active. The hint says what the two
    // words mean in relay terms, because "disabled" alone does not say whether the row is
    // kept or removed.
    statusRequired: 'Choose whether the stream is active',
    statusHint: 'An active stream relays; a disabled one is kept without broadcasting.',
    // One sentence per kind, drawn under the type picker as the visitor changes it. This
    // is the whole of what the picker decides - which payload the fields below collect -
    // so it is stated at the moment the choice is made rather than by the fields quietly
    // appearing and disappearing.
    hints: {
      Permanent: 'Relays from a list of addresses, all the time, and may keep an archive.',
      OnDemand: 'Relays from a list of addresses, but only when a viewer asks for it.',
      Redirect: 'Sends viewers on to another address rather than relaying a list.',
      OnlineSeries: 'Plays one series you select, by its identifier.',
      VodPlaylist: 'Plays one video-on-demand item, by its identifier.',
      FilePlaylist: 'Plays a list of stored files, in order.',
    },
    // The six dividers. Each names the section it opens rather than a field inside it, so
    // the visitor can find the group they came for without reading every label. The delay
    // and archive pair and the guide pair are separate sections because they answer
    // different questions - "how far behind live is it, and what does it keep" versus
    // "where does the programme guide come from" - and because the first pair is the one a
    // kind may not carry at all.
    payloadDivider: 'What it plays',
    timingDivider: 'Delay and archive',
    guideDivider: 'Programme guide',
    identityDivider: 'Identity',
    restreamDivider: 'Restreaming',
    // The ordered list of addresses (or of paths), which is one control used twice.
    sourcesLabel: 'Sources, in relay order',
    sourcesPlaceholder: 'https://example.com/stream.ts',
    filesLabel: 'Files, in play order',
    filesPlaceholder: '/media/news-1900.ts',
    // The three buttons of one entry, plus the one that adds another entry underneath. The
    // labels name the list's own noun - a source or a file - because the same component
    // draws both and "row" would describe the control rather than its contents.
    moveUp: 'Move up',
    moveDown: 'Move down',
    removeSource: 'Remove this source',
    addSource: 'Add a source',
    removeFile: 'Remove this file',
    addFile: 'Add a file',
    // The whole-list rule: at least one entry has to be filled in. Each sentence names its
    // list rather than any one entry, because no particular entry is at fault - they are
    // all blank.
    sourcesRequired: 'Add at least one source.',
    filesRequired: 'Add at least one file.',
    // The three identifier fields. Each label doubles as the name inside the "not a
    // uuid" sentence, so a refusal under two inputs of the guide pair says which fact each
    // one is missing.
    seriesId: 'Series',
    vodItems: 'On-demand videos',
    epgSourceId: 'EPG source',
    // The guide source is chosen from the ones that exist; the placeholder is the prompt
    // rather than a uuid to imitate, because a source is a row of this installation now.
    epgSourcePlaceholder: 'Choose a guide source',
    // The series and the video are chosen from their catalogues for the same reason the
    // source is: the field names a row of this installation, and an operator picking a series
    // by name is the whole of the interaction. The empty text is drawn when the catalogue has
    // nothing in it - which reads differently from a failed read, and the hint says which.
    seriesIdPlaceholder: 'Choose a series',
    seriesIdHint: 'Leave empty if this stream plays no series.',
    seriesIdEmpty: 'The catalogue holds no series yet.',
    vodIdPlaceholder: 'Choose a video',
    vodItemsHint: 'The playlist plays these videos in order and starts over at the end.',
    vodIdEmpty: 'The catalogue holds no videos yet.',
    addVod: 'Add a video',
    removeVod: 'Remove the video',
    vodItemsRequired: 'Add at least one video to the playlist.',
    idPlaceholder: '00000000-0000-0000-0000-000000000000',
    idHint: 'Leave empty if this stream has none.',
    idInvalid: '{{field}} must be an identifier.',
    epgChannelKey: 'EPG channel key',
    epgChannelKeyPlaceholder: 'channel.key',
    // The guide is two facts or neither: a half-configured pair would be stored as "no
    // guide", which tells the operator the form had not been saved. The same sentence
    // lands under both inputs, because neither one alone is the mistake.
    epgPair: 'Set both the source and the channel key, or neither.',
    epgPairHint: 'A programme guide needs both of these. Leave both empty for none.',
    delayLabel: 'Delay (minutes)',
    delayHint: 'How many minutes behind live the relay runs. Zero means no delay.',
    // Why the field is greyed out for the other five kinds, stated as the fact rather than
    // as a refusal: it is drawn rather than hidden, so a visitor can see that the control
    // exists and learn what kind of stream it belongs to.
    delayPermanentOnly: 'Only a permanent stream can have a delay.',
    archiveLabel: 'Archive length (days)',
    archiveHint: 'How many days of archive a permanent stream keeps. Zero keeps none.',
    // The same treatment for the archive: the field stays visible, the reason takes the
    // hint's place, and `toBody` sends zero for a kind that cannot keep one.
    archivePermanentOnly: 'Only a permanent stream keeps an archive.',
    nonNegative: 'Enter zero or more.',
    // Why the four restreaming controls are greyed together - one sentence above the group
    // rather than the same reason four times under each field.
    restreamPermanentOnDemandOnly:
      'HTTP restreaming settings are available only for permanent and on-demand streams.',
    logoUrlLabel: 'Icon address',
    urlPlaceholder: 'https://example.com/logo.png',
    enigma2SidLabel: 'Enigma2 service id',
    sidPlaceholder: '1:0:1:0:0:0:0:0:0:0',
    userAgentLabel: 'User agent',
    userAgentPlaceholder: 'Mozilla/5.0 ...',
    proxyLabel: 'Proxy',
    proxyPlaceholder: 'http://proxy.example:8080',
    // Cookies and headers are free text with no length bound on either side: what they
    // hold is a request line the relay copies verbatim, and a placeholder that invented a
    // format would be teaching a shape the server never asked for.
    cookiesLabel: 'Cookies',
    cookiesPlaceholder: 'session=abc; token=def',
    headersLabel: 'Extra headers',
    headersPlaceholder: 'X-Forwarded-For: 1.2.3.4',
    maxLength: 'Up to {{max}} characters.',
  },
  // The playlist import. It reuses `videoStreamsForm` for every field it shares with the add
  // form - the category, the state, the kind, the delay and archive pair and the restreaming
  // group - and adds only what is new here: the file itself and the report afterwards.
  videoStreamsImport: {
    title: 'Import streams',
    playlistLabel: 'M3U playlist',
    playlistRequired: 'Choose an M3U playlist file.',
    playlistHint:
      'Each entry becomes a stream of its own. Up to {{entries}} entries; an HLS playlist cannot be imported.',
    dropHint: 'Drop the playlist here',
    chooseHint: 'or choose a file from disk. Only .m3u and .m3u8 are accepted.',
    fileTooLarge: 'The playlist is larger than {{size}} MB.',
    totalCount: 'Entries: {{total}}',
    importedCount: 'Imported: {{imported}}',
    updatedCount: 'Updated: {{updated}}',
    skippedCount: 'Skipped: {{skipped}}',
    failedCount: 'Failed: {{failed}}',
    problemsTitle: 'Entries that were not imported',
    problemsTotal: 'Not imported: {{total}}',
    skippedTag: 'Skipped',
    failedTag: 'Failed',
    unnamed: 'No name',
    submit: 'Import',
    existingDivider: 'Existing streams',
    appendSourceLabel: 'Append source to existing streams',
    appendSourceHint:
      'When a name is already taken in the category, add the entryвЂ™s address to that streamвЂ™s source list instead of skipping it.',
    overwriteSettingsLabel: 'Overwrite existing stream settings',
    overwriteSettingsHint:
      'When a name is already taken in the category, replace that streamвЂ™s status, type, delay, archive length and restream settings.',
    columns: {
      line: 'Line',
      name: 'Name',
      outcome: 'Outcome',
      reason: 'Reason',
    },
  },
  // The on-demand playlist import. It reuses `videoOnDemandForm` for the category, the state
  // and the kind, and adds only the file and the report afterwards.
  videoOnDemandImport: {
    title: 'Import on-demand videos',
    playlistLabel: 'M3U playlist',
    playlistRequired: 'Choose an M3U playlist file.',
    playlistHint:
      'Each entry becomes a video of its own. Up to {{entries}} entries; an HLS playlist cannot be imported.',
    dropHint: 'Drop the playlist here',
    chooseHint: 'or choose a file from disk. Only .m3u and .m3u8 are accepted.',
    fileTooLarge: 'The playlist is larger than {{size}} MB.',
    totalCount: 'Entries: {{total}}',
    importedCount: 'Imported: {{imported}}',
    skippedCount: 'Skipped: {{skipped}}',
    failedCount: 'Failed: {{failed}}',
    problemsTitle: 'Entries that were not imported',
    problemsTotal: 'Not imported: {{total}}',
    skippedTag: 'Skipped',
    failedTag: 'Failed',
    unnamed: 'No name',
    submit: 'Import',
    columns: {
      line: 'Line',
      name: 'Name',
      outcome: 'Outcome',
      reason: 'Reason',
    },
  },
  // The admin's audio streams table. Its own block rather than more keys under
  // `videoStreams`, because the two are separate resources with separate permissions: a
  // group that may read the video streams may not read the audio ones, and a page that
  // answered 403 must be able to say which of the two it was asking for.
  audioStreams: {
    // The list page's heading. Deliberately *not* `nav.items.audioStreamsManage`, which
    // names the action (`Manage audio streams`/`РЈРїСЂР°РІР»РµРЅРёРµ`) - a card titled after the menu
    // item would name neither the page nor its subject. The pair here is `Audio streams`
    // beside `Manage audio streams`: one naming what is looked at, one naming how the
    // visitor got there.
    listTitle: 'Audio streams',
    // The form's heading, and the label of the add button above the table. One component
    // serves both addresses, so the title is chosen here rather than read from
    // `sectionKey`: the same page is this at `audio-streams/add` and the edit title at
    // `audio-streams/{id}`, and a key naming only the first would title the second a lie.
    addTitle: 'Add stream',
    editTitle: 'Edit stream',
    create: 'Create',
    backToList: 'Back to the list',
    // The seven columns of the table, and one more name. `type` has no column of its own -
    // it rides in the state column, under the status - but it is named here because the sort
    // field shares its name and the filter's own accessible label comes from this block too.
    columns: {
      id: 'ID',
      icon: 'Icon',
      name: 'Name',
      category: 'Category',
      activeSource: 'Active source',
      status: 'Status',
      actions: 'Actions',
      type: 'Type',
    },
    // Two descriptions, not a ramp: `AudioStreamStatus` is not an ordered scale, so the
    // words say what the state is rather than how much of it there is. The form and the
    // table use the same pair: a stream is active or disabled.
    statuses: {
      Stopped: 'Disabled',
      Running: 'Active',
    },
    // The two kinds, keyed by the enum member the way every other enum in this file is.
    types: {
      OnDemand: 'On demand',
      Redirect: 'Redirect',
    },
    actions: {
      start: 'Start',
      stop: 'Stop',
      restart: 'Restart',
      edit: 'Edit',
      delete: 'Delete',
      // Why play is greyed out. It is an operation on the relaying process, and that
      // process does not exist yet - so the reason is stated before the click, which is
      // the whole point of drawing the button disabled rather than live and letting a 404
      // explain it afterwards.
      notImplemented: 'Not implemented yet.',
    },
    searchLabel: 'Search by name',
    searchPlaceholder: 'Search by name',
    filterAnyCategory: 'Any category',
    filterAnyStatus: 'Any status',
    filterAnyType: 'Any type',
    resetWidths: 'Reset widths',
    // The auto-refresh, which is on by default and has a visible pause beside the search.
    // A page that silently re-reads under a visitor's cursor moves the row they were about
    // to press, and a pause they cannot find is not a pause.
    pauseRefresh: 'Pause auto-refresh',
    resumeRefresh: 'Resume auto-refresh',
    autoRefreshHint: 're-reads the table every {{seconds}} seconds',
    // The resize handle's own name, the only text it carries for a keyboard or a screen
    // reader: the column's own label lives in `columns`, and the handle says what it is
    // resizing rather than what the column is called.
    resizeColumn: 'Resize the {{column}} column',
    range: '{{from}}вЂ“{{to}} of {{total}}',
    empty: 'No audio streams yet.',
    noMatches: 'No audio streams match these filters.',
    // Why the bulk toolbar is greyed for an operator whose group holds no
    // `editAudioStreams` grant. The server refuses the same run with 403
    // `permission_denied`, so this is the same fact told before the click - and it names
    // the group rather than the visitor, because that is what the refusal is about.
    noPermission: 'Your group does not allow this.',
    // The bulk toolbar over the selection: one count and one menu, mirroring the accounts,
    // the groups and the video streams tables. Both are drawn even with nothing selected,
    // for the reason those three are: a control that appears and disappears reads as a
    // broken one.
    bulk: {
      menu: 'Actions',
      menuWithCount: 'Actions ({{count}})',
      // The three transitions and the three edits, in one menu. The labels are the row
      // actions' own names, because the operation is the same one over more than one row,
      // and a second name would be a second name for one thing.
      start: 'Start',
      stop: 'Stop',
      restart: 'Restart',
      delete: 'Delete',
      move: 'Move to category',
      type: 'Change type',
      restream: 'Change restream settings',
      // The move dialog. Its destination is a decision about the whole selection, so it is
      // asked for in a dialog rather than in the toolbar: an inline control would be
      // answering it for a menu that has already closed.
      moveTitle: 'Move these streams to a category',
      movePlaceholder: 'Choose a category',
      apply: 'Apply',
      // The type dialog. Both kinds are offered, because both carry the same payload and
      // there is nothing a change of kind would have to clear.
      typeTitle: 'Change the type of these streams',
      typePlaceholder: 'Choose a type',
      typeHint:
        'Both on-demand and redirect streams can be chosen here. Neither of them loses a setting when the kind changes.',
      // The four restream settings are replaced as a group, not patched field by field: a
      // blank input clears its setting, so "these are the values now" and "clear them" are
      // one request shape. The dialog says so once rather than beside each input.
      restreamTitle: 'Change the restream settings of these streams',
      restreamHint:
        'The four values replace the current ones on every selected stream. Leave a field empty to clear it.',
      restreamUserAgent: 'User agent',
      restreamProxy: 'Proxy',
      restreamCookies: 'Cookies',
      restreamHeaders: 'Extra headers',
      // Shown instead of a toast the moment at least one row was refused. Every line is
      // the server's own sentence for that row's code, already translated here, so the
      // reason is read rather than guessed at from a count.
      partialTitle: 'Changed {{done}} of {{total}}. The rest were refused:',
      // Only deletion asks first, for the reason only deletion asks first on a single row.
      deleteTitle: 'Delete these {{count}} streams?',
      deleteBody: 'Each stream will be removed with its sources. This cannot be undone.',
    },
    // The one action on the table that asks first. The body quotes the stream's own name,
    // because that is what the visitor is reading when they press it; what happens to the
    // *category* is the category's dialog to make - the server refuses to delete one a
    // stream still files under, and that is a question about the row's category.
    deleteConfirmTitle: 'Delete this stream?',
    deleteConfirmBody:
      'The stream "{{name}}" will be removed with its sources. This cannot be undone.',
    deleteConfirmOk: 'Delete',
  },
  // The add and edit form for one audio stream. Its own block rather than more keys under
  // `audioStreams`, because the two are not the same copy: the table names what a row
  // *is*, and the form names what an operator is being asked to *type*.
  audioStreamsForm: {
    idLabel: 'ID',
    nameLabel: 'Name',
    nameRequired: 'Enter a stream name',
    // What the field asks for before anything is typed. `maxLength` is the only other hint
    // the name carries, and it appears once the visitor is already over the limit - so the
    // shape of a good name is stated up front, in the field itself.
    namePlaceholder: 'Morning radio',
    typeRequired: 'Choose the kind of stream',
    categoryRequired: 'Choose a category',
    // The state picker, which the add form starts at active. The hint says what the two
    // words mean in relay terms, because "disabled" alone does not say whether the row is
    // kept or removed.
    statusRequired: 'Choose whether the stream is active',
    statusHint: 'An active stream relays; a disabled one is kept without broadcasting.',
    // One sentence per kind, drawn under the type picker as the visitor changes it. This
    // is the whole of what the picker decides - how the stream is played - so it is stated
    // at the moment the choice is made rather than by the fields quietly appearing and
    // disappearing.
    hints: {
      OnDemand: 'Relays from a list of addresses, but only when a listener asks for it.',
      Redirect: 'Sends listeners on to another address rather than relaying a list.',
    },
    // The three dividers. Each names the section it opens rather than a field inside it, so
    // the visitor can find the group they came for without reading every label.
    payloadDivider: 'What it plays',
    identityDivider: 'Identity',
    restreamDivider: 'Restreaming',
    // The ordered list of addresses, which is the one control every audio kind shares.
    sourcesLabel: 'Sources, in relay order',
    sourcesPlaceholder: 'https://example.com/stream.aac',
    // The three buttons of one entry, plus the one that adds another entry underneath. The
    // labels name the list's own noun - a source - because the same component draws the
    // video form's list too and "row" would describe the control rather than its contents.
    moveUp: 'Move up',
    moveDown: 'Move down',
    removeSource: 'Remove this source',
    addSource: 'Add a source',
    // The whole-list rule: at least one entry has to be filled in. The sentence names its
    // list rather than any one entry, because no particular entry is at fault - they are
    // all blank.
    sourcesRequired: 'Add at least one source.',
    logoUrlLabel: 'Icon address',
    urlPlaceholder: 'https://example.com/logo.png',
    enigma2SidLabel: 'Enigma2 service id',
    sidPlaceholder: '1:0:1:0:0:0:0:0:0:0',
    userAgentLabel: 'User agent',
    userAgentPlaceholder: 'Mozilla/5.0 ...',
    proxyLabel: 'Proxy',
    proxyPlaceholder: 'http://proxy.example:8080',
    // Cookies and headers are free text with no length bound on either side: what they
    // hold is a request line the relay copies verbatim, and a placeholder that invented a
    // format would be teaching a shape the server never asked for.
    cookiesLabel: 'Cookies',
    cookiesPlaceholder: 'session=abc; token=def',
    headersLabel: 'Extra headers',
    headersPlaceholder: 'X-Forwarded-For: 1.2.3.4',
    maxLength: 'Up to {{max}} characters.',
  },
  // The playlist import. It reuses `audioStreamsForm` for every field it shares with the
  // add form - the category, the state, the kind and the restreaming group - and adds only
  // what is new here: the file itself and the report afterwards.
  audioStreamsImport: {
    title: 'Import audio streams',
    playlistLabel: 'M3U playlist',
    playlistRequired: 'Choose an M3U playlist file.',
    playlistHint:
      'Each entry becomes an audio stream of its own. Up to {{entries}} entries; an HLS playlist cannot be imported.',
    dropHint: 'Drop the playlist here',
    chooseHint: 'or choose a file from disk. Only .m3u and .m3u8 are accepted.',
    fileTooLarge: 'The playlist is larger than {{size}} MB.',
    totalCount: 'Entries: {{total}}',
    importedCount: 'Imported: {{imported}}',
    updatedCount: 'Updated: {{updated}}',
    skippedCount: 'Skipped: {{skipped}}',
    failedCount: 'Failed: {{failed}}',
    problemsTitle: 'Entries that were not imported',
    problemsTotal: 'Not imported: {{total}}',
    skippedTag: 'Skipped',
    failedTag: 'Failed',
    unnamed: 'No name',
    submit: 'Import',
    existingDivider: 'Existing streams',
    appendSourceLabel: 'Append source to existing streams',
    appendSourceHint:
      'When a name is already taken in the category, add the entryвЂ™s address to that streamвЂ™s source list instead of skipping it.',
    overwriteSettingsLabel: 'Overwrite existing stream settings',
    overwriteSettingsHint:
      'When a name is already taken in the category, replace that streamвЂ™s status, type and restream settings.',
    columns: {
      line: 'Line',
      name: 'Name',
      outcome: 'Outcome',
      reason: 'Reason',
    },
  },
  // The tariff plans: the price list a subscription is sold under. Two screens share this
  // block - the table and the form - because they are two halves of one domain and a second
  // prefix would only hide that.
  tariffPlans: {
    // The list's heading. Deliberately *not* `nav.items.tariffPlans`: the menu says how the
    // visitor got there, and the card says what they are looking at.
    listTitle: 'Tariff plans',
    addTitle: 'Add tariff plan',
    editTitle: 'Edit tariff plan',
    create: 'Create',
    save: 'Save',
    backToList: 'Back to the list',
    columns: {
      id: 'ID',
      name: 'Name',
      cost: 'Cost',
      duration: 'Validity',
      // The two sets that used to be two columns, under one heading: what a subscription on
      // this plan actually gets. The cell draws delivery on its first line and the output
      // formats on its second.
      services: 'Services and formats',
      connections: 'Max connections',
      bouquets: 'Bouquets',
      actions: 'Actions',
    },
    actions: {
      edit: 'Edit',
      delete: 'Delete',
    },
    // The rows' two badge sets. Short forms: the cell holds several of them on one line.
    delivery: {
      m3u: 'M3U',
      mag: 'MAG',
      enigma2: 'Enigma2',
    },
    formats: {
      mpegTs: 'MPEG-TS',
      hls: 'HLS',
    },
    // The tag beside a plan's name. A trial is a property of the plan rather than a value to
    // compare down a column, which is why it rides beside the name.
    trialBadge: 'Trial',
    noPermission: 'Your group does not allow this.',
    searchLabel: 'Search by name',
    searchPlaceholder: 'Search by name',
    empty: 'No tariff plans yet.',
    noMatches: 'No tariff plan matches this search.',
    range: '{{from}}–{{to}} of {{total}}',
    resizeColumn: 'Resize the {{column}} column',
    resetWidths: 'Reset widths',
    deleteConfirmTitle: 'Delete this tariff plan?',
    // A plan that is deleted takes its two relations with it through the cascade, so the
    // sentence says so rather than leaving the visitor to discover it afterwards.
    deleteConfirmBody:
      'The plan "{{name}}", the groups it is offered to and the bouquets it delivers will be removed. This cannot be undone.',
    deleteConfirmOk: 'Delete',
  },
  // The add and edit form for one tariff plan. Its own block because the table names what a
  // row is and the form names what an operator is being asked to type - and there are five
  // sections of it: identity, services, formats, limits, and the two relations.
  tariffPlansForm: {
    idLabel: 'ID',
    // No heading over the identity fields: the owner asked for it to go, and a group of three
    // controls that open the form needs no name of its own.
    servicesSection: 'Services',
    formatsSection: 'Allowed video stream formats',
    limitsSection: 'Limits',
    accessSection: 'Access to the plan',
    contentSection: 'Plan contents',
    nameLabel: 'Name',
    nameRequired: 'Enter a name for the plan.',
    maxLength: 'No more than {{max}} characters.',
    isTrialLabel: 'Trial plan',
    isTrialHint:
      'A trial may last at most {{days}} days and may only be measured in hours or days.',
    costLabel: 'Cost',
    costRequired: 'Enter a cost.',
    costHint: 'In credits. Zero means the plan is free.',
    // The period is one parameter drawn as one row: the amount and its unit in a single
    // composite control, so nobody reads the unit as a setting of its own.
    durationLabel: 'Validity',
    durationHint: 'How long a subscription on this plan lasts.',
    durationValueRequired: 'Enter how long the plan lasts.',
    durationValueHint: 'At least one unit.',
    durationUnitRequired: 'Choose a unit.',
    trialDurationHint:
      'A trial may last at most {{days}} days, so it can only be measured in hours or days.',
    units: {
      hours: 'Hours',
      days: 'Days',
      months: 'Months',
      years: 'Years',
    },
    // The delivery methods, one line each, named exactly as the owner wrote them. One key per
    // box rather than a label plus a checkbox text: the form draws no heading over a checkbox,
    // so the name beside it is the whole of what a reader sees.
    supportsM3uCheckbox: 'M3U playlist',
    supportsMagCheckbox: 'MAG',
    supportsEnigma2Checkbox: 'Enigma2',
    // Two sentences about the same set: the requirement, and the reason for it. The first is
    // what a validator shows; the second is what the form always draws under the section's
    // heading.
    supportsRequired: 'Choose at least one service.',
    supportsHint: 'A plan has to offer at least one service.',
    outputMpegTsCheckbox: 'MPEG-TS',
    outputHlsCheckbox: 'HLS',
    formatsRequired: 'Choose at least one video stream format.',
    formatsHint: 'A plan has to allow at least one video stream format.',
    maxConnectionsLabel: 'Maximum connections',
    maxConnectionsRequired: 'Enter the number of connections.',
    maxConnectionsHint: 'Between {{min}} and {{max}}.',
    strictStbCheckbox: 'Enforce the STB device id strictly',
    strictStbHint: 'Only a MAG device carries an STB identifier, so this applies to MAG only.',
    strictStbNeedsMag: 'Enable MAG support first - the STB identifier is a MAG device fact.',
    allowRestreamCheckbox: 'Allow restreaming',
    allowRestreamHint: 'Whether a subscriber may relay the subscription through their own servers.',
    allowMultipleCountriesCheckbox: 'Allow connections from other countries',
    // The rule is about a subscriber *changing* country and keeping the subscription, not
    // about two devices in two countries at the same moment.
    allowMultipleCountriesHint:
      'Whether a subscriber may change their country and keep watching the subscription from there.',
    userGroupsLabel: 'User groups with access',
    userGroupsPlaceholder: 'Choose the groups',
    userGroupsHint: 'A plan has to be available to at least one group. Empty means nobody.',
    userGroupsRequired: 'Choose at least one user group.',
    userGroupsUnavailable:
      'The groups could not be read, so this picker offers nothing. The rest of the form still saves.',
    bouquetsLabel: 'Bouquets in the plan',
    bouquetsPlaceholder: 'Choose the bouquets',
    bouquetsHint: 'A plan has to deliver at least one bouquet.',
    bouquetsRequired: 'Choose at least one bouquet.',
    bouquetsUnavailable:
      'The bouquets could not be read, so this picker offers nothing. The rest of the form still saves.',
    selectAll: 'Select all',
    clear: 'Clear',
  },
  // The subscriptions: the rows an operator issues to a user. A block of its own rather than
  // part of `tariffPlans`, because a plan says what a subscriber may buy and a subscription is
  // one issued row that carries somebody's access.
  subscriptions: {
    listTitle: 'Subscriptions',
    addTitle: 'Add subscription',
    editTitle: 'Edit subscription',
    create: 'Create',
    save: 'Save',
    backToList: 'Back to the list',
    noPermission: 'Your group does not allow this.',
    searchLabel: 'Search by playlist id, MAG MAC or Enigma2 MAC',
    searchPlaceholder: 'Playlist id, MAG MAC or Enigma2 MAC',
    // The three filters.
    filterAnyStatus: 'Any status',
    filterAnyOwner: 'Any owner',
    filterAnyCreator: 'Any issuer',
    columns: {
      name: 'Subscription',
      expiry: 'Expires',
      connections: 'Max connections',
      creator: 'Issued by',
      actions: 'Actions',
    },
    // The status tags. Short words: the tag sits inside a two-line name cell.
    statuses: {
      active: 'Active',
      trial: 'Trial',
      blocked: 'Blocked',
      expired: 'Expired',
    },
    // The two "no plan" and "no date" answers the table draws rather than a blank.
    noPlan: 'No tariff plan',
    neverExpires: 'never',
    prolongNeedsPlan: 'Attach a tariff plan to prolong this subscription.',
    unlimited: 'unlimited',
    noCreator: 'deleted account',
    actions: {
      edit: 'Edit',
      extend: 'Extend',
      killConnections: 'Kill active connections (not available yet)',
      blockOwner: 'Block the owner',
      delete: 'Delete',
    },
    // The service subrows. Each strip names the service it is about, then its actions.
    services: {
      playlist: 'Playlist',
      mag: 'MAG',
      enigma2: 'Enigma2',
      copy: 'Copy',
      copied: 'Copied',
      regenerate: 'Regenerate',
      change: 'Change',
      delete: 'Delete',
      addPlaylist: 'Generate a playlist',
      addMag: 'Add MAG',
      addEnigma2: 'Add Enigma2',
    },
    // The small dialog that changes a MAC in place.
    changeMacTitle: 'Change the MAC address',
    changeMacLabel: 'MAC address',
    changeMacRequired: 'Enter a MAC address.',
    changeMacInvalid: 'Six pairs of hexadecimal digits, with ":" or "-" between them.',
    changeMacOk: 'Save',
    // The playlist id preview, used by both the form and the regenerate action.
    generatePlaylist: 'Generate a playlist id',
    regeneratePlaylist: 'Regenerate',
    playlistIdLabel: 'Playlist id',
    deleteConfirmTitle: 'Delete this subscription?',
    deleteConfirmBody:
      'The subscription of "{{name}}" and everything it carries will be removed. This cannot be undone.',
    deleteConfirmOk: 'Delete',
    blockOwnerConfirmTitle: 'Block the owner?',
    blockOwnerConfirmBody:
      'The account of "{{name}}" will be blocked and every one of its sessions ended. Its subscriptions will read blocked.',
    blockOwnerConfirmOk: 'Block',
    empty: 'No subscriptions yet.',
    noMatches: 'No subscription matches this filter.',
    range: '{{from}}–{{to}} of {{total}}',
    resizeColumn: 'Resize the {{column}} column',
    resetWidths: 'Reset widths',
    // The auto-refresh control, worded exactly as the streams table's.
    pauseRefresh: 'Pause auto refresh',
    resumeRefresh: 'Resume auto refresh',
    autoRefreshHint: 'Refreshes every {{seconds}} seconds',
    // The bulk menu's entry names and the dialogs behind the two that ask for a value.
    // The single-row wording a subrow action uses. Deliberately not the bulk summary: a click on
    // one row is not a "selection", and telling an operator about "selected rows" after clearing
    // one service described a run that did not happen (design decision 16). One sentence only -
    // the toast has no title of its own, so a `lastServiceTitle` key was never read.
    // The prolongation question. It names what will change (the date moves forward) rather than
    // repeating the button, because a second prolongation is a real second extension.
    prolongConfirmTitle: 'Extend this subscription?',
    prolongConfirmBody:
      'The subscription of {{name}} will be extended until {{expiry}} under the {{plan}} tariff plan.',
    prolongNoDateBody:
      'The expiry of "{{name}}" cannot be projected right now: the tariff plan length is missing, so the new date will be whatever the server computes.',
    prolongConfirmOk: 'Extend',
    lastServiceBody:
      'A subscription must keep at least one service. Add another one before removing this.',
    bulk: {
      menu: 'Actions',
      menuWithCount: 'Actions ({{count}})',
      changeTariff: 'Change the tariff plan',
      changeCreator: 'Change the issuer',
      delete: 'Delete',
      deletePlaylist: 'Clear the playlist',
      deleteMag: 'Clear the MAG devices',
      deleteEnigma2: 'Clear the Enigma2 devices',
      blockOwner: 'Block the owners',
      apply: 'Apply',
      extend: 'Extend',
      // The prolongation question for a selection, worded like the other bulk confirms.
      prolongTitle: 'Extend {{count}} subscriptions?',
      prolongBody:
        'Each subscription is extended by its own tariff plan\'s duration, keeping the time already left.',
      changeTariffTitle: 'Change the tariff plan',
      changeTariffPlaceholder: 'Choose a tariff plan',
      changeTariffHint:
        'Services the new plan does not deliver are cleared, and each clearance is reported.',
      changeCreatorTitle: 'Change the issuer',
      changeCreatorPlaceholder: 'Choose an administrator or a reseller',
      deleteTitle: 'Delete {{count}} subscriptions?',
      deleteBody: 'Every selected subscription and everything it carries will be removed.',
      deletePlaylistTitle: 'Clear the playlist of {{count}} subscriptions?',
      deletePlaylistBody: 'A subscription whose only service is its playlist is refused.',
      deleteMagTitle: 'Clear the MAG devices of {{count}} subscriptions?',
      deleteMagBody: 'A subscription whose only service is its MAG is refused.',
      deleteEnigma2Title: 'Clear the Enigma2 devices of {{count}} subscriptions?',
      deleteEnigma2Body: 'A subscription whose only service is its Enigma2 device is refused.',
      blockOwnerTitle: 'Block the owner of {{count}} subscriptions?',
      blockOwnerBody: 'Each account is blocked and its sessions ended. Your own is refused.',
      partialTitle: 'Changed {{done}} of {{total}}. Some rows were refused:',
    },
  },
  // The add and edit form for one subscription. Its own block because the table names what a row
  // is and the form names what an operator is being asked to type.
  subscriptionsForm: {
    idLabel: 'ID',
    ownerLabel: 'Owner',
    ownerRequired: 'Choose the owner.',
    planLabel: 'Tariff plan',
    planNone: 'No tariff plan',
    planUnavailable: 'The plans could not be read, so this picker offers nothing to pick.',
    expiryLabel: 'Expires',
    neverExpiresLabel: 'Never expires',
    expiryHint: "With a plan the expiry is its creation date plus the plan's length.",
    servicesSection: 'Services',
    servicesHint: 'A subscription has to deliver at least one service.',
    servicesRequired: 'Choose at least one service.',
    playlistIdLabel: 'Playlist id',
    playlistIdHint:
      '32 lower-case hexadecimal characters. Generate one, or paste an identifier from another system.',
    playlistIdInvalid: '32 lower-case hexadecimal characters.',
    generatePlaylistId: 'Generate',
    regeneratePlaylistId: 'Regenerate',
    magLabel: 'MAG MAC address',
    magNeedsPlanSupport: 'The selected plan does not deliver to MAG devices.',
    magInvalid: 'Six pairs of hexadecimal digits, with ":" or "-" between them.',
    enigma2Label: 'Enigma2 MAC address',
    enigma2NeedsPlanSupport: 'The selected plan does not deliver to Enigma2 devices.',
    enigma2Invalid: 'Six pairs of hexadecimal digits, with ":" or "-" between them.',
    formatsSection: 'Output formats',
    formatsRequired: 'Choose at least one format.',
    formatsHint: 'A subscription has to be delivered in at least one format.',
    // The two format boxes, named exactly as the server names them.
    formats: {
      mpegTs: 'MPEG-TS',
      hls: 'HLS',
    },
    limitsSection: 'Limits',
    maxConnectionsLabel: 'Maximum connections',
    maxConnectionsRequired: 'Enter the number of connections.',
    maxConnectionsHint: 'Between {{min}} and {{max}}. Leave it empty for unlimited.',
    unlimitedConnections: 'Unlimited',
    restreamLabel: 'Allow restreaming',
    restreamHint: 'Whether the subscriber may relay the subscription through their own servers.',
    stbLockLabel: 'Enforce the STB device id strictly',
    stbLockHint: 'Only a MAG device carries an STB identifier.',
    countryLockLabel: 'Pin to one country',
    countryLockHint: "Whether the subscription may only be watched from the owner's country.",
    ispLockLabel: 'Pin to one provider',
    ispLockHint: "Whether the subscription may only be watched from the owner's provider.",
    trialLabel: 'Trial subscription',
    trialHint: 'Whether the subscription is a trial rather than a paid one.',
    accessSection: 'Access',
    ipWhitelistLabel: 'Allowed IP addresses',
    ipWhitelistHint:
      'Up to {{max}} addresses or CIDR ranges, IPv4 or IPv6. Empty means no restriction.',
    ipWhitelistInvalid: 'Every entry must be an IP address or a CIDR range.',
    userAgentWhitelistLabel: 'Allowed user agents',
    userAgentWhitelistHint: 'Up to {{max}} entries of free text. Empty means no restriction.',
    userAgentWhitelistInvalid: 'Every entry must be between 1 and {{max}} characters.',
    contentSection: 'Bouquets',
    bouquetsLabel: 'Bouquets in the subscription',
    bouquetsPlaceholder: 'Choose the bouquets',
    bouquetsHint:
      "Used only while no tariff plan is attached. With a plan the plan's bouquets are delivered instead.",
    bouquetsRequired: 'Choose at least one bouquet.',
    bouquetsUnavailable:
      'The bouquets could not be read, so this picker offers nothing. The rest of the form still saves.',
    // The sentence that explains a disabled control while a plan is attached.
    planOwnedHint: 'The selected tariff plan decides this field.',
    selectAll: 'Select all',
    clear: 'Clear',
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
      registration_submitted: 'Check your inbox вЂ” we sent you a confirmation code.',
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
      // The row itself is not blocked - its group is - so `user_blocked` would name a state
      // the table does not show. The account cannot come back until the group does, and this
      // is the only place that says where the block actually lives.
      user_blocked_by_group: 'That account is blocked by the group it belongs to. Unblock the group instead.',
      // The group resource: the same three shapes as the accounts above (created / updated /
      // deleted), plus four refusals that only a group can produce. `immutable` and `in_use`
      // are facts about the installation rather than mistakes in the request, which is why
      // they are 409s with their own sentences instead of field errors.
      user_group_created: 'The group has been created.',
      user_group_updated: 'The group has been updated.',
      user_group_deleted: 'The group has been deleted.',
      user_group_not_found: 'There is no group with that identifier.',
      user_group_already_exists: 'That group name is already taken.',
      user_group_immutable:
        'The three groups this installation is built from may only be renamed. Create a group of your own for anything else.',
      user_group_in_use: 'Accounts still belong to this group. Move them somewhere else first.',
      // The content category resource: the same three shapes as the accounts and the
      // groups above, plus the reorder - one code for the whole-list write, because the
      // list as a whole is what the request names.
      content_category_created: 'The category has been created.',
      content_category_updated: 'The category has been updated.',
      content_category_deleted: 'The category has been deleted.',
      content_categories_reordered: 'The categories have been reordered.',
      content_category_not_found: 'There is no category with that identifier.',
      content_category_already_exists:
        'That category name is already taken within this content type.',
      // A category streams are still filed under. The server refuses with the count rather
      // than deleting anything, because the foreign key is RESTRICT and the rows are not
      // this dialog's to move - the sentence names what to do instead.
      content_category_in_use: '{{count}} streams still file under this category. Move them first.',
      // The bouquet resource: the same three shapes again, plus the reorder and the one
      // content write. Its name rule is stricter than the categories' - unique for the whole
      // installation, ignoring the case - and the sentence says so, because a form that broke
      // it shows the refusal under the input and names nothing else.
      bouquet_created: 'The bouquet has been created.',
      bouquet_updated: 'The bouquet has been renamed.',
      bouquet_deleted: 'The bouquet has been deleted.',
      bouquets_reordered: 'The bouquets have been reordered.',
      bouquet_content_set: 'The content of the bouquet has been saved.',
      bouquet_not_found: 'There is no bouquet with that identifier.',
      bouquet_already_exists: 'That bouquet name is already taken, in any spelling.',
      // The tariff plan resource: the same three shapes again, plus the two refusals a price
      // list shares with every other named resource. Its name rule matches the bouquets' - one
      // name per installation, ignoring the case - and the sentence says so.
      tariff_plan_created: 'The tariff plan has been created.',
      tariff_plan_updated: 'The tariff plan has been updated.',
      tariff_plan_deleted: 'The tariff plan has been deleted.',
      tariff_plan_not_found: 'There is no tariff plan with that identifier.',
      tariff_plan_already_exists: 'That tariff plan name is already taken, in any spelling.',
      // The subscription resource: the same three shapes the plans and the bouquets use, plus
      // the three warnings a plan change can report and the plan's own in-use refusal. A
      // duplicate names the field in `errors`, because the playlist id and the two MACs are
      // three different uniqueness rules on one row.
      subscription_created: 'The subscription has been created.',
      subscription_updated: 'The subscription has been updated.',
      subscription_deleted: 'The subscription has been deleted.',
      subscription_prolonged: 'The subscription has been prolonged.',
      subscription_not_found: 'There is no subscription with that identifier.',
      subscription_already_exists:
        'Another subscription already holds that playlist id or device address.',
      // A plan change that cleared a service the new plan cannot deliver. A warning, not a
      // refusal: the row was written and lost a setting, which is the consequence of the
      // operator's own choice of plan.
      subscription_playlist_cleared:
        'The new tariff plan does not deliver a playlist, so it was cleared.',
      subscription_mag_cleared:
        'The new tariff plan does not support MAG devices, so the MAG was cleared.',
      subscription_enigma2_cleared:
        'The new tariff plan does not support Enigma2 devices, so the Enigma2 device was cleared.',
      // A plan a subscription is still sold under. The server refuses with the count rather than
      // re-meaning the parameters that subscription derives from it.
      tariff_plan_in_use:
        'Subscriptions are still sold under this plan. Move them to another plan first.',
      // A bouquet that still holds content. The server refuses with the count rather than
      // unlinking anything, because the memberships are not this dialog's to empty.
      bouquet_not_empty: '{{count}} items are still in this bouquet. Empty it before deleting it.',
      // The stream resource: the same three shapes as the accounts, the groups and the
      // categories above, plus the two state transitions. A start and a stop are answers
      // about the row rather than about a process, and starting a stream that is already
      // running is a success rather than a complaint - so a double click is not a failure.
      video_stream_created: 'The stream has been created.',
      video_stream_updated: 'The stream has been updated.',
      video_stream_deleted: 'The stream has been deleted.',
      video_stream_started: 'The stream has been started.',
      video_stream_stopped: 'The stream has been stopped.',
      video_stream_restarted: 'The stream has been restarted.',
      video_stream_not_found: 'There is no stream with that identifier.',
      video_stream_already_exists:
        'That stream name is already taken within this category.',
      // The three warnings a bulk type change may attach to a row that nevertheless landed.
      // They are not failures: the row did what it was told and lost a setting its new kind
      // cannot carry, which the operator has to be told without being told it went wrong.
      video_stream_delay_cleared:
        'The delay was cleared because this stream is no longer permanent.',
      video_stream_archive_cleared:
        'The archive length was cleared because this stream is no longer permanent.',
      video_stream_restream_cleared:
        'The restream settings were cleared because a redirect stream cannot use them.',
      // A bulk run: one code for the run as a whole, because the run as a whole *was*
      // carried out - every identifier it named came back with a verdict, and those verdicts
      // are in `items` with the codes above rather than folded into one status. The counts
      // are interpolated so the sentence stays translated *and* says how much of it landed;
      // a row-by-row refusal opens the summary dialog instead of this toast.
      users_bulk_completed: 'Changed {{done}} of {{total}} accounts.',
      user_groups_bulk_completed: 'Changed {{done}} of {{total}} groups.',
      video_streams_bulk_completed: 'Changed {{done}} of {{total}} streams.',
      // A playlist import answers with counts rather than one row: the code names the run,
      // and the page reads the numbers out of the body beside it.
      video_streams_import_completed: 'The playlist has been imported.',
      video_stream_import_empty: 'The uploaded file is empty.',
      video_stream_import_hls:
        'An HLS playlist cannot be imported; upload an M3U playlist of streams.',
      video_stream_import_too_large: 'The uploaded file is too large.',
      video_stream_import_too_many_entries:
        'The playlist holds more entries than one import accepts.',
      video_stream_import_unreadable:
        'The uploaded file could not be read as an M3U playlist.',
      video_stream_import_conflict:
        'A name in the playlist was taken while the import was running. Nothing was imported; try again.',
      // The audio stream resource: the same shapes as the video one above, named apart
      // because they are a different resource behind a different permission - a group that
      // may change the video streams may not change the audio ones.
      audio_stream_created: 'The audio stream has been created.',
      audio_stream_updated: 'The audio stream has been updated.',
      audio_stream_deleted: 'The audio stream has been deleted.',
      audio_stream_started: 'The audio stream has been started.',
      audio_stream_stopped: 'The audio stream has been stopped.',
      audio_stream_restarted: 'The audio stream has been restarted.',
      audio_stream_not_found: 'There is no audio stream with that identifier.',
      audio_stream_already_exists:
        'That audio stream name is already taken within this category.',
      // An audio bulk run: one code for the run as a whole, for the reason the video one
      // above gives. There are no warning codes beside it: both audio kinds carry the same
      // payload, so a change of kind never costs a row a setting.
      audio_streams_bulk_completed: 'Changed {{done}} of {{total}} audio streams.',
      // An audio playlist import answers with counts rather than one row, exactly as the
      // video one does.
      audio_streams_import_completed: 'The playlist has been imported.',
      audio_stream_import_empty: 'The uploaded file is empty.',
      audio_stream_import_hls:
        'An HLS playlist cannot be imported; upload an M3U playlist of streams.',
      audio_stream_import_too_large: 'The uploaded file is too large.',
      audio_stream_import_too_many_entries:
        'The playlist holds more entries than one import accepts.',
      audio_stream_import_unreadable:
        'The uploaded file could not be read as an M3U playlist.',
      audio_stream_import_conflict:
        'A name in the playlist was taken while the import was running. Nothing was imported; try again.',
      // The on-demand resource: three shapes like the stream resources, plus the two
      // delivery refusals only its play endpoint can produce. `not_playable` is a source that
      // is neither an absolute URL nor a file the server can read; `unavailable` is a relay
      // that could not reach its origin.
      video_on_demand_created: 'The video has been created.',
      video_on_demand_updated: 'The video has been updated.',
      video_on_demand_deleted: 'The video has been deleted.',
      video_on_demand_not_found: 'There is no video with that identifier.',
      video_on_demand_already_exists:
        'That video name is already taken within this category.',
      video_on_demand_not_playable:
        'The source is neither an absolute URL nor a file this server can read.',
      video_on_demand_unavailable: 'The source could not be reached.',
      video_on_demand_bulk_completed: 'Changed {{done}} of {{total}} videos.',
      series_created: 'The series has been created.',
      series_updated: 'The series has been updated.',
      series_deleted: 'The series has been deleted.',
      series_not_found: 'There is no series with that identifier.',
      series_already_exists:
        'That series name is already taken within this category.',
      series_episode_created: 'The episode has been created.',
      series_episode_updated: 'The episode has been updated.',
      series_episode_deleted: 'The episode has been deleted.',
      series_episode_not_found:
        'There is no episode with that identifier in this series.',
      series_episode_already_exists:
        'Another episode of this series already has that season and number.',
      series_season_created: 'The season has been created.',
      series_season_already_exists: 'This season already has episodes.',
      series_bulk_completed: 'Changed {{done}} of {{total}} series.',
      series_episodes_bulk_completed: 'Changed {{done}} of {{total}} episodes.',
      // The guide source resource: three shapes like the catalogues above, plus the refusal
      // only a source a stream depends on can produce. The count is in the server's sentence
      // rather than in this one, because the delete dialog is not the place a number travels.
      epg_source_created: 'The guide source has been created.',
      epg_source_updated: 'The guide source has been updated.',
      epg_source_deleted: 'The guide source has been deleted.',
      epg_source_not_found: 'There is no guide source with that identifier.',
      epg_source_already_exists: 'That guide source name is already taken.',
      epg_source_in_use:
        'Streams still read their guide from this source. Clear the programme guide on those streams first.',
      video_on_demand_import_completed: 'The playlist has been imported.',
      video_on_demand_import_empty: 'The uploaded file is empty.',
      video_on_demand_import_hls:
        'An HLS playlist cannot be imported; upload an M3U playlist of videos.',
      video_on_demand_import_too_large: 'The uploaded file is too large.',
      video_on_demand_import_too_many_entries:
        'The playlist holds more entries than one import accepts.',
      video_on_demand_import_unreadable:
        'The uploaded file could not be read as an M3U playlist.',
      video_on_demand_import_conflict:
        'A name in the playlist was taken while the import was running. Nothing was imported; try again.',
      // No encoder is configured in this pass, so a transcode request is refused with a
      // sentence that names the fact rather than the request.
      transcode_not_configured:
        'This server has no transcoder configured, so the video or episode cannot be converted yet.',
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
      // Signed in, right area, wrong grant. Deliberately not `auth_role_changed`: signing in
      // again would produce the same refusal, so the sentence must not send anybody to do it.
      permission_denied: 'Your account does not have permission for this.',
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