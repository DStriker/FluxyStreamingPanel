import { describe, expect, it } from 'vitest'
import { formatSizeBytes } from '../../../src/features/backups/size'

/**
 * The size column's rule, asserted as a function.
 *
 * The table's `render` is unreachable from `probe:render` - every render lands before the first
 * answer, so no row is ever drawn - and this is the half of that column that can be wrong without
 * anything failing loudly.
 */
describe('formatSizeBytes', () => {
  it('shows whole bytes below a kilobyte', () => {
    expect(formatSizeBytes(0)).toBe('0 B')
    expect(formatSizeBytes(1)).toBe('1 B')
    expect(formatSizeBytes(1023)).toBe('1023 B')
  })

  it('shows one decimal in the first unit that fits', () => {
    expect(formatSizeBytes(1024)).toBe('1 KB')
    expect(formatSizeBytes(1536)).toBe('1.5 KB')
    expect(formatSizeBytes(1024 * 1024)).toBe('1 MB')
    expect(formatSizeBytes(1024 * 1024 * 1.25)).toBe('1.3 MB')
  })

  it('drops the decimal once the number itself is large', () => {
    expect(formatSizeBytes(1024 * 100)).toBe('100 KB')
    expect(formatSizeBytes(1024 * 1024 * 100)).toBe('100 MB')
  })

  it('answers a dash for a value that is not a size', () => {
    expect(formatSizeBytes(-1)).toBe('—')
    expect(formatSizeBytes(Number.NaN)).toBe('—')
    expect(formatSizeBytes(Number.POSITIVE_INFINITY)).toBe('—')
  })
})
