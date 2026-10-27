import { describe, expect, it } from 'vitest'
import {
  categoriesOf,
  filterByCategory,
  idsOf,
  isKnownCategory,
  moveSelectedDown,
  moveSelectedUp,
  sameOrder,
  sortByName,
} from '../../../src/features/bouquets/contentOrder'
import type { BouquetMember } from '../../../src/features/bouquets/types'

/**
 * The list arithmetic behind the content manager's three arrangement buttons.
 *
 * These are the rules the screen cannot show and the server cannot check: a group of ticked
 * rows has to travel together rather than each row overtaking the last, a group at the edge
 * has to stay put instead of disappearing past it, and the A-Z sort has to compare names the
 * way a person reads them rather than the way a code point table is ordered.
 */
const ids = (...values: string[]): string[] => values

describe('moveSelectedUp', () => {
  it('moves one row up one position', () => {
    expect(moveSelectedUp(ids('a', 'b', 'c'), new Set(['b']))).toEqual(['b', 'a', 'c'])
  })

  it('moves a group as a block, keeping its own order', () => {
    // The bug this pins: moving each ticked row up in turn would answer ['c','b','a','d'] -
    // the block reversed - which is not what a selection means.
    expect(moveSelectedUp(ids('a', 'b', 'c', 'd'), new Set(['b', 'c']))).toEqual([
      'b',
      'c',
      'a',
      'd',
    ])
  })

  it('does nothing when the selection is already at the top', () => {
    const before = ids('a', 'b', 'c')

    expect(moveSelectedUp(before, new Set(['a']))).toBe(before)
    expect(moveSelectedUp(before, new Set(['a', 'b']))).toBe(before)
  })

  it('returns the same array when nothing is selected', () => {
    const before = ids('a', 'b')

    expect(moveSelectedUp(before, new Set())).toBe(before)
  })

  it('returns the same array for a list that cannot move', () => {
    const single = ids('a')

    expect(moveSelectedUp(single, new Set(['a']))).toBe(single)
  })

  it('leaves ids that are not in the list alone', () => {
    expect(moveSelectedUp(ids('a', 'b'), new Set(['zzz']))).toEqual(['a', 'b'])
  })
})

describe('moveSelectedDown', () => {
  it('moves one row down one position', () => {
    expect(moveSelectedDown(ids('a', 'b', 'c'), new Set(['b']))).toEqual(['a', 'c', 'b'])
  })

  it('moves a group as a block, keeping its own order', () => {
    expect(moveSelectedDown(ids('a', 'b', 'c', 'd'), new Set(['b', 'c']))).toEqual([
      'a',
      'd',
      'b',
      'c',
    ])
  })

  it('does nothing when the selection is already at the bottom', () => {
    const before = ids('a', 'b', 'c')

    expect(moveSelectedDown(before, new Set(['c']))).toBe(before)
    expect(moveSelectedDown(before, new Set(['b', 'c']))).toBe(before)
  })

  it('is the mirror of the up move, not its reversal', () => {
    const before = ids('a', 'b', 'c', 'd')

    expect(moveSelectedDown(moveSelectedUp(before, new Set(['c', 'd'])), new Set(['c', 'd']))).toEqual(
      before,
    )
  })
})

describe('sortByName', () => {
  const member = (id: string, name: string): BouquetMember => ({
    id,
    name,
    categoryId: 'category',
    categoryName: 'Category',
  })

  it('orders by name for the reader, ignoring case', () => {
    const sorted = sortByName(
      [member('1', 'beta'), member('2', 'Alpha'), member('3', 'gamma')],
      'en',
    )

    expect(sorted.map((entry) => entry.name)).toEqual(['Alpha', 'beta', 'gamma'])
  })

  it('puts a Cyrillic name beside its own letter rather than after every Latin one', () => {
    // `sensitivity: 'base'` would answer ['Ёлка', 'Елка'] here - it treats Ё and Е as the
    // same letter, so the order falls through to the id tie-break and a Russian reader is
    // shown the alphabet backwards. `accent` keeps the diaeresis as a secondary difference.
    const sorted = sortByName([member('1', 'Ёлка'), member('2', 'Елка')], 'ru')

    expect(sorted.map((entry) => entry.name)).toEqual(['Елка', 'Ёлка'])
  })

  it('breaks a tie by identifier, so equal names still have one order', () => {
    const sorted = sortByName([member('b', 'Same'), member('a', 'Same')], 'en')

    expect(sorted.map((entry) => entry.id)).toEqual(['a', 'b'])
  })

  it('does not touch the array it was given', () => {
    const before = [member('1', 'b'), member('2', 'a')]
    const copy = [...before]

    sortByName(before, 'en')

    expect(before).toEqual(copy)
  })
})

describe('idsOf and sameOrder', () => {
  const item = (id: string, name: string): BouquetMember => ({
    id,
    name,
    categoryId: 'category',
    categoryName: 'Category',
  })

  it('reads the identifiers in order', () => {
    expect(idsOf([item('b', 'B'), item('a', 'A')])).toEqual(['b', 'a'])
  })

  it('compares order, not membership', () => {
    expect(sameOrder(['a', 'b'], ['a', 'b'])).toBe(true)
    expect(sameOrder(['a', 'b'], ['b', 'a'])).toBe(false)
    expect(sameOrder(['a'], ['a', 'b'])).toBe(false)
  })
})

describe('the category filter', () => {
  const member = (id: string, name: string, categoryId: string, categoryName: string): BouquetMember => ({
    id,
    name,
    categoryId,
    categoryName,
  })

  const news = (id: string, name: string) => member(id, name, 'cat-news', 'News')
  const sport = (id: string, name: string) => member(id, name, 'cat-sport', 'Sport')

  it('offers the categories of both lists together', () => {
    // The regression: this is the state *after* moving every News item out of the bouquet.
    // Reading the options off the member list alone would answer ['Sport'] and the News
    // option would vanish from the select the operator was still using.
    const members = [sport('s1', 'Sport One')]
    const available = [news('n1', 'News One'), news('n2', 'News Two')]

    expect(categoriesOf([...members, ...available]).map((entry) => entry.name)).toEqual([
      'News',
      'Sport',
    ])
  })

  it('keeps the category offered when its list is empty', () => {
    const available = [news('n1', 'News One')]

    // No member holds News any more, and the filter still has to offer it.
    expect(categoriesOf([...[], ...available]).map((entry) => entry.id)).toEqual(['cat-news'])
  })

  it('orders the options by name for the reader', () => {
    const all = [sport('s1', 'S'), news('n1', 'N')]

    expect(categoriesOf(all).map((entry) => entry.name)).toEqual(['News', 'Sport'])
  })

  it('narrows to one category and leaves the rest alone', () => {
    const all = [news('n1', 'One'), sport('s1', 'Two'), news('n2', 'Three')]

    expect(filterByCategory(all, 'cat-news').map((entry) => entry.id)).toEqual(['n1', 'n2'])
    expect(filterByCategory(all, null).map((entry) => entry.id)).toEqual(['n1', 's1', 'n2'])
  })

  it('does not touch the list it was given', () => {
    const all = [news('n1', 'One')]
    const copy = [...all]

    filterByCategory(all, 'cat-news')

    expect(all).toEqual(copy)
  })

  it('treats a category that no longer exists as no filter at all', () => {
    // Every item of it was deleted, so the select has no option behind the value any more.
    // Keeping the filter would hide the list behind something the operator cannot clear.
    const options = categoriesOf([news('n1', 'One')])

    expect(isKnownCategory(options, null)).toBe(true)
    expect(isKnownCategory(options, 'cat-news')).toBe(true)
    expect(isKnownCategory(options, 'cat-gone')).toBe(false)
  })
})
