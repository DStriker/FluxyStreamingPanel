/**
 * Which medium a connection delivered, spelled the way the endpoint's `kind` parameter spells
 * it.
 *
 * Two members and no third: a connection is a video stream or an audio stream, and the screen
 * draws one of them at a time - the titles live in two tables, so a list that mixed them would
 * have to join both to say what was being watched.
 */
export type ConnectionStreamKind = 'video' | 'audio'
