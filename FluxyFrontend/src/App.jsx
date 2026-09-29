import { App as AntApp, ConfigProvider, theme } from 'antd'
import ruRU from 'antd/locale/ru_RU'
import enUS from 'antd/locale/en_US'
import { RouterProvider } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import './i18n'
import { usePrefersDark } from './hooks/usePrefersDark'
import { router } from './router'

export default function App() {
  const dark = usePrefersDark()
  const { i18n } = useTranslation()
  const antdLocale = i18n.language.startsWith('ru') ? ruRU : enUS

  return (
    <ConfigProvider
      locale={antdLocale}
      theme={{
        algorithm: dark ? theme.darkAlgorithm : theme.defaultAlgorithm,
        token: { colorPrimary: '#1677ff' },
      }}
    >
      <AntApp>
        <RouterProvider router={router} />
      </AntApp>
    </ConfigProvider>
  )
}
