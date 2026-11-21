import type { BackupPeriod, MagContainer, MagTheme } from './types'

/**
 * Every runtime list the two settings screens and their contracts loop over.
 *
 * They live here rather than in `types/` because that folder and its barrel are **type-only**:
 * a value exported from it would be erased by a type-only import, and a caller reading the
 * array through the barrel would get `undefined` at runtime. The same rule
 * `subscriptionOptions.ts` and `user-logs/recordTypes.ts` state.
 *
 * **The values are the wire's, not the screens'.** The backend validates against exactly these
 * strings, so a "corrected" spelling here is a value the server answers `validation_failed`
 * for - and `cappucino` (the owner's spelling, one `c`, one `p`) is the pair this note exists
 * for.
 */

/**
 * The 31 set-top-box models the backend's catalogue holds, verbatim and in its order.
 *
 * This is not a convenience list: the PATCH matches each name against the catalogue
 * case-insensitively, stores the canonical spelling, de-duplicates in first-occurrence order,
 * and **refuses the whole list** for a name outside it rather than dropping it - because a
 * client that sent a name believes it asked for it. So the two multi-selects are drawn from
 * this array and nowhere else, and a catalogue change on the server is a change here.
 */
export const STB_TYPES = [
  'Aura HD',
  'Aura HD2',
  'Aura HD3',
  'Aura HD4',
  'Aura HD5',
  'Aura HD6',
  'Aura HD7',
  'Aura HD8',
  'Aura HD9',
  'MAG200',
  'MAG245',
  'MAG245D',
  'MAG250',
  'MAG254',
  'MAG255',
  'MAG256',
  'MAG257',
  'MAG260',
  'MAG270',
  'MAG275',
  'MAG322',
  'MAG323',
  'MAG324',
  'MAG325',
  'MAG349',
  'MAG350',
  'MAG351',
  'MAG352',
  'MAG420',
  'MAG520',
  'WR320',
] as const

/**
 * The five theme wire values, in the order the portal's own list reads them.
 *
 * `ocean blue` keeps its space and `cappucino` its spelling: both are the owner's, and the
 * response answers with them. The selector maps each to its own locale key rather than
 * composing one from the value, because `messages.themes.cappucino` and
 * `messages.themes.oceanBlue` are two different shapes.
 */
export const MAG_THEMES: readonly MagTheme[] = [
  'default',
  'digital',
  'emerald',
  'cappucino',
  'ocean blue',
]

/** The two containers, canonical spellings only - `mpegts` is accepted but never answered. */
export const MAG_CONTAINERS: readonly MagContainer[] = ['mpeg-ts', 'hls']

/** The four backup periods, in the backend enum's order (`Hourly`..`Monthly` stored 0..3). */
export const BACKUP_PERIODS: readonly BackupPeriod[] = ['hourly', 'daily', 'weekly', 'monthly']

/**
 * The locale key of one theme's label.
 *
 * A map rather than `` `magSettingsForm.themes.${theme}` `` for the reason the union exists:
 * the two owner's spellings are not valid identifier segments, and a template that produced
 * `themes.ocean blue` would be a key neither bundle has.
 */
export const THEME_KEY_OF: Readonly<Record<MagTheme, string>> = {
  default: 'default',
  digital: 'digital',
  emerald: 'emerald',
  cappucino: 'cappucino',
  'ocean blue': 'oceanBlue',
}

/** The locale key of one container's label. */
export const CONTAINER_KEY_OF: Readonly<Record<MagContainer, string>> = {
  'mpeg-ts': 'mpegTs',
  hls: 'hls',
}
