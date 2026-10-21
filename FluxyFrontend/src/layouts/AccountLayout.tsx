import { useEffect, useState } from 'react'
import { Drawer, Layout } from 'antd'
import { useTranslation } from 'react-i18next'
import { Outlet, useLocation } from 'react-router-dom'
import AccountHeader from './AccountHeader'
import AccountSidebar from './AccountSidebar'
import { useIsCompact } from '../hooks/useIsCompact'
import { useIsDark } from '../utils/theme'

const SIDEBAR_KEY = 'fluxy-sidebar-collapsed'

const readCollapsed = (): boolean => {
  try {
    return localStorage.getItem(SIDEBAR_KEY) === '1'
  } catch {
    return false
  }
}

const writeCollapsed = (value: boolean): void => {
  try {
    localStorage.setItem(SIDEBAR_KEY, value ? '1' : '0')
  } catch {
    // The collapse still applies; only the memory of it is lost.
  }
}

/**
 * The frame every signed-in page sits in: a pinned header, a pinned and independently
 * scrolling sidebar, and a workspace that scrolls on its own.
 *
 * It renders `<Outlet/>` rather than children, which is the point of nesting the three
 * areas under it in the router: moving between two sections of the same area replaces
 * only what is inside the outlet, so the header, the sidebar's scroll position and its
 * collapsed state survive navigation, while the one open category follows the page rather
 * than being reset by a remount. A layout re-mounted per page would reset all of them, and
 * the reset would be visible as a flash of collapsed menu on every click.
 *
 * The backgrounds are antd's, not ours. `.ant-layout` paints itself from `Layout.bodyBg`,
 * the bar from `Layout.headerBg` and the rail from `Layout.siderBg` in the dark variant
 * (`lightSiderBg` in the light one) - and two of those three would be wrong unaided,
 * because antd hardcodes `headerBg` and `siderBg` to the v3 navy `#001529` and the
 * algorithm never touches either. `appTheme` gives both of them `colorBgContainer`, which
 * is what keeps the bar and the rail one surface in both themes; here that leaves nothing
 * to write, which is the point - a background declared in two places is a background that
 * can be changed in only one of them.
 *
 * The rail and the menu each declare their own theme, and they always declare the same
 * one. `Sider` does not hand its `theme` to the `Menu` inside it: the two props are
 * independent and each picks its own palette, so a dark menu under a light rail is not a
 * mismatch antd would smooth over, it is simply what you get for setting only half. Both
 * take the variant from `useIsDark` - the store the header's switch drives - so neither
 * can end up answering a different theme than the algorithm above them.
 *
 * The variant is worth declaring even though `appTheme` overrides `siderBg` regardless:
 * `theme` decides where the paint comes from, and the menu's dark variant is what selects
 * its dark *semantics* - labels at 65% white, a solid primary behind the selected row, a
 * white hover. The navy antd attaches to that variant is overridden for the same reason
 * `headerBg` is: it ignores the algorithm, and `#001529` beside `#141414` would be two
 * unrelated darks standing in one frame.
 *
 * The sidebar's collapsed state is remembered per browser rather than per visit, because
 * it is a preference about the shape of the screen and not about a page - re-expanding it
 * on every load would be the shell forgetting what the visitor told it. That memory belongs
 * to the desktop rail alone. Below `lg` the rail is not drawn at all: the menu becomes a
 * `Drawer`, which is a layer over the page rather than a column taking 76 pixels from it,
 * and its open/closed state is a fact about the current visit rather than a preference worth
 * storing.
 *
 * The compact decision is made in JavaScript rather than left to `Sider`'s own `breakpoint`:
 * one hook answers the question for the whole shell, so the rail and the drawer can never
 * both be mounted, and the content width is full on the very first mobile paint.
 */
/** The locale key of the area's heading - `titles.clientArea`, `titles.adminArea`. */
export default function AccountLayout({ titleKey }: { titleKey: string }) {
  const dark = useIsDark()
  const compact = useIsCompact()
  const location = useLocation()
  const { t } = useTranslation()
  const [collapsed, setCollapsed] = useState(readCollapsed)
  const [drawerOpen, setDrawerOpen] = useState(false)

  // A drawer is a layer over a page, not a part of its history: every move to another
  // address closes it, including back/forward and a navigation a page performs itself. The
  // menu's own `onNavigate` covers the one move this effect cannot see - choosing the page
  // that is already open, which leaves the address unchanged.
  useEffect(() => {
    setDrawerOpen(false)
  }, [compact, location.pathname])

  const toggle = () => {
    if (compact) {
      setDrawerOpen((open) => !open)
      return
    }

    // Written outside the updater on purpose: a state updater has to be pure, and React
    // may call it twice in development to check that it is. The value is computed from
    // what is on screen now, which is what a click always means.
    const next = !collapsed
    setCollapsed(next)
    writeCollapsed(next)
  }

  return (
    <Layout className="layout-shell">
      <Layout.Header className="layout-shell__header">
        <AccountHeader
          titleKey={titleKey}
          collapsed={collapsed}
          compact={compact}
          menuOpen={drawerOpen}
          onToggle={toggle}
        />
      </Layout.Header>

      <Layout hasSider={!compact} className="layout-shell__body">
        {compact ? (
          <Drawer
            rootClassName="layout-shell__drawer"
            placement="left"
            width="min(84vw, 320px)"
            title={t(titleKey)}
            open={drawerOpen}
            onClose={() => setDrawerOpen(false)}
            styles={{ body: { padding: 0 } }}
          >
            <AccountSidebar collapsed={false} onNavigate={() => setDrawerOpen(false)} />
          </Drawer>
        ) : (
          <Layout.Sider
            className="layout-shell__sider"
            theme={dark ? 'dark' : 'light'}
            width={240}
            collapsedWidth={76}
            collapsible
            trigger={null}
            collapsed={collapsed}
          >
            <AccountSidebar collapsed={collapsed} />
          </Layout.Sider>
        )}

        <Layout.Content className="layout-shell__content">
          <Outlet />
        </Layout.Content>
      </Layout>
    </Layout>
  )
}
