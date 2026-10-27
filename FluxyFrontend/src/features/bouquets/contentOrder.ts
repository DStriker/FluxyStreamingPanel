import type { BouquetMember } from './types'

/**
 * The pure list arithmetic the bouquet content manager runs, kept out of the component so
 * it can be tested without a render.
 *
 * Three operations, and each one has a rule the screen depends on:
 *
 * - moving selected items up or down moves the **group**, not each item by one step, and
 *   keeps the group's own internal order. Moving three selected rows up one position each
 *   in turn would reverse them, which is not what a selection means;
 * - a group already at the top (or bottom) does not move, and the call answers the same
 *   array rather than a copy that happens to be equal, so the screen can tell "nothing to
 *   do" without comparing every id;
 * - sorting is by name, for the reader's own language, and case-insensitively. It is a
 *   draft action like the other two: nothing is written until the operator saves.
 */

/** Whether two id lists hold the same identifiers in the same order. */
export const sameOrder = (left: readonly string[], right: readonly string[]): boolean =>
  left.length === right.length && left.every((id, index) => id === right[index])

/**
 * Moves every selected id one position towards the front, as a block.
 *
 * The block keeps its own relative order and slides over the unselected ids above it. The
 * ids are walked from the top so an id cannot be swapped past a neighbour that has already
 * moved this pass - that is what makes a run of selected rows travel together instead of
 * each one overtaking the last.
 *
 * @param ids Current order of the list.
 * @param selected Identifiers the operator has ticked; anything else is left where it is.
 * @returns The new order, or `ids` itself when nothing could move.
 */
export const moveSelectedUp = (ids: readonly string[], selected: ReadonlySet<string>): readonly string[] => {
  if (ids.length < 2 || selected.size === 0) {
    return ids
  }

  const next = [...ids]
  let moved = false

  for (let index = 1; index < next.length; index += 1) {
    const current = next[index]!
    const previous = next[index - 1]!

    if (!selected.has(current) || selected.has(previous)) {
      continue
    }

    next[index - 1] = current
    next[index] = previous
    moved = true
  }

  return moved ? next : ids
}

/**
 * Moves every selected id one position towards the back, as a block.
 *
 * The mirror of {@link moveSelectedUp}: walked from the bottom for the same reason, so a
 * run of selected rows travels together rather than each one overtaking the last.
 *
 * @param ids Current order of the list.
 * @param selected Identifiers the operator has ticked; anything else is left where it is.
 * @returns The new order, or `ids` itself when nothing could move.
 */
export const moveSelectedDown = (ids: readonly string[], selected: ReadonlySet<string>): readonly string[] => {
  if (ids.length < 2 || selected.size === 0) {
    return ids
  }

  const next = [...ids]
  let moved = false

  for (let index = next.length - 2; index >= 0; index -= 1) {
    const current = next[index]!
    const following = next[index + 1]!

    if (!selected.has(current) || selected.has(following)) {
      continue
    }

    next[index] = following
    next[index + 1] = current
    moved = true
  }

  return moved ? next : ids
}

/**
 * Orders members by name for the reader's language.
 *
 * `localeCompare` with `sensitivity: 'accent'` rather than a plain `<`: the list is read by
 * a person, and a `sortOrder` over code points would put every upper-case name before every
 * lower-case one - "alpha" would come after "Zulu". `accent` compares letters first and case
 * only as a tie-break inside the same letter, so "beta" and "Alpha" still order by their
 * letters while the two spellings of one word stay together.
 *
 * Not `base`, which was the first version of this line and was wrong: `base` treats "Ё" and
 * "Е" as *equal*, so their order fell through to the identifier tie-break and a list sorted
 * A-Z could show "Ёлка" before "Елка" - the opposite of what a Russian reader expects, and
 * an order that changed the day a row's identifier changed. `accent` keeps the diaeresis as
 * a secondary difference, which is exactly where the alphabet puts it.
 *
 * The identifier is the final tie-break, so two names that compare equal still have one
 * order rather than whatever the engine happened to do.
 *
 * @param members Members to sort.
 * @param locale Language the page is being read in, from `i18n.resolvedLanguage`.
 * @returns A new array, ordered by name. The input is not touched.
 */
export const sortByName = (members: readonly BouquetMember[], locale: string): BouquetMember[] =>
  [...members].sort(
    (left, right) =>
      left.name.localeCompare(right.name, locale, { sensitivity: 'accent' }) ||
      left.id.localeCompare(right.id),
  )

/**
 * The identifiers of one list, in order - what the save sends and the draft compares.
 *
 * @param members Members to read.
 */
export const idsOf = (members: readonly BouquetMember[]): string[] => members.map((member) => member.id)

/**
 * Every member of both lists as one array, so an id can be resolved to its name without
 * carrying two arrays around.
 *
 * @param members What the bouquet holds.
 * @param available What it does not.
 */
export const allMembers = (
  members: readonly BouquetMember[],
  available: readonly BouquetMember[],
): BouquetMember[] => [...members, ...available]

/** One category as the filter offers it: its identity and the name a reader picks it by. */
export interface CategoryOption {
  id: string
  name: string
}

/**
 * The categories a kind of content is filed under, ordered by name.
 *
 * Called with **both** lists of the picker together, and that is load-bearing rather than
 * convenient: a category belongs to the kind of content, not to the side of the screen an
 * item currently sits on. Deriving the options from one list meant that selecting every item
 * of a category and moving it across emptied that list of the category, the option vanished
 * from its own select, and the filter the operator was still using disappeared from under
 * them. The union only changes when the content does.
 *
 * @param members Both lists, concatenated - see `allMembers`.
 */
export const categoriesOf = (members: readonly BouquetMember[]): CategoryOption[] => {
  const byId = new Map<string, string>()

  for (const member of members) {
    byId.set(member.categoryId, member.categoryName)
  }

  return [...byId.entries()]
    .map(([id, name]) => ({ id, name }))
    .sort((left, right) => left.name.localeCompare(right.name))
}

/**
 * Narrows a list to one category, or leaves it whole.
 *
 * Combined with a name filter rather than replacing it: the two narrow different things, and
 * "the News streams I can see" is a question an operator asks more often than either half.
 *
 * @param members The list to narrow.
 * @param category Category to keep, or null for all of them.
 */
export const filterByCategory = (
  members: readonly BouquetMember[],
  category: string | null,
): BouquetMember[] =>
  category === null ? [...members] : members.filter((member) => member.categoryId === category)

/**
 * Whether a selected category still exists in the universe the filter offers.
 *
 * A category disappears only when the content itself does - every item of it deleted while
 * the screen was open - and at that point the filter has to stop narrowing rather than keep
 * hiding the list behind a value whose option is gone and cannot be cleared.
 *
 * @param categories The options the select offers.
 * @param category The value currently selected.
 */
export const isKnownCategory = (
  categories: readonly CategoryOption[],
  category: string | null,
): boolean => category === null || categories.some((entry) => entry.id === category)
