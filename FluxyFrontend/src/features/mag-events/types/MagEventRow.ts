import type { MagEventStatus } from './MagEventStatus'
import type { MagEventType } from './MagEventType'

/**
 * One entry of the MAG event journal, as `GET /admin/mag-events` answers with it.
 *
 * The row records one operation sent to one subscription's device: which subscription,
 * which device address, which operation, whether it was confirmed, and when.
 * `magMac` is resolved from the subscription when the page is built, never stored on the
 * event row, so a corrected device address reads as it is now. It is null when the
 * subscription has no MAG device.
 *
 * `id` and `subscriptionId` are text because a JavaScript client parses a uuid as a number
 * otherwise. They are unrelated: the first is this record's own key, the second is the
 * subscription the cell links back to.
 */
export interface MagEventRow {
  /** Identifier of the event, as text. */
  id: string
  /** Identifier of the subscription it was sent under, as text. */
  subscriptionId: string
  /** MAG address of that subscription, or null when it has none. */
  magMac: string | null
  /** The operation the device was asked to perform. */
  eventType: MagEventType
  /** The operation's payload - a message text, a stream identifier, or null. */
  message: string | null
  /** Whether the device confirmed the operation. */
  status: MagEventStatus
  /** When the event was recorded, as an ISO timestamp. */
  createdAt: string
}
