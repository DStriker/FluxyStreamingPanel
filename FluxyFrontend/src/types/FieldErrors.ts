/**
 * Rejected fields as the server sends them: `{ fieldName: [reason, ...] }`, keyed by the
 * camelCase name of the JSON property - deliberately the same name the antd `Form.Item`
 * uses, so `fieldErrors()` can hand them to `form.setFields` unchanged.
 *
 * `null` rather than an empty object when nothing was rejected, so a caller can test the
 * value as a boolean without also having to count its keys.
 */
export type FieldErrors = Record<string, string[]> | null
