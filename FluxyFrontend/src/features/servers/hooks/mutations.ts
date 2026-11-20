import { useMutation, useQueryClient } from '@tanstack/react-query'
import { createServer, deleteServer, updateServer } from '../api'
import type { ServerValues } from '../api'
import { useWrite } from '../../../hooks/useWrite'

/**
 * The servers feature's writes: the create and the change the form sends, and the delete the
 * table's row action sends.
 *
 * Built on `useWrite`, so the antiforgery token is minted at call time and the call sites
 * hand over only the endpoint's own arguments.
 *
 * **Every write invalidates `['servers']` on success**, which is the one job a mutation may
 * do here beyond calling the endpoint: a create adds a row the page is reading, a change
 * rewrites one, and a delete removes one - and a caller that reported the answer would still
 * leave a stale page behind. The page keeps the reporting.
 */

const useInvalidateServers = () => {
  const queryClient = useQueryClient()

  return () => queryClient.invalidateQueries({ queryKey: ['servers'] })
}

export const useCreateServer = () => {
  const invalidate = useInvalidateServers()
  const create = useWrite(createServer)

  return useMutation({
    mutationFn: (values: ServerValues) => create.mutateAsync({ values }),
    onSuccess: invalidate,
  })
}

export const useUpdateServer = () => {
  const invalidate = useInvalidateServers()
  const update = useWrite(updateServer)

  return useMutation({
    mutationFn: ({ id, values }: { id: string; values: ServerValues }) =>
      update.mutateAsync({ id, values }),
    onSuccess: invalidate,
  })
}

export const useDeleteServer = () => {
  const invalidate = useInvalidateServers()
  const remove = useWrite(deleteServer)

  return useMutation({
    mutationFn: ({ id }: { id: string }) => remove.mutateAsync({ id }),
    onSuccess: invalidate,
  })
}
