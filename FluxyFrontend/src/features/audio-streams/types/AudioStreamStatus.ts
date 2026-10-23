/**
 * The two states an audio stream can be in, spelled exactly as the backend's
 * `AudioStreamStatus` spells them.
 *
 * Two descriptions, not a ramp - the same reasoning `VideoStreamStatus` and `UserStatus`
 * carry. A stream is running or it is not, and neither state is "more" of anything than the
 * other, so nothing here is ever ordered or compared.
 *
 * The add and edit forms choose it, and the start, stop and restart actions change it. A
 * disabled stream is kept without broadcasting; an active one is the state the relay plays.
 */
export type AudioStreamStatus = 'Stopped' | 'Running'
