/**
 * One thing a successful bulk row had to tell the operator but did not refuse - the
 * frontend's copy of `BulkItemWarningResponse`.
 *
 * A warning belongs to a row that **was** written: a bulk type change that had to clear an
 * archive length is a change that landed, and reporting the cleared setting as an error
 * would redraw a successful row as a refusal. `code` is the machine-readable name of what
 * was cleared (`video_stream_archive_cleared`), so the client can translate it through
 * `messages.api` exactly as it translates every other server code; `message` is the English
 * fallback for a build that has no translation yet.
 */
export interface BulkItemWarning {
  /** Machine readable name of what happened, snake_case. */
  code: string
  /** English fallback for this warning. */
  message: string
}
