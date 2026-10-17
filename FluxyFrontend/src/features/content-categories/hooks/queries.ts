import { useQuery } from '@tanstack/react-query'
import { getContentCategories } from '../api'
import type { ContentTypeParam } from '../api'

/**
 * The content-categories feature's one read - one endpoint asked by four section pages.
 */

/**
 * Every category of one kind of content, in the order the operator arranged them in.
 *
 * The key *is* the type, so the four section pages - each asking about a different kind -
 * never share a cache entry, and a drop that rewrites this list by hand still lands under
 * the same key the next read answers from.
 */
export const useContentCategories = (type: ContentTypeParam) =>
  useQuery({
    queryKey: ['contentCategories', type],
    queryFn: ({ signal }) => getContentCategories({ type, signal }),
  })
