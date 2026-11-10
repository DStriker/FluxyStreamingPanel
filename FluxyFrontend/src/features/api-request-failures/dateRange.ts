/**
 * The API request failure log's date range: the two query-string names it uses.
 *
 * The conversion between the picker's calendar days and the two wire instants is the shared
 * arithmetic in `src/utils/dateRange.ts` - the connection, credit and user logs filter on
 * ranges of their own with different parameter names - so this module names the keys and
 * re-exports what the page calls. That keeps this table's parameter spellings beside the
 * feature they belong to while the account-zone arithmetic stays written once.
 *
 * Both bounds are inclusive on the server and each side is optional: the picker names whole
 * days, so "the 1st" must keep everything written during the 1st.
 */

/** The query-string name of the lower bound, inclusive. */
export const DATE_FROM_KEY = 'dateFrom'

/** The query-string name of the upper bound, inclusive. */
export const DATE_TO_KEY = 'dateTo'

export {
  NO_DATE_RANGE,
  dateRangeParams,
  endOfDayIso,
  pickerDay,
  rangeFromPicker,
  rangePickerValue,
  startOfDayIso,
} from '../../utils/dateRange'
export type { DateRange } from '../../utils/dateRange'
