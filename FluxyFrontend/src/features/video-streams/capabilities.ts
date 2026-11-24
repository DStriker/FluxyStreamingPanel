import type { VideoStreamType } from './types'

/**
 * Which kinds of stream carry each of the settings the form draws.
 *
 * `acceptsArchive` mirrors the server's own rule: a non-zero archive on any other kind is
 * refused with `validation_failed`. `acceptsDelay` and `acceptsRestream` are the form's
 * rules for now - the endpoint accepts both sets of values on every type, so a client that
 * does not come through this form can still store them. Keeping the three here means the
 * disabled control, the reason beside it and the body normalization all read one fact
 * rather than three copies of it.
 */
export const acceptsArchive = (type: VideoStreamType): boolean => type === 'Permanent'

export const acceptsDelay = (type: VideoStreamType): boolean => type === 'Permanent'

export const acceptsRestream = (type: VideoStreamType): boolean =>
  type === 'Permanent' || type === 'OnDemand'

/**
 * Whether the form demands a transcoding server for this kind of stream.
 *
 * A form rule, not a server one: the endpoint accepts a permanent stream with no transcoding
 * server at all. Stated here so the required rule and the option filter below cannot disagree
 * about which kind they are talking about.
 */
export const requiresTranscodingServer = (type: VideoStreamType): boolean =>
  type === 'Permanent'

/**
 * Whether a timeshift-only server may be chosen as the transcoder.
 *
 * A permanent stream must be transcoded by a full server, so the picker hides the timeshift-only
 * rows there; every other kind may name any server, which is what the endpoint itself allows.
 */
export const allowsTimeshiftTranscoder = (type: VideoStreamType): boolean =>
  !requiresTranscodingServer(type)

/**
 * Whether the archive-server field is offered: a permanent stream that keeps an archive for at
 * least one day. The value behind the field survives a change of either fact - owner decision O4 -
 * so this decides whether the control is drawn, never whether the binding is cleared.
 */
export const offersArchiveServer = (type: VideoStreamType, archiveDays: number): boolean =>
  acceptsArchive(type) && archiveDays > 0
