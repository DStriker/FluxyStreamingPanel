import { dateRangeParams } from '../../utils/dateRange'
import type { DateRange } from '../../utils/dateRange'

/**
 * The connection log's start-time range: the two URL parameters it uses, and the mode gate
 * that decides whether a screen may apply them at all.
 *
 * The conversion between the picker's calendar days and the two wire instants is **shared
 * arithmetic** - the credit log filters on a date range of its own with different parameter
 * names - so it lives in `src/utils/dateRange.ts` and is re-exported here for the call sites
 * this feature already had. Only what is specific to this table stays: the names of its two
 * parameters, the type they are read into, and the rule that only the activity view may
 * apply them.
 */

/** The query-string name of the lower bound, inclusive. */
export const STARTED_FROM_KEY = 'startedFrom'

/** The query-string name of the upper bound, inclusive. */
export const STARTED_TO_KEY = 'startedTo'

/** The two bounds of this table's range, as an address carries them. */
export type StartRange = DateRange

/** Reads both bounds out of the address; an absent or empty value is null. */
export const startRangeParams = (params: URLSearchParams): StartRange =>
  dateRangeParams(params, STARTED_FROM_KEY, STARTED_TO_KEY)

export {
  NO_DATE_RANGE as NO_START_RANGE,
  endOfDayIso,
  pickerDay,
  rangeFromPicker,
  rangePickerValue,
  startOfDayIso,
} from '../../utils/dateRange'

/**
 * The range **this mode** may apply.
 *
 * Only the activity view draws the range control, so only it may send the two bounds. The live
 * view pins `state=live` - "every session still open, whatever time it started" - and a bound
 * that arrived in a hand-edited or saved address would silently narrow that promise. **The
 * parameters are ignored rather than stripped**: the address is the visitor's, a stale link must
 * not be rewritten under them, and ignoring the two keys is what makes the live query the same
 * query whatever they hold.
 *
 * This is a function rather than a branch at the call site so the rule can be unit-tested: no
 * render can carry a live `?startedFrom=` without also carrying the query it feeds, and the
 * probe only sees the markup the gate produces.
 */
export const startRangeForMode = (
  params: URLSearchParams,
  hasControl: boolean,
): StartRange => (hasControl ? startRangeParams(params) : { from: null, to: null })
