import { apiFetch, apiGet } from '../../utils/http'
import type { MessageResponse } from '../../types'
import type { BackupFile } from './types'

/**
 * The database archives on the API host: the list and the three writes.
 *
 * **There is no settings write here.** `backupEnabled`, `backupPeriod` and `backupRotationCount`
 * are edited only on `/admin/settings`, whose PATCH is a full write of the whole row - a second
 * editing surface would send seventeen values it never showed and clobber them. The backups
 * screen echoes the three read-only and links to the page that owns them.
 *
 * **The list is unpaged.** The collection is a directory bounded by the rotation, not a table:
 * the server holds it in full and the screen draws all of it.
 */

/** The collection: `GET /admin/backups`, newest first. */
interface BackupsListResponse {
  items: BackupFile[]
}

/** Every archive in the backup directory, newest first. */
export const listBackups = async (signal?: AbortSignal): Promise<BackupFile[]> => {
  const body = await apiGet<BackupsListResponse>('/admin/backups', signal)

  return body.items
}

/** Takes a backup now: `POST /admin/backups`, answered `201` with a `Location`. */
export const createBackup = ({
  csrfToken,
}: {
  csrfToken: string | null
}): Promise<MessageResponse> => apiFetch('/admin/backups', { method: 'POST', csrfToken })

/**
 * Replaces the database with one archive: `POST /admin/backups/{fileName}/restore`.
 *
 * The departure is the server's: a restore is a state transition of the whole database, not a
 * write to the archive, so it is named as an act rather than with `PUT`. The `fileName` is
 * encoded because the name is a route segment.
 */
export const restoreBackup = ({
  fileName,
  csrfToken,
}: {
  fileName: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/backups/${encodeURIComponent(fileName)}/restore`, {
    method: 'POST',
    csrfToken,
  })

/** Deletes one archive: `DELETE /admin/backups/{fileName}`, answered `200`. */
export const deleteBackup = ({
  fileName,
  csrfToken,
}: {
  fileName: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/backups/${encodeURIComponent(fileName)}`, { method: 'DELETE', csrfToken })
