/**
 * Whether the device confirmed the operation it was sent, spelled exactly as the backend's
 * `MagEventStatus` spells it on the wire.
 *
 * Two descriptions rather than a ramp - the same reasoning `UserStatus` and
 * `VideoStreamStatus` carry. A sent event starts `notConfirmed`; the confirmation path that
 * flips it belongs to the future streaming subsystem, not to this screen.
 */
export type MagEventStatus = 'confirmed' | 'notConfirmed'
