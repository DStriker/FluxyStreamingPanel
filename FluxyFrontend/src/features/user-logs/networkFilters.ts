/**
 * The user log's three network filters: the query-string keys and the read-back half.
 *
 * The names are the connection log's, parameter for parameter, because one component writes
 * them on both screens: `ConnectionNetworkCell`'s `onFilter` hands the page a `country`, `ip`
 * or `isp` key and the value it read off the row.
 *
 * **The response field and the request parameter are spelled differently on purpose.**
 * `GET /admin/user-logs` answers with `countryCode`, but the filter it accepts is `country` -
 * the same wire name the connection log's cell writes. A reader that reached for the response
 * field would send a parameter the server ignores, and the table would quietly stay unfiltered.
 *
 * This module is data plus one pure function, tested in
 * `tests/features/user-logs/networkFilters.test.ts` for the reason `dateRange.ts` is: the keys
 * are strings no type can check against the endpoint, and every render lands before a click.
 */

/** The three filters the shared network cell writes, in the order its tag row draws them. */
export type UserLogNetworkFilterKey = 'country' | 'ip' | 'isp'

/** One value per network filter, or null when the address names none. */
export type UserLogNetworkFilters = Record<UserLogNetworkFilterKey, string | null>

/**
 * Reads the three network filters out of an address.
 *
 * An absent and an empty value are both "no filter": the endpoint refuses neither, but a tag
 * or a bound built from an empty string would claim a narrowing the request does not carry.
 */
export const networkFilterParams = (params: URLSearchParams): UserLogNetworkFilters => ({
  country: params.get('country') || null,
  ip: params.get('ip') || null,
  isp: params.get('isp') || null,
})
