import type { MagSettings } from './MagSettings'

/**
 * The body `PATCH /admin/mag-settings` sends.
 *
 * All eleven fields are required, and this endpoint has no write-only field - so the body is the
 * read shape exactly, and an alias is the honest way to say that. A separate interface with the
 * same members would be a second place for a field to be added to one side only.
 */
export type MagSettingsValues = MagSettings
