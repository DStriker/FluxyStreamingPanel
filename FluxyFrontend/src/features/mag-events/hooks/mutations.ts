import { useMutation, useQueryClient } from '@tanstack/react-query'
import { clearMagEvents, sendMagEvent } from '../api'
import type { MagEventSendBody } from '../api'
import { useWrite } from '../../../hooks/useWrite'

/**
 * The MAG event journal's two writes: one event sent to a device, and the whole-table clear.
 *
 * Both are built on `useWrite`, so the antiforgery token is minted at call time and the call
 * sites hand over only the endpoint's own arguments. Both are granted by `editSubscriptions`:
 * sending changes what the subscription surface holds, and the wipe destroys the record of it.
 *
 * **Both invalidate `['magEvents']` on success**, which is the one job a mutation may do here
 * beyond calling the endpoint: a send and a clear change the list the page is reading, and a
 * caller that reported the answer would still leave a stale page behind. The page keeps the
 * reporting - the toast and the dialog it closes.
 */

export const useSendMagEvent = () => {
  const queryClient = useQueryClient()
  const send = useWrite(sendMagEvent)

  return useMutation({
    mutationFn: (body: MagEventSendBody) => send.mutateAsync(body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['magEvents'] }),
  })
}

export const useClearMagEvents = () => {
  const queryClient = useQueryClient()
  const clear = useWrite(clearMagEvents)

  return useMutation({
    mutationFn: () => clear.mutateAsync({}),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['magEvents'] }),
  })
}
