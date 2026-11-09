/**
 * **The colour of a record type's tag**, as antd's `Tag` names it: green for an attempt that
 * worked, red for one that was refused.
 *
 * `isSuccess` is the stored fact and the only input - it is deliberately not derived from the
 * record type, because the same type has both outcomes (`login` succeeds and `login` fails)
 * and the two rows mean different things. Extracted here rather than left inline in the page
 * because no render in `probe:render` ever reaches a cell `render`: every render lands before
 * the first answer, so no row is drawn and a wrong mapping would keep compiling.
 *
 * The refusal is `'error'` and not `'danger'`. `Tag`'s `color` takes the status names
 * `success | processing | error | default | warning` plus the preset and hex colours;
 * `'danger'` is a `Typography.Text` type, and on a `Tag` it is an unknown string with no
 * colour class behind it - the failure column drew grey, not red. The return type names the
 * two status values, so an invalid word is a compile error rather than a silent grey tag.
 */
export const recordTone = (row: { isSuccess: boolean }): 'success' | 'error' =>
  row.isSuccess ? 'success' : 'error'
