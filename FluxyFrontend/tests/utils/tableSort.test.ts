import { describe, expect, it } from 'vitest'
import { SORT_DIRECTIONS, nextSortDirection } from '../../src/utils/tableSort'

/**
 * The cycle a sortable header walks.
 *
 * This is the pure half of a bug no render can see: a two-entry cycle answers `undefined` after
 * `descend`, the table reads that as "no order" and writes its default back, and a column whose
 * default is descending can therefore never leave it. The assertion is not that the array has
 * three items for their own sake - it is that **every** click produces a defined direction that
 * differs from the one on screen.
 */
describe('nextSortDirection', () => {
  it('answers a defined direction from every state a header can be in', () => {
    for (const current of [null, 'ascend', 'descend'] as const) {
      expect(nextSortDirection(SORT_DIRECTIONS, current)).toBeDefined()
    }
  })

  it('never answers the direction the header is already showing', () => {
    expect(nextSortDirection(SORT_DIRECTIONS, 'ascend')).toBe('descend')
    expect(nextSortDirection(SORT_DIRECTIONS, 'descend')).toBe('ascend')
  })

  it('starts an unsorted column at ascending, as antd does', () => {
    expect(nextSortDirection(SORT_DIRECTIONS, null)).toBe('ascend')
  })

  it('walking from a descending column always changes the request', () => {
    // The regression in one line: three clicks from `descend` must not repeat it.
    const walk: (string | undefined)[] = []
    let current: 'ascend' | 'descend' | null = null

    for (let step = 0; step < 4; step += 1) {
      current = nextSortDirection(SORT_DIRECTIONS, current) ?? null
      walk.push(current ?? 'undefined')
    }

    expect(walk).toEqual(['ascend', 'descend', 'ascend', 'descend'])
  })
})
