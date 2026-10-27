import { useQuery } from '@tanstack/react-query'
import { getBouquetContent, getBouquets } from '../api'
import type { ContentTypeParam } from '../api'

/**
 * The bouquets feature's reads: the whole list, and the two content lists of one kind.
 */

/**
 * Every bouquet, in the order the operator arranged them in.
 *
 * One key for the whole list rather than a key per page: the table, the content manager's
 * heading and the picker on the four content forms all read the same rows, and a create, a
 * rename or a drop invalidates this one entry.
 */
export const useBouquets = () =>
  useQuery({
    queryKey: ['bouquets'],
    queryFn: ({ signal }) => getBouquets({ signal }),
  })

/**
 * The two lists one tab of the content manager draws, for one kind of content.
 *
 * The key is the bouquet *and* the kind, so the four tabs never share an entry - a tab that
 * switched kinds must not paint the previous tab's members while the new answer is in
 * flight. `enabled` is what keeps a tab nobody has opened from being fetched: the manager
 * loads a kind's lists when its tab is first shown, and a save only touches a kind that was
 * read (and therefore drafted).
 */
export const useBouquetContent = (id: string, type: ContentTypeParam, enabled = true) =>
  useQuery({
    queryKey: ['bouquetContent', id, type],
    queryFn: ({ signal }) => getBouquetContent({ id, type, signal }),
    enabled: enabled && id.length > 0,
  })
