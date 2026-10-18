/**
 * The address as a table's state: the two pure functions a page writes it with.
 *
 * The five parameters `useServerTable` reads - page, page size, search, order and direction -
 * and one filter are all the same kind of thing: what the visitor is looking at, written
 * once into the query string rather than held twice. These live in `src/utils` because both
 * are pure functions over `URLSearchParams`, the half of that decision no render can reach:
 * a probe lands before any interaction, and the unit tests here are what pin the rules.
 */

/**
 * The address after one filter changes: the key is set when it has a value and removed when
 * it does not, and `page` is dropped either way.
 *
 * Dropping the page is not tidiness. A different filter is a different list, so the pager
 * that belonged to the old one stops at the first page of the new - leaving `page=3` over a
 * filter that admits one page would show an empty table while `total` above it said 1. Doing
 * it in the same write is what keeps a filter change **one** navigation: the filter and the
 * reset are one address, so one request follows rather than a filter followed by a page that
 * starts over.
 */
export function withFilter(
  params: URLSearchParams,
  key: string,
  value: string | null | undefined,
): URLSearchParams {
  const next = new URLSearchParams(params)

  if (value) next.set(key, value)
  else next.delete(key)

  next.delete('page')

  return next
}

/**
 * One of a fixed set of values, as an address can name it.
 *
 * Anything else - an absent key, an empty value, a hand-edited word the endpoint would
 * answer `validation_failed` for - is `null`, which is the same "nothing chosen" the control
 * shows when the filter is off. The point is not that the browser judges filters; it is that
 * a stale or mistyped link draws an unfiltered table rather than a 400.
 */
export function enumParam<T extends string>(
  params: URLSearchParams,
  key: string,
  values: readonly T[],
): T | null {
  const raw = params.get(key)

  // The membership check one line above is what makes the return a narrowing rather than a
  // hope: `includes` cannot tell the compiler that `raw` is one of `values`.
  if (!raw || !(values as readonly string[]).includes(raw)) return null

  return raw as T
}
