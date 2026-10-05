/**
 * The countries a sign-in may be restricted to, and how to name them for a visitor.
 *
 * The *codes* are what the rest of the app is about: `allowedCountry` on the contract,
 * `GeoLookup.countryCode` from the lookup and the two letters the server stores are all
 * ISO 3166-1 alpha-2, and none of them change when the interface speaks another language.
 * The names are a presentation detail, so they are never stored, never sent and never
 * searched over by anything but the selector's own filter - they are looked up here from
 * `Intl` at the moment they are needed.
 */

/**
 * Every ISO 3166-1 alpha-2 code in the set the server recognises, sorted. Sorted because
 * a reader comparing it against a list elsewhere expects alphabetical order, not because
 * anything here depends on it: `countryOptions` sorts by name, in the visitor's language.
 *
 * Obsolete codes (AN, TP, YU) are deliberately absent, and so is XK, which is not in ISO
 * at all - a country the database cannot answer for would be stored and then drawn as an
 * empty selector.
 */
export const COUNTRY_CODES: readonly string[] = [
  'AD', 'AE', 'AF', 'AG', 'AI', 'AL', 'AM', 'AO', 'AQ', 'AR', 'AS', 'AT', 'AU', 'AW', 'AX', 'AZ',
  'BA', 'BB', 'BD', 'BE', 'BF', 'BG', 'BH', 'BI', 'BJ', 'BL', 'BM', 'BN', 'BO', 'BQ', 'BR', 'BS',
  'BT', 'BV', 'BW', 'BY', 'BZ',
  'CA', 'CC', 'CD', 'CF', 'CG', 'CH', 'CI', 'CK', 'CL', 'CM', 'CN', 'CO', 'CR', 'CU', 'CV', 'CW',
  'CX', 'CY', 'CZ',
  'DE', 'DJ', 'DK', 'DM', 'DO', 'DZ',
  'EC', 'EE', 'EG', 'EH', 'ER', 'ES', 'ET',
  'FI', 'FJ', 'FK', 'FM', 'FO', 'FR',
  'GA', 'GB', 'GD', 'GE', 'GF', 'GG', 'GH', 'GI', 'GL', 'GM', 'GN', 'GP', 'GQ', 'GR', 'GS', 'GT',
  'GU', 'GW', 'GY',
  'HK', 'HM', 'HN', 'HR', 'HT', 'HU',
  'ID', 'IE', 'IL', 'IM', 'IN', 'IO', 'IQ', 'IR', 'IS', 'IT',
  'JE', 'JM', 'JO', 'JP',
  'KE', 'KG', 'KH', 'KI', 'KM', 'KN', 'KP', 'KR', 'KW', 'KY', 'KZ',
  'LA', 'LB', 'LC', 'LI', 'LK', 'LR', 'LS', 'LT', 'LU', 'LV', 'LY',
  'MA', 'MC', 'MD', 'ME', 'MF', 'MG', 'MH', 'MK', 'ML', 'MM', 'MN', 'MO', 'MP', 'MQ', 'MR', 'MS',
  'MT', 'MU', 'MV', 'MW', 'MX', 'MY', 'MZ',
  'NA', 'NC', 'NE', 'NF', 'NG', 'NI', 'NL', 'NO', 'NP', 'NR', 'NU', 'NZ',
  'OM',
  'PA', 'PE', 'PF', 'PG', 'PH', 'PK', 'PL', 'PM', 'PN', 'PR', 'PS', 'PT', 'PW', 'PY',
  'QA',
  'RE', 'RO', 'RS', 'RU', 'RW',
  'SA', 'SB', 'SC', 'SD', 'SE', 'SG', 'SH', 'SI', 'SJ', 'SK', 'SL', 'SM', 'SN', 'SO', 'SR', 'SS',
  'ST', 'SV', 'SX', 'SY', 'SZ',
  'TC', 'TD', 'TF', 'TG', 'TH', 'TJ', 'TK', 'TL', 'TM', 'TN', 'TO', 'TR', 'TT', 'TV', 'TW', 'TZ',
  'UA', 'UG', 'UM', 'US', 'UY', 'UZ',
  'VA', 'VC', 'VE', 'VG', 'VI', 'VN', 'VU',
  'WF', 'WS',
  'YE', 'YT', 'ZA', 'ZM', 'ZW',
]

/** One row of the country selector: the code the form holds, the name that shows it. */
export interface CountryOption {
  value: string
  label: string
}

/**
 * Whether a code is one this selector can draw. Used before storing a value the visitor
 * never chose - a country from the GeoIP lookup, say - because a `Select` asked to show a
 * value it has no option for shows nothing at all, which reads as "no country picked"
 * rather than as "a country we cannot spell".
 */
export const isCountryCode = (code: string): boolean => COUNTRY_CODES.includes(code)

/**
 * The options for one language, sorted by that language's alphabet, built once per
 * language and reused: a selector re-rendering on every keystroke must not be rebuilding
 * `Intl.DisplayNames` and re-sorting 249 rows with it.
 *
 * A missing or unusable `Intl` - an engine without `DisplayNames`, or a language tag
 * nothing understands - does not lose the country, it only loses its name: the code is
 * used as the label, which is what the field collected before this existed.
 */
const optionsByLanguage = new Map<string, CountryOption[]>()

export const countryOptions = (language: string): CountryOption[] => {
  const cached = optionsByLanguage.get(language)
  if (cached) return cached

  const built = (() => {
    try {
      const names = new Intl.DisplayNames([language], { type: 'region' })
      return COUNTRY_CODES.map((value) => ({ value, label: names.of(value) ?? value })).sort(
        (a, b) => a.label.localeCompare(b.label, language),
      )
    } catch {
      return COUNTRY_CODES.map((value) => ({ value, label: value }))
    }
  })()

  optionsByLanguage.set(language, built)
  return built
}
