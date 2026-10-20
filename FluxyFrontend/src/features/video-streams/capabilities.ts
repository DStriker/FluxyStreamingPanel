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
