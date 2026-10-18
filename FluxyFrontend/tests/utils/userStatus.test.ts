import { describe, expect, it } from 'vitest'
import { combineStatus } from '../../src/utils/userStatus'
import type { UserStatus } from '../../src/types'

/**
 * The whole of `combineStatus` is the tie-breaking of one table, so the tests are that
 * table written out. The case worth more than the rest is `Unregistered` against
 * `Registered`: comparing the enum's numbers (`Unregistered = 0, Registered = 1`) would
 * rank a waiting account above a working one, which is the exact mistake the
 * `RESTRICTIVENESS` map exists to prevent.
 */

const CASES: readonly (readonly [UserStatus, UserStatus, UserStatus])[] = [
  ['Registered', 'Registered', 'Registered'],
  ['Registered', 'Unregistered', 'Unregistered'],
  ['Registered', 'Blocked', 'Blocked'],
  ['Unregistered', 'Registered', 'Unregistered'],
  ['Unregistered', 'Unregistered', 'Unregistered'],
  ['Unregistered', 'Blocked', 'Blocked'],
  ['Blocked', 'Registered', 'Blocked'],
  ['Blocked', 'Unregistered', 'Blocked'],
  ['Blocked', 'Blocked', 'Blocked'],
]

describe('combineStatus', () => {
  it.each(CASES)('combines %s with %s into %s', (own, group, expected) => {
    expect(combineStatus(own, group)).toBe(expected)
  })

  it('ranks an unregistered account as more restricted than a registered one', () => {
    // The one pair the numeric order of the enum gets wrong.
    expect(combineStatus('Unregistered', 'Registered')).toBe('Unregistered')
    expect(combineStatus('Registered', 'Unregistered')).toBe('Unregistered')
  })

  it('does not care which half is which', () => {
    for (const [own, group, expected] of CASES) {
      expect(combineStatus(group, own)).toBe(expected)
    }
  })
})
