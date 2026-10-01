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
  },
  actions: {
    login: 'Sign in',
    register: 'Sign up',
    confirm: 'Confirm',
    back: 'Use a different account',
    haveAccount: 'Already have an account? Sign in',
    noAccount: "Don't have an account? Sign up",
  },
  messages: {
    // Shown by the login forms, whose endpoints have no server code to translate yet.
    sent: 'Form submitted',
    sendFailed: 'Failed to submit the form',
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
    },
  },
}