/**
 * One content item as a bouquet screen names it: an identifier, a name and the category it
 * is filed under.
 *
 * Deliberately not the content's own list model. The two lists of the content manager exist
 * to be moved between, and almost every field beyond the name - the state, the kind - would
 * be a column nobody reads in a list that can hold thousands of rows.
 *
 * The category is the one exception, and it earns its place: it is what the filter over each
 * list works on, which is how an operator moves a whole section into a package at once.
 */
export interface BouquetMember {
  /** Identifier of the content item, as text. */
  id: string
  /** Display name of the content item. */
  name: string
  /** Identifier of the category the item is filed under, as text. */
  categoryId: string
  /** Display name of that category. */
  categoryName: string
}
