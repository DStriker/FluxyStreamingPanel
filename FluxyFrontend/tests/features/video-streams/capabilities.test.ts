import { describe, expect, it } from 'vitest'
import {
  acceptsArchive,
  acceptsDelay,
  acceptsRestream,
  allowsTimeshiftTranscoder,
  offersArchiveServer,
  requiresTranscodingServer,
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

  it('requires a transcoding server of a permanent stream alone', () => {
    expect(TYPES.filter(requiresTranscodingServer)).toEqual(['Permanent'])
  })

  it('lets only a non-permanent stream take a timeshift-only transcoder', () => {
    // The mirror of the server's merged-row rule: a permanent stream is refused with a
    // timeshift-only transcoder, so the picker must not offer one - while every other kind may
    // name any server at all.
    expect(TYPES.filter(allowsTimeshiftTranscoder)).toEqual([
      'OnDemand',
      'Redirect',
      'OnlineSeries',
      'VodPlaylist',
      'FilePlaylist',
    ])
    expect(TYPES.filter((type) => !allowsTimeshiftTranscoder(type))).toEqual(['Permanent'])
  })

  it('offers the archive server only to a permanent stream that keeps an archive', () => {
    // Both halves matter: the archive length is a fact of the row, not of the kind, so a
    // permanent stream with zero days has no archive server to name.
    expect(offersArchiveServer('Permanent', 1)).toBe(true)
    expect(offersArchiveServer('Permanent', 30)).toBe(true)
    expect(offersArchiveServer('Permanent', 0)).toBe(false)
    expect(TYPES.filter((type) => type !== 'Permanent').every((type) => !offersArchiveServer(type, 7))).toBe(true)
  })
})
