/**
 * The two states a stream can be in, spelled exactly as the backend's `VideoStreamStatus`
 * spells them.
 *
 * Two descriptions, not a ramp - the same reasoning `UserStatus` carries. A stream is
 * running or it is not, and neither state is "more" of anything than the other, so nothing
 * here is ever ordered or compared.
 *
 * The add and edit forms choose it, and the start, stop and restart actions change it. A
 * disabled stream is kept without broadcasting; an active one is the state the relay plays.
 */
export type VideoStreamStatus = 'Stopped' | 'Running'
