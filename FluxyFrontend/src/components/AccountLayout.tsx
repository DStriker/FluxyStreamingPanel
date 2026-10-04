import { useState } from 'react'
import { Layout } from 'antd'
import { Outlet } from 'react-router-dom'
import AccountHeader from './AccountHeader'
import AccountSidebar from './AccountSidebar'
import { useIsDark } from '../lib/theme'

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
 * only what is inside the outlet, so the header, the sidebar and its scroll position and
 * open categories all survive navigation. A layout re-mounted per page would reset all
 * three, and the reset would be visible as a flash of collapsed menu on every click.
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
 * on every load would be the shell forgetting what the visitor told it. The responsive
 * breakpoint still collapses it on a narrow window, deliberately without writing that to
 * storage: it is a fact about the viewport, and storing it would leave somebody who once
 * resized the window with a sidebar they cannot get back.
 */
/** The locale key of the area's heading - `titles.clientArea`, `titles.adminArea`. */
export default function AccountLayout({ titleKey }: { titleKey: string }) {
  const dark = useIsDark()
  const [collapsed, setCollapsed] = useState(readCollapsed)

  const toggle = () => {
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
        <AccountHeader titleKey={titleKey} collapsed={collapsed} onToggle={toggle} />
      </Layout.Header>

      <Layout hasSider className="layout-shell__body">
        <Layout.Sider
          className="layout-shell__sider"
          theme={dark ? 'dark' : 'light'}
          width={240}
          collapsedWidth={76}
          collapsible
          trigger={null}
          collapsed={collapsed}
          breakpoint="lg"
          onBreakpoint={setCollapsed}
        >
          <AccountSidebar />
        </Layout.Sider>

        <Layout.Content className="layout-shell__content">
          <Outlet />
        </Layout.Content>
      </Layout>
    </Layout>
  )
}
