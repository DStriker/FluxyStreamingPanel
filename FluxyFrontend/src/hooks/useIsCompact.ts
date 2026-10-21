import { useSyncExternalStore } from 'react'

/**
 * The width at which the shell stops drawing a permanent rail.
 *
 * 991 is antd's `lg` breakpoint minus one pixel, and it is deliberately the same boundary
 * the `Sider` used to collapse at. Below it the shell owns the full width and the menu is a
 * `Drawer`; at 992 and above it is still the familiar rail. The number appears in two places
 * by necessity - this query and the CSS media query that shapes the compact card headers -
 * so a change to one has to be made in `src/index.css` as well.
 */
const QUERY = '(max-width: 991px)'

/**
 * Whether the signed-in shell is in its compact mode, as one subscription for the whole app.
 *
 * The same shape as `usePrefersDark`, and for the same reason: `AccountLayout` is the only
 * consumer today, but keeping the query module-level makes the first render read the truth
 * synchronously and gives a single listener for every future caller. The server snapshot is
 * `false` on purpose - `probe:render` renders under Node with no `matchMedia`, and the shell
 * it asserts is the desktop one. A browser evaluates the query on its first render, so a
 * phone never paints the rail for a frame before the drawer replaces it.
 */
let media: MediaQueryList | null = null
const listeners = new Set<() => void>()

const notify = (): void => listeners.forEach((listener) => listener())

const ensureMedia = (): MediaQueryList | null => {
  if (media !== null) return media
  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return null
  media = window.matchMedia(QUERY)
  return media
}

const subscribe = (listener: () => void): (() => void) => {
  const target = ensureMedia()
  listeners.add(listener)
  target?.addEventListener('change', notify)
  return () => {
    listeners.delete(listener)
    if (listeners.size === 0) target?.removeEventListener('change', notify)
  }
}

const getSnapshot = (): boolean => ensureMedia()?.matches ?? false

/** The shell's compact mode: a drawer instead of a permanent sidebar. */
export function useIsCompact(): boolean {
  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot)
}
