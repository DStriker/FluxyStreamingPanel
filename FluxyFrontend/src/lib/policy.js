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
 * At least one lowercase letter, one uppercase letter and one digit - the same three
 * rules the server counts with `char.IsLower` / `IsUpper` / `IsDigit`, expressed for
 * a JavaScript string.
 *
 * The lookaheads are separate on purpose: one combined pattern would reject a password
 * for the wrong reason and could not say which rule was broken.
 */
export const HAS_LOWERCASE = /[a-z]/
export const HAS_UPPERCASE = /[A-Z]/
export const HAS_DIGIT = /\d/

export const meetsPasswordComplexity = (value) =>
  HAS_LOWERCASE.test(value) && HAS_UPPERCASE.test(value) && HAS_DIGIT.test(value)