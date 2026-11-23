import { useQuery } from '@tanstack/react-query'
import { listBackups } from '../api'

/**
 * The backups feature's one read.
 *
 * The key carries no parameters, because the endpoint takes none: the list is the whole
 * directory, newest first, and the server already bounds it with the rotation. The
 * `AbortSignal` is the query's own, so leaving the page cancels the request rather than merely
 * ignoring its answer.
 */
export const useBackups = () =>
  useQuery({
    queryKey: ['backups'],
    queryFn: ({ signal }) => listBackups(signal),
  })
