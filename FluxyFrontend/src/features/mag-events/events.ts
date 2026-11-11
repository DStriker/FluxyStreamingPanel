import type { MagEventType } from './types'

/**
 * The six operations a MAG event may name, as a runtime list in their wire order.
 *
 * It lives at the feature root rather than in `types/` for the reason
 * `API_REQUEST_FAILURE_REASONS` does: `types/` and its barrel are type-only, so a value there
 * would erase to nothing at the call site that needs it. Those call sites are the filter
 * picker, the address read-back (`enumParam`) and the send form's event picker.
 *
 * The order is the server's own (`MagEventRules.KnownEvents`), which is also the order the
 * refusal sentence names them in: `playChannel`, `closePortal`, `reloadPortal`,
 * `rebootDevice`, `sendMessage`, `resetStbLock`.
 */
export const MAG_EVENT_TYPES: readonly MagEventType[] = [
  'playChannel',
  'closePortal',
  'reloadPortal',
  'rebootDevice',
  'sendMessage',
  'resetStbLock',
]
