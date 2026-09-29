export default {
  fields: {
    username: 'Username',
    email: 'Email',
    password: 'Password',
    confirmPassword: 'Password confirmation',
    usernamePlaceholder: 'user',
    emailPlaceholder: 'user@example.com',
  },
  validation: {
    usernameRequired: 'Enter your username',
    usernameMin: 'At least 3 characters',
    emailRequired: 'Enter your email',
    emailInvalid: 'Invalid email address',
    passwordRequired: 'Enter your password',
    passwordMin: 'At least 6 characters',
    confirmRequired: 'Confirm your password',
    passwordMismatch: 'Passwords do not match',
  },
  titles: {
    register: 'Registration',
    clientLogin: 'Client area',
    resellerLogin: 'Reseller panel',
    adminLogin: 'Admin panel',
  },
  actions: {
    login: 'Sign in',
    register: 'Sign up',
    haveAccount: 'Already have an account? Sign in',
    noAccount: "Don't have an account? Sign up",
  },
  messages: {
    sent: 'Form submitted',
    sendFailed: 'Failed to submit the form',
    serverUnavailable: 'Server unavailable: start the backend or configure the Vite proxy',
    serverError: 'Server error ({{status}})',
  },
}
