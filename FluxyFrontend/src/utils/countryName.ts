/**
 * The full name of a country for its ISO 3166-1 alpha-2 code, in the language the page is
 * being read in.
 *
 * The server holds the code, because that is what GeoIP produces and what an allow list is
 * written against; a person reads the name. The mapping is `Intl.DisplayNames` rather than a
 * table in this repository: it ships the same CLDR data the platforms themselves use, so the
 * answer follows the language switcher on its own and stays current without a release, and
 * a country added to the standard next year needs nothing from anybody here.
 *
 * Three properties the caller depends on:
 *
 * - **The code is upper-cased first.** `Intl.DisplayNames` answers a lower-case `de` with
 *   `de` - not an error, not `Germany`, the string back unchanged - so a case the source
 *   happened to use would turn the whole column into codes that look unexplained.
 * - **A code that is not two letters never reaches the lookup.** `X1` is a `RangeError`, and
 *   an exception thrown while rendering a cell takes the table down over one field.
 * - **Anything the table has no name for comes back as itself.** `AA` is answered `AA`, which
 *   is still a fact worth showing. The one exception is `ZZ`, which CLDR carries as "Unknown
 *   Region" rather than as itself - and GeoIP does not hand out `ZZ`.
 *
 * The formatters are memoised by locale. Building one is the expensive part and looking a
 * code up in it is not, while a page does one lookup per row per repaint - and this page
 * repaints on every frame of a column drag.
 */
const IS_REGION = /^[A-Z]{2}$/

const regionsByLocale = new Map<string, Intl.DisplayNames | null>()

function regionsFor(locale: string): Intl.DisplayNames | null {
  const cached = regionsByLocale.get(locale)
  if (cached !== undefined) return cached

  let regions: Intl.DisplayNames | null = null

  try {
    regions = new Intl.DisplayNames([locale], { type: 'region' })
  } catch {
    // An ill-formed locale lands here rather than on the page. It is not a hypothetical:
    // `ru_RU` written with an underscore is a `RangeError`, and `navigator.language` is the
    // kind of source that produces such a spelling.
    regions = null
  }

  regionsByLocale.set(locale, regions)
  return regions
}

/**
 * `Germany` for `DE`, `Германия` for `DE` under a Russian page, and `null` for no code at
 * all - which is what a loopback address or a missing GeoIP database answers with, and which
 * the caller draws as a dash rather than as an empty cell.
 */
export function countryName(code: string | null | undefined, locale: string): string | null {
  if (!code) return null

  const trimmed = code.trim().toUpperCase()
  if (trimmed === '') return null
  if (!IS_REGION.test(trimmed)) return trimmed

  const regions = regionsFor(locale)
  if (!regions) return trimmed

  try {
    return regions.of(trimmed) ?? trimmed
  } catch {
    return trimmed
  }
}
