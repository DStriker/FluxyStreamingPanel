import { describe, expect, it } from 'vitest'
import {
  CODE_LENGTH,
  EMAIL_MAX,
  GENERATED_PASSWORD_LENGTH,
  LOGIN_GUARD_MAX_IPS,
  PASSWORD_MAX,
  PASSWORD_MIN,
  USERNAME_MAX,
  USERNAME_MIN,
  generatePassword,
  meetsPasswordComplexity,
} from '../../src/utils/policy'

/**
 * The frontend's half of a two-sided contract: every number here mirrors
 * `RegistrationPolicy`/`LoginGuardPolicy` on the server, and the complexity rule mirrors
 * `char.IsLower`/`IsUpper`/`IsDigit`. Pinning the values makes a change on either side a
 * failing test rather than a form that disagrees with the endpoint behind it.
 */

describe('password complexity', () => {
  it('accepts a password carrying one of each class the server counts', () => {
    expect(meetsPasswordComplexity('Abcdefg1')).toBe(true)
    expect(meetsPasswordComplexity('ZZZZzzzz9')).toBe(true)
  })

  it('refuses a password missing any one class, and ties the reason to the class', () => {
    expect(meetsPasswordComplexity('abcdefg1')).toBe(false)
    expect(meetsPasswordComplexity('ABCDEFG1')).toBe(false)
    expect(meetsPasswordComplexity('Abcdefgh')).toBe(false)
    expect(meetsPasswordComplexity('')).toBe(false)
  })

  it('does not accept a non-ASCII letter as a lower- or upper-case one', () => {
    // The server counts with `char.IsLower`/`IsUpper` on the ASCII range the rule was
    // written for; a Cyrillic letter must not silently satisfy it on one side only.
    expect(meetsPasswordComplexity('абвгдеё1')).toBe(false)
    expect(meetsPasswordComplexity('АБВГДЕЁ1')).toBe(false)
  })
})

describe('generatePassword', () => {
  it('draws the length the button promises', () => {
    expect(generatePassword()).toHaveLength(GENERATED_PASSWORD_LENGTH)
  })

  it('satisfies the rule it exists to satisfy, by construction', () => {
    // Ten naive draws would miss an uppercase letter roughly one time in eleven - the
    // failure mode the three required characters prevent. Repeated draws catch a
    // regression that dropped one of them.
    for (let i = 0; i < 50; i++) {
      expect(meetsPasswordComplexity(generatePassword())).toBe(true)
    }
  })

  it('uses only the letters and digits the generator documents', () => {
    for (let i = 0; i < 20; i++) {
      expect(generatePassword()).toMatch(/^[A-Za-z0-9]+$/)
    }
  })

  it('does not answer the same value twice', () => {
    // 62^10 possibilities, so a collision here is not a thing that happens by accident.
    const draws = new Set(Array.from({ length: 10 }, () => generatePassword()))

    expect(draws.size).toBe(10)
  })
})

describe('the mirrored bounds', () => {
  it('keeps the numbers the server applies', () => {
    expect(USERNAME_MIN).toBe(5)
    expect(USERNAME_MAX).toBe(20)
    expect(PASSWORD_MIN).toBe(8)
    expect(PASSWORD_MAX).toBe(100)
    expect(EMAIL_MAX).toBe(254)
    expect(CODE_LENGTH).toBe(6)
    expect(LOGIN_GUARD_MAX_IPS).toBe(5)
    expect(GENERATED_PASSWORD_LENGTH).toBe(10)
  })
})
