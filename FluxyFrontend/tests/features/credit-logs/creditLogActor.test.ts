import { describe, expect, it } from 'vitest'
import {
  creditLogActorName,
  creditLogTone,
} from '../../../src/features/credit-logs/creditLogActor'
import type { CreditLog } from '../../../src/features/credit-logs/types'

/**
 * The two display rules of the credit log that no render reaches: a static render draws no
 * cell at all, and a cell only runs once a row exists.
 *
 * Both have branches that compile whatever they return, so the test states the branch rather
 * than the shape.
 */

const log = (overrides: Partial<CreditLog> = {}): CreditLog => ({
  id: '11111111-1111-1111-1111-111111111111',
  resellerId: '22222222-2222-2222-2222-222222222222',
  resellerUsername: 'reseller',
  issuedById: '33333333-3333-3333-3333-333333333333',
  issuedByUsername: 'root',
  amount: 10,
  description: 'Credits changed: 0 → 10',
  createdAt: '2026-05-01T10:00:00.000Z',
  ...overrides,
})

describe('creditLogActorName', () => {
  it('names the issuer on credit that was issued', () => {
    expect(creditLogActorName(log({ amount: 25, issuedByUsername: 'root' }))).toBe('root')
  })

  it('names the issuer on an admin-panel decrease, because the admin made the change', () => {
    // Every admin-panel row carries `issued_by_id`, whatever the sign: a decrease is still an
    // administrative edit, and the reseller must not be shown as its author.
    expect(
      creditLogActorName(
        log({ amount: -5, issuedById: '33333333-3333-3333-3333-333333333333', issuedByUsername: 'root' }),
      ),
    ).toBe('root')
  })

  it('names the reseller on a negative row whose issuer is absent', () => {
    // A reseller-initiated spend (a future write path) has no issuer at all; the row names the
    // account whose balance moved.
    expect(
      creditLogActorName(
        log({ amount: -5, issuedById: null, issuedByUsername: null, resellerUsername: 'client' }),
      ),
    ).toBe('client')
  })

  it('names the issuer on a create row that started at zero', () => {
    // A zero balance is still a row the acting administrator opened the account with, and the
    // row carries them as its issuer.
    expect(creditLogActorName(log({ amount: 0, issuedByUsername: 'root' }))).toBe('root')
  })

  it('draws an em dash when the issuer is gone, never the reseller', () => {
    // The issuer's foreign key is ON DELETE SET NULL, so the history survives its issuer's
    // deletion - as a dash, not as the reseller's name and not as an empty cell.
    expect(
      creditLogActorName(
        log({ amount: 10, issuedById: null, issuedByUsername: null, resellerUsername: 'client' }),
      ),
    ).toBe('\u2014')
  })

  it('draws an em dash when an issuer identifier arrives without a name', () => {
    expect(
      creditLogActorName(log({ amount: 10, issuedById: '33333333-3333-3333-3333-333333333333', issuedByUsername: null })),
    ).toBe('\u2014')
  })
})

describe('creditLogTone', () => {
  it('is green for credit issued', () => {
    expect(creditLogTone(1)).toBe('success')
    expect(creditLogTone(1000)).toBe('success')
  })

  it('is red for credit spent and for zero', () => {
    // Zero is red rather than neutral on the owner's call: the two tones mean "money in" and
    // "money out or nothing came in", not a three-step scale.
    expect(creditLogTone(0)).toBe('danger')
    expect(creditLogTone(-1)).toBe('danger')
    expect(creditLogTone(-1000)).toBe('danger')
  })
})
