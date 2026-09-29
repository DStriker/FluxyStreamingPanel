import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import ru from './locales/ru'
import en from './locales/en'

const STORAGE_KEY = 'fluxy-lang'
const SUPPORTED = ['en', 'ru']

const detectLanguage = () => {
  try {
    const saved = localStorage.getItem(STORAGE_KEY)
    if (SUPPORTED.includes(saved)) return saved
  } catch {
    // localStorage isn't available
  }
  const browser = typeof navigator !== 'undefined' ? navigator.language?.slice(0, 2) : null
  return SUPPORTED.includes(browser) ? browser : 'en'
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
