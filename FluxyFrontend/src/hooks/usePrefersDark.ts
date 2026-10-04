import { useSyncExternalStore } from 'react'

const QUERY = '(prefers-color-scheme: dark)'

/**
 * Whether the OS is on a dark colour scheme, as one subscription for the whole app.
 *
 * Module-level for the reason `src/lib/theme.ts` keeps its copy there too: three components
 * ask this question - `App`, `AccountLayout`, `AccountSidebar` - and until now each of them
 * got its own `MediaQueryList`, its own listener and its own copy of the same answer. Three
 * states of one fact, three listeners, and nothing anywhere could say which copy was
 * right. There is now one of each, created on the first question rather than at import:
 * the probes bundle this file for Node, where `window` does not exist at all.
 *
 * `getSnapshot` reads `media.matches` through `ensureMedia` rather than caching a boolean,
 * because the value has to be right on the *first* render - `subscribe` runs after that
 * render, not before it - and `MediaQueryList.matches` is live anyway. The `change` event
 * is what tells React a render is needed; the listener is dropped when the last consumer
 * unmounts, so an idle page holds none.
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

/** The OS preference: the same value to every consumer, from a single listener. */
export function usePrefersDark(): boolean {
  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot)
}
