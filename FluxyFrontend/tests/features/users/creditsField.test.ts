import { describe, expect, it } from 'vitest'
import { isCreditAmount } from '../../../src/features/users/creditsField'

/**
 * The balance rule the form's validator states, pinned as a function.
 *
 * The negative branch is the one a browser cannot easily reach: `InputNumber` has `min={0}`
 * and clamps the typed value before submit, so the validator's own refusal would be
 * unreachable through the control - which is exactly why the rule is written as a function
 * and pinned here instead of being trusted to the widget.
 */
describe('isCreditAmount', () => {
  it('accepts a whole number of zero or more, zero included', () => {
    expect(isCreditAmount(0)).toBe(true)
    expect(isCreditAmount(1)).toBe(true)
    expect(isCreditAmount(1000)).toBe(true)
  })

  it('refuses a negative number', () => {
    expect(isCreditAmount(-1)).toBe(false)
    expect(isCreditAmount(-1000)).toBe(false)
  })

  it('refuses a fraction', () => {
    expect(isCreditAmount(1.5)).toBe(false)
    expect(isCreditAmount(-0.5)).toBe(false)
  })

  it('accepts nothing at all, because an empty field is not a value yet', () => {
    // The form starts with `0`, and a cleared `InputNumber` puts `null` in the store; the
    // validator must not turn "nothing typed" into a red sentence before the visitor has
    // typed anything.
    expect(isCreditAmount(null)).toBe(true)
    expect(isCreditAmount(undefined)).toBe(true)
  })
})
