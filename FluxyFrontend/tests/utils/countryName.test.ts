import { describe, expect, it } from 'vitest'
import { countryName } from '../../src/lib/countryName'

/**
 * The three properties the visit history depends on, asserted without a render: the code
 * is upper-cased before the lookup, something that is not two letters never reaches
 * `Intl.DisplayNames` (where it would be a `RangeError` thrown while drawing a cell), and
 * an unknown code comes back as itself rather than as an empty cell.
 *
 * The names asserted here come from the platform's CLDR data - `Germany` for `en`,
 * `Германия` for `ru` - so the test also states which half of the answer belongs to the
 * locale and which to this module.
 */

describe('countryName', () => {
  it('answers nothing for no code - the loopback or missing-database case', () => {
    expect(countryName(null, 'en')).toBeNull()
    expect(countryName(undefined, 'en')).toBeNull()
    expect(countryName('', 'en')).toBeNull()
    expect(countryName('   ', 'en')).toBeNull()
  })

  it('upper-cases the code before the lookup, because `de` is answered with `de`', () => {
    // A lower-case code reaches `Intl.DisplayNames` as itself - not an error, not a name -
    // which is exactly why the column would read as a wall of codes without this step.
    expect(countryName('de', 'en')).toBe('Germany')
    expect(countryName(' de ', 'en')).toBe('Germany')
    expect(countryName('DE', 'en')).toBe('Germany')
  })

  it("answers in the page's own language, not the browser's", () => {
    expect(countryName('DE', 'ru')).toBe('Германия')
  })

  it('never hands a value that is not two letters to the lookup', () => {
    // `X1` would be a `RangeError`; `USA` is simply not a code.
    expect(countryName('X1', 'en')).toBe('X1')
    expect(countryName('USA', 'en')).toBe('USA')
    expect(countryName('1A', 'en')).toBe('1A')
  })

  it('returns the code itself for a region the table has no name for', () => {
    expect(countryName('AA', 'en')).toBe('AA')
  })

  it('degrades to the code for a locale the platform refuses to parse', () => {
    // `ru_RU` written with an underscore is a `RangeError` from `new Intl.DisplayNames`,
    // and `navigator.language` is the kind of source that produces such a spelling.
    expect(countryName('DE', 'ru_RU')).toBe('DE')
  })
})
