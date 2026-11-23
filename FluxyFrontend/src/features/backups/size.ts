/**
 * A file size as the backups table shows it: whole bytes below a kilobyte, then one decimal in
 * the binary unit that fits.
 *
 * Binary units (1024) rather than decimal, because the number the server answers is a byte count
 * read from the filesystem and the size an operator compares it against is the one their own file
 * manager shows. Below a kilobyte the byte count is the readable form - rounding "812 B" to
 * "0.8 KB" would hide the difference between a real archive and an empty file.
 *
 * It is a pure function so the unit boundaries can be pinned by a unit test: `npm test` is the
 * only checker that can reach a column's renderer, because every render in `probe:render` lands
 * before the first answer arrives.
 */
export const formatSizeBytes = (bytes: number): string => {
  if (!Number.isFinite(bytes) || bytes < 0) return '—'

  if (bytes < 1024) return `${Math.round(bytes)} B`

  const units = ['KB', 'MB', 'GB', 'TB'] as const
  let value = bytes / 1024
  let unit = 0

  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024
    unit += 1
  }

  // One decimal until the number is large enough that a tenth is noise, which keeps a column of
  // sizes the same width from top to bottom.
  const rounded = value >= 100 ? Math.round(value) : Math.round(value * 10) / 10

  return `${rounded} ${units[unit]}`
}
