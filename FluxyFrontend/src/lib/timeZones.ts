/**
 * The selector's value for "follow the browser".
 *
 * It cannot collide with a zone from the list: every real IANA identifier is either
 * `Area/City` or a short upper case name such as `UTC`, so `auto` is not one of them - and
 * the mapping is enforced on both sides of the prop anyway, since `onChange` answers a
 * `string | null` and the `null` is what the server stores.
 */
export const AutoTimeZone = 'auto'

/**
 * Zones offered on a runtime that cannot name them itself.
 *
 * `Intl.supportedValuesOf` answers roughly four hundred entries and is the real source when
 * it exists; this list is what the selector falls back to where it does not, chosen to
 * spread over the offsets a visitor is likely to look for rather than to be complete.
 */
const FallbackZones = [
  'UTC',
  'Europe/Moscow',
  'Europe/Kyiv',
  'Europe/London',
  'Europe/Berlin',
  'America/New_York',
  'America/Los_Angeles',
  'Asia/Yekaterinburg',
  'Asia/Novosibirsk',
  'Asia/Tokyo',
  'Australia/Sydney',
]

/**
 * Every zone this browser knows, or the short list above when it knows none.
 *
 * Here rather than inside `TimeZoneCard` because two forms pick a zone - the profile's own
 * card and the administrator's user form - and a second copy of the fallback would be a
 * second answer to the same question on a runtime that gives none.
 */
export const knownZones = (): string[] => {
  const supported = (Intl as { supportedValuesOf?: (key: 'timeZone') => string[] })
    .supportedValuesOf

  if (typeof supported !== 'function') return FallbackZones

  try {
    return supported.call(Intl, 'timeZone')
  } catch {
    // Present but refusing the key, which some engines do: the fallback is the whole point
    // of the try, because a form with no options is worse than a form with eleven.
    return FallbackZones
  }
}
