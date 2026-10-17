/**
 * The two states a stream can be in, spelled exactly as the backend's `VideoStreamStatus`
 * spells them.
 *
 * Two descriptions, not a ramp - the same reasoning `UserStatus` carries. A stream is
 * running or it is not, and neither state is "more" of anything than the other, so nothing
 * here is ever ordered or compared.
 *
 * Only the start and stop actions write this. It is deliberately absent from the edit
 * body: a status a form could set would make "this stream is broadcasting" a claim an
 * operator types rather than a fact about a process, and the table would then draw a
 * verdict nobody asked a process for.
 */
export type VideoStreamStatus = 'Stopped' | 'Running'
