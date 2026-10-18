import { describe, expect, it } from 'vitest'
import { ApiError } from '../../src/utils/http'
import { tableResults } from '../../src/utils/tableResults'

describe('tableResults', () => {
  it('passes the rows and the busy flag through, with no failure to show', () => {
    const data = { items: [1, 2, 3], total: 3 }
    const result = tableResults({ data, isError: false, error: null, isFetching: true })

    expect(result.data).toBe(data)
    expect(result.loading).toBe(true)
    expect(result.loadError).toBeNull()
  })

  it('resolves an API refusal through its code, not through the server sentence', () => {
    const error = new ApiError({ code: 'invalid_credentials', message: 'Wrong.', status: 401 })
    const result = tableResults({ data: undefined, isError: true, error, isFetching: false })

    expect(result.loadError).not.toBeNull()
    expect(result.loadError).not.toBe('invalid_credentials')
    expect(result.loadError).not.toBe('Wrong.')
  })

  it('falls back to the message of an error that carries no code', () => {
    const result = tableResults({
      data: undefined,
      isError: true,
      error: new Error('socket closed'),
      isFetching: false,
    })

    expect(result.loadError).toBe('socket closed')
  })

  it('keeps the failure visible while its retry is in flight', () => {
    const error = new ApiError({ code: 'invalid_credentials', message: 'Wrong.', status: 401 })
    const result = tableResults({ data: undefined, isError: true, error, isFetching: true })

    expect(result.loadError).not.toBeNull()
    expect(result.loading).toBe(true)
  })
})
