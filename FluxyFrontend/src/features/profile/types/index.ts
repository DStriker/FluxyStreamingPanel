/**
 * The profile feature's response contracts — the account's own facts and the network a
 * request arrived from — re-exported as types only so a consumer names the folder rather
 * than a file. `export type` because nothing here is a runtime value.
 */
export type { GeoLookup } from './GeoLookup'
export type { ProfileResponse } from './ProfileResponse'
