import { describe, expect, it } from 'vitest'
import { liveConnectionsPath, videoStreamsPath } from '../../../src/features/dashboard/links'

/**
 * The two deep links the admin dashboard builds.
 *
 * Both are addresses a visitor arrives at from a card, so a wrong shape is not a broken
 * request - it is a page that opens unfiltered and looks as if the click did nothing.
 * `probe:render` never clicks; the unit test is where the parameters are pinned.
 */

describe('liveConnectionsPath', () => {
  it('opens the area own live connections page without a server', () => {
    expect(liveConnectionsPath('/admin')).toBe('/admin/live-connections')
  })

  it('narrows to one server when one is given', () => {
    expect(liveConnectionsPath('/admin', '11111111-1111-1111-1111-111111111111')).toBe(
      '/admin/live-connections?serverId=11111111-1111-1111-1111-111111111111',
    )
  })
})

describe('videoStreamsPath', () => {
  it('opens the area own streams page without a status', () => {
    expect(videoStreamsPath('/admin')).toBe('/admin/video-streams')
  })

  it('narrows to one status, spelled as the wire enum spells it', () => {
    // Capital S is the whole point: the streams page compares the parameter against its
    // enum members, so `stopped` would be dropped and the link would open the full list.
    expect(videoStreamsPath('/admin', 'Stopped')).toBe('/admin/video-streams?status=Stopped')
    expect(videoStreamsPath('/admin', 'Running')).toBe('/admin/video-streams?status=Running')
  })
})
