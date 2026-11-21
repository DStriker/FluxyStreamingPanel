/**
 * The settings feature's contracts, re-exported as types only.
 *
 * Two one-row resources and the guest read beside them: `ApplicationSettings` and
 * `MagSettings` are what the admin screens read, `ApplicationSettingsValues` is the one body
 * with an asymmetry (the write-only reCAPTCHA secret), and the three wire vocabularies are
 * unions so a selector cannot offer a word the server refuses.
 */
export type { ApplicationSettings } from './ApplicationSettings'
export type { ApplicationSettingsValues } from './ApplicationSettingsValues'
export type { BackupPeriod } from './BackupPeriod'
export type { MagContainer } from './MagContainer'
export type { MagSettings } from './MagSettings'
export type { MagSettingsValues } from './MagSettingsValues'
export type { MagTheme } from './MagTheme'
export type { PublicSettings } from './PublicSettings'
