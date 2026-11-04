/**
 * The directions a sortable header walks, and why the list has three entries rather than two.
 *
 * Every server-paged table here is *always* ordered by something: the pager depends on the server
 * applying an order, so "no order" is a state the address cannot express - `useServerTable` turns
 * it back into the table's default. A two-entry cycle cannot express that. antd computes the next
 * state as `sortDirections[indexOf(current) + 1]`, so from `descend` a two-entry array answers
 * `undefined`; the table's handler reads that as "no order", writes the default back, and the
 * controlled `sortOrder` re-seeds the very direction the click started from - so a column whose
 * default is descending can never leave `descend`, and every click on it is one request for the
 * list already on screen. That is the bug this constant exists to prevent, and it is invisible to
 * `typecheck`, `lint` and the build: the header keeps its arrow and nothing logs a failure.
 *
 * The rule is stated once here because it belongs to the frame every table draws through
 * (`ServerTable` sets it as the table-wide default), not to any one page. A page that needs to
 * pin its own cycle can still pass `sortDirections` on a column, but it must not shorten this
 * list back to antd's default.
 */
export const SORT_DIRECTIONS: ('ascend' | 'descend')[] = ['ascend', 'descend', 'ascend']

/**
 * The direction antd picks after `current`, by antd's own rule.
 *
 * Exported for the unit test rather than for a consumer: the failure it describes (`undefined`
 * after the list runs out) happens inside the library, three layers below any page, and a test
 * that walks the cycle is the only place it can be pinned without a browser.
 *
 * @param directions The cycle a column walks.
 * @param current The direction the header is showing, or `null` when it is not the sorted column.
 * @returns The next direction, or `undefined` when `current` is the last entry.
 */
export const nextSortDirection = (
  directions: readonly ('ascend' | 'descend')[],
  current: 'ascend' | 'descend' | null,
): 'ascend' | 'descend' | undefined =>
  current === null ? directions[0] : directions[directions.indexOf(current) + 1]
