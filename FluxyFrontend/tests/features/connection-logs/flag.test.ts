import { describe, expect, it } from 'vitest'
import { FLAG_BASE_URL, flagUrl } from '../../../src/features/connection-logs/flag'

/**
 * The flag URL, pinned as a rule rather than described in a comment.
 *
 * This is the half of the owner's "the flag does not render" report that a render cannot see: no
 * test in this repository fetches an image, and a static render produces the `<img>` with whatever
 * `src` the component happened to write. So the URL and its case folding are asserted directly,
 * and the host was verified by HTTP separately (403 Cloudflare on the old one, 200 `image/png` on
 * this one, both case sensitive).
 */
describe('flagUrl', () => {
  it('points at the host that actually answers a PNG', () => {
    expect(FLAG_BASE_URL).toBe('https://www.iptrackeronline.com/flags')
  })

  it('folds the stored upper-case code to the lower case the service answers', () => {
    // The row stores `DE`; `DE.png` is a 404 and `de.png` is the flag.
    expect(flagUrl('DE')).toBe('https://www.iptrackeronline.com/flags/de.png')
    expect(flagUrl('de')).toBe('https://www.iptrackeronline.com/flags/de.png')
    expect(flagUrl(' gb ')).toBe('https://www.iptrackeronline.com/flags/gb.png')
  })
})
