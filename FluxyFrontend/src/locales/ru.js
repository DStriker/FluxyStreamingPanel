export default {
  fields: {
    username: 'Имя пользователя',
    email: 'Email',
    password: 'Пароль',
    confirmPassword: 'Подтверждение пароля',
    usernamePlaceholder: 'user',
    emailPlaceholder: 'user@example.com',
  },
  validation: {
    usernameRequired: 'Введите имя пользователя',
    usernameMin: 'Минимум 3 символа',
    emailRequired: 'Введите email',
    emailInvalid: 'Некорректный email',
    passwordRequired: 'Введите пароль',
    passwordMin: 'Минимум 6 символов',
    confirmRequired: 'Подтвердите пароль',
    passwordMismatch: 'Пароли не совпадают',
  },
  titles: {
    register: 'Регистрация',
    clientLogin: 'Вход для клиента',
    resellerLogin: 'Вход для реселлера',
    adminLogin: 'Вход для администратора',
  },
  actions: {
    login: 'Войти',
    register: 'Зарегистрироваться',
    haveAccount: 'Уже есть аккаунт? Войти',
    noAccount: 'Нет аккаунта? Зарегистрироваться',
  },
  messages: {
    sent: 'Форма отправлена',
    sendFailed: 'Не удалось отправить форму',
    serverUnavailable: 'Сервер недоступен: запустите backend или настройте прокси Vite',
    serverError: 'Ошибка сервера ({{status}})',
  },
}
