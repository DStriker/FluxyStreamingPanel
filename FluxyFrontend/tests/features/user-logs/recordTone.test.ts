import { describe, expect, it } from 'vitest'
import { recordTone } from '../../../src/features/user-logs/recordTone'
import { USER_LOG_RECORD_TYPES } from '../../../src/features/user-logs/recordTypes'

/**
 * The two row rules a static render cannot reach: a render lands before the first answer, so
 * no cell `render` ever runs - and the record-type vocabulary, which the address read-back
 * and the filter picker both build their membership test from.
 */
describe('recordTone', () => {
  it('is green for an attempt that worked and red for a refusal', () => {
    expect(recordTone({ isSuccess: true })).toBe('success')
    // `'error'` is the red `Tag` status name; `'danger'` is a `Typography.Text` type and
    // renders a `Tag` with no colour class at all.
    expect(recordTone({ isSuccess: false })).toBe('error')
  })
})

describe('USER_LOG_RECORD_TYPES', () => {
  it('names all ten wire values, in the server enum order', () => {
    expect([...USER_LOG_RECORD_TYPES]).toEqual([
      'registration_submit',
      'registration_confirm',
      'login',
      'password_reset_request',
      'password_reset_confirm',
      'username_change',
      'email_change',
      'password_change',
      'login_guard_change',
      'logout',
    ])
  })

  it('has no duplicates - a repeated value would draw one option twice', () => {
    expect(new Set(USER_LOG_RECORD_TYPES).size).toBe(USER_LOG_RECORD_TYPES.length)
  })
})
