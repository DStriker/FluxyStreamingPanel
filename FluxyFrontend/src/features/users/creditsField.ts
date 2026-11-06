/**
 * The rule under the account form's balance field: a whole number of zero or more.
 *
 * The control already states `min={0}` and `precision={0}`, which is what stops most of the
 * bad input at the keypress - but antd's `InputNumber` clamps a typed negative on the way
 * out and leaves a fractional value in the store until the form is judged, so the rule is
 * stated here as well rather than trusted to the widget. A server refusal for the same value
 * is `validation_failed` keyed `credits`, which lands under this very input.
 *
 * A pure function because no render reaches a form validator: the browser check proved the
 * fractional branch, and this is what makes the negative branch provable without one.
 */
export const isCreditAmount = (value: number | null | undefined): boolean =>
  value === null || value === undefined || (Number.isInteger(value) && value >= 0)
