/**
 * The one signal that a session is over, and the one way a listener hears it.
 *
 * `src/utils/http.ts` is where a refresh is attempted and refused - and it is also the only
 * layer that can tell "the server said no" apart from "the server could not be asked",
 * which is the distinction that decides whether a visitor is actually signed out. The
 * reaction - a toast and a way back to the sign-in form - is a component above the
 * router. Neither side may import the other (the fetch layer must stay renderable in the
 * Node probes, and the component must not pull the transport into every page), so the
 * fact is published here and `SessionExpiredWatcher` subscribes.
 *
 * A burst of parallel calls that all fail at once is one event, not five: the first
 * signal is delivered and anything inside the cooldown is the same refusal arriving
 * again. Without that, a page with six queries would stack six toasts and six
 * navigations over one expired session.
 */
type Listener = () => void

const listeners = new Set<Listener>()

/** How long two signals are considered the same event. */
const COOLDOWN_MS = 1000

/** When the last signal was delivered, on `Date.now`'s clock. */
let lastSignal = 0

/** Announces that the session is over, at most once per cooldown. */
export function emitSessionExpired(): void {
  const now = Date.now()
  if (now - lastSignal < COOLDOWN_MS) return

  lastSignal = now
  listeners.forEach((listener) => listener())
}

/** Registers a reaction and answers with the way to unregister it. */
export function subscribeToSessionExpired(listener: Listener): () => void {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}
