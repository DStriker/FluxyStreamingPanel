import { useSyncExternalStore } from 'react'
import { theme, type ThemeConfig } from 'antd'
import { usePrefersDark } from '../hooks/usePrefersDark'

/**
 * The antd theme this application runs on, as a `ConfigProvider` config.
 *
 * `dark` selects the algorithm. Everything else here is the translation of
 * `ant-design-dark-theme/index.ts` - the LESS variable set that repository ships - into the
 * design tokens antd v6 actually reads. The file is worked through row by row below so the
 * port can be checked rather than taken on trust, and so a row that does *not* port says
 * why instead of quietly disappearing:
 *
 *   ...dark (the palette)          -> `theme.darkAlgorithm`. The spread was the entire
 *                                     point of that file - loading antd's own dark
 *                                     palette - and in v6 the palette is the algorithm.
 *   @font-size-base: 14px          -> `token.fontSize`, still 14; stated because the
 *                                     source states it.
 *   @font-size-lg: 16px            -> `token.fontSizeLG`, still 16.
 *   @menu-item-vertical-margin: 0  -> `components.Menu.itemMarginBlock`, which was
 *                                     `marginXXS` (4). This is what stops the items
 *                                     floating apart from one another.
 *   @menu-item-boundary-margin: 0  -> `components.Menu.itemMarginInline`, also
 *                                     `marginXXS` (4). The selected item stops being
 *                                     inset from the rail's edges.
 *
 *   @form-item-margin-bottom: 24px -> `Form.itemMarginBottom` already resolves to
 *                                     `marginLG`, which is 24. Setting it would change
 *                                     nothing, so it is not set.
 *   @zindex-notification/popover/tooltip
 *                                   -> v6 derives every popup from one base
 *                                     (`zIndexPopupBase`) plus a per-component offset:
 *                                     popover +30, tooltip +70, notification on top of
 *                                     both. Those three absolute numbers were a fix for
 *                                     v4's *ties*; porting them now would drag the tooltip
 *                                     below the popover, which is the opposite of what
 *                                     they were for.
 *   @background-color-base: #555,
 *   @skeleton-color, @gray-8        -> v4 painted these as flat greys picked by hand. v6
 *                                     derives the whole neutral ramp from the algorithm,
 *                                     which is what `...dark` was there to provide; a
 *                                     single flat grey would fight the ramp it replaces.
 *   @site-*, @pro-*, @screen-*      -> the antd documentation site, not this application.
 *   @anchor-border-width,
 *   @tabs-horizontal-padding        -> components this application never renders.
 *
 * The header is where antd gets a value *wrong* rather than merely leaving one alone:
 * `Layout.headerBg` is the hardcoded navy `#001529` of the v3 layout and ignores the
 * algorithm entirely, so switching to the dark theme would leave a navy bar over a
 * grey-black body. It takes `colorBgContainer`, which is what `Layout.lightSiderBg`
 * already resolves to, so the bar and the rail stay one surface in both themes - and no
 * inline style is needed on either of them to make that true. `Layout.siderBg` is the
 * same mistake on the other half of that pair and is corrected alongside it; the menu's
 * own hardcoded values are listed with the variant below.
 *
 * The menu rows are applied in both themes even though they come from a dark theme file.
 * They are a spacing decision, not a colour one, and the sidebar would otherwise change
 * shape when the visitor flipped the switch.
 *
 * The rest of this config belongs to antd's dark **variant** - `Menu theme="dark"` and
 * `Sider theme="dark"`, which are two separate declarations that neither component
 * inherits from the other - and the reference file has no rows for any of it, because in
 * v4 a theme was one palette rather than a palette plus a variant:
 *
 *   Menu.darkItemBg, Menu.darkSubMenuItemBg
 *                              -> antd pins them to `#001529` and `#000c17`, the same
 *                                 hardcoded navy as `Layout.headerBg` above - and both are
 *                                 backgrounds sitting behind rows that are doing nothing,
 *                                 an idle menu root and a group panel. They go to
 *                                 `transparent` rather than to another colour: the rail
 *                                 paints the surface, and the menu paints only its own
 *                                 state - the selected row, the hover. This is what
 *                                 removes the block behind every group. Before the switch
 *                                 to the dark variant that block came from `subMenuItemBg`
 *                                 instead, which under the dark algorithm is
 *                                 `rgba(255,255,255,0.04)` - invisible on white,
 *                                 unmistakably a panel on `#141414`.
 *   Menu.itemBg                 -> the light variant's counterpart. It resolves to
 *                                 `colorBgContainer`, and so does the rail under it, so
 *                                 this changes nothing today; it says out loud that the
 *                                 menu paints nothing in either theme.
 *   Menu.darkPopupBg            -> also `#001529`. Takes `colorBgElevated`, so a submenu
 *                                 opening out of the collapsed rail reads as raised
 *                                 above it instead of as the same box in a darker tint.
 *   Layout.siderBg              -> `#001529` once more. It takes the same
 *                                 `colorBgContainer` as the header, so the bar and the
 *                                 rail are one surface and the transparent menu above
 *                                 has the right colour underneath it.
 */
/**
 * What `appTheme` answers with: the config for `ConfigProvider`, and the tokens of that
 * exact theme for everything `ConfigProvider` cannot reach - `body`, which paints itself
 * from `App`'s effect.
 *
 * `derived` is antd's own return type of `getDesignToken` rather than `GlobalToken`, which
 * is a *different* type in v6: `GlobalToken` is the cssinjs-utils shape carrying the
 * component tokens as well, and the alias token the algorithm produces is not assignable
 * to it. Naming it here once keeps both halves of the answer honest.
 */
type AppTheme = {
  config: ThemeConfig
  derived: ReturnType<typeof theme.getDesignToken>
}

/**
 * The primary of each theme, and why there are two of them rather than one.
 *
 * `#1677ff` - the antd default this used to be - fails WCAG AA everywhere this application
 * paints it: white on it is 4.10:1 and it as link text on `#f5f5f5` is 3.77:1, and a 14px
 * label needs 4.5. The ratio is symmetric, so one darker blue fixes the fill and the text
 * in a single move: `#0958d9` measures 6.16:1 against white and 5.65:1 against the page.
 *
 * The dark theme needs the opposite direction, and no single colour can serve both. Its
 * text sits on `#141414`, where a blue dark enough to carry white text is a blue nobody
 * can read on a near-black page - the two requirements are the same ratio asked of the two
 * ends of the scale at once, and between them the window is empty. So the seed goes *light*
 * there instead (`#4096ff`, which the dark algorithm renders as `#3983dc`), and what is
 * written on a primary fill goes dark rather than white - `onPrimary` below is that answer,
 * 4.78:1 for a button label in either direction.
 *
 * `colorLink` is set to the same seed deliberately rather than left to follow
 * `colorPrimary`: in v6 it no longer does. Untouched it stays `#1677ff` - the very value
 * this replaces - and it is the colour of all six links on the guest pages, which are the
 * most-read text in the application. Measured with `theme.getDesignToken` rather than
 * reasoned: the derived palette rounds the seed, so `#4096ff` comes out of the dark
 * algorithm as `#3983dc` and `colorPrimaryHover` comes out of `#0958d9` as `#2e7ae6`.
 *
 * Every state, measured against the surface it is painted on, with what stock antd gave
 * before this in the last line:
 *
 *            light: rest / hover / active        dark: rest / hover / active
 *   link     6.16 / 2.80 / 8.97                  4.78 / 2.20 / 3.29
 *   button   6.16 / 4.16 / 8.97                  4.78 / 6.82 / 3.29
 *   before   4.10 / 2.25 / 6.16                  3.55 / 1.83 / 2.55
 *
 * Hover is the honest weak spot and it is not settable from here: `colorLinkHover` and
 * `colorLinkActive` are not seeds but `linkColors[4]` and `linkColors[7]` in antd's
 * `genColorMapToken`, computed *after* the seed is read, so a value handed to
 * `ConfigProvider` for them is overwritten. It improves with the seed rather than with a
 * setting, and it belongs to antd's palette rather than to this file - which is why the
 * resting state, the one a reader spends their time in, is the one these two colours were
 * chosen to fix.
 */
const primaryFor = (dark: boolean): string => (dark ? '#4096ff' : '#0958d9')

/**
 * What a primary fill says: white in light, the page's own near-black in dark.
 *
 * This is `Button.primaryColor` and `Menu.darkItemSelectedColor`, both of which default to
 * `colorTextLightSolid` (`#fff` in both themes) over a background of `colorPrimary` - so
 * with the light dark-theme seed above, leaving them alone would put white on `#3983dc`
 * for 3.85:1. `dangerColor` is deliberately *not* set: it takes the same default and sits
 * on red, where white is still the right answer.
 */
const onPrimaryFor = (dark: boolean): string => (dark ? '#141414' : '#ffffff')

export const appTheme = (dark: boolean): AppTheme => {
  const algorithm = dark ? theme.darkAlgorithm : theme.defaultAlgorithm
  const onPrimary = onPrimaryFor(dark)
  // One object, used by both halves of the answer. `derived` has to be computed from the
  // exact seed `ConfigProvider` receives - `body` reads it in `App` - and two copies of the
  // same list are two copies that can drift. They already did once while this was being
  // written: the `colorLink` below reached the page while this call did not have it, so
  // `derived` reported `#1677ff` for a link the browser painted `#0958d9` - the same lie
  // the paragraph above exists to prevent, in a quieter voice.
  const token = {
    colorPrimary: primaryFor(dark),
    colorLink: primaryFor(dark),
    fontSize: 14,
    fontSizeLG: 16,
  }
  const derived = theme.getDesignToken({ algorithm, token })

  return {
    /** Exactly what `ConfigProvider` takes as its `theme` prop - nothing else. */
    config: {
      algorithm,
      token,
      components: {
        Button: { primaryColor: onPrimary },
        Layout: { headerBg: derived.colorBgContainer, siderBg: derived.colorBgContainer },
        Menu: {
          itemMarginBlock: 0,
          itemMarginInline: 0,
          itemBg: 'transparent',
          darkItemBg: 'transparent',
          darkSubMenuItemBg: 'transparent',
          darkPopupBg: derived.colorBgElevated,
          // The selected row of the dark menu is a primary *fill* - `darkItemSelectedBg`
          // takes `colorPrimary` - so it is the button's problem again and gets the same
          // answer from `onPrimary` above.
          darkItemSelectedColor: onPrimary,
        },
      },
    },
    /** The tokens of this exact theme, for anything `ConfigProvider` cannot reach - `body`. */
    derived,
  }
}

const STORAGE_KEY = 'fluxy-theme'

/**
 * The three states a visitor can be in; `system` follows the OS and is the default.
 *
 * `as const` so each member is its literal rather than `string`: `ThemeChoice.Dark` is
 * then `'dark'`, and the union below is the set of values `useThemeChoice` can answer
 * with. A plain object would widen every one of them to `string` and the type would say
 * nothing.
 */
export const ThemeChoice = {
  Light: 'light',
  Dark: 'dark',
  System: 'system',
} as const

/** One of the three, and the only thing the stored preference can be. */
type ThemeChoiceValue = (typeof ThemeChoice)[keyof typeof ThemeChoice]

const isChoice = (value: unknown): value is ThemeChoiceValue =>
  value === ThemeChoice.Light || value === ThemeChoice.Dark || value === ThemeChoice.System

const readStored = (): ThemeChoiceValue => {
  try {
    const saved = localStorage.getItem(STORAGE_KEY)
    return isChoice(saved) ? saved : ThemeChoice.System
  } catch {
    // localStorage isn't available - the choice then lives for the session only.
    return ThemeChoice.System
  }
}

// Module-level rather than React state, for one reason: `App` owns the antd
// `ConfigProvider` that the algorithm comes from, and the header's switch sits deep inside
// the router tree. A context between them would have to wrap `ConfigProvider`, and
// `ConfigProvider` is what produces the token the switch renders with - a cycle for no
// gain. Both ends subscribe to this instead, and there is exactly one copy of the value.
let choice: ThemeChoiceValue = readStored()
const listeners = new Set<() => void>()

const notify = () => listeners.forEach((listener) => listener())

const themeChoice = (): ThemeChoiceValue => choice

export const setThemeChoice = (next: ThemeChoiceValue): void => {
  if (!isChoice(next) || next === choice) return
  choice = next
  try {
    if (next === ThemeChoice.System) localStorage.removeItem(STORAGE_KEY)
    else localStorage.setItem(STORAGE_KEY, next)
  } catch {
    // The choice still applies for the session; only the memory of it is lost.
  }
  notify()
}

const subscribe = (listener: () => void): (() => void) => {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

/** The stored preference: `light`, `dark`, or `system` to follow the OS. */
export function useThemeChoice(): ThemeChoiceValue {
  return useSyncExternalStore(subscribe, themeChoice, themeChoice)
}

/**
 * Whether the page is currently dark: the stored choice when there is one, the OS
 * otherwise.
 *
 * This is what feeds `theme.darkAlgorithm`, and it is deliberately not `usePrefersDark`
 * on its own - that hook only ever reports the OS, which is exactly why there was no way
 * to override it. It stays as the fallback rather than being replaced, so a first visit
 * still matches the visitor's own system setting.
 */
export function useIsDark(): boolean {
  const stored = useThemeChoice()
  const systemDark = usePrefersDark()
  return stored === ThemeChoice.System ? systemDark : stored === ThemeChoice.Dark
}
