import { createElement } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { Menu } from 'antd'
import { useTranslation } from 'react-i18next'
import { NAVIGATION } from '../lib/navigation'
import { areaForRole, homeForRole } from '../lib/session'
import { useSession } from '../lib/sessionContext'
import { useIsDark } from '../lib/theme'

/**
 * The left rail: the role's own menu, grouped into categories, scrolling on its own.
 *
 * The menu keys are the **absolute paths** rather than opaque ids, which is what removes
 * two bookkeeping tables at once: `selectedKeys` is read straight off `location.pathname`
 * with no lookup, and a click navigates to `key` with no mapping. The base is
 * `areaForRole(session.role)` - the very prefix the router hangs this role's sections from
 * - so the menu can never disagree with the routes it points at. The one item with no path
 * of its own is the home, and it takes `homeForRole` instead: the landing path ends in a
 * page and only that function knows which one.
 *
 * Scroll lives on a wrapper inside the `Sider`, not on the `Sider` itself. `Sider` sets its
 * own overflow and wins the cascade depending on where antd injects its style, whereas a
 * child element has no opinion of its own: it is `height: 100%` of a container that is
 * already sized, so anything taller scrolls and nothing else in the frame moves.
 *
 * `theme` is set explicitly rather than left at antd's default of `light`, because the two
 * variants are two palettes and not two shades of one. In the dark theme the light variant
 * is what paints a panel behind every group - `subMenuItemBg` under the dark algorithm is
 * `rgba(255,255,255,0.04)` - and leaves a muted `#15325b` under the selected row; the dark
 * variant instead gives labels at 65% white, a white hover and a solid primary behind the
 * selection. The panel it would paint in its own place (`darkSubMenuItemBg` is `#000c17`)
 * is the part `appTheme` takes back by setting that token to transparent: the variant
 * picks the semantics, the tokens pick the surfaces. `Sider` does not pass its `theme`
 * down, so this half is written here as well as on the rail in `AccountLayout` - one
 * declaration each, both from `useIsDark`.
 */
export default function AccountSidebar() {
  const { t } = useTranslation()
  const dark = useIsDark()
  const session = useSession()
  const navigate = useNavigate()
  const location = useLocation()

  // Same rule as the header: only `RequireAuth` renders the rail, and a `null` session here
  // would be that guard being bypassed. Drawing a menu would be worse than drawing none -
  // every item of it points at a section the visitor has no session to open.
  if (!session) return null

  const area = areaForRole(session.role)
  const home = homeForRole(session.role)
  const absolute = (path: string) => (path ? `${area}/${path}` : home)

  // A trailing slash still matches the route - `matchPath` is not strict about one - but
  // it would not match the menu key built above, and the effect would be a section that
  // renders with nothing selected. The index route needs the `/` case kept intact, hence
  // stripping only the tail and never a lone slash.
  const current = location.pathname.replace(/\/+$/, '') || '/'

  const groups = NAVIGATION[session.role] ?? []

  const items = groups.map((group) => ({
    key: group.key,
    icon: createElement(group.icon),
    label: t(group.labelKey),
    children: group.items.map((item) => ({
      key: absolute(item.path),
      icon: createElement(item.icon),
      label: t(item.labelKey),
    })),
  }))

  return (
    <div className="layout-shell__sider-scroll">
      <Menu
        mode="inline"
        theme={dark ? 'dark' : 'light'}
        items={items}
        selectedKeys={[current]}
        defaultOpenKeys={groups.map((group) => group.key)}
        onClick={({ key }) => navigate(key)}
        // rc-menu writes `padding-left` as `depth * inlineIndent`, counting depth from 1:
        // the category title lands on `inlineIndent`, every page inside it on twice that.
        // The default 24px therefore puts a page's icon at 48px - a fifth of this rail -
        // while the category's own sits at 24px, which is far enough apart for the two to
        // stop reading as one hierarchy. 16px puts the category on antd's own 16px item
        // inset and its pages at 32px: still stepped, nothing flush with the edge. The
        // gaps *between* items are design tokens and live in `appTheme`, not on this prop.
        inlineIndent={16}
        style={{ borderInlineEnd: 'none' }}
      />
    </div>
  )
}
