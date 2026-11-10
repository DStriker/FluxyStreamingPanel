import type { ApiRequestFailureReason } from './types'

/**
 * **The colour of a reason's tag**, as antd's `Tag` names it.
 *
 * The six refusals are not six degrees of one event, so one colour for all of them would say
 * nothing: a blocked account and an expired subscription are the two a subscriber is likely
 * to call about, an unknown identifier is a bad credential, and a Geo-IP or connection-limit
 * refusal is a policy working as intended. The mapping is extracted here rather than left
 * inline in the page for the reason `recordTone` is: every `probe:render` lands before the
 * first answer, so no row is drawn and a wrong mapping would keep compiling silently.
 *
 * The return type names antd's status words, so a colour that is not one of them is a compile
 * error rather than a tag with no class behind it - the mistake the user log made with
 * `'danger'`, which is a `Typography.Text` type and drew grey on a `Tag`.
 */
export const reasonTone = (
  reason: ApiRequestFailureReason,
): 'error' | 'warning' | 'processing' | 'default' => {
  switch (reason) {
    case 'unknown_id':
      return 'error'
    case 'geo_ip_violation':
      return 'warning'
    case 'auth_failed':
      return 'error'
    case 'too_many_connections':
      return 'processing'
    case 'subscription_expired':
      return 'warning'
    case 'user_blocked':
      return 'default'
  }
}
