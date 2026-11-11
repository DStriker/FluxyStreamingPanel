/**
 * The operation a MAG event asks a device to perform, spelled exactly as the backend's
 * `MagEventType` spells it on the wire.
 *
 * A union rather than `string` so that a seventh member added on the server is a compile
 * error at the picker that offers the six rather than a value the endpoint refuses with
 * `validation_failed`. The members arrive on every row as the event's own camelCase name -
 * `playChannel`, `closePortal`, `reloadPortal`, `rebootDevice`, `sendMessage`, `resetStbLock`
 * - the same spelling the `event` filter accepts.
 *
 * The type is also what decides which payload an operation carries: `sendMessage` requires
 * a non-blank message, `playChannel` requires a stream identifier as a uuid, and the other
 * four carry nothing at all. That rule is the server's (`MagEventRules`), mirrored here by
 * `eventRules.ts` so the form can refuse a value before a request is made.
 */
export type MagEventType =
  | 'playChannel'
  | 'closePortal'
  | 'reloadPortal'
  | 'rebootDevice'
  | 'sendMessage'
  | 'resetStbLock'
