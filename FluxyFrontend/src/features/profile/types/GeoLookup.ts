/**
 * `GET /auth/geo/lookup` - what the GeoIP database knows about the network this browser
 * arrived from.
 *
 * Best effort: every field but the address may be null when the database does not know
 * it, and the page has to render that rather than treat it as "no restriction". Nulls
 * here describe the lookup, not the guard - an unknown country is refused by a non-empty
 * country list, not allowed by it.
 */
export interface GeoLookup {
  ip: string | null
  countryCode: string | null
  autonomousSystemNumber: number | null
  organization: string | null
}
