import { useQuery } from '@tanstack/react-query'
import { getMagSettings, getSettings } from '../api'

/**
 * The settings feature's two reads, one hook per endpoint with its key beside it.
 *
 * Both keys are the resource's own name with no parameters, because the resources have none:
 * one row each, read whole. They are separate keys rather than one `['settings']` prefix so the
 * two writes below can invalidate only the screen they changed - the MAG write has no effect on
 * the application settings, and re-reading them anyway would be a request nobody asked for.
 *
 * **`enabled` exists for the self-gated pages.** A visitor whose group withholds `viewSettings`
 * would have the read answered `403 permission_denied`; a screen that asked anyway would show
 * that refusal instead of its own sentence. The page disables the read once the profile has
 * answered without the grant - the same shape `useProfile` takes for the header chip.
 *
 * The guest read is deliberately **not** hooked here. It is not React state at all: it is
 * memoised in `publicSettings.ts` because it is read from a submit handler, not from a
 * component, and a `useQuery` cannot serve a call that happens outside a render.
 */

/** Optional switches for the settings read. */
interface UseSettingsOptions {
  /** Whether to ask at all. Defaults to true. */
  enabled?: boolean
}

/** The installation settings, for the settings screen. */
export const useSettings = ({ enabled = true }: UseSettingsOptions = {}) =>
  useQuery({
    queryKey: ['settings'],
    queryFn: ({ signal }) => getSettings(signal),
    enabled,
  })

/** The MAG portal settings, for the MAG settings screen. */
export const useMagSettings = ({ enabled = true }: UseSettingsOptions = {}) =>
  useQuery({
    queryKey: ['magSettings'],
    queryFn: ({ signal }) => getMagSettings(signal),
    enabled,
  })
