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
    currentPasswordRequired: 'Enter your current password',
  },
  titles: {
    register: 'Registration',
    confirmRegistration: 'Confirm your registration',
    forgotPassword: 'Password reset',
    confirmPasswordReset: 'Confirm the password reset',
    profile: 'Profile',
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
    cancel: 'Cancel',
  },
  nav: {
    // Categories are the sidebar's submenus; items are the routes beneath them. Both are
    // rendered from `src/lib/navigation.ts`, which decides where each item points - so a
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