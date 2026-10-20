import { describe, expect, it } from 'vitest'
import { MIN_COLUMN_WIDTH, actionsColumnWidth, distributeWidths } from '../../src/utils/tableWidths'

/**
 * The width an actions column is given, which is the width of the buttons it holds.
 *
 * The numbers are the ones the browser was measured at: an icon-only small button is a
 * square of 24 pixels, `RowActions` puts four pixels between two of them, and a table cell
 * has sixteen pixels of padding on each side. The cases below are the four, five and three
 * button clusters the administrator tables draw - and the one that would be a mistake, a
 * column of no buttons at all, which still has to hold a cell.
 */
describe('actionsColumnWidth', () => {
  it('is the buttons, the gaps between them and the cell padding', () => {
    expect(actionsColumnWidth(1)).toBe(56)
    expect(actionsColumnWidth(3)).toBe(112)
    expect(actionsColumnWidth(4)).toBe(140)
    expect(actionsColumnWidth(5)).toBe(168)
  })

  it('grows by one button and its gap at a time', () => {
    expect(actionsColumnWidth(5) - actionsColumnWidth(4)).toBe(28)
    expect(actionsColumnWidth(4) - actionsColumnWidth(3)).toBe(28)
  })

  it('is a cell of its own even with no buttons in it', () => {
    expect(actionsColumnWidth(0)).toBe(32)
  })
})

/**
 * The room between the columns divided again, which is what keeps a table from being wider
 * than the box it is drawn in.
 *
 * The cases below are the three that matter: a room that is bigger than the columns need
 * (nothing is squeezed), a room that is smaller (the columns are divided in the proportions
 * they were arranged in), and a room so small that some of them would fall below the floor
 * (they stop at the floor, and the room they no longer need goes to the ones that can take
 * it - which is the part that keeps the sum at the width of the room).
 */
describe('distributeWidths', () => {
  it('gives the columns back their own widths when the room is theirs', () => {
    expect(distributeWidths([100, 300, 200], 600, MIN_COLUMN_WIDTH)).toEqual([100, 300, 200])
  })

  it('loses nothing to rounding when the room is theirs', () => {
    // The share of the third column is 230 * (860 / 860), which is 229.999... in
    // floating point - a table cut to whole pixels would be a pixel narrower than the
    // arrangement it was given, and the browser would stretch that pixel into the
    // actions column.
    const widths = distributeWidths([170, 150, 120, 190, 230], 860, MIN_COLUMN_WIDTH)

    expect(widths).toEqual([170, 150, 120, 190, 230])
    expect(widths.reduce((sum, width) => sum + width, 0)).toBe(860)
  })

  it('divides the room in the proportions the columns were arranged in', () => {
    expect(distributeWidths([200, 400, 200], 400, MIN_COLUMN_WIDTH)).toEqual([100, 200, 100])
  })

  it('stops a column at the floor and gives its room to the others', () => {
    const widths = distributeWidths([100, 300, 200], 320, MIN_COLUMN_WIDTH)

    expect(widths[0]).toBe(MIN_COLUMN_WIDTH)
    expect(widths.reduce((sum, width) => sum + width, 0)).toBe(320)
  })

  it('never hands out less than the floor, however small the room is', () => {
    const widths = distributeWidths([100, 300, 200], 10, MIN_COLUMN_WIDTH)

    expect(widths.every((width) => width >= MIN_COLUMN_WIDTH)).toBe(true)
  })
})
