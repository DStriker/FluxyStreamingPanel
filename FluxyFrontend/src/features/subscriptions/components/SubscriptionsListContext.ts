import { createContext, useContext } from 'react'
import type { Subscription } from '../types'

/** Which of the two device kinds a MAC dialog is about. */
export type SubscriptionMacKind = 'mag' | 'enigma2'

/**
 * The actions one subscription's service subrows perform, handed down through a context.
 *
 * A context rather than props because `ServerTable`'s `expandedRowRender` is a function of the
 * row alone: there is no prop path from the page into one cell of a row it did not build. The
 * page supplies the handlers once and the strips read them, which is the same shape
 * `RowDragContext` uses on the categories table.
 */
export interface SubscriptionsListActions {
  /** Whether the operator may change subscriptions at all, which gates every strip's buttons. */
  canEdit: boolean
  /** Copies a value to the clipboard and says so. */
  copy: (value: string) => void
  /** Replaces the playlist id with one the server minted. */
  regenerate: (subscription: Subscription) => void
  /** Mints a playlist id and stores it on a subscription that has none. */
  addPlaylist: (subscription: Subscription) => void
  /** Opens the MAC dialog for one device kind. */
  changeMac: (subscription: Subscription, kind: SubscriptionMacKind) => void
  /** Clears the three service fields. */
  clearPlaylist: (subscription: Subscription) => void
  clearMag: (subscription: Subscription) => void
  clearEnigma2: (subscription: Subscription) => void
}

/**
 * The context the service strips read. Defaults to a full set of refusals rather than to null so
 * a strip rendered outside the page - a probe, a future screen - draws disabled buttons instead of
 * throwing.
 */
const noop = () => {}

export const SubscriptionsListContext = createContext<SubscriptionsListActions>({
  canEdit: false,
  copy: noop,
  regenerate: noop,
  addPlaylist: noop,
  changeMac: noop,
  clearPlaylist: noop,
  clearMag: noop,
  clearEnigma2: noop,
})

/**
 * The actions the service strips perform.
 *
 * Wrapped so a caller names one hook rather than the context itself, and so the default above is
 * stated once.
 */
export const useSubscriptionsListActions = () => useContext(SubscriptionsListContext)
