import { useMutation, useQueryClient } from '@tanstack/react-query'
import { updateMagSettings, updateSettings } from '../api'
import type { ApplicationSettingsValues, MagSettingsValues } from '../types'
import { useWrite } from '../../../hooks/useWrite'
import { resetPublicSettings } from '../publicSettings'

/**
 * The settings feature's two writes.
 *
 * Built on `useWrite`, so the antiforgery token is minted at call time and the call sites hand
 * over only the endpoint's own arguments.
 *
 * **The application write also resets the public-settings memo**, and that is the one effect a
 * mutation has beyond its endpoint. The guest forms resolve the reCAPTCHA site key from that
 * memo; an operator who has just changed the key would otherwise leave the tab minting tokens
 * against a site the new secret does not recognise, and every sign-in in that tab would fail
 * with `captcha_invalid` until a reload. The reset is cold state, not a cache to invalidate -
 * the next submit re-reads. It fires in `onSuccess` rather than `onSettled` on purpose: a
 * refused save changed nothing, and re-reading a key that did not move is a request for no
 * reason.
 */

/** Writes the installation settings and resets the guest site-key memo. */
export const useUpdateSettings = () => {
  const queryClient = useQueryClient()
  const write = useWrite(updateSettings)

  return useMutation({
    mutationFn: (values: ApplicationSettingsValues) => write.mutateAsync({ values }),
    onSuccess: async () => {
      resetPublicSettings()
      await queryClient.invalidateQueries({ queryKey: ['settings'] })
    },
  })
}

/**
 * Writes the MAG portal settings.
 *
 * No memo to reset here: none of the eleven values is read outside this resource, and the guest
 * read is the application row's site key alone.
 */
export const useUpdateMagSettings = () => {
  const queryClient = useQueryClient()
  const write = useWrite(updateMagSettings)

  return useMutation({
    mutationFn: (values: MagSettingsValues) => write.mutateAsync({ values }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['magSettings'] }),
  })
}
