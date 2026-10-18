/**
 * How many networks one login guard may allow. Mirrors `LoginGuardPolicy.MaxAllowedIps`
 * on the server: the card stops at five, the server refuses a sixth, and the two have to
 * agree or the sixth address fails with a toast instead of never being typed.
 *
 * It lives in this feature rather than in `src/utils` because the two flows that draw
 * the guard - the profile's own card and the administrator's account form through
 * `LoginGuardFields` - are this feature's components.
 */
export const LOGIN_GUARD_MAX_IPS = 5
