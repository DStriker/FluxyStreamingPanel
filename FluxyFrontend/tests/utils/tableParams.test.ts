import { describe, expect, it } from 'vitest'
import { enumParam, withFilter } from '../../src/utils/tableParams'
import type { UserStatus } from '../../src/types'

/**
 * The two rules `useServerTable` and every filtered table depend on, and neither is
 * reachable from a probe: both happen inside an event handler, and every render a probe
 * makes lands before the first interaction. The cases that matter are the quiet ones - a
 * filter change that forgot to drop `page`, and an address naming a value no control on the
 * page could have produced.
 */

const STATUSES: UserStatus[] = ['Unregistered', 'Registered', 'Blocked']

describe('withFilter', () => {
  it('sets the filter and drops the page in one address', () => {
    const next = withFilter(new URLSearchParams('page=3&search=foo'), 'status', 'Blocked')

    expect(next.get('status')).toBe('Blocked')
    expect(next.get('search')).toBe('foo')
    expect(next.get('page')).toBeNull()
  })

  it('removes the filter when there is no value, and still drops the page', () => {
    for (const empty of [null, undefined, '']) {
      const next = withFilter(new URLSearchParams('page=4&status=Blocked'), 'status', empty)

      expect(next.get('status')).toBeNull()
      expect(next.get('page')).toBeNull()
    }
  })

  it('leaves the parameters it was not asked about exactly as they were', () => {
    const before = new URLSearchParams('sortBy=ip&sortOrder=asc&pageSize=50')
    const after = withFilter(before, 'status', 'Registered')

    expect(after.get('sortBy')).toBe('ip')
    expect(after.get('sortOrder')).toBe('asc')
    expect(after.get('pageSize')).toBe('50')
    // The argument itself is not mutated: the pages pass the location's params, and a
    // function that edited them in place would be writing to a value React Router owns.
    expect(before.get('status')).toBeNull()
  })
})

describe('enumParam', () => {
  it('answers with the value the address named', () => {
    const params = new URLSearchParams('status=Blocked')

    expect(enumParam(params, 'status', STATUSES)).toBe('Blocked')
  })

  it('answers null for an absent or empty value', () => {
    expect(enumParam(new URLSearchParams(''), 'status', STATUSES)).toBeNull()
    expect(enumParam(new URLSearchParams('status='), 'status', STATUSES)).toBeNull()
  })

  it('answers null for a value of some other set', () => {
    // `Admin` is a role; the status filter is not the control that could have written it.
    expect(enumParam(new URLSearchParams('status=Admin'), 'status', STATUSES)).toBeNull()
    expect(enumParam(new URLSearchParams('status=blocked'), 'status', STATUSES)).toBeNull()
  })

  it('reads the value out of an address that carries more than the filter', () => {
    const params = new URLSearchParams('page=2&status=Registered&search=ann')

    expect(enumParam(params, 'status', STATUSES)).toBe('Registered')
  })
})
