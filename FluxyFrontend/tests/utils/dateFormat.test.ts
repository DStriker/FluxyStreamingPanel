import { describe, expect, it } from 'vitest'
import { formatChatTime } from '../../src/utils/dateFormat'

/**
 * The chat bubble's time line, pinned as the two shapes it can take.
 *
 * The rule the tests exist for is the "today" judgement: it is made in the viewer's zone, not
 * the browser's, and the reference instant is passed in so the answer does not depend on when
 * the suite runs. The formatting itself is `Intl`'s and is asserted by shape rather than by
 * exact punctuation - the locale may change it, and a test that pinned "14:30" would fail on
 * a machine whose ICU writes "14:30" with a narrow no-break space.
 */

const REFERENCE = new Date('2026-05-03T12:00:00.000Z')

describe('formatChatTime', () => {
  it('draws a bare 24-hour time for a message written today in the reader’s zone', () => {
    const value = formatChatTime('2026-05-03T09:41:00.000Z', 'en', 'UTC', REFERENCE)

    expect(value).toMatch(/^09:41$/)
  })

  it('treats “today” by the chosen zone rather than by UTC', () => {
    // 20:30 UTC is still 3 May in UTC but already 23:30 in Moscow, and the reference at
    // 21:30 UTC is 4 May in Moscow (00:30). So the *UTC* reading calls both the same day,
    // while the chosen zone puts them on either side of midnight - which is the divergence
    // this test is about: the reader in Moscow must see a date beside the time.
    const moscowReference = new Date('2026-05-03T21:30:00.000Z')
    const inUtc = formatChatTime('2026-05-03T20:30:00.000Z', 'en', 'UTC', moscowReference)
    const inMoscow = formatChatTime(
      '2026-05-03T20:30:00.000Z',
      'en',
      'Europe/Moscow',
      moscowReference,
    )

    expect(inUtc).toBe('20:30')
    expect(inMoscow).not.toBe('23:30')
    expect(inMoscow).toContain('23:30')
  })

  it('draws a short date and a time for an earlier day', () => {
    const value = formatChatTime('2026-05-01T09:41:00.000Z', 'en', 'UTC', REFERENCE)

    // Both branches print the same 24-hour clock, so the only difference is the date in front
    // of it. The exact date order is the locale's and is deliberately not pinned.
    expect(value).toContain('09:41')
    expect(value).not.toBe('09:41')
  })

  it('draws a dash for a value that is absent or unreadable', () => {
    expect(formatChatTime(null, 'en', 'UTC', REFERENCE)).toBe('—')
    expect(formatChatTime('not a date', 'en', 'UTC', REFERENCE)).toBe('—')
  })

  it('falls back to the browser zone when the stored one is not usable', () => {
    // A zone this ICU build refuses must not make the cell throw; the fallback is the same
    // one `formatTimestamp` uses, and the answer is still a time rather than a dash.
    const value = formatChatTime('2026-05-03T09:41:00.000Z', 'en', 'Not/AZone', REFERENCE)

    expect(value).not.toBe('—')
    expect(value).toMatch(/\d{1,2}[:.]\d{2}/)
  })
})
