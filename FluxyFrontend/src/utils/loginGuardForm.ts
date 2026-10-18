import type { LoginGuardSettings } from '../types'

/**
 * What the five guard fields collect. The names spell exactly what the server may reject -
 * `allowedIps`, not `ips` - because a rejection the form has no input for arrives as a bare
 * toast instead of a reason under the input.
 *
 * Deliberately *not* including `currentPassword`: that field belongs to the profile's
 * confirmation step and to nothing else. The administrator editing somebody else's guard
 * has no password of that account to type, and a shared shape that carried it would force
 * the admin form to render a field whose value the request must never contain.
 */
export interface GuardFieldValues {
  geoProtectionEnabled: boolean
  bindSessionToIp: boolean
  allowedIps: string[]
  /** ISO 3166-1 alpha-2 upper case, or the empty string for "no restriction". */
  allowedCountry: string
  allowedAutonomousSystemNumber: number | null
}

/**
 * The stored guard spelled as form content: `null` where the contract has `null`, and the
 * empty string where a field has nothing in it yet - neither an `Input` nor a `Select` can
 * hold `null`, so the translation happens on the way in and back on the way out, in exactly
 * these two places.
 *
 * The country is upper-cased on the way in for the reason the selector can only draw what
 * its options list holds: a stored `ru` has no option, and a `Select` asked for a value with
 * no option shows *nothing selected* - which reads as "no country is set" over an account
 * that has one, until a save writes `null` over it.
 */
export const guardToFormValues = (guard: LoginGuardSettings): GuardFieldValues => ({
  geoProtectionEnabled: guard.geoProtectionEnabled,
  bindSessionToIp: guard.bindSessionToIp,
  allowedIps: guard.allowedIps,
  allowedCountry: (guard.allowedCountry ?? '').toUpperCase(),
  allowedAutonomousSystemNumber: guard.allowedAutonomousSystemNumber,
})

/**
 * The form's content spelled back as the contract: tags trimmed and emptied ones dropped,
 * the country upper-cased or `null`, and an absent switch read as `false` rather than as
 * `undefined` - the contract's two switches are non-nullable, and `null` there would be a
 * deserialization refusal rather than a default.
 *
 * Used by both flows on purpose: one place where "what the visitor sees" becomes "what the
 * server accepts" is one place to get wrong, and the two call sites would otherwise drift
 * apart exactly the way two hand-written copies do.
 */
export const formValuesToGuard = (values: Partial<GuardFieldValues>): LoginGuardSettings => ({
  geoProtectionEnabled: values.geoProtectionEnabled ?? false,
  bindSessionToIp: values.bindSessionToIp ?? false,
  allowedIps: (values.allowedIps ?? [])
    .map((entry) => entry.trim())
    .filter((entry) => entry !== ''),
  allowedCountry: values.allowedCountry?.trim()
    ? values.allowedCountry.trim().toUpperCase()
    : null,
  allowedAutonomousSystemNumber: values.allowedAutonomousSystemNumber ?? null,
})
