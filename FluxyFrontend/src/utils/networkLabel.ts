/**
 * The provider line every network cell draws, and the row shape it reads it from.
 *
 * The two table families store the provider differently, and that is a fact about their data
 * rather than about how it is drawn: the connection log captured one provider string when the
 * session opened (it must be filterable, so it is a column), while the visit history stores
 * nothing and is handed the resolver's organisation and AS number while the page is built.
 *
 * This module exists so that both the cell and the pages that build its props name the same
 * shape: a page that passed the wrong field would otherwise draw a provider of `undefined`, and
 * nothing in `typecheck`, `lint` or the build would object to an optional field being absent.
 */

/** What a network cell needs from a row, whichever table it came from. */
export interface NetworkCellRow {
  /** Address the session came from, or null when the row recorded none. */
  ip: string | null
  /** Upper-case ISO alpha-2 country, or null when unknown. */
  countryCode: string | null
  /** The single provider string the connection log stores, or absent on a visit row. */
  isp?: string | null
  /** The organisation the visit's address was registered to. */
  organization?: string | null
  /** The autonomous system number of the visit's address. */
  autonomousSystemNumber?: number | null
}

/**
 * The provider line's text: the stored string, or the resolved organisation and AS number.
 *
 * `organization · AS15169`, or whichever half the server knows - the shape the visit history drew
 * before the owner merged its columns, and the same reason: a provider name is a registry's
 * opinion and an AS number is a fact, and a row that had one but not the other still says
 * something true.
 *
 * `isp` is distinguished from absent with `!== undefined`: the connection log always passes the
 * field (null when the session recorded none, which must draw a dash rather than fall through to
 * a visit's organisation), while a visit row has no such field at all.
 */
export const networkLabel = (row: NetworkCellRow): string | null => {
  if (row.isp !== undefined) return row.isp

  const { organization, autonomousSystemNumber } = row

  if (organization == null && autonomousSystemNumber == null) return null
  if (autonomousSystemNumber == null) return organization ?? null
  if (organization == null) return `AS${autonomousSystemNumber}`

  return `${organization} · AS${autonomousSystemNumber}`
}
