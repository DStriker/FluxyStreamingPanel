import { QueryClient } from '@tanstack/react-query'

/**
 * The QueryClient this application runs on, and the reason its defaults are what they are.
 *
 * `staleTime: 0` is deliberate and is not the library being misread. What React Query buys
 * here is not a long-lived cache - a cached list of accounts, sessions or groups is a stale
 * one, and the pages' old effects re-read on every mount for precisely that reason. What it
 * buys no matter how short the cache lives is:
 *
 * - **deduplication**: two pages mounting with the same key share one request, where two
 *   effects each started their own;
 * - **a real `AbortSignal`** instead of a `cancelled` flag that let requests ride on to
 *   completion after the page was gone;
 * - **`refetch` / `invalidateQueries`** in place of fifteen hand-made `attempt` counters.
 *
 * `retry: false` keeps the behaviour the page had: a failed load shows its own retry button,
 * rather than running a silent second attempt behind the visitor's back. Focus and reconnect
 * refetches stay off for the same reason - a table that silently re-asks because the window
 * regained focus is a request nobody asked for, and the old behaviour did neither.
 *
 * A factory rather than a module-level instance: `probe:render` renders each page with a
 * client of its own, so that nothing one render cached could ever reach the next.
 */
export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 0,
        gcTime: 5 * 60_000,
        retry: false,
        refetchOnWindowFocus: false,
        refetchOnReconnect: false,
      },
    },
  })
}
