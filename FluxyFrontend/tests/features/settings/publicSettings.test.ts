import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { PublicSettings } from '../../../src/features/settings/types'

/**
 * The memo's one concurrency rule: a read that was in flight when `resetPublicSettings` ran
 * must not write its answer back over the reset.
 *
 * The failure path already guards with `inFlight === attempt`; the success path has to use
 * the same identity check, or a late answer repopulates the memo with the key the operator
 * just replaced - and the next submit silently mints tokens against the old site.
 */

const state = vi.hoisted(() => ({ getPublicSettings: vi.fn() }))

vi.mock('../../../src/features/settings/api', () => ({
  getPublicSettings: state.getPublicSettings,
}))

const { readPublicSettings, resetPublicSettings } = await import(
  '../../../src/features/settings/publicSettings'
)

/** A promise whose settlement the test controls. */
const deferred = () => {
  let resolve!: (value: PublicSettings) => void
  const promise = new Promise<PublicSettings>((settle) => {
    resolve = settle
  })
  return { promise, resolve }
}

beforeEach(() => {
  state.getPublicSettings.mockReset()
  resetPublicSettings()
})

describe('readPublicSettings', () => {
  it('does not repopulate the memo from a read that a reset superseded', async () => {
    const first = deferred()
    state.getPublicSettings.mockReturnValueOnce(first.promise)

    const superseded = readPublicSettings()
    // The operator saves the settings while the guest read is still on the wire.
    resetPublicSettings()
    first.resolve({ recaptchaSiteKey: 'old-key' })

    // The caller that was waiting still gets the answer it was promised...
    await expect(superseded).resolves.toEqual({ recaptchaSiteKey: 'old-key' })

    // ...but the memo stayed empty, so the next read is a fresh request, not the old answer.
    const second = deferred()
    state.getPublicSettings.mockReturnValueOnce(second.promise)

    const fresh = readPublicSettings()

    expect(state.getPublicSettings).toHaveBeenCalledTimes(2)

    second.resolve({ recaptchaSiteKey: 'new-key' })
    await expect(fresh).resolves.toEqual({ recaptchaSiteKey: 'new-key' })
  })

  it('still memoises an answer that no reset superseded', async () => {
    state.getPublicSettings.mockResolvedValueOnce({ recaptchaSiteKey: 'one-key' })

    await expect(readPublicSettings()).resolves.toEqual({ recaptchaSiteKey: 'one-key' })
    await expect(readPublicSettings()).resolves.toEqual({ recaptchaSiteKey: 'one-key' })

    expect(state.getPublicSettings).toHaveBeenCalledTimes(1)
  })
})
