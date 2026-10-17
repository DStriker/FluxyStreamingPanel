import { useEffect } from 'react'
import { App } from 'antd'
import { useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { router } from '../../../router'
import { subscribeToSessionExpired } from '../../../lib/sessionEvents'
import { loginRouteForPath } from '../../../lib/session'

/**
 * The reaction to a session that ended while a page was open on top of it.
 *
 * The fact itself is published by `src/lib/http.ts` (see `sessionEvents` for why the two
 * ends are separate); this component is the end that can show things - a toast, and the
 * sign-in form of the area the visitor was reading. Without it an expiry mid-page left
 * the visitor on stale rows with a red toast and no way forward until they happened to
 * navigate, which is the next time the guard would have asked anyway.
 *
 * It sits in `App.tsx` under `<AntApp>` (for `message`) and inside the query client (for
 * the cache). The router is reached through the same module `App` builds it from rather
 * than through context, so this component renders nothing and can never suspend.
 *
 * The cache is cleared, not left alone. A cached row belongs to the account that read it,
 * and `staleTime: 0` only marks an entry stale - it does not stop `useQuery` from painting
 * it while the refetch is on its way. After an expiry, the next account to sign in on this
 * machine would otherwise see the previous one's rows for a frame, which no server-side
 * check can prevent because nothing is being asked.
 */
export default function SessionExpiredWatcher() {
  const { message } = App.useApp()
  const queryClient = useQueryClient()
  const { t } = useTranslation()

  useEffect(
    () =>
      subscribeToSessionExpired(() => {
        queryClient.clear()
        message.warning(t('messages.api.session_expired'))
        // The destination comes from the path being read, because by now the session is
        // gone and the address is the only evidence of which area this visitor belonged
        // to - see `loginRouteForPath` for that rule.
        router.navigate(loginRouteForPath(router.state.location.pathname), { replace: true })
      }),
    [message, queryClient, t],
  )

  return null
}
