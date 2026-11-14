import { describe, expect, it } from 'vitest'
import {
  isTicketUnread,
  ticketStatusLabelKey,
  ticketTagColor,
} from '../../../src/features/tickets/ticketStatus'
import type { TicketStatus } from '../../../src/features/tickets/types'

/**
 * The ticket status as the screen reads it: one active state, two quiet ones, and a label
 * key per wire value.
 *
 * The pair worth pinning is `unread`/`read`: they are viewer relative on the server, and a
 * frontend that coloured `read` like `unread` would make an opened conversation look like a
 * new one down the whole admin list. The second pair is `closed`: it is quiet, and it is
 * never counted as unread even when the last message has not been read.
 */

const STATUSES: readonly TicketStatus[] = ['unread', 'read', 'closed']

describe('ticketTagColor', () => {
  it('draws the unread conversation as the active state', () => {
    expect(ticketTagColor('unread')).toBe('processing')
  })

  it('keeps read and closed neutral, so a finished thread does not read as new', () => {
    expect(ticketTagColor('read')).toBe('default')
    expect(ticketTagColor('closed')).toBe('default')
  })

  it('answers one of the two colours for every wire value', () => {
    for (const status of STATUSES) {
      expect(['processing', 'default']).toContain(ticketTagColor(status))
    }
  })
})

describe('ticketStatusLabelKey', () => {
  it('names the locale key of the wire value it was given', () => {
    expect(ticketStatusLabelKey('unread')).toBe('tickets.statuses.unread')
    expect(ticketStatusLabelKey('read')).toBe('tickets.statuses.read')
    expect(ticketStatusLabelKey('closed')).toBe('tickets.statuses.closed')
  })
})

describe('isTicketUnread', () => {
  it('is true for the unread state alone', () => {
    expect(isTicketUnread('unread')).toBe(true)
  })

  it('is false for a conversation that was read or is over', () => {
    expect(isTicketUnread('read')).toBe(false)
    expect(isTicketUnread('closed')).toBe(false)
  })
})
