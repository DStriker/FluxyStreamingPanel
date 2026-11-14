import { describe, expect, it } from 'vitest'
import { canCreateTicket, canReply } from '../../../src/features/tickets/ticketRules'
import type { Role } from '../../../src/types'

/**
 * The two rules both ticket screens ask: who may open one, and when a reply is possible.
 *
 * Both are mirrored from the server's own gates, and both have one exception worth pinning:
 * the administrator does not create tickets (they answer them), and a closed ticket is the
 * only status that refuses a reply.
 */

const ROLES: readonly Role[] = ['Client', 'Reseller', 'Admin']

describe('canCreateTicket', () => {
  it('allows the two roles that own the account area', () => {
    expect(canCreateTicket('Client')).toBe(true)
    expect(canCreateTicket('Reseller')).toBe(true)
  })

  it('refuses the administrator, who answers tickets rather than opens them', () => {
    expect(canCreateTicket('Admin')).toBe(false)
  })

  it('answers every role without falling through', () => {
    for (const role of ROLES) expect(typeof canCreateTicket(role)).toBe('boolean')
  })
})

describe('canReply', () => {
  it('allows a conversation that is still in progress, read or not', () => {
    expect(canReply('unread')).toBe(true)
    expect(canReply('read')).toBe(true)
  })

  it('refuses a closed ticket', () => {
    expect(canReply('closed')).toBe(false)
  })
})
