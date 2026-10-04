import { Segmented } from 'antd'
import { useTranslation } from 'react-i18next'

const OPTIONS = [
  { label: 'EN', value: 'en' },
  { label: 'RU', value: 'ru' },
]

export default function LanguageSwitcher() {
  const { i18n } = useTranslation()
  const value = i18n.language.startsWith('ru') ? 'ru' : 'en'

  return (
    <Segmented
      className="lang-switcher"
      size="small"
      value={value}
      onChange={(next) => i18n.changeLanguage(next)}
      options={OPTIONS}
    />
  )
}
