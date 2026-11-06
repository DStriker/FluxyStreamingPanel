import { useQuery } from '@tanstack/react-query'
import { getProfile } from '../api'

/**
 * The profile feature's one read.
 *
 * It answers the three facts the profile page shows, and pages of other domains read it
 * for the account's time zone and for the login-guard floor under `LoginGuardFields`.
 * That is a page importing a second feature, which the layering allows; nothing inside a
 * feature reaches across.
 */

/** Optional switches for the profile read. */
interface UseProfileOptions {
  /**
   * Whether to ask at all. Defaults to true, because every page that names this hook needs the
   * answer; the header chip is the one caller that reads a fact only a reseller has and must
   * not issue a request for the two roles that cannot show it.
   */
  enabled?: boolean
}

/** The three facts the profile page shows, shared by every page that needs the zone or the grants. */
export const useProfile = ({ enabled = true }: UseProfileOptions = {}) =>
  useQuery({
    queryKey: ['profile'],
    queryFn: ({ signal }) => getProfile(signal),
    enabled,
  })
