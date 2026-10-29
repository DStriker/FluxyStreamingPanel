import { describe, expect, it } from 'vitest'
import {
  PLAYLIST_ID_LENGTH,
  SUBSCRIPTION_CONNECTIONS_MAX,
  WHITELIST_MAX_ENTRIES,
  hasOutputFormat,
  hasService,
  isMac,
  isPlaylistId,
  isUsableConnections,
} from '../../../src/features/subscriptions/subscriptionRules'

/**
 * The subscription rules the form mirrors from the server.
 *
 * Asserted as functions because a render never reaches them - every `probe:render` lands before
 * the first answer and before any submit - so the pure halves are pinned here, the way the tariff
 * plan mirror is.
 */

describe('isPlaylistId', () => {
  it('accepts the 32 lower-case hex characters the server mints', () => {
    expect(isPlaylistId('0123456789abcdef0123456789abcdef')).toBe(true)
    expect(PLAYLIST_ID_LENGTH).toBe(32)
  })

  it('refuses another length, another case and a non-hex character', () => {
    expect(isPlaylistId('0123456789abcdef0123456789abcde')).toBe(false)
    expect(isPlaylistId('0123456789abcdef0123456789abcdef0')).toBe(false)
    expect(isPlaylistId('0123456789ABCDEF0123456789ABCDEF')).toBe(false)
    expect(isPlaylistId('zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz')).toBe(false)
    expect(isPlaylistId('')).toBe(false)
  })
})

describe('isMac', () => {
  it('accepts either separator in either case', () => {
    expect(isMac('AA:BB:CC:DD:EE:FF')).toBe(true)
    expect(isMac('aa-bb-cc-dd-ee-ff')).toBe(true)
  })

  it('refuses another pair count or a non-hex pair', () => {
    expect(isMac('AA:BB:CC:DD:EE')).toBe(false)
    expect(isMac('AA:BB:CC:DD:EE:FF:00')).toBe(false)
    expect(isMac('GG:BB:CC:DD:EE:FF')).toBe(false)
    expect(isMac('')).toBe(false)
  })
})

describe('hasService', () => {
  it('is true when one service survives the plan support', () => {
    expect(
      hasService({
        playlistId: 'id',
        magMac: null,
        enigma2Mac: null,
        supportsPlaylist: true,
        supportsMag: false,
        supportsEnigma2: false,
      }),
    ).toBe(true)
  })

  it('is false when the only service the row holds is one the plan cannot deliver', () => {
    expect(
      hasService({
        playlistId: null,
        magMac: 'AA:BB:CC:DD:EE:FF',
        enigma2Mac: null,
        supportsPlaylist: true,
        supportsMag: false,
        supportsEnigma2: false,
      }),
    ).toBe(false)
  })

  it('is false when nothing at all is attached', () => {
    expect(
      hasService({
        playlistId: null,
        magMac: null,
        enigma2Mac: null,
        supportsPlaylist: true,
        supportsMag: true,
        supportsEnigma2: true,
      }),
    ).toBe(false)
  })
})

describe('hasOutputFormat', () => {
  it('needs at least one format', () => {
    expect(hasOutputFormat([])).toBe(false)
    expect(hasOutputFormat(['mpegTs'])).toBe(true)
    expect(hasOutputFormat(['mpegTs', 'hls'])).toBe(true)
  })
})

describe('isUsableConnections', () => {
  it('treats a null ceiling as unlimited and refuses one outside the range', () => {
    expect(isUsableConnections(null)).toBe(true)
    expect(isUsableConnections(0)).toBe(false)
    expect(isUsableConnections(1)).toBe(true)
    expect(isUsableConnections(SUBSCRIPTION_CONNECTIONS_MAX)).toBe(true)
    expect(isUsableConnections(SUBSCRIPTION_CONNECTIONS_MAX + 1)).toBe(false)
  })
})

describe('WHITELIST_MAX_ENTRIES', () => {
  it('is the twenty the server enforces', () => {
    expect(WHITELIST_MAX_ENTRIES).toBe(20)
  })
})
