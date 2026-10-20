import { createElement, useEffect, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { Menu, theme, type MenuProps } from 'antd'
import { useTranslation } from 'react-i18next'
import { groupKeyForPath, menuPath, NAVIGATION } from '../utils/navigation'
import { useSession } from '../utils/sessionContext'
import { useIsDark } from '../utils/theme'

/**
 * The left rail: the role's own menu, grouped into categories, scrolling on its own.
 *
 * The menu keys are the **absolute paths** rather than opaque ids, which is what removes
 * two bookkeeping tables at once: `selectedKeys` is read straight off `location.pathname`
 * with no lookup, and a click navigates to `key` with no mapping. The base is the role's
 * area - the very prefix the router hangs this role's sections from - and the one item with
 * no path of its own is the landing page `homeForRole` names; `menuPath` builds either key,
 * so the rail and the rule deciding which category is open cannot disagree about an
 * address.
 *
 * The categories open **one at a time, and the one open follows the page**. `openKeys` is
 * therefore controlled: `defaultOpenKeys` opened every category at mount and then left the
 * rail to antd, so a reload always showed the whole menu expanded. The initial value is
 * the category `groupKeyForPath` names for the current address, and the effect below
 * reopens it whenever the address moves to another category or the rail comes back from
 * collapsed - rc-menu clears controlled open keys while an inline menu is collapsed, so
 * without that half the current category would be remembered as closed on every expand.
 * `onOpenChange` receives the complete set of open keys and keeps only the one that was
 * not open before: opening a second category closes the first, while closing the open
 * category leaves the rail with none. Opening a category is a reading choice rather than
 * navigation - it changes neither the address nor the selection.
 *
 * Collapsed, the rail shows only the category's icon, so antd opens its pages in a popup.
 * That popup carries the category's name as a styled heading (`popupHeading`), because the
 * submenu title that names it in the expanded rail is not rendered at icon width. A
 * category with no pages has no popup to head, so it becomes a disabled item whose tooltip
 * is that same name.
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
export default function AccountSidebar({ collapsed }: { collapsed: boolean }) {
  const { t } = useTranslation()
  const dark = useIsDark()
  const { token } = theme.useToken()
  const session = useSession()
  const navigate = useNavigate()
  const location = useLocation()

  // A trailing slash still matches the route - `matchPath` is not strict about one - but
  // it would not match the menu key built by `menuPath`, and the effect would be a section
  // that renders with nothing selected. The index route needs the `/` case kept intact,
  // hence stripping only the tail and never a lone slash.
  const current = location.pathname.replace(/\/+$/, '') || '/'

  const activeGroup = session ? groupKeyForPath(session.role, current) : null

  const [openKeys, setOpenKeys] = useState<string[]>(() => (activeGroup ? [activeGroup] : []))

  useEffect(() => {
    setOpenKeys(collapsed || !activeGroup ? [] : [activeGroup])
  }, [collapsed, activeGroup])

  // Same rule as the header: only `RequireAuth` renders the rail, and a `null` session here
  // would be that guard being bypassed. Drawing a menu would be worse than drawing none -
  // every item of it points at a section the visitor has no session to open.
  if (!session) return null

  const groups = NAVIGATION[session.role] ?? []

  // The collapsed rail hides a submenu's title, so this heading is the only place the
  // category is still named. It is deliberately louder than antd's group title: full text
  // contrast, a strong weight and a primary marker, over a filled strip that spans the
  // popup. The negative margin cancels the group title's own padding (`paddingXS` /
  // `padding`) and the padding puts the text back where antd drew it, which is what makes
  // the strip reach both edges instead of floating as an inset chip.
  const popupHeading = (text: string) => (
    <span
      style={{
        display: 'block',
        margin: `-${token.paddingXS}px -${token.padding}px`,
        padding: `${token.paddingXS}px ${token.padding}px`,
        background: token.colorFillTertiary,
        borderBottom: `${token.lineWidth}px ${token.lineType} ${token.colorBorderSecondary}`,
      }}
    >
      <span
        style={{
          display: 'inline-flex',
          alignItems: 'center',
          gap: token.marginXS,
          color: token.colorText,
          fontWeight: token.fontWeightStrong,
        }}
      >
        <span
          aria-hidden
          style={{
            width: token.lineWidthBold,
            height: token.fontSize,
            borderRadius: token.borderRadiusSM,
            background: token.colorPrimary,
          }}
        />
        {text}
      </span>
    </span>
  )

  // The complete set arrives on every toggle, and rc-menu appends the category that just
  // opened. Keeping only that key is the accordion; an empty set is a category being
  // closed, which leaves the rail with nothing open.
  const handleOpenChange = (keys: string[]) => {
    const opened = keys.find((key) => !openKeys.includes(key))
    setOpenKeys(opened ? [opened] : [])
  }

  const items: MenuProps['items'] = groups.map((group) => {
    const label = t(group.labelKey)
    const children = group.items.map((item) => ({
      key: menuPath(session.role, item.path),
      icon: createElement(item.icon),
      label: t(item.labelKey),
    }))

    // A category with no pages has nothing to open, so it is a plain item: the icon stays
    // in the collapsed rail and `title` is what antd turns into its tooltip there. It is
    // disabled because there is nowhere to navigate, not because the category is forbidden.
    if (children.length === 0) {
      return {
        key: group.key,
        icon: createElement(group.icon),
        label,
        title: label,
        disabled: true,
      }
    }

    return {
      key: group.key,
      icon: createElement(group.icon),
      label,
      // The collapsed rail hides a submenu's own label and opens its children in a popup
      // that would otherwise have no heading at all. A group titled with the category's
      // name gives the popup that heading (styled by `popupHeading`). Expanded, the
      // submenu title is already on screen, so the children stay flat and the heading is
      // not drawn twice.
      children: collapsed
        ? [
            {
              type: 'group' as const,
              key: `${group.key}-title`,
              label: popupHeading(label),
              children,
            },
          ]
        : children,
    }
  })

  return (
    <div className="layout-shell__sider-scroll">
      <Menu
        mode="inline"
        theme={dark ? 'dark' : 'light'}
        items={items}
        selectedKeys={[current]}
        openKeys={openKeys}
        onOpenChange={handleOpenChange}
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
