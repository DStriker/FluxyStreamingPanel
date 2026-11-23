/**
 * One archive on the API host, as `GET /admin/backups` answers it.
 *
 * `createdAtUtc` is the instant encoded in the file name, not the filesystem's creation time:
 * there is no metadata row, and a copy or a remount rewrites the latter. The screen parses this
 * value rather than the name, so the one place that knows the naming rule stays the server.
 */
export interface BackupFile {
  /** Name of the file inside the backup directory. */
  fileName: string
  /** Length of the file on disk, in bytes. */
  sizeBytes: number
  /** Instant encoded in the file name, as an ISO-8601 string. */
  createdAtUtc: string
}
