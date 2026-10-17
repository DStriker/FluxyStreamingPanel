import { useQuery } from '@tanstack/react-query'
import { getProfile } from '../../../lib/api'

/**
 * The profile feature's one read.
 *
 * It answers the three facts the profile page shows, and pages of other domains read it
 * for the account's time zone and for the login-guard floor under `LoginGuardFields`.
 * That is a page importing a second feature, which the layering allows; nothing inside a
 * feature reaches across.
 */

/** The three facts the profile page shows, shared by every page that needs the zone or the grants. */
export const useProfile = () =>
  useQuery({
    queryKey: ['profile'],
    queryFn: ({ signal }) => getProfile(signal),
  })
