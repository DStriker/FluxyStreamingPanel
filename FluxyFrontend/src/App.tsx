import { Suspense, useEffect, useMemo } from 'react'
import { App as AntApp, ConfigProvider, Spin, Typography } from 'antd'
import ruRU from 'antd/locale/ru_RU'
import enUS from 'antd/locale/en_US'
import { RouterProvider } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import './i18n'
import { appTheme, useIsDark } from './lib/theme'
import { router } from './router'

export default function App() {
  const dark = useIsDark()
  const { t, i18n } = useTranslation()
  const antdLocale = i18n.language.startsWith('ru') ? ruRU : enUS
  // One object per theme, not per render: `derived` below is an identity the effect can
  // depend on without re-firing on every keystroke in an input somewhere.
  const { config, derived } = useMemo(() => appTheme(dark), [dark])

  // The page itself is outside `ConfigProvider`, so antd cannot paint it: the auth pages
  // and the area behind the shell both sit on `body`, which the stylesheet gives a fixed
  // hex. Reading the token out of `appTheme` - the same call `ConfigProvider` makes with
  // the same config - is what stops the two from drifting the moment the visitor picks a
  // theme the OS does not agree with. Guessing a "dark background" here instead would
  // eventually disagree with the algorithm a millimetre away.
  //
  // `color-scheme` is set for the same reason it is worth doing at all: it tells the
  // browser which flavour of native widget to draw - scrollbars, form controls, the
  // overscroll area - and leaving it on `light dark` would keep those following the OS
  // while everything else followed the button.
  useEffect(() => {
    document.body.style.backgroundColor = derived.colorBgLayout
    document.body.style.color = derived.colorText
    document.documentElement.style.colorScheme = dark ? 'dark' : 'light'
    document.documentElement.dataset.theme = dark ? 'dark' : 'light'
  }, [dark, derived])

  return (
    <ConfigProvider locale={antdLocale} theme={config}>
      <AntApp>
        {/* The pages are route-level chunks (`src/router/routes.tsx`), so the first visit
            to any of them arrives a moment after the navigation does. This is the boundary
            those imports resolve into, placed above the router rather than inside a route
            because the guard, the layout and every page are one subtree that suspends. It
            sits inside `ConfigProvider` and `AntApp`, so the spinner is themed and the
            locale it would announce is already chosen - which is why the label below is a
            real sentence: a `role="status"` region with nothing but a spinner in it
            announces nothing to the one visitor it exists for. */}
        <Suspense
          fallback={
            <div className="route-fallback" role="status">
              <Spin />
              <Typography.Text type="secondary">{t('common.loading')}</Typography.Text>
            </div>
          }
        >
          <RouterProvider router={router} />
        </Suspense>
      </AntApp>
    </ConfigProvider>
  )
}
