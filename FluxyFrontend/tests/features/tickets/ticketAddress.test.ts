import { describe, expect, it } from 'vitest'
import { createdTicketPath } from '../../../src/features/tickets/ticketAddress'

/**
 * The address a just-created ticket's conversation is opened at.
 *
 * The create endpoint's `Location` is `/auth/tickets/{id}` - the API's path, not a route this
 * router has. Following it directly was a real 404 in a runtime check, so the id is what
 * travels and the caller's own list path is what the address is built from.
 */

const ID = '67cc79d1-eef6-4de6-b4cc-e9f8022268bf'

describe('createdTicketPath', () => {
  it('rebuilds the address under the caller’s own list path', () => {
    expect(createdTicketPath('/client/tickets', `/auth/tickets/${ID}`)).toBe(
      `/client/tickets/${ID}`,
    )
    expect(createdTicketPath('/reseller/tickets', `/auth/tickets/${ID}`)).toBe(
      `/reseller/tickets/${ID}`,
    )
  })

  it('drops a query string and a hash from the header', () => {
    expect(createdTicketPath('/client/tickets', `/auth/tickets/${ID}?from=create`)).toBe(
      `/client/tickets/${ID}`,
    )
    expect(createdTicketPath('/client/tickets', `/auth/tickets/${ID}#top`)).toBe(
      `/client/tickets/${ID}`,
    )
  })

  it('ignores a trailing slash', () => {
    expect(createdTicketPath('/client/tickets', `/auth/tickets/${ID}/`)).toBe(
      `/client/tickets/${ID}`,
    )
  })

  it('answers null with no header and no list path, so the caller stays on the list', () => {
    expect(createdTicketPath('/client/tickets', null)).toBeNull()
    expect(createdTicketPath(null, `/auth/tickets/${ID}`)).toBeNull()
    expect(createdTicketPath('/client/tickets', '')).toBeNull()
  })
})
