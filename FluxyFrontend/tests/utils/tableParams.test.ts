import { describe, expect, it } from 'vitest'
import { enumParam, listParam, withFilter, withFilterList } from '../../src/utils/tableParams'
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

describe('withFilterList', () => {
  it('writes one parameter per value and drops the page in one address', () => {
    const next = withFilterList(new URLSearchParams('page=3&search=foo'), 'userIds', [
      '11111111-1111-1111-1111-111111111111',
      '22222222-2222-2222-2222-222222222222',
    ])

    expect(next.getAll('userIds')).toEqual([
      '11111111-1111-1111-1111-111111111111',
      '22222222-2222-2222-2222-222222222222',
    ])
    expect(next.get('search')).toBe('foo')
    expect(next.get('page')).toBeNull()
  })

  it('replaces the values already in the address rather than appending to them', () => {
    const before = new URLSearchParams('userIds=old&userIds=older&page=2')
    const after = withFilterList(before, 'userIds', ['new'])

    expect(after.getAll('userIds')).toEqual(['new'])
    expect(after.get('page')).toBeNull()
    // The argument itself is not mutated: the pages pass the location's params.
    expect(before.getAll('userIds')).toEqual(['old', 'older'])
  })

  it('removes the key when the list is empty, and still drops the page', () => {
    const next = withFilterList(new URLSearchParams('page=4&recordTypes=login&recordTypes=logout'), 'recordTypes', [])

    expect(next.get('recordTypes')).toBeNull()
    expect(next.get('page')).toBeNull()
  })

  it('leaves the parameters it was not asked about exactly as they were', () => {
    const before = new URLSearchParams('sortBy=recordType&sortOrder=asc&pageSize=50')
    const after = withFilterList(before, 'recordTypes', ['login'])

    expect(after.get('sortBy')).toBe('recordType')
    expect(after.get('sortOrder')).toBe('asc')
    expect(after.get('pageSize')).toBe('50')
  })
})

describe('listParam', () => {
  const RECORD_TYPES = ['login', 'logout'] as const

  it('reads every value of a repeated parameter', () => {
    const params = new URLSearchParams('recordTypes=login&recordTypes=logout')

    expect(listParam(params, 'recordTypes', RECORD_TYPES)).toEqual(['login', 'logout'])
  })

  it('answers an empty list for an absent or empty parameter', () => {
    expect(listParam(new URLSearchParams(''), 'recordTypes', RECORD_TYPES)).toEqual([])
    expect(listParam(new URLSearchParams('recordTypes='), 'recordTypes', RECORD_TYPES)).toEqual([])
  })

  it('drops an element outside the fixed set, keeping the ones that are in it', () => {
    const params = new URLSearchParams('recordTypes=login&recordTypes=nope&recordTypes=logout')

    expect(listParam(params, 'recordTypes', RECORD_TYPES)).toEqual(['login', 'logout'])
  })

  it('reads any repeated parameter through a predicate, dropping what it refuses', () => {
    const params = new URLSearchParams(
      'userIds=11111111-1111-1111-1111-111111111111&userIds=not-a-uuid',
    )
    const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

    expect(listParam(params, 'userIds', (value) => uuid.test(value))).toEqual([
      '11111111-1111-1111-1111-111111111111',
    ])
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
