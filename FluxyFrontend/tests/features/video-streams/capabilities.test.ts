import { describe, expect, it } from 'vitest'
import {
  acceptsArchive,
  acceptsDelay,
  acceptsRestream,
} from '../../../src/features/video-streams/capabilities'
import type { VideoStreamType } from '../../../src/features/video-streams/types'

/**
 * The three answers the stream form branches on when it decides whether a field may be
 * edited. `acceptsArchive` mirrors the server; the other two are the form's own rules, so
 * these cases are the only place the split is written down outside the page.
 *
 * The list is every member of `VideoStreamType`, not only the ones that answer true: a new
 * kind added to the union should make somebody decide here whether it takes a delay or
 * restreaming, rather than inheriting "no" by accident.
 */

const TYPES: readonly VideoStreamType[] = [
  'Permanent',
  'OnDemand',
  'Redirect',
  'OnlineSeries',
  'VodPlaylist',
  'FilePlaylist',
]

describe('video stream capabilities', () => {
  it('gives a delay and an archive to Permanent alone', () => {
    expect(TYPES.filter(acceptsArchive)).toEqual(['Permanent'])
    expect(TYPES.filter(acceptsDelay)).toEqual(['Permanent'])
  })

  it('gives restreaming settings to Permanent and OnDemand alone', () => {
    expect(TYPES.filter(acceptsRestream)).toEqual(['Permanent', 'OnDemand'])
  })
})
