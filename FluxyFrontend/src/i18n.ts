import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import ru from './locales/ru'
import en from './locales/en'

const STORAGE_KEY = 'fluxy-lang'
const SUPPORTED = ['en', 'ru']

/**
 * The language to start with: what the visitor chose last, then what the browser reports,
 * then English.
 *
 * `saved` and `browser` are both checked against `SUPPORTED` rather than trusted, because
 * `localStorage` holds whatever was put there and `navigator.language` can be `de-DE`,
 * which is two letters short of being one of the two keys this build has. The narrowing
 * also keeps the return type a plain `string` - the `includes` calls below are what decide
 * it.
 */
const detectLanguage = (): string => {
  try {
    const saved = localStorage.getItem(STORAGE_KEY)
    if (saved !== null && SUPPORTED.includes(saved)) return saved
  } catch {
    // localStorage isn't available
  }
  const browser = typeof navigator !== 'undefined' ? navigator.language?.slice(0, 2) : null
  return browser !== null && browser !== undefined && SUPPORTED.includes(browser)
    ? browser
    : 'en'
}

i18n.use(initReactI18next).init({
  resources: {
    en: { translation: en },
    ru: { translation: ru },
  },
  lng: detectLanguage(),
  fallbackLng: 'en',
  interpolation: { escapeValue: false },
  react: { useSuspense: false },
})

i18n.on('languageChanged', (lng) => {
  try {
    localStorage.setItem(STORAGE_KEY, lng)
  } catch {
    // localStorage isn't available
  }
})

export default i18n
