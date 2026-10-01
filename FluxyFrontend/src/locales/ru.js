export default {
  fields: {
    username: 'Имя пользователя',
    email: 'Email',
    password: 'Пароль',
    confirmPassword: 'Подтверждение пароля',
    code: 'Код подтверждения',
    usernamePlaceholder: 'user',
    emailPlaceholder: 'user@example.com',
  },
  validation: {
    usernameRequired: 'Введите имя пользователя',
    usernameLength: 'От 5 до 20 символов',
    emailRequired: 'Введите email',
    emailInvalid: 'Некорректный email',
    passwordRequired: 'Введите пароль',
    passwordLength: 'От 8 до 100 символов',
    passwordComplexity: 'Нужна строчная буква, заглавная буква и цифра',
    confirmRequired: 'Подтвердите пароль',
    passwordMismatch: 'Пароли не совпадают',
    codeRequired: 'Введите код из письма',
    codeLength: 'Код состоит из 6 цифр',
  },
  titles: {
    register: 'Регистрация',
    confirmRegistration: 'Подтверждение регистрации',
    clientLogin: 'Вход для клиента',
    resellerLogin: 'Вход для реселлера',
    adminLogin: 'Вход для администратора',
  },
  actions: {
    login: 'Войти',
    register: 'Зарегистрироваться',
    confirm: 'Подтвердить',
    back: 'Другой аккаунт',
    haveAccount: 'Уже есть аккаунт? Войти',
    noAccount: 'Нет аккаунта? Зарегистрироваться',
  },
  messages: {
    // Показывается формами входа, у которых пока нет кода от сервера для перевода.
    sent: 'Форма отправлена',
    sendFailed: 'Не удалось отправить форму',
    // Все коды, которые может вернуть API. Ключи - это значения поля `code` сервера,
    // поэтому новый код на бэкенде до добавления сюда откатится на его английский
    // `message`, а не на голый ключ i18n.
    api: {
      registration_submitted: 'Проверьте почту — мы отправили код подтверждения.',
      registration_confirmed: 'Регистрация подтверждена. Можно входить.',
      registration_not_configured: 'Регистрация сейчас недоступна на этом сервере.',
      validation_failed: 'Проверьте выделенные поля.',
      user_already_exists: 'Это имя пользователя или email уже заняты.',
      invalid_code: 'Код подтверждения неверный.',
      code_expired: 'Срок действия кода истёк. Зарегистрируйтесь заново, чтобы получить новый.',
      captcha_invalid: 'Не удалось подтвердить, что запрос отправлен человеком. Попробуйте ещё раз.',
      csrf_invalid: 'Сессия истекла. Попробуйте ещё раз.',
      registration_rate_limited: 'Слишком много регистраций с вашего адреса. Попробуйте позже.',
      confirmation_rate_limited: 'Слишком много попыток. Попробуйте позже.',
      email_delivery_failed: 'Не удалось отправить письмо с подтверждением. Попробуйте позже.',
      // Сервер их не присылает - оба означают, что запрос до него не дошёл.
      network_error: 'Сервер недоступен. Проверьте, что бэкенд запущен.',
      server_error: 'Ошибка сервера ({{status}}).',
    },
  },
}