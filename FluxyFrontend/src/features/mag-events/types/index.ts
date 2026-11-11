/**
 * The MAG event journal's response contracts - the row, the page it arrives in, and the two
 * vocabularies the row is written with - re-exported as types only so a consumer names the
 * folder rather than a file. `export type` because nothing here is a runtime value: the two
 * runtime lists live in `events.ts` beside the feature's rules.
 */
export type { MagEventRow } from './MagEventRow'
export type { MagEventRowList } from './MagEventRowList'
export type { MagEventStatus } from './MagEventStatus'
export type { MagEventType } from './MagEventType'
