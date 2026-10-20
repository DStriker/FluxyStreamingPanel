/**
 * The width rules the tables share.
 *
 * Three of them are about a column the visitor may drag, and one is about a column the
 * visitor may not: the floor and the ceiling of a drag, the width of the buttons an actions
 * column holds, and the padding a cell adds around them.
 *
 * They are here rather than beside the table frame because they are numbers and a sum, not
 * a component: the frame reads them, the pages read them, and the hook that drags a column
 * reads them - and none of those three is the owner of the others.
 */

/**
 * The floor and the ceiling of a dragged column.
 *
 * The floor is a header width rather than zero: a column narrowed to nothing is a column
 * whose resize handle can no longer be reached to widen it again, and the handle lives at
 * the right edge of the cell. The ceiling exists so a drag cannot produce a table wider
 * than any screen could show - 1200 per column is already past that.
 *
 * The same floor is what a *squeezed* column stops at, for a different reason: a column
 * the screen made narrower has to keep the name of the column in it. Ninety pixels are the
 * cell's own padding and a word besides, which is what a heading is made of - a column of
 * forty pixels has a letter in it, not a name. The table's own guaranteed width is the sum
 * of those floors, and that sum is what decides whether a table fits the screen it is on
 * or has to scroll inside its card.
 */
export const MIN_COLUMN_WIDTH = 90
export const MAX_COLUMN_WIDTH = 1200

/** An icon-only small button is a square of antd's `controlHeightSM`. */
export const ROW_ACTION_BUTTON_WIDTH = 24

/** The gap `RowActions` puts between two buttons of a cluster. */
export const ROW_ACTION_GAP = 4

/** A table cell's padding on each side. */
export const TABLE_CELL_PADDING = 16

/**
 * The width an actions column needs for the buttons it holds.
 *
 * Three numbers, and all three are somebody else's: an icon-only small button is a square
 * of antd's `controlHeightSM` (24 pixels), `RowActions` puts four pixels between two of
 * them, and a table cell has sixteen pixels of padding on each side. A cluster of `count`
 * buttons is therefore `count * 24 + (count - 1) * 4` wide, and the column that holds it
 * needs the cell's own padding besides.
 *
 * The point of the number is that the column is **exactly** as wide as its content. A
 * column wider than the buttons in it is a column of dead space, and dead space at the end
 * of a row is the part of a table a visitor reads as "something is missing here" - and the
 * thing that is missing is nothing at all.
 *
 * The width is a starting point like every other column's: the visitor may still drag it,
 * and the reset button gives it this one back.
 */
export const actionsColumnWidth = (count: number): number =>
  count * ROW_ACTION_BUTTON_WIDTH +
  Math.max(count - 1, 0) * ROW_ACTION_GAP +
  TABLE_CELL_PADDING * 2

/**
 * The width each column takes when the room between them has to be divided.
 *
 * The columns keep the proportions the visitor arranged them in, as far as that is possible:
 * the room is divided between them in the ratio of their widths. A column whose share would
 * fall below the floor is given the floor instead - and the room it no longer needs goes to
 * the columns that can still take it, which is the part that matters: without it the sum of
 * the shares is *larger* than the room, and a table wider than the box it is drawn in is the
 * one thing this whole exercise exists to prevent.
 *
 * The result sums to the room exactly, or to the sum of the floors when the room is smaller
 * than that - which is the table's own guaranteed width, and the reason a narrow screen
 * gets a scrolling table rather than a table of unreadable slivers.
 */
export const distributeWidths = (widths: number[], room: number, floor: number): number[] => {
  const result = widths.map(() => 0)

  // The columns that still have room to be given, each with the weight it is divided by.
  let free = widths.map((weight, index) => ({ index, weight }))
  let remaining = room

  while (free.length > 0) {
    const total = free.reduce((sum, entry) => sum + entry.weight, 0)
    const shares = free.map((entry) =>
      total === 0 ? remaining / free.length : (entry.weight / total) * remaining,
    )

    // The first column that cannot take its share: it takes the floor, and the room it no
    // longer needs is divided between the others on the next turn.
    const tooNarrow = shares.findIndex((share) => share < floor)

    if (tooNarrow === -1) {
      // The whole of the room is divided here, and the sum has to come out as the room
      // exactly: a table a pixel narrower than the box it is in is stretched by the
      // browser, and the column that grows in that stretch is the one whose width was
      // measured - the actions column. So the shares are cut into whole pixels and the
      // pixels the cutting leaves over go to the columns with the largest remainders.
      const whole = shares.map((share) => Math.floor(share))
      const leftover = remaining - whole.reduce((sum, width) => sum + width, 0)
      const byRemainder = shares
        .map((share, position) => ({ position, remainder: share - (whole[position] ?? 0) }))
        .sort((one, other) => other.remainder - one.remainder)

      for (let index = 0; index < leftover; index += 1) {
        const entry = byRemainder[index]
        if (!entry) break
        whole[entry.position] = (whole[entry.position] ?? 0) + 1
      }

      free.forEach((entry, position) => {
        result[entry.index] = whole[position] ?? 0
      })
      break
    }

    const locked = free[tooNarrow]
    if (!locked) break

    result[locked.index] = floor
    remaining = Math.max(0, remaining - floor)
    free = free.filter((_, position) => position !== tooNarrow)
  }

  return result
}

