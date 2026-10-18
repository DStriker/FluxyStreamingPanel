import { describe, expect, it } from 'vitest'
import { LOGIN_GUARD_MAX_IPS } from '../../../src/features/profile/loginGuardPolicy'

/**
 * The frontend's half of a two-sided contract: the number mirrors `LoginGuardPolicy` on
 * the server, so pinning it makes a change on either side a failing test rather than a
 * form that disagrees with the endpoint behind it.
 */

describe('LOGIN_GUARD_MAX_IPS', () => {
  it('keeps the number the server applies', () => {
    expect(LOGIN_GUARD_MAX_IPS).toBe(5)
  })
})
