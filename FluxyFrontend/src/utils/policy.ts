/**
 * The limits the registration form enforces, mirroring
 * `RegistrationPolicy` on the server so that a value the server would reject is caught
 * before the request is sent.
 *
 * The server stays the authority - it applies the same numbers and a client is free to
 * ignore whatever is advertised - but agreeing on them means the visitor sees the
 * problem under the offending field instead of in a toast about "invalid values".
 */

export const USERNAME_MIN = 5
export const USERNAME_MAX = 20
export const PASSWORD_MIN = 8
export const PASSWORD_MAX = 100
export const EMAIL_MAX = 254

/** Digits of the confirmation code the server mails out. */
export const CODE_LENGTH = 6

/**
 * How many networks one login guard may allow. Mirrors `LoginGuardPolicy.MaxAllowedIps`
 * on the server: the card stops at five, the server refuses a sixth, and the two have to
 * agree or the sixth address fails with a toast instead of never being typed.
 */
export const LOGIN_GUARD_MAX_IPS = 5

/**
 * At least one lowercase letter, one uppercase letter and one digit - the same three
 * rules the server counts with `char.IsLower` / `IsUpper` / `IsDigit`, expressed for
 * a JavaScript string.
 *
 * The lookaheads are separate on purpose: one combined pattern would reject a password
 * for the wrong reason and could not say which rule was broken.
 */
const HAS_LOWERCASE = /[a-z]/
const HAS_UPPERCASE = /[A-Z]/
const HAS_DIGIT = /\d/

export const meetsPasswordComplexity = (value: string): boolean =>
  HAS_LOWERCASE.test(value) && HAS_UPPERCASE.test(value) && HAS_DIGIT.test(value)

/**
 * The character classes a generated password is drawn from, and the length the admin
 * form's generate button produces. The length is that button's contract with whoever
 * clicks it, not a server rule - the server only counts the three classes above, which
 * this carries by construction at any length.
 */
const LOWERCASE = 'abcdefghijklmnopqrstuvwxyz'
const UPPERCASE = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'
const DIGITS = '0123456789'
const ALL_CHARACTERS = LOWERCASE + UPPERCASE + DIGITS

export const GENERATED_PASSWORD_LENGTH = 10

/**
 * A uniformly chosen index in `[0, bound)` from the browser's CSPRNG.
 *
 * Rejection sampling rather than a bare `% bound`: 2^32 is not a multiple of 62, so a
 * plain modulo would make the first few characters fractionally more likely than the
 * rest. Correct costs one loop here and is the whole point of the button.
 */
const randomIndex = (bound: number): number => {
  const accepted = Math.floor(0x100000000 / bound) * bound
  const draws = new Uint32Array(1)
  let value = 0
  do {
    crypto.getRandomValues(draws)
    value = draws[0] ?? 0
  } while (value >= accepted)
  return value % bound
}

/**
 * Every character of `pool` once, without replacement, in random order - a Fisher-Yates
 * expressed on a string: at each step every remaining character is equally likely to be
 * the next one drawn.
 */
const shuffle = (pool: string): string => {
  let remaining = pool
  let result = ''
  while (remaining.length > 0) {
    const index = randomIndex(remaining.length)
    result += remaining.charAt(index)
    remaining = remaining.slice(0, index) + remaining.slice(index + 1)
  }
  return result
}

/**
 * A password worth handing to a new account: `GENERATED_PASSWORD_LENGTH` letters and
 * digits carrying one of each class `meetsPasswordComplexity` counts - so it passes the
 * rule by construction rather than by luck (ten naive draws miss an uppercase letter
 * about one time in eleven, which is a button failing its own validator).
 *
 * `crypto.getRandomValues`, not `Math.random`: the second is documented as not
 * cryptographically secure, which is exactly the property this button is selling. It
 * lives beside the rule it satisfies on purpose - a rule that ever grows a fourth class
 * breaks this function in the same file, where the two have to be read together.
 */
export const generatePassword = (): string => {
  const required =
    LOWERCASE.charAt(randomIndex(LOWERCASE.length)) +
    UPPERCASE.charAt(randomIndex(UPPERCASE.length)) +
    DIGITS.charAt(randomIndex(DIGITS.length))
  let value = required
  while (value.length < GENERATED_PASSWORD_LENGTH) {
    value += ALL_CHARACTERS.charAt(randomIndex(ALL_CHARACTERS.length))
  }
  return shuffle(value)
}