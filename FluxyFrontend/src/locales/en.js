export default {
  fields: {
    username: 'Username',
    email: 'Email',
    password: 'Password',
    confirmPassword: 'Password confirmation',
    code: 'Confirmation code',
    usernamePlaceholder: 'user',
    emailPlaceholder: 'user@example.com',
  },
  validation: {
    usernameRequired: 'Enter your username',
    usernameLength: 'Between 5 and 20 characters',
    emailRequired: 'Enter your email',
    emailInvalid: 'Invalid email address',
    passwordRequired: 'Enter your password',
    passwordLength: 'Between 8 and 100 characters',
    passwordComplexity: 'Needs a lowercase letter, an uppercase letter and a digit',
    confirmRequired: 'Confirm your password',
    passwordMismatch: 'Passwords do not match',
    codeRequired: 'Enter the code from the email',
    codeLength: 'The code is 6 digits',
  },
  titles: {
    register: 'Registration',
    confirmRegistration: 'Confirm your registration',
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
  },
  nav: {
    // Categories are the sidebar's submenus; items are the routes beneath them. Both are
    // rendered from `src/lib/navigation.js`, which decides where each item points - so a
    // label added here and nothing else shows up nowhere.
    groups: {
      overview: 'Overview',
      management: 'Management',
      account: 'Account',
    },
    items: {
      dashboard: 'Dashboard',
      orders: 'Orders',
      clients: 'Clients',
      users: 'Users',
      settings: 'Settings',
      profile: 'Profile',
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
  messages: {
    // Shown by the login forms, whose endpoints have no server code to translate yet.
    sent: 'Form submitted',
    sendFailed: 'Failed to submit the form',
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
      code_expired: 'The code has expired. Register again to get a new one.',
      captcha_invalid: 'Could not verify that a person sent this. Please try again.',
      csrf_invalid: 'Your session expired. Please try again.',
      registration_rate_limited: 'Too many registrations from your address. Please try again later.',
      confirmation_rate_limited: 'Too many attempts. Please try again later.',
      email_delivery_failed: 'The confirmation email could not be sent. Please try again later.',
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